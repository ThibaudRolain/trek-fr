using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Infrastructure.Communes;

/// <summary>
/// Chargement one-shot du dataset `communes-fr.json` (embedded resource) et requêtes
/// partagées par les features destination + weather (reverse-geocoding rudimentaire).
/// </summary>
public sealed class CommuneDataset : INearestCommuneFinder
{
    private const string EmbeddedResourceName = "TrekFr.Infrastructure.Communes.communes-fr.json";

    private readonly IReadOnlyList<CommuneEntry> _entries;

    public CommuneDataset()
    {
        _entries = LoadEmbedded();
    }

    public IReadOnlyList<CommuneEntry> Entries => _entries;

    public Commune? FindNearest(Coordinate point, double maxDistanceKm = 50)
    {
        var nearest = FindNearestWithDistance(point);
        if (nearest is null) return null;
        if (nearest.Value.DistanceMeters > maxDistanceKm * 1000d) return null;
        return nearest.Value.Commune;
    }

    /// <summary>
    /// Nearest commune in the dataset + its crow-fly distance, without any max cap.
    /// Used to enrich warnings ("no commune ≤ 2 km — nearest is X at Y km").
    /// </summary>
    public (Commune Commune, double DistanceMeters)? FindNearestWithDistance(Coordinate point)
    {
        CommuneEntry? best = null;
        var bestDist = double.PositiveInfinity;
        foreach (var entry in _entries)
        {
            var d = Haversine(point.Latitude, point.Longitude, entry.Lat, entry.Lon);
            if (d < bestDist)
            {
                bestDist = d;
                best = entry;
            }
        }
        if (best is null) return null;
        return (new Commune(best.Name, new Coordinate(best.Lat, best.Lon), best.Population), bestDist);
    }

    private static IReadOnlyList<CommuneEntry> LoadEmbedded()
    {
        var assembly = typeof(CommuneDataset).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found. Run backend/tools/BuildCommunes to generate communes-fr.json.");
        return JsonSerializer.Deserialize<List<CommuneEntry>>(stream)
            ?? throw new InvalidOperationException("communes-fr.json deserialized to null.");
    }

    internal static double Haversine(double lat1, double lon1, double lat2, double lon2)
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

public sealed record CommuneEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon,
    [property: JsonPropertyName("pop")] int Population,
    [property: JsonPropertyName("score")] double Score);
