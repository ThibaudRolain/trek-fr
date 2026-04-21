using System.Globalization;
using System.Text;

namespace TrekFr.Tools.BuildCommunes;

/// <summary>
/// Base Mérimée = liste officielle des immeubles protégés au titre des
/// monuments historiques (ministère de la Culture). Donne un comptage par
/// commune plus complet que ce que Wikidata connaît.
/// </summary>
public sealed class MerimeeDataSource(HttpClient http)
{
    // Candidate endpoints tried in order until one responds with CSV.
    // The exact URL of the Mérimée export can change; we try a few known shapes.
    private static readonly string[] CsvUrls =
    [
        "https://data.culture.gouv.fr/api/explore/v2.1/catalog/datasets/liste-des-immeubles-proteges-au-titre-des-monuments-historiques/exports/csv?delimiter=;",
        "https://data.culture.gouv.fr/explore/dataset/liste-des-immeubles-proteges-au-titre-des-monuments-historiques/download/?format=csv&use_labels_for_header=true&csv_separator=;",
        "https://www.data.gouv.fr/fr/datasets/r/d5e72f86-4e4a-4e56-8ec8-0265e76b1abf.csv",
    ];

    public async Task<Dictionary<string, int>?> TryFetchCountsByInseeAsync(CancellationToken ct = default)
    {
        foreach (var url in CsvUrls)
        {
            try
            {
                Console.WriteLine($"  trying {new Uri(url).Host}...");
                using var resp = await http.GetAsync(url, ct);
                if (!resp.IsSuccessStatusCode)
                {
                    Console.WriteLine($"    {(int)resp.StatusCode}");
                    continue;
                }
                var contentType = resp.Content.Headers.ContentType?.MediaType ?? "";
                if (!contentType.Contains("csv") && !contentType.Contains("text"))
                {
                    Console.WriteLine($"    unexpected content-type: {contentType}");
                    continue;
                }
                await using var stream = await resp.Content.ReadAsStreamAsync(ct);
                using var reader = new StreamReader(stream, Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
                var counts = ParseInseeCounts(reader);
                if (counts.Count > 0)
                {
                    Console.WriteLine($"    parsed {counts.Count} communes with MH entries");
                    return counts;
                }
                Console.WriteLine("    CSV parsed but no INSEE-keyed counts");
            }
            catch (Exception ex) when (ex is HttpRequestException or IOException or TaskCanceledException)
            {
                Console.WriteLine($"    {ex.Message.Split('\n')[0]}");
            }
        }
        return null;
    }

    private static Dictionary<string, int> ParseInseeCounts(StreamReader reader)
    {
        var header = reader.ReadLine();
        if (header is null) return new Dictionary<string, int>();
        var separator = header.Contains(';') ? ';' : ',';
        var cols = SplitCsv(header, separator);
        var inseeIndex = FindInseeColumn(cols);
        if (inseeIndex < 0)
        {
            Console.WriteLine($"    no INSEE-like column found in header; sample columns: {string.Join(", ", cols.Take(10).Select(c => c.Trim('"')))}");
            return new Dictionary<string, int>();
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            if (line.Length == 0) continue;
            var fields = SplitCsv(line, separator);
            if (fields.Length <= inseeIndex) continue;
            var insee = NormaliseInsee(fields[inseeIndex]);
            if (insee is null) continue;
            counts[insee] = counts.TryGetValue(insee, out var n) ? n + 1 : 1;
        }
        return counts;
    }

    private static int FindInseeColumn(string[] header)
    {
        // Prefer exact "insee"-looking names first, fall back to any column containing "insee".
        var normalised = header.Select(h => h.Trim('"').Trim().ToLowerInvariant()).ToArray();
        for (int i = 0; i < normalised.Length; i++)
        {
            if (normalised[i] is "insee" or "code_insee" or "code insee" or "codeinsee" or "ref_insee" or "com_insee") return i;
        }
        for (int i = 0; i < normalised.Length; i++)
        {
            if (normalised[i].Contains("insee")) return i;
        }
        // Last resort: some Mérimée exports use "com" (commune code) as the INSEE-like key.
        for (int i = 0; i < normalised.Length; i++)
        {
            if (normalised[i] is "com" or "codecommune" or "code_commune" or "code commune") return i;
        }
        return -1;
    }

    private static string? NormaliseInsee(string raw)
    {
        var s = raw.Trim().Trim('"').Trim();
        if (s.Length == 0) return null;
        // Mérimée sometimes gives "75056" or "2A004" (Corsica). Accept both.
        if (s.Length == 4 && int.TryParse(s, out _)) return "0" + s; // e.g. "1234" → "01234"
        if (s.Length == 5) return s;
        return null;
    }

    private static string[] SplitCsv(string line, char sep)
    {
        var parts = new List<string>();
        var sb = new StringBuilder();
        bool inQuotes = false;
        for (int i = 0; i < line.Length; i++)
        {
            var c = line[i];
            if (c == '"')
            {
                // RFC 4180: doubled "" inside a quoted field is an escaped quote.
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    sb.Append('"');
                    i++;
                    continue;
                }
                inQuotes = !inQuotes;
                continue;
            }
            if (c == sep && !inQuotes) { parts.Add(sb.ToString()); sb.Clear(); continue; }
            sb.Append(c);
        }
        parts.Add(sb.ToString());
        return parts.ToArray();
    }
}
