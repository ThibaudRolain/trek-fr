using System.Collections.Generic;
using System.Text.Json;
using System.Text.Json.Serialization;
using TrekFr.Core.Domain;
using TrekFr.Infrastructure.OpenRouteService;
using Xunit;

namespace TrekFr.Tests;

/// <summary>
/// Vérifie le parsing des extras ORS (waytypes + surface) depuis des payloads réels.
/// </summary>
public class WaytypesSurfaceParsingTests
{
    // Payload réel ORS (foot-hiking) — extras.surface et extras.waytypes avec summary
    private const string OrsWithExtras = """
        {
          "features": [
            {
              "geometry": {
                "coordinates": [
                  [2.35, 48.85, 35.0],
                  [2.36, 48.86, 40.0]
                ]
              },
              "properties": {
                "extras": {
                  "waytype": {
                    "values": [[0,5,4],[5,7,4]],
                    "summary": [
                      {"value":4,"amount":62.5,"distance":1250.0},
                      {"value":3,"amount":37.5,"distance":750.0}
                    ]
                  },
                  "surface": {
                    "values": [[0,5,1],[5,7,0]],
                    "summary": [
                      {"value":1,"amount":62.5,"distance":1250.0},
                      {"value":0,"amount":37.5,"distance":750.0}
                    ]
                  }
                }
              }
            }
          ]
        }
        """;

    // Payload sans extras (ORS ne retourne pas toujours le bloc selon le profil/zone)
    private const string OrsWithoutExtras = """
        {
          "features": [
            {
              "geometry": {
                "coordinates": [[2.35, 48.85, 35.0]]
              },
              "properties": {}
            }
          ]
        }
        """;

    // Payload avec extras mais summary vide
    private const string OrsWithEmptyExtras = """
        {
          "features": [
            {
              "geometry": { "coordinates": [[2.35, 48.85]] },
              "properties": {
                "extras": {
                  "waytype": { "values": [], "summary": [] },
                  "surface": { "values": [], "summary": [] }
                }
              }
            }
          ]
        }
        """;

    private static OrsProperties? DeserializeProperties(string json)
    {
        var opts = new JsonSerializerOptions(JsonSerializerDefaults.Web)
        {
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
        using var doc = JsonDocument.Parse(json);
        var feature = doc.RootElement
            .GetProperty("features")[0];
        if (!feature.TryGetProperty("properties", out var props)) return null;
        return props.Deserialize<OrsProperties>(opts);
    }

    [Fact]
    public void ParseExtras_returns_waytypes_and_surface_entries()
    {
        var properties = DeserializeProperties(OrsWithExtras);
        var extras = OpenRouteServiceRouter.ParseExtras(properties);

        Assert.NotNull(extras);
        Assert.NotNull(extras.WayTypes);
        Assert.NotNull(extras.Surface);
        Assert.Equal(2, extras.WayTypes!.Count);
        Assert.Equal(2, extras.Surface!.Count);
    }

    [Fact]
    public void ParseExtras_maps_waytype_fields_correctly()
    {
        var properties = DeserializeProperties(OrsWithExtras);
        var extras = OpenRouteServiceRouter.ParseExtras(properties);

        var first = extras!.WayTypes![0];
        Assert.Equal(4, first.TypeId);
        Assert.Equal(62.5, first.Amount);
        Assert.Equal(1250.0, first.Distance);
    }

    [Fact]
    public void ParseExtras_maps_surface_fields_correctly()
    {
        var properties = DeserializeProperties(OrsWithExtras);
        var extras = OpenRouteServiceRouter.ParseExtras(properties);

        var second = extras!.Surface![1];
        Assert.Equal(0, second.TypeId);
        Assert.Equal(37.5, second.Amount);
        Assert.Equal(750.0, second.Distance);
    }

    [Fact]
    public void ParseExtras_returns_null_when_properties_has_no_extras()
    {
        var properties = DeserializeProperties(OrsWithoutExtras);
        var extras = OpenRouteServiceRouter.ParseExtras(properties);

        Assert.Null(extras);
    }

    [Fact]
    public void ParseExtras_returns_null_when_properties_is_null()
    {
        var extras = OpenRouteServiceRouter.ParseExtras(null);
        Assert.Null(extras);
    }

    [Fact]
    public void ParseExtras_returns_null_when_summaries_are_empty()
    {
        var properties = DeserializeProperties(OrsWithEmptyExtras);
        var extras = OpenRouteServiceRouter.ParseExtras(properties);

        Assert.Null(extras);
    }

    [Fact]
    public void CompositionDto_From_returns_null_when_stats_has_no_surface_or_waytypes()
    {
        var stats = new TrackStats(10_000, 500, 200, System.TimeSpan.FromHours(2));
        var dto = TrekFr.Api.Tracks.CompositionDto.From(stats);
        Assert.Null(dto);
    }

    [Fact]
    public void CompositionDto_From_maps_entries_when_stats_has_data()
    {
        var stats = new TrackStats(
            10_000, 500, 200, System.TimeSpan.FromHours(2),
            Surface: [new SurfaceEntry(1, 62.5, 1250.0), new SurfaceEntry(0, 37.5, 750.0)],
            WayTypes: [new WayTypeEntry(4, 100.0, 2000.0)]);

        var dto = TrekFr.Api.Tracks.CompositionDto.From(stats);

        Assert.NotNull(dto);
        Assert.Equal(2, dto!.Surface.Count);
        Assert.Single(dto.WayTypes);
        Assert.Equal(1, dto.Surface[0].TypeId);
        Assert.Equal(62.5, dto.Surface[0].Amount);
        Assert.Equal(4, dto.WayTypes[0].TypeId);
    }

    [Fact]
    public async Task RouteAsync_parses_extras_from_real_ors_payload()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(OrsWithExtras));
        var router = new OpenRouteServiceRouter(
            new System.Net.Http.HttpClient(handler) { BaseAddress = new System.Uri("https://ors.test") },
            Microsoft.Extensions.Options.Options.Create(
                new OpenRouteServiceOptions { ApiKey = "test" }));

        var (track, extras) = await router.RouteAsync(
            new Coordinate(48.85, 2.35),
            new Coordinate(48.86, 2.36),
            Profile.Foot);

        Assert.Equal(2, track.Points.Count);
        Assert.NotNull(extras);
        Assert.Equal(2, extras!.WayTypes!.Count);
        Assert.Equal(2, extras.Surface!.Count);
    }
}
