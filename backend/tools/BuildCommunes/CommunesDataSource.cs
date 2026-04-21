using System.Globalization;

namespace TrekFr.Tools.BuildCommunes;

public sealed class CommunesDataSource(WikidataSparqlClient sparql)
{
    private const int CommunesPageSize = 2000;
    private const int LabelBatchSize = 800;

    public async Task<List<CommuneRaw>> FetchCommunesAsync(CancellationToken ct = default)
    {
        var idsCoordsPop = await FetchCommuneIdsAsync(ct);
        Console.WriteLine($"  → {idsCoordsPop.Count} commune IDs with coords");
        if (idsCoordsPop.Count == 0) return [];

        var labels = await FetchLabelsAsync(idsCoordsPop.Select(c => c.wdId).ToList(), ct);
        Console.WriteLine($"  → {labels.Count} labels resolved");

        var result = new List<CommuneRaw>(idsCoordsPop.Count);
        foreach (var (wdId, lat, lon, pop) in idsCoordsPop)
        {
            if (!labels.TryGetValue(wdId, out var name)) continue;
            result.Add(new CommuneRaw(wdId, name, lat, lon, pop));
        }
        return result;
    }

    public async Task<Dictionary<string, int>> FetchHeritageCountsAsync(CancellationToken ct = default)
    {
        // Count per-commune locally from a flat list of (heritage item, commune) pairs filtered to France.
        const string query = """
            SELECT ?commune WHERE {
              ?mh wdt:P17 wd:Q142 .
              ?mh wdt:P1435 ?designation .
              ?mh wdt:P131 ?commune .
            }
            """;
        try
        {
            var rows = await sparql.RunTsvAsync(query, ct);
            var dict = new Dictionary<string, int>();
            foreach (var row in rows)
            {
                if (!row.TryGetValue("commune", out var uri)) continue;
                var id = WikidataSparqlClient.StripWikidataUri(uri);
                dict[id] = dict.TryGetValue(id, out var n) ? n + 1 : 1;
            }
            return dict;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ heritage query failed: {ex.Message.Split('\n')[0]}");
            return [];
        }
    }

    public async Task<HashSet<string>> FetchCommunesMatchingOrgLabelAsync(string orgLabel, CancellationToken ct = default)
    {
        var escaped = orgLabel.Replace("\"", "\\\"");
        var query = $$"""
            SELECT DISTINCT ?commune WHERE {
              ?org rdfs:label "{{escaped}}"@fr .
              { ?commune wdt:P463 ?org . }
              UNION { ?commune wdt:P1435 ?org . }
              UNION { ?commune wdt:P166 ?org . }
              ?commune wdt:P31 wd:Q484170 .
            }
            """;
        return await FetchCommuneIdSetAsync(query, ct);
    }

    public async Task<HashSet<string>> FetchCommunesMatchingOrgSubstringsAsync(
        IReadOnlyList<string> mustContain,
        CancellationToken ct = default)
    {
        var filters = string.Join(" && ",
            mustContain.Select(s => $"CONTAINS(LCASE(STR(?orgLabel)), LCASE(\"{s.Replace("\"", "\\\"")}\"))"));
        var query = $$"""
            SELECT DISTINCT ?commune WHERE {
              ?org rdfs:label ?orgLabel .
              FILTER(LANG(?orgLabel) = "fr")
              FILTER({{filters}})
              { ?commune wdt:P463 ?org . }
              UNION { ?commune wdt:P1435 ?org . }
              UNION { ?commune wdt:P166 ?org . }
              ?commune wdt:P31 wd:Q484170 .
            }
            """;
        return await FetchCommuneIdSetAsync(query, ct);
    }

    private async Task<List<(string wdId, double lat, double lon, int? pop)>> FetchCommuneIdsAsync(CancellationToken ct)
    {
        // Paginated to avoid Wikidata's 60s timeout on scans of all 35k communes.
        var all = new List<(string, double, double, int?)>();
        int offset = 0;
        while (true)
        {
            var query = $$"""
                SELECT ?commune ?coord ?pop WHERE {
                  ?commune wdt:P31 wd:Q484170 ;
                           wdt:P625 ?coord .
                  OPTIONAL { ?commune wdt:P1082 ?pop . }
                }
                ORDER BY ?commune
                LIMIT {{CommunesPageSize}}
                OFFSET {{offset}}
                """;
            var rows = await sparql.RunTsvAsync(query, ct);
            if (rows.Count == 0) break;
            foreach (var row in rows)
            {
                if (!row.TryGetValue("commune", out var uri)) continue;
                if (!row.TryGetValue("coord", out var coord)) continue;
                if (!TryParsePoint(coord, out var lat, out var lon)) continue;
                var wdId = WikidataSparqlClient.StripWikidataUri(uri);
                int? pop = null;
                if (row.TryGetValue("pop", out var popStr) &&
                    double.TryParse(popStr, NumberStyles.Float, CultureInfo.InvariantCulture, out var popD))
                {
                    pop = (int)Math.Round(popD);
                }
                all.Add((wdId, lat, lon, pop));
            }
            if (rows.Count < CommunesPageSize) break;
            offset += CommunesPageSize;
            Console.WriteLine($"  ...offset {offset}, {all.Count} so far");
            await Task.Delay(300, ct);
        }
        return all;
    }

    private async Task<Dictionary<string, string>> FetchLabelsAsync(IReadOnlyList<string> wdIds, CancellationToken ct)
    {
        var labels = new Dictionary<string, string>(wdIds.Count);
        for (int start = 0; start < wdIds.Count; start += LabelBatchSize)
        {
            var batch = wdIds.Skip(start).Take(LabelBatchSize).ToList();
            var values = string.Join(" ", batch.Select(id => "wd:" + id));
            var query = $$"""
                SELECT ?commune ?communeLabel WHERE {
                  VALUES ?commune { {{values}} }
                  SERVICE wikibase:label { bd:serviceParam wikibase:language "fr,en". }
                }
                """;
            var rows = await sparql.RunTsvAsync(query, ct);
            foreach (var row in rows)
            {
                if (!row.TryGetValue("commune", out var uri)) continue;
                if (!row.TryGetValue("communeLabel", out var name)) continue;
                var wdId = WikidataSparqlClient.StripWikidataUri(uri);
                // Skip when the label service falls back to the Q-id.
                if (name.StartsWith('Q') && name.Length <= 10 && int.TryParse(name[1..], out _)) continue;
                labels[wdId] = name;
            }
            await Task.Delay(200, ct);
        }
        return labels;
    }

    private async Task<HashSet<string>> FetchCommuneIdSetAsync(string query, CancellationToken ct)
    {
        try
        {
            var rows = await sparql.RunTsvAsync(query, ct);
            var set = new HashSet<string>();
            foreach (var row in rows)
            {
                if (!row.TryGetValue("commune", out var uri)) continue;
                set.Add(WikidataSparqlClient.StripWikidataUri(uri));
            }
            return set;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"  ⚠ query failed: {ex.Message.Split('\n')[0]}");
            return [];
        }
    }

    private static bool TryParsePoint(string wkt, out double lat, out double lon)
    {
        lat = 0; lon = 0;
        if (!wkt.StartsWith("Point(", StringComparison.OrdinalIgnoreCase)) return false;
        var inner = wkt[6..^1];
        var parts = inner.Split(' ');
        if (parts.Length < 2) return false;
        if (!double.TryParse(parts[0], NumberStyles.Float, CultureInfo.InvariantCulture, out lon)) return false;
        if (!double.TryParse(parts[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lat)) return false;
        return true;
    }
}
