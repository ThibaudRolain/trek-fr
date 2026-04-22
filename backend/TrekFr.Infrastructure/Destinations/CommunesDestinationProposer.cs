using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Infrastructure.Destinations;

/// <summary>
/// Propose une ville d'arrivée à partir d'un dataset de communes françaises bundled
/// (`communes-fr.json`, généré par le tool BuildCommunes). Filtre par distance crow-fly
/// avec tolérance fixe, ranke par score patrimonial, tire aléatoirement dans le top 5.
/// </summary>
public sealed class CommunesDestinationProposer : IDestinationProposer
{
    private const double DistanceTolerance = 0.10; // ±10% autour de la distance cible
    private const int TopCandidates = 5;
    private const string EmbeddedResourceName = "TrekFr.Infrastructure.Destinations.communes-fr.json";

    private readonly IReadOnlyList<CommuneRecord> _communes;

    public CommunesDestinationProposer()
    {
        _communes = LoadEmbedded();
    }

    public Task<ProposedDestination?> ProposeAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed,
        CancellationToken ct = default)
    {
        var min = targetDistanceMeters * (1 - DistanceTolerance);
        var max = targetDistanceMeters * (1 + DistanceTolerance);

        var candidates = _communes
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

    private static IReadOnlyList<CommuneRecord> LoadEmbedded()
    {
        var assembly = typeof(CommunesDestinationProposer).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found. Run backend/tools/BuildCommunes to generate communes-fr.json.");
        var result = JsonSerializer.Deserialize<List<CommuneRecord>>(stream)
            ?? throw new InvalidOperationException("communes-fr.json deserialized to null.");
        return result;
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

    private sealed record CommuneRecord(
        [property: JsonPropertyName("name")] string Name,
        [property: JsonPropertyName("lat")] double Lat,
        [property: JsonPropertyName("lon")] double Lon,
        [property: JsonPropertyName("pop")] int Population,
        [property: JsonPropertyName("score")] double Score);
}
