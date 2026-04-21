using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class GenerateRoundTrip(IRoutingProvider router)
{
    public async Task<GeneratedTrack> ExecuteAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed,
        CancellationToken ct = default)
    {
        var track = await router.GenerateRoundTripAsync(start, targetDistanceMeters, profile, seed, ct);
        var stats = TrackStatsCalculator.Compute(track);
        return new GeneratedTrack(track, stats);
    }
}

public sealed record GeneratedTrack(Track Track, TrackStats Stats);
