using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class SplitIntoStages(ISleepSpotProvider spotProvider)
{
    private const double ElevationThresholdMeters = 3d;
    private const double EarthRadiusMeters = 6_371_000d;
    private const double PatrimonyDistancePenaltyPerKm = 5d;
    private const int MaxStagesSafety = 100;

    public async Task<IReadOnlyList<Stage>> ExecuteAsync(
        Track track,
        StageOptions options,
        CancellationToken ct = default)
    {
        if (track.Points.Count < 2)
            throw new ArgumentException("Track must have at least 2 points.", nameof(track));
        if (options.MaxDistancePerDayMeters <= 0)
            throw new ArgumentException("MaxDistancePerDayMeters must be > 0.", nameof(options));
        if (options.MaxElevationGainPerDay <= 0)
            throw new ArgumentException("MaxElevationGainPerDay must be > 0.", nameof(options));

        var cumDist = ComputeCumulativeDistance(track.Points);
        var cumGain = ComputeCumulativeGain(track.Points);
        var lastIndex = track.Points.Count - 1;

        if (cumDist[lastIndex] <= options.MaxDistancePerDayMeters
            && cumGain[lastIndex] <= options.MaxElevationGainPerDay)
        {
            return [BuildFinalStage(track, 1, 0, lastIndex, options.ArrivalName)];
        }

        var candidates = await spotProvider.FindAlongTrackAsync(
            track.Points, options.MaxOffTrackMeters, ct);

        var stages = new List<Stage>();
        var fromIndex = 0;
        var stageIndex = 1;

        while (true)
        {
            var remainingDist = cumDist[lastIndex] - cumDist[fromIndex];
            var remainingGain = cumGain[lastIndex] - cumGain[fromIndex];
            if (remainingDist <= options.MaxDistancePerDayMeters
                && remainingGain <= options.MaxElevationGainPerDay)
            {
                stages.Add(BuildFinalStage(track, stageIndex, fromIndex, lastIndex, options.ArrivalName));
                break;
            }

            var pivotIndex = FindPivot(cumDist, cumGain, fromIndex, lastIndex, options);
            var pivotCumDist = cumDist[pivotIndex];
            var stageDistAtPivot = pivotCumDist - cumDist[fromIndex];

            var pick = PickCandidate(
                candidates, cumDist, fromIndex, lastIndex, pivotCumDist,
                stageDistAtPivot, options.WindowTolerance);

            if (pick is null)
            {
                var approxKm = stageDistAtPivot / 1000d;
                throw new NoStageSleepSpotException(
                    stageIndex,
                    approxKm,
                    track.Points[pivotIndex],
                    $"Pas de commune à ≤ 2 km de la trace autour de l'étape {stageIndex} " +
                    $"(~{approxKm:F0} km du départ). L'app ne consulte pas Airbnb / Booking / " +
                    "Abritel — augmente km/jour ou D+/jour, ou utilise les liens ci-dessous.");
            }

            stages.Add(BuildStage(track, stageIndex, fromIndex, pick));
            fromIndex = pick.Candidate.NearestTrackIndex;
            stageIndex++;

            if (stages.Count > MaxStagesSafety)
                throw new InvalidOperationException(
                    "Stage splitting produced too many stages; check input parameters.");
        }

        return stages;
    }

    private static int FindPivot(double[] cumDist, double[] cumGain, int fromIndex, int lastIndex, StageOptions opts)
    {
        var baseDist = cumDist[fromIndex];
        var baseGain = cumGain[fromIndex];
        for (var i = fromIndex + 1; i <= lastIndex; i++)
        {
            if (cumDist[i] - baseDist >= opts.MaxDistancePerDayMeters
                || cumGain[i] - baseGain >= opts.MaxElevationGainPerDay)
            {
                return i;
            }
        }
        return lastIndex;
    }

    private static CandidatePick? PickCandidate(
        IReadOnlyList<SleepSpotCandidate> candidates,
        double[] cumDist,
        int fromIndex,
        int lastIndex,
        double pivotCumDist,
        double stageDistAtPivot,
        double windowTolerance)
    {
        var windowHalfWidth = stageDistAtPivot * windowTolerance;
        var minCumDist = pivotCumDist - windowHalfWidth;
        var maxCumDist = pivotCumDist + windowHalfWidth;

        CandidatePick? best = null;
        foreach (var c in candidates)
        {
            if (c.NearestTrackIndex <= fromIndex) continue;
            if (c.NearestTrackIndex >= lastIndex) continue;
            var d = cumDist[c.NearestTrackIndex];
            if (d < minCumDist || d > maxCumDist) continue;

            var kmFromPivot = Math.Abs(d - pivotCumDist) / 1000d;
            var score = c.Spot.Kind == SleepSpotKind.Refuge
                ? 1_000_000d - kmFromPivot * PatrimonyDistancePenaltyPerKm
                : c.PatrimonyScore - kmFromPivot * PatrimonyDistancePenaltyPerKm;

            if (best is null || score > best.Score)
                best = new CandidatePick(c, score);
        }
        return best;
    }

    private static Stage BuildStage(Track track, int index, int fromIndex, CandidatePick pick)
    {
        var toIndex = pick.Candidate.NearestTrackIndex;
        var slice = SlicePoints(track.Points, fromIndex, toIndex);
        var stats = TrackStatsCalculator.Compute(new Track(slice, track.Profile));
        return new Stage(
            index,
            slice,
            stats,
            pick.Candidate.Spot,
            pick.Candidate.OffTrackDistanceMeters);
    }

    private static Stage BuildFinalStage(Track track, int index, int fromIndex, int lastIndex, string arrivalName)
    {
        var slice = SlicePoints(track.Points, fromIndex, lastIndex);
        var stats = TrackStatsCalculator.Compute(new Track(slice, track.Profile));
        var arrival = new SleepSpot(arrivalName, track.Points[lastIndex], SleepSpotKind.Arrival);
        return new Stage(index, slice, stats, arrival, OffTrackDistanceMeters: null);
    }

    private static Coordinate[] SlicePoints(IReadOnlyList<Coordinate> points, int from, int to)
    {
        var count = to - from + 1;
        var result = new Coordinate[count];
        for (var i = 0; i < count; i++) result[i] = points[from + i];
        return result;
    }

    private static double[] ComputeCumulativeDistance(IReadOnlyList<Coordinate> points)
    {
        var cum = new double[points.Count];
        for (var i = 1; i < points.Count; i++)
            cum[i] = cum[i - 1] + Haversine(points[i - 1], points[i]);
        return cum;
    }

    private static double[] ComputeCumulativeGain(IReadOnlyList<Coordinate> points)
    {
        var cum = new double[points.Count];
        double? reference = null;
        var gain = 0d;
        for (var i = 0; i < points.Count; i++)
        {
            if (points[i].Elevation is double ele)
            {
                if (reference is null)
                {
                    reference = ele;
                }
                else
                {
                    var delta = ele - reference.Value;
                    if (delta > ElevationThresholdMeters)
                    {
                        gain += delta;
                        reference = ele;
                    }
                    else if (delta < -ElevationThresholdMeters)
                    {
                        reference = ele;
                    }
                }
            }
            cum[i] = gain;
        }
        return cum;
    }

    private static double Haversine(Coordinate a, Coordinate b)
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

    private sealed record CandidatePick(SleepSpotCandidate Candidate, double Score);
}

public sealed record StageOptions(
    double MaxDistancePerDayMeters,
    double MaxElevationGainPerDay,
    double WindowTolerance = 0.20,
    double MaxOffTrackMeters = 2_000,
    string ArrivalName = "Arrivée");

public sealed class NoStageSleepSpotException(
    int stageIndex,
    double approxKmFromStart,
    Coordinate pivotLocation,
    string message) : Exception(message)
{
    public int StageIndex { get; } = stageIndex;
    public double ApproxKmFromStart { get; } = approxKmFromStart;
    public Coordinate PivotLocation { get; } = pivotLocation;
}
