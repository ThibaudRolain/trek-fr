using System;
using System.Net;
using System.Net.Http;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TrekFr.Core.Domain;
using TrekFr.Infrastructure.OpenRouteService;
using Xunit;

namespace TrekFr.Tests;

public class OpenRouteServiceRouterTests
{
    private static OpenRouteServiceRouter RouterWith(FakeHttpHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://ors.test") },
            Options.Create(new OpenRouteServiceOptions { ApiKey = "test-key" }));

    private const string TwoPointGeoJson = """
        {
          "features": [
            {
              "geometry": {
                "coordinates": [
                  [2.35, 48.85, 35.0],
                  [2.36, 48.86, 40.0],
                  [2.37, 48.87, 42.0]
                ]
              }
            }
          ]
        }
        """;

    [Fact]
    public async Task RouteAsync_parses_GeoJSON_coordinates_in_lon_lat_elev_order()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(TwoPointGeoJson));
        var router = RouterWith(handler);

        var track = await router.RouteAsync(
            new Coordinate(48.85, 2.35),
            new Coordinate(48.87, 2.37),
            Profile.Foot);

        Assert.Equal(3, track.Points.Count);
        // GeoJSON = [lon, lat, elev] → Coordinate(lat, lon, elev).
        Assert.Equal(48.85, track.Points[0].Latitude);
        Assert.Equal(2.35, track.Points[0].Longitude);
        Assert.Equal(35.0, track.Points[0].Elevation);
        Assert.Equal(Profile.Foot, track.Profile);
    }

    [Fact]
    public async Task RouteAsync_sends_api_key_in_Authorization_header()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(TwoPointGeoJson));
        var router = RouterWith(handler);

        await router.RouteAsync(new Coordinate(48.85, 2.35), new Coordinate(48.87, 2.37), Profile.Foot);

        Assert.NotNull(handler.LastRequest);
        Assert.True(handler.LastRequest!.Headers.TryGetValues("Authorization", out var values));
        Assert.Contains("test-key", values!);
    }

    [Fact]
    public async Task RouteAsync_uses_cycling_mountain_profile_in_url_for_Mtb()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(TwoPointGeoJson));
        var router = RouterWith(handler);

        await router.RouteAsync(new Coordinate(48.85, 2.35), new Coordinate(48.87, 2.37), Profile.Mtb);

        Assert.Contains("/v2/directions/cycling-mountain/geojson", handler.LastRequest!.RequestUri!.ToString());
    }

    [Fact]
    public async Task RouteAsync_throws_OpenRouteServiceException_on_4xx()
    {
        var handler = new FakeHttpHandler(_ =>
            FakeHttpHandler.Text("bad request", HttpStatusCode.BadRequest));
        var router = RouterWith(handler);

        var ex = await Assert.ThrowsAsync<OpenRouteServiceException>(() =>
            router.RouteAsync(new Coordinate(48.85, 2.35), new Coordinate(48.87, 2.37), Profile.Foot));
        Assert.Contains("400", ex.Message);
    }

    [Fact]
    public async Task RouteAsync_throws_on_5xx()
    {
        var handler = new FakeHttpHandler(_ =>
            FakeHttpHandler.Text("boom", HttpStatusCode.InternalServerError));
        var router = RouterWith(handler);

        await Assert.ThrowsAsync<OpenRouteServiceException>(() =>
            router.RouteAsync(new Coordinate(48.85, 2.35), new Coordinate(48.87, 2.37), Profile.Foot));
    }

    [Fact]
    public async Task RouteAsync_throws_when_response_has_no_feature()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("""{"features":[]}"""));
        var router = RouterWith(handler);

        await Assert.ThrowsAsync<OpenRouteServiceException>(() =>
            router.RouteAsync(new Coordinate(48.85, 2.35), new Coordinate(48.87, 2.37), Profile.Foot));
    }

    [Fact]
    public async Task GenerateRoundTripAsync_posts_round_trip_options_with_seed()
    {
        string? capturedBody = null;
        var handler = new FakeHttpHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return FakeHttpHandler.Json(TwoPointGeoJson);
        });
        var router = RouterWith(handler);

        var track = await router.GenerateRoundTripAsync(
            new Coordinate(48.85, 2.35), 15_000d, Profile.Foot, seed: 42);

        Assert.Equal(3, track.Points.Count);
        Assert.NotNull(capturedBody);
        Assert.Contains("\"round_trip\"", capturedBody);
        Assert.Contains("\"seed\":42", capturedBody);
        Assert.Contains("\"length\":15000", capturedBody);
    }

    [Fact]
    public async Task GenerateRoundTripAsync_throws_on_server_error()
    {
        var handler = new FakeHttpHandler(_ =>
            FakeHttpHandler.Text("rate limit", HttpStatusCode.TooManyRequests));
        var router = RouterWith(handler);

        await Assert.ThrowsAsync<OpenRouteServiceException>(() =>
            router.GenerateRoundTripAsync(new Coordinate(48.85, 2.35), 15_000d, Profile.Foot, seed: 1));
    }

    [Fact]
    public async Task Coordinates_without_elevation_yield_null_elevation()
    {
        const string body = """
            {"features":[{"geometry":{"coordinates":[[2.35,48.85],[2.36,48.86]]}}]}
            """;
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(body));
        var router = RouterWith(handler);

        var track = await router.RouteAsync(
            new Coordinate(48.85, 2.35), new Coordinate(48.86, 2.36), Profile.Foot);

        Assert.Null(track.Points[0].Elevation);
    }
}
