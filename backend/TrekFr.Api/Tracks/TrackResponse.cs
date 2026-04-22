using System.Collections.Generic;
using System.Linq;
using TrekFr.Core.Domain;
using TrekFr.Core.UseCases;

namespace TrekFr.Api.Tracks;

public sealed record TrackResponse(
    string? Name,
    string Profile,
    TrackStatsDto Stats,
    object Geojson,
    double[]? Bbox,
    string? ProposedDestinationName,
    IReadOnlyList<StageDto>? Stages,
    IReadOnlyList<string>? Warnings)
{
    public static TrackResponse From(
        Track track,
        TrackStats stats,
        string? proposedDestinationName = null,
        IReadOnlyList<Stage>? stages = null,
        IReadOnlyList<string>? warnings = null)
    {
        var profile = track.Profile.ToString().ToLowerInvariant();
        return new TrackResponse(
            track.Name,
            profile,
            TrackStatsDto.From(stats),
            BuildLineStringFeature(track.Points, track.Name, profile),
            ComputeBbox(track.Points),
            proposedDestinationName,
            stages?.Select(s => StageDto.From(s, profile)).ToList(),
            warnings);
    }

    public static TrackResponse From(ImportedTrack imported) => From(imported.Track, imported.Stats);
    public static TrackResponse From(GeneratedTrack generated) => From(generated.Track, generated.Stats);
    public static TrackResponse From(ProposedGeneratedTrack proposed) =>
        From(proposed.Track, proposed.Stats, proposed.Destination.Name);

    internal static object BuildLineStringFeature(IReadOnlyList<Coordinate> points, string? name, string profile)
    {
        var coords = points
            .Select(p => p.Elevation is { } e
                ? new[] { p.Longitude, p.Latitude, e }
                : new[] { p.Longitude, p.Latitude })
            .ToArray();

        return new
        {
            type = "Feature",
            geometry = new
            {
                type = "LineString",
                coordinates = coords,
            },
            properties = new Dictionary<string, object?>
            {
                ["name"] = name,
                ["profile"] = profile,
            },
        };
    }

    internal static double[]? ComputeBbox(IReadOnlyList<Coordinate> points)
    {
        if (points.Count == 0) return null;
        var minLat = double.PositiveInfinity;
        var minLon = double.PositiveInfinity;
        var maxLat = double.NegativeInfinity;
        var maxLon = double.NegativeInfinity;
        foreach (var p in points)
        {
            if (p.Latitude < minLat) minLat = p.Latitude;
            if (p.Latitude > maxLat) maxLat = p.Latitude;
            if (p.Longitude < minLon) minLon = p.Longitude;
            if (p.Longitude > maxLon) maxLon = p.Longitude;
        }
        return [minLon, minLat, maxLon, maxLat];
    }
}

public sealed record TrackStatsDto(
    double DistanceMeters,
    double ElevationGainMeters,
    double ElevationLossMeters,
    double EstimatedDurationSeconds)
{
    public static TrackStatsDto From(TrackStats s) => new(
        s.DistanceMeters,
        s.ElevationGainMeters,
        s.ElevationLossMeters,
        s.EstimatedDuration.TotalSeconds);
}

public sealed record StageDto(
    int Index,
    TrackStatsDto Stats,
    object Geojson,
    double[]? Bbox,
    SleepSpotDto EndSleepSpot,
    double? OffTrackDistanceMeters)
{
    public static StageDto From(Stage stage, string profile) => new(
        stage.Index,
        TrackStatsDto.From(stage.Stats),
        TrackResponse.BuildLineStringFeature(stage.Points, name: null, profile),
        TrackResponse.ComputeBbox(stage.Points),
        SleepSpotDto.From(stage.EndSleepSpot),
        stage.OffTrackDistanceMeters);
}

public sealed record SleepSpotDto(
    string Name,
    double Latitude,
    double Longitude,
    SleepSpotKind Kind)
{
    public static SleepSpotDto From(SleepSpot s) =>
        new(s.Name, s.Location.Latitude, s.Location.Longitude, s.Kind);
}
