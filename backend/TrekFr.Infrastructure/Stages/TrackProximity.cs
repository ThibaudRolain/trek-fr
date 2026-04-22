using System;
using System.Collections.Generic;
using TrekFr.Core.Domain;

namespace TrekFr.Infrastructure.Stages;

internal static class TrackProximity
{
    private const double EarthRadiusMeters = 6_371_000d;

    public static (int Index, double DistanceMeters) FindNearest(
        IReadOnlyList<Coordinate> track, Coordinate point)
    {
        var bestIdx = 0;
        var bestDist = double.PositiveInfinity;
        for (var i = 0; i < track.Count; i++)
        {
            var d = Haversine(track[i], point);
            if (d < bestDist) { bestDist = d; bestIdx = i; }
        }
        return (bestIdx, bestDist);
    }

    public static (double MinLat, double MaxLat, double MinLon, double MaxLon) ComputeBBox(
        IReadOnlyList<Coordinate> track)
    {
        var minLat = double.PositiveInfinity;
        var maxLat = double.NegativeInfinity;
        var minLon = double.PositiveInfinity;
        var maxLon = double.NegativeInfinity;
        foreach (var p in track)
        {
            if (p.Latitude < minLat) minLat = p.Latitude;
            if (p.Latitude > maxLat) maxLat = p.Latitude;
            if (p.Longitude < minLon) minLon = p.Longitude;
            if (p.Longitude > maxLon) maxLon = p.Longitude;
        }
        return (minLat, maxLat, minLon, maxLon);
    }

    public static double Haversine(Coordinate a, Coordinate b)
    {
        var lat1 = ToRadians(a.Latitude);
        var lat2 = ToRadians(b.Latitude);
        var dLat = ToRadians(b.Latitude - a.Latitude);
        var dLon = ToRadians(b.Longitude - a.Longitude);
        var s = Math.Sin(dLat / 2);
        var t = Math.Sin(dLon / 2);
        var h = s * s + Math.Cos(lat1) * Math.Cos(lat2) * t * t;
        return 2 * EarthRadiusMeters * Math.Asin(Math.Min(1, Math.Sqrt(h)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
