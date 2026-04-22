using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Infrastructure.Destinations;

/// <summary>
/// Propose une ville d'arrivée à partir du dataset bundled de communes françaises.
/// Filtre par distance crow-fly avec tolérance fixe, ranke par score patrimonial,
/// tire aléatoirement dans le top 5.
/// </summary>
public sealed class CommunesDestinationProposer(CommunesDataset dataset) : IDestinationProposer
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

        var candidates = dataset.Communes
            .Select(c => (commune: c, distance: Haversine(start.Latitude, start.Longitude, c.Lat, c.Lon)))
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

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        const double earthRadiusMeters = 6_371_000d;
        var dLat = DegToRad(lat2 - lat1);
        var dLon = DegToRad(lon2 - lon1);
        var sinLat = Math.Sin(dLat / 2);
        var sinLon = Math.Sin(dLon / 2);
        var a = sinLat * sinLat + Math.Cos(DegToRad(lat1)) * Math.Cos(DegToRad(lat2)) * sinLon * sinLon;
        return 2 * earthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }

    private static double DegToRad(double deg) => deg * Math.PI / 180d;
}
