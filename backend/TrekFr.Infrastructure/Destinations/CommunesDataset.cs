using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using TrekFr.Core.Domain;

namespace TrekFr.Infrastructure.Destinations;

/// <summary>
/// Singleton wrapper around the bundled communes-fr.json dataset. Loaded once at startup
/// and shared by every consumer (destination proposer, town provider, etc.).
/// </summary>
public sealed class CommunesDataset
{
    private const string EmbeddedResourceName = "TrekFr.Infrastructure.Destinations.communes-fr.json";
    private const double EarthRadiusMeters = 6_371_000d;

    public IReadOnlyList<CommuneRecord> Communes { get; }

    public CommunesDataset()
    {
        Communes = LoadEmbedded();
    }

    /// <summary>
    /// Nearest commune to an arbitrary point, without any buffer filter. Used to
    /// enrich "no sleep spot found" warnings with a meaningful anchor even when
    /// the regular provider filter came up empty.
    /// </summary>
    public (CommuneRecord Commune, double DistanceMeters) FindNearest(Coordinate point)
    {
        CommuneRecord? best = null;
        var bestDist = double.PositiveInfinity;
        foreach (var c in Communes)
        {
            var d = Haversine(point.Latitude, point.Longitude, c.Lat, c.Lon);
            if (d < bestDist) { bestDist = d; best = c; }
        }
        if (best is null)
            throw new InvalidOperationException("Communes dataset is empty.");
        return (best, bestDist);
    }

    private static IReadOnlyList<CommuneRecord> LoadEmbedded()
    {
        var assembly = typeof(CommunesDataset).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found. Run backend/tools/BuildCommunes to generate communes-fr.json.");
        return JsonSerializer.Deserialize<List<CommuneRecord>>(stream)
            ?? throw new InvalidOperationException("communes-fr.json deserialized to null.");
    }

    private static double Haversine(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = (lat2 - lat1) * Math.PI / 180d;
        var dLon = (lon2 - lon1) * Math.PI / 180d;
        var sLat = Math.Sin(dLat / 2);
        var sLon = Math.Sin(dLon / 2);
        var a = sLat * sLat
                + Math.Cos(lat1 * Math.PI / 180d) * Math.Cos(lat2 * Math.PI / 180d) * sLon * sLon;
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(a)));
    }
}
