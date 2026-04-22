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
        if (elevationFilter is null || !elevationFilter.IsActive)
        {
            var dest = await proposer.ProposeAsync(start, targetDistanceMeters, profile, seed, ct)
                ?? throw new NoDestinationCandidateException(
                    $"Aucune ville candidate dans le rayon {targetDistanceMeters / 1000d:F0} km (±10 %). Essaie une autre distance ou un autre point de départ.");
            var track = await router.RouteAsync(start, dest.Location, profile, ct);
            return new ProposedGeneratedTrack(track, TrackStatsCalculator.Compute(track), dest);
        }

        var candidates = await proposer.GetTopCandidatesAsync(start, targetDistanceMeters, profile, FilterIterationTopN, ct);
        if (candidates.Count == 0)
        {
            throw new NoDestinationCandidateException(
                $"Aucune ville candidate dans le rayon {targetDistanceMeters / 1000d:F0} km (±10 %). Essaie une autre distance ou un autre point de départ.");
        }

        foreach (var dest in candidates)
        {
            var track = await router.RouteAsync(start, dest.Location, profile, ct);
            var stats = TrackStatsCalculator.Compute(track);
            if (elevationFilter.Matches(stats.ElevationGainMeters))
                return new ProposedGeneratedTrack(track, stats, dest);
        }
        throw new ElevationOutOfRangeException(
            elevationFilter,
            $"les {candidates.Count} villes candidates à cette distance");
    }
}

public sealed record ProposedGeneratedTrack(Track Track, TrackStats Stats, ProposedDestination Destination);

public sealed class NoDestinationCandidateException(string message) : Exception(message);
