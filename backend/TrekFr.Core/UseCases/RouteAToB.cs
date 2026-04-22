using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class RouteAToB(IRoutingProvider router)
{
    public async Task<GeneratedTrack> ExecuteAsync(
        Coordinate from,
        Coordinate to,
        Profile profile,
        ElevationFilter? elevationFilter = null,
        CancellationToken ct = default)
    {
        var track = await router.RouteAsync(from, to, profile, ct);
        var stats = TrackStatsCalculator.Compute(track);

        // A→B imposé est déterministe : pas de retry possible. Si le filter ne match pas,
        // throw pour laisser l'utilisateur retuner (cf. feedback_walk_tolerances.md).
        if (elevationFilter is { IsActive: true } f && !f.Matches(stats.ElevationGainMeters))
        {
            throw new ElevationOutOfRangeException(
                f, $"la route A→B ({stats.DistanceMeters / 1000d:F1} km)");
        }

        return new GeneratedTrack(track, stats);
    }
}
