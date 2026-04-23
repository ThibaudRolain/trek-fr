using System;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class GenerateRoundTrip(IRoutingProvider router)
{
    // 10 seeds différents donnent 10 formes de boucle différentes. Au-delà, ORS renvoie
    // des variations de moins en moins utiles — si aucune des 10 ne match la tolérance,
    // c'est que le réseau routier du point de départ ne supporte pas cette distance.
    private const int MaxAttempts = 10;

    /// <summary>
    /// Seuil au-delà duquel la distance ORS est considérée "trop éloignée" de la cible.
    /// ORS round_trip a naturellement de la variance (la trace suit des chemins existants
    /// donc ne peut pas être pile à la target) ; ±20 % couvre la variance normale sans
    /// laisser passer les cas pathologiques (zones isolées où ORS renvoie du 5-10× off).
    /// </summary>
    private const double DistanceToleranceRatio = 0.20;

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
        var filter = elevationFilter ?? new ElevationFilter(null, null);

        // Attempts jusqu'à MaxAttempts : chaque essai doit satisfaire à la fois la tolérance
        // distance (toujours active) ET le filtre dénivelé (si set). Si le user n'a pas mis
        // de filter, le premier essai suffit en pratique — les retries servent aux cas où
        // ORS renvoie une longueur aberrante pour le premier seed.
        // On traque la distance la + proche de la cible sur tous les essais foirés pour
        // donner une info utile au user ("on a essayé N fois, le mieux a été X km").
        double? bestDistanceSoFar = null;
        int distanceMismatchCount = 0;
        for (int i = 0; i < MaxAttempts; i++)
        {
            var attemptSeed = baseSeed + i;
            var (track, extras) = await router.GenerateRoundTripAsync(start, targetDistanceMeters, profile, attemptSeed, ct);
            var baseStats = TrackStatsCalculator.Compute(track);

            if (!IsDistanceInTolerance(baseStats.DistanceMeters, targetDistanceMeters))
            {
                distanceMismatchCount++;
                if (bestDistanceSoFar is null ||
                    Math.Abs(baseStats.DistanceMeters - targetDistanceMeters) < Math.Abs(bestDistanceSoFar.Value - targetDistanceMeters))
                {
                    bestDistanceSoFar = baseStats.DistanceMeters;
                }
                continue;
            }
            if (!filter.Matches(baseStats.ElevationGainMeters))
            {
                continue;
            }
            var stats = extras is not null
                ? baseStats with { Surface = extras.Surface, WayTypes = extras.WayTypes }
                : baseStats;
            return new GeneratedTrack(track, stats, attemptSeed);
        }

        // Pas de succès complet après MaxAttempts. On privilégie le message "distance off"
        // si c'est la cause systématique : l'user ne pourra de toute façon pas obtenir
        // mieux avec le filtre tant que la base distance ne match pas.
        if (bestDistanceSoFar is not null && distanceMismatchCount == MaxAttempts)
        {
            throw new DistanceMismatchException(targetDistanceMeters, bestDistanceSoFar.Value, MaxAttempts);
        }
        throw new ElevationOutOfRangeException(filter, "un round-trip à cette distance");
    }

    private static bool IsDistanceInTolerance(double actual, double target)
    {
        if (target <= 0) return true;
        var ratio = Math.Abs(actual - target) / target;
        return ratio <= DistanceToleranceRatio;
    }
}

public sealed record GeneratedTrack(Track Track, TrackStats Stats, int? Seed = null);
