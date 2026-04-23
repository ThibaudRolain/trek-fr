using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class GenerateRoundTrip(IRoutingProvider router)
{
    private const int VariantCount = 3;
    private const int MaxElevationFilterAttempts = 5;

    /// <summary>
    /// Génère VariantCount variantes, triées par proximité à targetDistanceMeters.
    /// Appelé sur la première génération (seed == null). Apprend le ratio d'overshoot ORS
    /// sur le premier appel et l'applique aux suivants pour compenser le biais local.
    /// Max VariantCount appels ORS + 300 ms entre chaque.
    /// </summary>
    public async Task<IReadOnlyList<GeneratedTrack>> GenerateVariantsAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        CancellationToken ct = default)
    {
        var baseSeed = Random.Shared.Next();
        var results = new List<GeneratedTrack>(VariantCount);

        // Premier appel avec la target brute pour mesurer l'overshoot local
        var track0 = await router.GenerateRoundTripAsync(start, targetDistanceMeters, profile, baseSeed, ct);
        var stats0 = TrackStatsCalculator.Compute(track0);
        results.Add(new GeneratedTrack(track0, stats0, baseSeed));

        var deviation0 = Math.Abs(stats0.DistanceMeters - targetDistanceMeters) / targetDistanceMeters;
        // Si ORS overshoot : cible corrigée = target²/actual (annule le ratio d'overshoot)
        var effectiveTarget = deviation0 > 0.15
            ? targetDistanceMeters * targetDistanceMeters / stats0.DistanceMeters
            : targetDistanceMeters;

        // Variantes suivantes avec la target corrigée et des seeds différents
        for (int i = 1; i < VariantCount; i++)
        {
            await Task.Delay(300, ct);
            var seed = baseSeed + i;
            var track = await router.GenerateRoundTripAsync(start, effectiveTarget, profile, seed, ct);
            var stats = TrackStatsCalculator.Compute(track);
            results.Add(new GeneratedTrack(track, stats, seed));
        }

        return results
            .OrderBy(r => Math.Abs(r.Stats.DistanceMeters - targetDistanceMeters))
            .ToList();
    }

    /// <summary>
    /// Génère une trace unique pour un seed explicite (ex : "Autre variante" depuis le front,
    /// ou rejeu d'une trace sauvegardée). Applique la correction d'overshoot si nécessaire
    /// sauf si le seed est explicitement fourni par l'utilisateur (on respecte ce qu'il demande).
    /// </summary>
    public async Task<GeneratedTrack> ExecuteAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed,
        ElevationFilter? elevationFilter = null,
        CancellationToken ct = default)
    {
        var baseSeed = seed ?? Random.Shared.Next();

        if (elevationFilter is null || !elevationFilter.IsActive)
        {
            var track = await router.GenerateRoundTripAsync(start, targetDistanceMeters, profile, baseSeed, ct);
            return new GeneratedTrack(track, TrackStatsCalculator.Compute(track), baseSeed);
        }

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
