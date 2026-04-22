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
        // Génère un seed si l'utilisateur n'en fournit pas, pour pouvoir le reporter
        // au client et rendre la trace reproductible ("Régénérer cette variante").
        var baseSeed = seed ?? Random.Shared.Next();

        if (elevationFilter is null || !elevationFilter.IsActive)
        {
            var track = await router.GenerateRoundTripAsync(start, targetDistanceMeters, profile, baseSeed, ct);
            return new GeneratedTrack(track, TrackStatsCalculator.Compute(track), baseSeed);
        }

        for (int i = 0; i < MaxElevationFilterAttempts; i++)
        {
            var attemptSeed = baseSeed + i;
            var track = await router.GenerateRoundTripAsync(start, targetDistanceMeters, profile, attemptSeed, ct);
            var stats = TrackStatsCalculator.Compute(track);
            if (elevationFilter.Matches(stats.ElevationGainMeters))
                return new GeneratedTrack(track, stats, attemptSeed);
        }
        throw new ElevationOutOfRangeException(elevationFilter, "un round-trip à cette distance");
    }
}

public sealed record GeneratedTrack(Track Track, TrackStats Stats, int? Seed = null);
