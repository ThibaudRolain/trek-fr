using System.Text.Json;
using System.Web;

namespace TrekFr.Tools.BuildCommunes;

/// <summary>
/// Récupère les Wikidata Q-IDs des membres d'une catégorie sur fr.wikipedia.org
/// via l'API MediaWiki. Utile pour des labels dont Wikidata est incomplet
/// (ex: Villes et Pays d'art et d'histoire — SPARQL n'en trouve que 8, la
/// catégorie Wikipedia en liste ~200).
/// </summary>
public sealed class MediaWikiCategoryDataSource(HttpClient http)
{
    private const string ApiEndpoint = "https://fr.wikipedia.org/w/api.php";

    public async Task<HashSet<string>> FetchQIdsForCategoryAsync(
        string categoryTitleWithoutPrefix,
        CancellationToken ct = default)
    {
        var qids = new HashSet<string>();
        string? continueToken = null;

        do
        {
            var url = BuildUrl(categoryTitleWithoutPrefix, continueToken);
            using var resp = await http.GetAsync(url, ct);
            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                throw new InvalidOperationException($"MediaWiki {(int)resp.StatusCode}: {Truncate(body, 200)}");
            }
            await using var stream = await resp.Content.ReadAsStreamAsync(ct);
            using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

            if (doc.RootElement.TryGetProperty("query", out var query)
                && query.TryGetProperty("pages", out var pages))
            {
                foreach (var page in pages.EnumerateObject())
                {
                    if (page.Value.TryGetProperty("pageprops", out var props)
                        && props.TryGetProperty("wikibase_item", out var qid))
                    {
                        var value = qid.GetString();
                        if (!string.IsNullOrEmpty(value)) qids.Add(value);
                    }
                }
            }

            continueToken = doc.RootElement.TryGetProperty("continue", out var cont)
                            && cont.TryGetProperty("gcmcontinue", out var gcm)
                ? gcm.GetString()
                : null;
        } while (continueToken is not null);

        return qids;
    }

    private static string BuildUrl(string categoryTitle, string? continueToken)
    {
        var cmTitle = $"Catégorie:{categoryTitle}";
        var q = new Dictionary<string, string>
        {
            ["action"] = "query",
            ["format"] = "json",
            ["generator"] = "categorymembers",
            ["gcmtitle"] = cmTitle,
            ["gcmlimit"] = "500",
            ["gcmtype"] = "page",
            ["prop"] = "pageprops",
            ["ppprop"] = "wikibase_item",
        };
        if (continueToken is not null) q["gcmcontinue"] = continueToken;

        var query = string.Join("&", q.Select(kv =>
            $"{HttpUtility.UrlEncode(kv.Key)}={HttpUtility.UrlEncode(kv.Value)}"));
        return $"{ApiEndpoint}?{query}";
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s[..max] + "...";
}
