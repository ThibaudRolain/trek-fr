namespace TrekFr.Tools.BuildCommunes;

public sealed class CommuneScorer(int populationThreshold)
{
    public List<CommuneEntry> Score(
        IReadOnlyList<CommuneRaw> communes,
        IReadOnlyDictionary<string, int> heritageCounts,
        IReadOnlySet<string> plusBeauxVillages,
        IReadOnlySet<string> villesArtHistoire)
    {
        var result = new List<CommuneEntry>(communes.Count);
        foreach (var c in communes)
        {
            if (c.Population is < 0 or null) continue;
            if (c.Population < populationThreshold) continue;

            heritageCounts.TryGetValue(c.WikidataId, out var mh);
            var isPbv = plusBeauxVillages.Contains(c.WikidataId);
            var isVah = villesArtHistoire.Contains(c.WikidataId);

            var score = 10d * mh
                        + (isPbv ? 50d : 0d)
                        + (isVah ? 30d : 0d)
                        + (c.Population > 0 ? Math.Log(c.Population.Value) : 0d);

            result.Add(new CommuneEntry(
                c.Name,
                Math.Round(c.Lat, 5),
                Math.Round(c.Lon, 5),
                c.Population.Value,
                Math.Round(score, 2),
                mh,
                isPbv,
                isVah));
        }
        result.Sort((a, b) => b.Score.CompareTo(a.Score));
        return result;
    }
}
