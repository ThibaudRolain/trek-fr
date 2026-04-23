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
        var (track, extras) = await router.RouteAsync(from, to, profile, ct);
        var baseStats = TrackStatsCalculator.Compute(track);

        // A→B imposé est déterministe : pas de retry possible. Si le filter ne match pas,
        // throw pour laisser l'utilisateur retuner (cf. feedback_walk_tolerances.md).
        if (elevationFilter is { IsActive: true } f && !f.Matches(baseStats.ElevationGainMeters))
        {
            throw new ElevationOutOfRangeException(
                f, $"la route A→B ({baseStats.DistanceMeters / 1000d:F1} km)");
        }

        var stats = extras is not null
            ? baseStats with { Surface = extras.Surface, WayTypes = extras.WayTypes }
            : baseStats;
        return new GeneratedTrack(track, stats);
    }
}
