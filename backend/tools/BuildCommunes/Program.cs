using System.Net;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace TrekFr.Tools.BuildCommunes;

internal static class Program
{
    private const string OutputRelativePath = "backend/TrekFr.Infrastructure/Destinations/communes-fr.json";
    private const int PopulationThreshold = 200;
    private const int BetweenStagesMs = 500;

    public static async Task<int> Main(string[] args)
    {
        var repoRoot = ResolveRepoRoot();
        Console.WriteLine($"Repo root: {repoRoot}");

        using var http = BuildHttpClient();
        var sparql = new WikidataSparqlClient(http);
        var source = new CommunesDataSource(sparql);
        var merimee = new MerimeeDataSource(http);
        var scorer = new CommuneScorer(PopulationThreshold);

        Console.WriteLine("Fetching communes (IDs, coords, pop, INSEE, then labels)...");
        var communes = await source.FetchCommunesAsync();
        Console.WriteLine($"  → {communes.Count} communes");

        await Task.Delay(BetweenStagesMs);
        Console.WriteLine("Fetching heritage items in France (Wikidata, fallback if Mérimée fails)...");
        var heritage = await source.FetchHeritageCountsAsync();
        Console.WriteLine($"  → Wikidata heritage data on {heritage.Count} communes");

        await Task.Delay(BetweenStagesMs);
        Console.WriteLine("Fetching Base Mérimée (data.culture.gouv.fr)...");
        var merimeeCounts = await merimee.TryFetchCountsByInseeAsync();
        if (merimeeCounts is not null)
        {
            Console.WriteLine($"  → Mérimée counts on {merimeeCounts.Count} communes (will override Wikidata for score)");
        }
        else
        {
            Console.WriteLine("  → Mérimée unreachable, falling back to Wikidata heritage counts");
        }

        await Task.Delay(BetweenStagesMs);
        Console.WriteLine("Fetching 'Plus beaux villages de France'...");
        var pbv = await source.FetchCommunesMatchingOrgLabelAsync("Les Plus Beaux Villages de France");
        Console.WriteLine($"  → {pbv.Count} communes labelled");

        await Task.Delay(BetweenStagesMs);
        Console.WriteLine("Fetching 'Villes et Pays d'art et d'histoire'...");
        // Known limitation: the direct label→entity link finds ~8 communes ; the SPARQL discovery
        // approach times out (504) on Wikidata public endpoint. Fix deferred.
        var vah = await source.FetchCommunesMatchingOrgLabelAsync("Villes et Pays d'art et d'histoire");
        Console.WriteLine($"  → {vah.Count} communes labelled");

        Console.WriteLine("Scoring and filtering...");
        var signals = new HeritageSignals(heritage, merimeeCounts, pbv, vah);
        var scored = scorer.Score(communes, signals);
        Console.WriteLine($"Kept {scored.Count} communes (pop ≥ {PopulationThreshold}).");

        await WriteOutputAsync(scored, repoRoot);
        PrintTopBottom(scored);
        return 0;
    }

    private static HttpClient BuildHttpClient()
    {
        var handler = new HttpClientHandler
        {
            AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate,
        };
        var http = new HttpClient(handler);
        http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "trek-fr-BuildCommunes/0.2 (https://github.com/ThibaudRolain/trek-fr; formation@olympp.fr)");
        http.Timeout = TimeSpan.FromMinutes(5);
        return http;
    }

    private static async Task WriteOutputAsync(IReadOnlyList<CommuneEntry> entries, string repoRoot)
    {
        var outputPath = Path.Combine(repoRoot, OutputRelativePath.Replace('/', Path.DirectorySeparatorChar));
        Directory.CreateDirectory(Path.GetDirectoryName(outputPath)!);
        var json = JsonSerializer.Serialize(entries, new JsonSerializerOptions
        {
            WriteIndented = false,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingDefault,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
        });
        await File.WriteAllTextAsync(outputPath, json);
        Console.WriteLine($"Wrote {new FileInfo(outputPath).Length / 1024} kB → {outputPath}");
    }

    private static void PrintTopBottom(List<CommuneEntry> results)
    {
        Console.WriteLine();
        Console.WriteLine("Top 10 by score:");
        foreach (var r in results.Take(10))
        {
            Console.WriteLine($"  {r.Score,8:F2}  {r.Name,-30}  pop={r.Population,-7}  MH={r.MonumentsHistoriques,-3}  PBV={(r.IsPlusBeauVillage ? "✓" : " ")}  VAH={(r.IsVilleArtHistoire ? "✓" : " ")}");
        }
        Console.WriteLine();
        Console.WriteLine("Bottom 5 by score:");
        foreach (var r in results.TakeLast(5))
        {
            Console.WriteLine($"  {r.Score,8:F2}  {r.Name,-30}  pop={r.Population}");
        }
    }

    private static string ResolveRepoRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "backend", "TrekFr.slnx")))
        {
            dir = dir.Parent;
        }
        return dir?.FullName ?? throw new InvalidOperationException("Cannot find repo root (looked for backend/TrekFr.slnx).");
    }
}
