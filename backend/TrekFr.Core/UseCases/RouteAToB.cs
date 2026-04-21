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
        CancellationToken ct = default)
    {
        var track = await router.RouteAsync(from, to, profile, ct);
        var stats = TrackStatsCalculator.Compute(track);
        return new GeneratedTrack(track, stats);
    }
}
