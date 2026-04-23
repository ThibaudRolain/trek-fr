using System;
using System.Net;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using TrekFr.Infrastructure.OpenRouteService;
using TrekFr.Infrastructure.Weather;
using Xunit;

namespace TrekFr.Tests;

public class TracksEndpointsTests : IClassFixture<TracksEndpointsTests.Fixture>
{
    public sealed class Fixture : IDisposable
    {
        public TestWebApplicationFactory Factory { get; } = new();
        public HttpClient Client { get; }
        public Fixture() { Client = Factory.CreateClient(); }
        public void Dispose() { Client.Dispose(); Factory.Dispose(); }
    }

    private readonly Fixture _fx;
    public TracksEndpointsTests(Fixture fx) { _fx = fx; }

    // ---- /tracks/generate — validation ----

    [Fact]
    public async Task Generate_400_on_invalid_start_coords()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
        {
            latitude = 95.0, longitude = 2.35, distanceKm = 10, mode = "roundTrip",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_400_on_distance_out_of_range_roundTrip()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
        {
            latitude = 48.85, longitude = 2.35, distanceKm = 150, mode = "roundTrip",
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_400_on_invalid_end_coords_aToB()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
        {
            latitude = 48.85, longitude = 2.35, distanceKm = 50, mode = "aToB",
            endLatitude = 95.0, endLongitude = 2.5,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    // ---- /tracks/generate — happy paths ----

    [Fact]
    public async Task Generate_roundTrip_returns_track_with_stats()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
        {
            latitude = 48.85, longitude = 2.35, distanceKm = 10, mode = "roundTrip", seed = 1,
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("foot", doc.RootElement.GetProperty("profile").GetString());
        Assert.True(doc.RootElement.GetProperty("stats").GetProperty("distanceMeters").GetDouble() > 0);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("proposedDestinationName").ValueKind);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("variants").ValueKind);
    }

    [Fact]
    public async Task Generate_roundTrip_without_seed_returns_variants_array_of_three()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
        {
            latitude = 48.85, longitude = 2.35, distanceKm = 10, mode = "roundTrip",
            // seed absent → declenche GenerateVariantsAsync
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var variants = doc.RootElement.GetProperty("variants");
        Assert.Equal(JsonValueKind.Array, variants.ValueKind);
        Assert.Equal(3, variants.GetArrayLength());
        var first = variants[0];
        Assert.Equal(JsonValueKind.Object, first.GetProperty("geojson").ValueKind);
        Assert.True(first.GetProperty("stats").GetProperty("distanceMeters").GetDouble() > 0);
    }

    [Fact]
    public async Task Generate_aToB_explicit_end_routes_between_points()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
        {
            latitude = 48.85, longitude = 2.35, distanceKm = 50, mode = "aToB",
            endLatitude = 49.0, endLongitude = 2.5,
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        // bbox = [minLon, minLat, maxLon, maxLat] — doit contenir start ET end.
        var bbox = doc.RootElement.GetProperty("bbox");
        Assert.Equal(48.85, bbox[1].GetDouble(), precision: 4);
        Assert.Equal(49.0, bbox[3].GetDouble(), precision: 4);
        Assert.Equal(JsonValueKind.Null, doc.RootElement.GetProperty("proposedDestinationName").ValueKind);
    }

    [Fact]
    public async Task Generate_aToB_without_end_uses_proposer_and_exposes_destination_name()
    {
        _fx.Factory.Proposer.Destination = new TrekFr.Core.Abstractions.ProposedDestination(
            "Sarlat-la-Canéda", new TrekFr.Core.Domain.Coordinate(44.89, 1.21), 9000);

        var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
        {
            latitude = 48.85, longitude = 2.35, distanceKm = 50, mode = "aToB",
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Sarlat-la-Canéda", doc.RootElement.GetProperty("proposedDestinationName").GetString());
    }

    [Fact]
    public async Task Generate_aToB_without_end_and_no_candidate_returns_400()
    {
        _fx.Factory.Proposer.Destination = null;
        try
        {
            var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
            {
                latitude = 48.85, longitude = 2.35, distanceKm = 30, mode = "aToB",
            });
            Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        }
        finally
        {
            // Restore pour les autres tests qui partagent la fixture.
            _fx.Factory.Proposer.Destination = new TrekFr.Core.Abstractions.ProposedDestination(
                "TestVille", new TrekFr.Core.Domain.Coordinate(48.0, 2.5), 500);
        }
    }

    [Fact]
    public async Task Generate_returns_502_when_routing_provider_throws()
    {
        _fx.Factory.Routing.ThrowOnCall = new OpenRouteServiceException("ORS 500");
        try
        {
            var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
            {
                latitude = 48.85, longitude = 2.35, distanceKm = 10, mode = "roundTrip",
            });
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            _fx.Factory.Routing.ThrowOnCall = null;
        }
    }

    // ---- /tracks/weather ----

    [Fact]
    public async Task Weather_400_when_no_points()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/weather", new
        {
            points = Array.Empty<object>(), days = 3,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Weather_400_when_too_many_points()
    {
        var points = new object[11];
        for (int i = 0; i < points.Length; i++)
            points[i] = new { label = $"p{i}", latitude = 48.0 + i * 0.01, longitude = 2.0 };
        var response = await _fx.Client.PostAsJsonAsync("/tracks/weather", new { points, days = 3 });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Weather_400_when_days_out_of_range()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/weather", new
        {
            points = new[] { new { label = "A", latitude = 48.85, longitude = 2.35 } },
            days = 20,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Weather_400_on_invalid_coordinates()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/weather", new
        {
            points = new[] { new { label = "A", latitude = 95.0, longitude = 2.35 } },
            days = 3,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Weather_happy_path_returns_forecast_per_point()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/weather", new
        {
            points = new[]
            {
                new { label = "Départ", latitude = 48.85, longitude = 2.35 },
                new { label = "Arrivée", latitude = 43.60, longitude = 1.44 },
            },
            days = 3,
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal(2, doc.RootElement.GetArrayLength());
        Assert.Equal("Départ", doc.RootElement[0].GetProperty("label").GetString());
        Assert.Equal(3, doc.RootElement[0].GetProperty("forecast").GetArrayLength());
    }

    [Fact]
    public async Task Weather_502_when_provider_throws_OpenMeteoException()
    {
        _fx.Factory.Weather.ThrowOnCall = new OpenMeteoException("provider down");
        try
        {
            var response = await _fx.Client.PostAsJsonAsync("/tracks/weather", new
            {
                points = new[] { new { label = "A", latitude = 48.85, longitude = 2.35 } },
                days = 3,
            });
            Assert.Equal(HttpStatusCode.BadGateway, response.StatusCode);
        }
        finally
        {
            _fx.Factory.Weather.ThrowOnCall = null;
        }
    }

    // ---- /tracks/import ----

    [Fact]
    public async Task Import_400_when_gpx_missing()
    {
        using var form = new MultipartFormDataContent();
        var response = await _fx.Client.PostAsync("/tracks/import", form);
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Import_parses_uploaded_gpx_and_returns_track()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><name>Upload</name><trkseg>
                <trkpt lat="48.85" lon="2.35"><ele>35</ele></trkpt>
                <trkpt lat="48.86" lon="2.36"><ele>40</ele></trkpt>
              </trkseg></trk>
            </gpx>
            """;
        using var form = new MultipartFormDataContent();
        var fileContent = new ByteArrayContent(Encoding.UTF8.GetBytes(xml));
        fileContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/gpx+xml");
        form.Add(fileContent, "gpx", "sample.gpx");

        var response = await _fx.Client.PostAsync("/tracks/import", form);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Upload", doc.RootElement.GetProperty("name").GetString());
    }

    // ---- /tracks/generate — splitStages ----

    [Fact]
    public async Task Generate_splitStages_400_when_stageDistance_missing()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
        {
            latitude = 48.85, longitude = 2.35, distanceKm = 10, mode = "roundTrip",
            splitStages = true, stageElevationGain = 500,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_splitStages_400_when_stageElevation_out_of_range()
    {
        var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
        {
            latitude = 48.85, longitude = 2.35, distanceKm = 10, mode = "roundTrip",
            splitStages = true, stageDistanceKm = 20, stageElevationGain = 50_000,
        });
        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Generate_splitStages_returns_stages_array_when_route_long_enough()
    {
        // Route de 2 points (~17 km à latitude 48) sous la limite 25 km/jour → 1 stage "Arrivée".
        _fx.Factory.Routing.PointToPointResponder = (from, to, profile) =>
            new TrekFr.Core.Domain.Track([from, to], profile);

        var response = await _fx.Client.PostAsJsonAsync("/tracks/generate", new
        {
            latitude = 48.85, longitude = 2.35, distanceKm = 20, mode = "aToB",
            endLatitude = 48.85, endLongitude = 2.50,
            splitStages = true, stageDistanceKm = 25, stageElevationGain = 1_000,
        });
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var stages = doc.RootElement.GetProperty("stages");
        Assert.Equal(JsonValueKind.Array, stages.ValueKind);
        Assert.Equal(1, stages.GetArrayLength());
        Assert.Equal("arrival", stages[0].GetProperty("endSleepSpot").GetProperty("kind").GetString());
    }

    // ---- /health ----

    [Fact]
    public async Task Health_returns_ok()
    {
        var response = await _fx.Client.GetAsync("/health");
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("ok", doc.RootElement.GetProperty("status").GetString());
    }
}
