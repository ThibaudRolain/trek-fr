using System;
using System.Collections.Generic;

namespace TrekFr.Core.Domain;

public static class TrackStatsCalculator
{
    private const double DefaultElevationThresholdMeters = 3d;

    public static TrackStats Compute(
        Track track,
        double elevationThresholdMeters = DefaultElevationThresholdMeters)
    {
        var distance = ComputeDistanceMeters(track.Points);
        var (gain, loss) = ComputeElevation(track.Points, elevationThresholdMeters);
        var duration = EstimateDuration(track.Profile, distance, gain);
        return new TrackStats(distance, gain, loss, duration);
    }

    private static double ComputeDistanceMeters(IReadOnlyList<Coordinate> points)
    {
        if (points.Count < 2) return 0d;
        var total = 0d;
        for (var i = 1; i < points.Count; i++)
        {
            total += Geo.HaversineMeters(points[i - 1], points[i]);
        }
        return total;
    }

    private static (double gain, double loss) ComputeElevation(
        IReadOnlyList<Coordinate> points,
        double threshold)
    {
        double? reference = null;
        var gain = 0d;
        var loss = 0d;
        foreach (var p in points)
        {
            if (p.Elevation is not { } ele) continue;
            if (reference is null)
            {
                reference = ele;
                continue;
            }
            var delta = ele - reference.Value;
            if (delta > threshold)
            {
                gain += delta;
                reference = ele;
            }
            else if (delta < -threshold)
            {
                loss += -delta;
                reference = ele;
            }
        }
        return (gain, loss);
    }

    private static TimeSpan EstimateDuration(Profile profile, double distanceMeters, double gainMeters)
    {
        var km = distanceMeters / 1000d;
        var hours = profile switch
        {
            Profile.Foot => km / 5d + Math.Max(0d, gainMeters / 600d),
            Profile.Mtb => km / 15d + Math.Max(0d, gainMeters / 500d),
            Profile.Road => km / 22d + Math.Max(0d, gainMeters / 400d),
            _ => km / 5d,
        };
        return TimeSpan.FromHours(hours);
    }
}
