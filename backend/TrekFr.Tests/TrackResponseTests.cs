using System;
using System.Collections.Generic;
using System.Text.Json;
using TrekFr.Api.Tracks;
using TrekFr.Core.Domain;
using TrekFr.Core.UseCases;
using Xunit;

namespace TrekFr.Tests;

public class TrackResponseTests
{
    private static readonly Track SampleTrack = new(
        new List<Coordinate>
        {
            new(48.85, 2.35, 35),
            new(48.86, 2.36, 40),
            new(48.87, 2.37, 50),
        },
        Profile.Foot,
        Name: "Sample");

    private static readonly TrackStats SampleStats =
        new(DistanceMeters: 1234, ElevationGainMeters: 15, ElevationLossMeters: 0, EstimatedDuration: TimeSpan.FromMinutes(30));

    [Fact]
    public void Maps_profile_enum_to_lowercase_string()
    {
        var track = SampleTrack with { Profile = Profile.Mtb };
        var response = TrackResponse.From(track, SampleStats);
        Assert.Equal("mtb", response.Profile);
    }

    [Fact]
    public void Keeps_name_when_track_is_named()
    {
        var response = TrackResponse.From(SampleTrack, SampleStats);
        Assert.Equal("Sample", response.Name);
    }

    [Fact]
    public void Bbox_is_minLon_minLat_maxLon_maxLat()
    {
        var response = TrackResponse.From(SampleTrack, SampleStats);
        Assert.NotNull(response.Bbox);
        // points : (48.85,2.35) → (48.87,2.37)
        Assert.Equal(new[] { 2.35, 48.85, 2.37, 48.87 }, response.Bbox);
    }

    [Fact]
    public void Bbox_is_null_when_track_has_no_points()
    {
        var empty = new Track(Array.Empty<Coordinate>(), Profile.Foot);
        var response = TrackResponse.From(empty, SampleStats);
        Assert.Null(response.Bbox);
    }

    [Fact]
    public void GeoJSON_encodes_coordinates_as_lon_lat_elev()
    {
        var response = TrackResponse.From(SampleTrack, SampleStats);
        // Le payload anonyme passe par System.Text.Json pour être envoyé — on reflète ce chemin.
        var json = JsonSerializer.Serialize(response.Geojson);
        using var doc = JsonDocument.Parse(json);
        var coords = doc.RootElement.GetProperty("geometry").GetProperty("coordinates");

        Assert.Equal("Feature", doc.RootElement.GetProperty("type").GetString());
        Assert.Equal(3, coords.GetArrayLength());
        Assert.Equal(2.35, coords[0][0].GetDouble()); // lon
        Assert.Equal(48.85, coords[0][1].GetDouble()); // lat
        Assert.Equal(35, coords[0][2].GetDouble());   // ele
    }

    [Fact]
    public void GeoJSON_skips_elevation_when_coordinate_has_none()
    {
        var track = new Track(
            new List<Coordinate> { new(48.85, 2.35), new(48.86, 2.36) },
            Profile.Foot);
        var response = TrackResponse.From(track, SampleStats);
        var json = JsonSerializer.Serialize(response.Geojson);
        using var doc = JsonDocument.Parse(json);
        var coords = doc.RootElement.GetProperty("geometry").GetProperty("coordinates");
        Assert.Equal(2, coords[0].GetArrayLength()); // juste [lon, lat]
    }

    [Fact]
    public void Stats_are_converted_to_DTO_in_seconds()
    {
        var response = TrackResponse.From(SampleTrack, SampleStats);
        Assert.Equal(1234, response.Stats.DistanceMeters);
        Assert.Equal(15, response.Stats.ElevationGainMeters);
        Assert.Equal(1800, response.Stats.EstimatedDurationSeconds); // 30 min
    }

    [Fact]
    public void ProposedDestinationName_is_null_by_default()
    {
        var response = TrackResponse.From(SampleTrack, SampleStats);
        Assert.Null(response.ProposedDestinationName);
    }

    [Fact]
    public void From_ProposedGeneratedTrack_exposes_destination_name()
    {
        var proposed = new ProposedGeneratedTrack(
            SampleTrack,
            SampleStats,
            new TrekFr.Core.Abstractions.ProposedDestination(
                "Vézelay", new Coordinate(47.47, 3.74), Population: 420));
        var response = TrackResponse.From(proposed);
        Assert.Equal("Vézelay", response.ProposedDestinationName);
    }

    [Fact]
    public void From_GeneratedTrack_has_no_proposed_destination()
    {
        var generated = new GeneratedTrack(SampleTrack, SampleStats);
        var response = TrackResponse.From(generated);
        Assert.Null(response.ProposedDestinationName);
    }
}
