using System;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class ProposeDestination(IDestinationProposer proposer, IRoutingProvider router)
{
    private const int FilterIterationTopN = 5;

    public async Task<ProposedGeneratedTrack> ExecuteAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed,
        ElevationFilter? elevationFilter = null,
        CancellationToken ct = default)
    {
        var effectiveSeed = seed ?? Random.Shared.Next();

        if (elevationFilter is null || !elevationFilter.IsActive)
        {
            var dest = await proposer.ProposeAsync(start, targetDistanceMeters, profile, effectiveSeed, ct)
                ?? throw new NoDestinationCandidateException(
                    $"Aucune ville candidate dans le rayon {targetDistanceMeters / 1000d:F0} km (±10 %). Essaie une autre distance ou un autre point de départ.");
            var (track, extras) = await router.RouteAsync(start, dest.Location, profile, ct);
            var baseStats = TrackStatsCalculator.Compute(track);
            var stats = extras is not null
                ? baseStats with { Surface = extras.Surface, WayTypes = extras.WayTypes }
                : baseStats;
            return new ProposedGeneratedTrack(track, stats, dest, effectiveSeed);
        }

        var candidates = await proposer.GetTopCandidatesAsync(start, targetDistanceMeters, profile, FilterIterationTopN, ct);
        if (candidates.Count == 0)
        {
            throw new NoDestinationCandidateException(
                $"Aucune ville candidate dans le rayon {targetDistanceMeters / 1000d:F0} km (±10 %). Essaie une autre distance ou un autre point de départ.");
        }

        foreach (var dest in candidates)
        {
            var (track, extras) = await router.RouteAsync(start, dest.Location, profile, ct);
            var baseStats = TrackStatsCalculator.Compute(track);
            if (elevationFilter.Matches(baseStats.ElevationGainMeters))
            {
                var stats = extras is not null
                    ? baseStats with { Surface = extras.Surface, WayTypes = extras.WayTypes }
                    : baseStats;
                return new ProposedGeneratedTrack(track, stats, dest, effectiveSeed);
            }
        }
        throw new ElevationOutOfRangeException(
            elevationFilter,
            $"les {candidates.Count} villes candidates à cette distance");
    }
}

public sealed record ProposedGeneratedTrack(Track Track, TrackStats Stats, ProposedDestination Destination, int? Seed = null);

public sealed class NoDestinationCandidateException(string message) : Exception(message);
