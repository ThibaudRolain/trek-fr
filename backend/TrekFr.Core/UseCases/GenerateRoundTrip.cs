using System;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class GenerateRoundTrip(IRoutingProvider router)
{
    private const int MaxElevationFilterAttempts = 5;

    public async Task<GeneratedTrack> ExecuteAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed,
        ElevationFilter? elevationFilter = null,
        CancellationToken ct = default)
    {
        if (elevationFilter is null || !elevationFilter.IsActive)
        {
            var track = await router.GenerateRoundTripAsync(start, targetDistanceMeters, profile, seed, ct);
            return new GeneratedTrack(track, TrackStatsCalculator.Compute(track));
        }

        for (int i = 0; i < MaxElevationFilterAttempts; i++)
        {
            var attemptSeed = seed.HasValue ? seed.Value + i : Random.Shared.Next();
            var track = await router.GenerateRoundTripAsync(start, targetDistanceMeters, profile, attemptSeed, ct);
            var stats = TrackStatsCalculator.Compute(track);
            if (elevationFilter.Matches(stats.ElevationGainMeters))
                return new GeneratedTrack(track, stats);
        }
        throw new ElevationOutOfRangeException(elevationFilter, "un round-trip à cette distance");
    }
}

public sealed record GeneratedTrack(Track Track, TrackStats Stats);
