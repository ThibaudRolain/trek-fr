using System.Net.Http.Headers;
using System.Text;

namespace TrekFr.Tools.BuildCommunes;

public sealed class WikidataSparqlClient(HttpClient http)
{
    private const string Endpoint = "https://query.wikidata.org/sparql";
    private const int MaxAttempts = 3;

    public async Task<List<Dictionary<string, string>>> RunTsvAsync(string query, CancellationToken ct = default)
    {
        HttpResponseMessage? resp = null;
        string? body = null;
        for (int attempt = 1; attempt <= MaxAttempts; attempt++)
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, Endpoint);
            req.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("text/tab-separated-values"));
            req.Content = new FormUrlEncodedContent(new[]
            {
                new KeyValuePair<string, string>("query", query),
            });
            resp?.Dispose();
            resp = await http.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode) break;
            body = await resp.Content.ReadAsStringAsync(ct);
            if (attempt == MaxAttempts) break;
            var backoff = TimeSpan.FromSeconds(2 * attempt);
            Console.WriteLine($"  SPARQL {(int)resp.StatusCode} on attempt {attempt}/{MaxAttempts}, retrying in {backoff.TotalSeconds}s...");
            await Task.Delay(backoff, ct);
        }
        if (resp is null || !resp.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"SPARQL {(int?)resp?.StatusCode}: {body}");
        }
        using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var reader = new StreamReader(stream);

        var headerLine = await reader.ReadLineAsync(ct);
        if (headerLine is null) return [];
        var headers = headerLine.Split('\t').Select(h => h.TrimStart('?')).ToArray();

        var rows = new List<Dictionary<string, string>>();
        string? line;
        while ((line = await reader.ReadLineAsync(ct)) is not null)
        {
            var fields = line.Split('\t');
            var dict = new Dictionary<string, string>(headers.Length);
            for (int i = 0; i < headers.Length && i < fields.Length; i++)
            {
                var raw = fields[i];
                if (string.IsNullOrEmpty(raw)) continue;
                dict[headers[i]] = UnquoteTsv(raw);
            }
            rows.Add(dict);
        }
        return rows;
    }

    internal static string StripWikidataUri(string uri)
    {
        var slash = uri.LastIndexOf('/');
        return slash < 0 ? uri : uri[(slash + 1)..].TrimEnd('>');
    }

    private static string UnquoteTsv(string raw)
    {
        // Wikidata TSV: URIs as <http://...>, literals as "..."@lang or "..."^^<datatype>.
        if (raw.Length >= 2 && raw[0] == '<' && raw[^1] == '>') return raw[1..^1];
        if (raw.Length >= 2 && raw[0] == '"')
        {
            var sb = new StringBuilder();
            int i = 1;
            while (i < raw.Length)
            {
                var c = raw[i];
                if (c == '\\' && i + 1 < raw.Length)
                {
                    sb.Append(raw[i + 1] switch
                    {
                        '"' => '"',
                        '\\' => '\\',
                        'n' => '\n',
                        't' => '\t',
                        'r' => '\r',
                        var ch => ch,
                    });
                    i += 2;
                    continue;
                }
                if (c == '"') break;
                sb.Append(c);
                i++;
            }
            return sb.ToString();
        }
        return raw;
    }
}
