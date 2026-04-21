namespace TrekFr.Tools.BuildCommunes;

public sealed class CommuneScorer(int populationThreshold)
{
    private const double MonumentHistoriqueWeight = 10d;
    private const double PlusBeauVillageBonus = 50d;
    private const double VilleArtHistoireBonus = 30d;

    public List<CommuneEntry> Score(IReadOnlyList<CommuneRaw> communes, HeritageSignals signals)
    {
        var result = new List<CommuneEntry>(communes.Count);
        foreach (var c in communes)
        {
            if (c.Population is null || c.Population < populationThreshold) continue;

            var mh = ResolveHeritageCount(c, signals);
            var isPbv = signals.PlusBeauxVillages.Contains(c.WikidataId);
            var isVah = signals.VillesArtHistoire.Contains(c.WikidataId);

            var score = MonumentHistoriqueWeight * mh
                        + (isPbv ? PlusBeauVillageBonus : 0d)
                        + (isVah ? VilleArtHistoireBonus : 0d)
                        + Math.Log(c.Population.Value);

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

    private static int ResolveHeritageCount(CommuneRaw c, HeritageSignals signals)
    {
        // Prefer Mérimée (authoritative for France) when the commune has an INSEE match.
        if (signals.MerimeeCountsByInsee is not null && c.InseeCode is not null &&
            signals.MerimeeCountsByInsee.TryGetValue(c.InseeCode, out var merimee))
        {
            return merimee;
        }
        return signals.WikidataHeritageCounts.TryGetValue(c.WikidataId, out var wdMh) ? wdMh : 0;
    }
}
