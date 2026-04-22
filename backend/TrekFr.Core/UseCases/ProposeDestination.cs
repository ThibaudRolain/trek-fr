using System;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class ProposeDestination(IDestinationProposer proposer, IRoutingProvider router)
{
    public async Task<ProposedGeneratedTrack> ExecuteAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed,
        CancellationToken ct = default)
    {
        var dest = await proposer.ProposeAsync(start, targetDistanceMeters, profile, seed, ct)
            ?? throw new NoDestinationCandidateException(
                $"Aucune ville candidate dans le rayon {targetDistanceMeters / 1000d:F0} km (±10 %). Essaie une autre distance ou un autre point de départ.");
        var track = await router.RouteAsync(start, dest.Location, profile, ct);
        var stats = TrackStatsCalculator.Compute(track);
        return new ProposedGeneratedTrack(track, stats, dest);
    }
}

public sealed record ProposedGeneratedTrack(Track Track, TrackStats Stats, ProposedDestination Destination);

public sealed class NoDestinationCandidateException(string message) : Exception(message);
