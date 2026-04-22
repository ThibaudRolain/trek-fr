using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Infrastructure.Stages;

/// <summary>
/// ISleepSpotProvider effectif : fusionne les refuges (via IRefugeProvider — aujourd'hui vide)
/// et les villes (CommunesTownProvider). Les refuges sont prioritaires au scoring en amont
/// dans SplitIntoStages, pas besoin de pré-trier ici.
/// </summary>
public sealed class CompositeSleepSpotProvider(
    IRefugeProvider refugeProvider,
    CommunesTownProvider townProvider) : ISleepSpotProvider
{
    public async Task<IReadOnlyList<SleepSpotCandidate>> FindAlongTrackAsync(
        IReadOnlyList<Coordinate> trackPoints,
        double bufferMeters,
        CancellationToken ct = default)
    {
        var refuges = await refugeProvider.FindNearAsync(trackPoints, bufferMeters, ct);
        var towns = await townProvider.FindAsync(trackPoints, bufferMeters, ct);

        var result = new List<SleepSpotCandidate>(refuges.Count + towns.Count);
        foreach (var r in refuges)
        {
            var (idx, dist) = TrackProximity.FindNearest(trackPoints, r.Location);
            if (dist > bufferMeters) continue;
            var spot = new SleepSpot(r.Name, r.Location, SleepSpotKind.Refuge);
            result.Add(new SleepSpotCandidate(spot, idx, dist, 0d));
        }
        result.AddRange(towns);
        return result;
    }
}
