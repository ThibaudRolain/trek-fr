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
    double[]? Bbox)
{
    public static TrackResponse From(Track track, TrackStats stats)
    {
        var coords = track.Points
            .Select(p => p.Elevation is { } e
                ? new[] { p.Longitude, p.Latitude, e }
                : new[] { p.Longitude, p.Latitude })
            .ToArray();

        var geojson = new
        {
            type = "Feature",
            geometry = new
            {
                type = "LineString",
                coordinates = coords,
            },
            properties = new Dictionary<string, object?>
            {
                ["name"] = track.Name,
                ["profile"] = track.Profile.ToString().ToLowerInvariant(),
            },
        };

        return new TrackResponse(
            track.Name,
            track.Profile.ToString().ToLowerInvariant(),
            TrackStatsDto.From(stats),
            geojson,
            ComputeBbox(track.Points));
    }

    public static TrackResponse From(ImportedTrack imported) => From(imported.Track, imported.Stats);
    public static TrackResponse From(GeneratedTrack generated) => From(generated.Track, generated.Stats);

    private static double[]? ComputeBbox(IReadOnlyList<Coordinate> points)
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
