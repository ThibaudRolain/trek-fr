using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using TrekFr.Infrastructure.Communes;

namespace TrekFr.Infrastructure.Destinations;

/// <summary>
/// Propose une ville d'arrivée à partir du dataset partagé `CommuneDataset`. Filtre par
/// distance crow-fly avec tolérance fixe, ranke par score patrimonial, tire aléatoirement
/// dans le top 5.
/// </summary>
public sealed class CommunesDestinationProposer(CommuneDataset dataset) : IDestinationProposer
{
    private const double DistanceTolerance = 0.10;
    private const int TopCandidates = 5;

    public Task<ProposedDestination?> ProposeAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed,
        CancellationToken ct = default)
    {
        var min = targetDistanceMeters * (1 - DistanceTolerance);
        var max = targetDistanceMeters * (1 + DistanceTolerance);

        var candidates = dataset.Entries
            .Select(c => (commune: c, distance: Geo.HaversineMeters(start.Latitude, start.Longitude, c.Lat, c.Lon)))
            .Where(x => x.distance >= min && x.distance <= max)
            .OrderByDescending(x => x.commune.Score)
            .Take(TopCandidates)
            .Select(x => x.commune)
            .ToList();

        if (candidates.Count == 0) return Task.FromResult<ProposedDestination?>(null);

        var rng = seed is { } s ? new Random(s) : Random.Shared;
        var picked = candidates[rng.Next(candidates.Count)];

        return Task.FromResult<ProposedDestination?>(
            new ProposedDestination(
                picked.Name,
                new Coordinate(picked.Lat, picked.Lon),
                picked.Population));
    }
}
