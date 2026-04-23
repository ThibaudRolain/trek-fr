using System.Collections.Generic;
using System.Linq;
using TrekFr.Core.Abstractions;
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
    DestinationInfoDto? DestinationInfo,
    IReadOnlyList<StageDto>? Stages,
    IReadOnlyList<WarningDto>? Warnings,
    int? Seed,
    IReadOnlyList<PoiOnRouteDto>? PoisOnRoute)
{
    public static TrackResponse From(
        Track track,
        TrackStats stats,
        ProposedDestination? destination = null,
        IReadOnlyList<Stage>? stages = null,
        IReadOnlyList<WarningDto>? warnings = null,
        int? seed = null,
        IReadOnlyList<PoiOnRouteDto>? pois = null)
    {
        var profile = track.Profile.ToString().ToLowerInvariant();
        return new TrackResponse(
            track.Name,
            profile,
            TrackStatsDto.From(stats),
            BuildLineStringFeature(track.Points, track.Name, profile),
            ComputeBbox(track.Points),
            destination?.Name,
            DestinationInfoDto.From(destination),
            stages?.Select(s => StageDto.From(s, profile)).ToList(),
            warnings,
            seed,
            pois);
    }

    public static TrackResponse From(ImportedTrack imported) => From(imported.Track, imported.Stats);
    public static TrackResponse From(GeneratedTrack generated, IReadOnlyList<PoiOnRouteDto>? pois = null) =>
        From(generated.Track, generated.Stats, seed: generated.Seed, pois: pois);
    public static TrackResponse From(ProposedGeneratedTrack proposed, IReadOnlyList<PoiOnRouteDto>? pois = null) =>
        From(proposed.Track, proposed.Stats, proposed.Destination, seed: proposed.Seed, pois: pois);

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

public sealed record DestinationInfoDto(
    string Name,
    int? MonumentsHistoriques,
    bool IsPlusBeauVillage,
    bool IsVilleArtHistoire)
{
    internal static DestinationInfoDto? From(ProposedDestination? dest) => dest is null ? null
        : new(dest.Name, dest.MonumentsHistoriques, dest.IsPlusBeauVillage, dest.IsVilleArtHistoire);
}

/// <summary>
/// Warning non-bloquant attaché à la réponse. NearbyPlace est la commune la plus
/// proche du point de rupture (même au-delà du buffer 2 km) — le front construit
/// les liens Airbnb/Booking/Abritel sur ce nom pour que l'utilisateur vérifie
/// l'offre réelle.
/// </summary>
public sealed record WarningDto(
    string Message,
    string? NearbyPlace = null,
    double? NearbyPlaceDistanceMeters = null);

/// <summary>
/// POI patrimonial le long de la trace : commune ayant des monuments historiques
/// (données Mérimée, décomptées dans communes-fr.json) à moins de 2 km de la trace.
/// </summary>
public sealed record PoiOnRouteDto(
    string CommuneName,
    int MonumentCount,
    double Latitude,
    double Longitude,
    double DistanceFromStartMeters,
    double DistanceFromTrackMeters)
{
    public static PoiOnRouteDto From(TrekFr.Core.Domain.MhPoi p) => new(
        p.CommuneName,
        p.MonumentCount,
        p.Location.Latitude,
        p.Location.Longitude,
        p.DistanceFromStartMeters,
        p.DistanceFromTrackMeters);
}
