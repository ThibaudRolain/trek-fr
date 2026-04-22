using System;

namespace TrekFr.Core.Domain;

public static class Geo
{
    private const double EarthRadiusMeters = 6_371_000d;

    public static double HaversineMeters(Coordinate a, Coordinate b) =>
        HaversineMeters(a.Latitude, a.Longitude, b.Latitude, b.Longitude);

    public static double HaversineMeters(double lat1, double lon1, double lat2, double lon2)
    {
        var dLat = ToRadians(lat2 - lat1);
        var dLon = ToRadians(lon2 - lon1);
        var sinLat = Math.Sin(dLat / 2d);
        var sinLon = Math.Sin(dLon / 2d);
        var h = sinLat * sinLat
              + Math.Cos(ToRadians(lat1)) * Math.Cos(ToRadians(lat2)) * sinLon * sinLon;
        return 2d * EarthRadiusMeters * Math.Asin(Math.Min(1d, Math.Sqrt(h)));
    }

    private static double ToRadians(double degrees) => degrees * Math.PI / 180d;
}
