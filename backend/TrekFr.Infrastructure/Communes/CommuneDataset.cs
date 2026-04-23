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
        var maxMeters = maxDistanceKm * 1000d;
        // Bbox prefilter : évite ~31k haversines quand on cherche un rayon de quelques km.
        // 1° de latitude ≈ 111 km ; 1° de longitude ≈ 111 km × cos(lat).
        var dLat = maxDistanceKm / 111d;
        var cosLat = Math.Cos(point.Latitude * Math.PI / 180d);
        var dLon = cosLat > 0.01 ? maxDistanceKm / (111d * cosLat) : 180d;

        CommuneEntry? best = null;
        var bestDist = double.PositiveInfinity;
        foreach (var entry in _entries)
        {
            if (Math.Abs(entry.Lat - point.Latitude) > dLat) continue;
            if (Math.Abs(entry.Lon - point.Longitude) > dLon) continue;
            var d = Geo.HaversineMeters(point.Latitude, point.Longitude, entry.Lat, entry.Lon);
            if (d < bestDist)
            {
                bestDist = d;
                best = entry;
            }
        }
        if (best is null || bestDist > maxMeters) return null;
        return new Commune(best.Name, new Coordinate(best.Lat, best.Lon), best.Population);
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
            var d = Geo.HaversineMeters(point.Latitude, point.Longitude, entry.Lat, entry.Lon);
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
}

public sealed record CommuneEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon,
    [property: JsonPropertyName("pop")] int Population,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("mh")] int? MonumentsHistoriques = null,
    [property: JsonPropertyName("pbv")] bool IsPlusBeauVillage = false,
    [property: JsonPropertyName("vah")] bool IsVilleArtHistoire = false);
