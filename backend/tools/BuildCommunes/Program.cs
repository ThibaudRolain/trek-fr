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
        var scorer = new CommuneScorer(PopulationThreshold);

        Console.WriteLine("Fetching communes (IDs, coords, pop, then labels)...");
        var communes = await source.FetchCommunesAsync();
        Console.WriteLine($"  → {communes.Count} communes");

        await Task.Delay(BetweenStagesMs);
        Console.WriteLine("Fetching heritage items in France...");
        var heritage = await source.FetchHeritageCountsAsync();
        Console.WriteLine($"  → heritage data on {heritage.Count} communes");

        await Task.Delay(BetweenStagesMs);
        Console.WriteLine("Fetching 'Plus beaux villages de France'...");
        var pbv = await source.FetchCommunesMatchingOrgLabelAsync("Les Plus Beaux Villages de France");
        Console.WriteLine($"  → {pbv.Count} communes labelled");

        await Task.Delay(BetweenStagesMs);
        Console.WriteLine("Fetching 'Villes et Pays d'art et d'histoire'...");
        var vah = await source.FetchCommunesMatchingOrgSubstringsAsync(["art et d", "histoire"]);
        Console.WriteLine($"  → {vah.Count} communes labelled");

        Console.WriteLine("Scoring and filtering...");
        var scored = scorer.Score(communes, heritage, pbv, vah);
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
