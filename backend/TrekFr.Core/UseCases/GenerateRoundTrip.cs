using System;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class GenerateRoundTrip(IRoutingProvider router)
{
    // Retries pour le filtre D+ : chaque seed génère un dénivelé différent.
    private const int MaxElevationFilterAttempts = 5;

    public async Task<GeneratedTrack> ExecuteAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed,
        ElevationFilter? elevationFilter = null,
        CancellationToken ct = default)
    {
        var baseSeed = seed ?? Random.Shared.Next();

        // Sans filtre D+ : appel initial avec la target demandée.
        // Si ORS overshoot de >15 %, une correction proportionnelle (target²/actual) compense
        // le biais du réseau local — si le ratio d'overshoot est constant, la formule donne
        // exactement la bonne target au 2e appel. Max 2 appels ORS, 300 ms entre les deux.
        // Seed explicite (bouton "Autre variante") : on ne corrige pas, on respecte le seed.
        if (elevationFilter is null || !elevationFilter.IsActive)
        {
            var track1 = await router.GenerateRoundTripAsync(start, targetDistanceMeters, profile, baseSeed, ct);
            var stats1 = TrackStatsCalculator.Compute(track1);
            var deviation1 = Math.Abs(stats1.DistanceMeters - targetDistanceMeters) / targetDistanceMeters;

            if (deviation1 <= 0.15 || seed is not null)
                return new GeneratedTrack(track1, stats1, baseSeed);

            // Correction : target² / actual compense le ratio d'overshoot
            var correctedTarget = targetDistanceMeters * targetDistanceMeters / stats1.DistanceMeters;
            await Task.Delay(300, ct);
            var track2 = await router.GenerateRoundTripAsync(start, correctedTarget, profile, baseSeed, ct);
            var stats2 = TrackStatsCalculator.Compute(track2);

            return Math.Abs(stats2.DistanceMeters - targetDistanceMeters) < Math.Abs(stats1.DistanceMeters - targetDistanceMeters)
                ? new GeneratedTrack(track2, stats2, baseSeed)
                : new GeneratedTrack(track1, stats1, baseSeed);
        }

        // Avec filtre D+ : on essaie plusieurs seeds pour trouver une variante dans la
        // plage D+ demandée. Délai entre les appels pour ne pas saturer le rate limit ORS.
        for (int i = 0; i < MaxElevationFilterAttempts; i++)
        {
            if (i > 0) await Task.Delay(500, ct);
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
