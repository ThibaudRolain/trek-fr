using System;
using System.Net;
using System.Net.Http;
using Microsoft.Extensions.Options;
using TrekFr.Core.Domain;
using TrekFr.Infrastructure.Weather;
using Xunit;

namespace TrekFr.Tests;

public class OpenMeteoWeatherProviderTests
{
    private static OpenMeteoWeatherProvider ProviderWith(FakeHttpHandler handler) =>
        new(
            new HttpClient(handler) { BaseAddress = new Uri("https://api.open-meteo.test") },
            Options.Create(new OpenMeteoOptions()));

    [Fact]
    public async Task Empty_points_returns_empty_without_calling_api()
    {
        var handler = new FakeHttpHandler(_ => throw new InvalidOperationException("should not call"));
        var provider = ProviderWith(handler);

        var result = await provider.GetForecastAsync(Array.Empty<Coordinate>(), new DateOnly(2026, 6, 1), 3);

        Assert.Empty(result);
        Assert.Equal(0, handler.CallCount);
    }

    [Fact]
    public async Task Days_out_of_range_throws()
    {
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json("{}"));
        var provider = ProviderWith(handler);
        var pt = new[] { new Coordinate(48.85, 2.35) };

        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            provider.GetForecastAsync(pt, new DateOnly(2026, 6, 1), 0));
        await Assert.ThrowsAsync<ArgumentOutOfRangeException>(() =>
            provider.GetForecastAsync(pt, new DateOnly(2026, 6, 1), 17));
    }

    [Fact]
    public async Task Single_point_parses_daily_samples_and_summary()
    {
        const string body = """
            {
              "utc_offset_seconds": 7200,
              "daily": {
                "time": ["2026-06-01", "2026-06-02"],
                "temperature_2m_max": [22.5, 25.0],
                "temperature_2m_min": [12.3, 14.1],
                "precipitation_sum": [0.0, 2.4],
                "wind_speed_10m_max": [15.0, 18.5],
                "weather_code": [0, 61]
              }
            }
            """;
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(body));
        var provider = ProviderWith(handler);
        var pt = new[] { new Coordinate(48.85, 2.35) };

        var result = await provider.GetForecastAsync(pt, new DateOnly(2026, 6, 1), 2);

        Assert.Equal(2, result.Count);
        Assert.Equal(22.5, result[0].TemperatureMaxCelsius);
        Assert.Equal(12.3, result[0].TemperatureMinCelsius);
        Assert.Equal(0, result[0].WmoCode);
        Assert.Equal("Ciel clair", result[0].Summary);
        Assert.Equal(61, result[1].WmoCode);
        Assert.Equal("Pluie légère", result[1].Summary);
        Assert.Equal(pt[0], result[0].At);
    }

    [Fact]
    public async Task Multi_point_response_is_an_array_and_parses_each_point()
    {
        // Open-Meteo renvoie un tableau quand N >= 2 coordonnées.
        const string body = """
            [
              {
                "utc_offset_seconds": 7200,
                "daily": {
                  "time": ["2026-06-01"],
                  "temperature_2m_max": [20.0],
                  "temperature_2m_min": [10.0],
                  "precipitation_sum": [0.0],
                  "wind_speed_10m_max": [10.0],
                  "weather_code": [0]
                }
              },
              {
                "utc_offset_seconds": 7200,
                "daily": {
                  "time": ["2026-06-01"],
                  "temperature_2m_max": [30.0],
                  "temperature_2m_min": [20.0],
                  "precipitation_sum": [5.0],
                  "wind_speed_10m_max": [30.0],
                  "weather_code": [63]
                }
              }
            ]
            """;
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(body));
        var provider = ProviderWith(handler);
        var pts = new[] { new Coordinate(48.85, 2.35), new Coordinate(43.6, 1.44) };

        var result = await provider.GetForecastAsync(pts, new DateOnly(2026, 6, 1), 1);

        Assert.Equal(2, result.Count);
        Assert.Equal(pts[0], result[0].At);
        Assert.Equal(pts[1], result[1].At);
        Assert.Equal(20.0, result[0].TemperatureMaxCelsius);
        Assert.Equal(30.0, result[1].TemperatureMaxCelsius);
        Assert.Equal("Pluie", result[1].Summary);
    }

    [Fact]
    public async Task Mismatch_between_response_count_and_points_throws()
    {
        // 2 points mais 1 seule réponse → Open-Meteo a mal répondu, on le signale.
        const string body = """
            {
              "utc_offset_seconds": 0,
              "daily": {
                "time": ["2026-06-01"],
                "temperature_2m_max": [20.0],
                "temperature_2m_min": [10.0],
                "precipitation_sum": [0.0],
                "wind_speed_10m_max": [10.0],
                "weather_code": [0]
              }
            }
            """;
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(body));
        var provider = ProviderWith(handler);
        var pts = new[] { new Coordinate(48.85, 2.35), new Coordinate(43.6, 1.44) };

        await Assert.ThrowsAsync<OpenMeteoException>(() =>
            provider.GetForecastAsync(pts, new DateOnly(2026, 6, 1), 1));
    }

    [Fact]
    public async Task Http_error_throws_OpenMeteoException()
    {
        var handler = new FakeHttpHandler(_ =>
            FakeHttpHandler.Text("rate limited", HttpStatusCode.TooManyRequests));
        var provider = ProviderWith(handler);
        var pt = new[] { new Coordinate(48.85, 2.35) };

        var ex = await Assert.ThrowsAsync<OpenMeteoException>(() =>
            provider.GetForecastAsync(pt, new DateOnly(2026, 6, 1), 3));
        Assert.Contains("429", ex.Message);
    }

    [Fact]
    public async Task Days_with_null_fields_are_skipped()
    {
        const string body = """
            {
              "utc_offset_seconds": 0,
              "daily": {
                "time": ["2026-06-01", "2026-06-02"],
                "temperature_2m_max": [20.0, null],
                "temperature_2m_min": [10.0, 8.0],
                "precipitation_sum": [0.0, 0.0],
                "wind_speed_10m_max": [10.0, 10.0],
                "weather_code": [0, 0]
              }
            }
            """;
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(body));
        var provider = ProviderWith(handler);
        var pt = new[] { new Coordinate(48.85, 2.35) };

        var result = await provider.GetForecastAsync(pt, new DateOnly(2026, 6, 1), 2);

        Assert.Single(result);
    }

    [Fact]
    public async Task Url_includes_lat_lon_and_date_range_params()
    {
        const string body = """
            {
              "utc_offset_seconds": 0,
              "daily": {
                "time": ["2026-06-01"],
                "temperature_2m_max": [20.0],
                "temperature_2m_min": [10.0],
                "precipitation_sum": [0.0],
                "wind_speed_10m_max": [10.0],
                "weather_code": [0]
              }
            }
            """;
        var handler = new FakeHttpHandler(_ => FakeHttpHandler.Json(body));
        var provider = ProviderWith(handler);

        await provider.GetForecastAsync(
            new[] { new Coordinate(48.8566, 2.3522) },
            new DateOnly(2026, 6, 1),
            1);

        var uri = handler.LastRequest!.RequestUri!.ToString();
        Assert.Contains("latitude=48.8566", uri);
        Assert.Contains("longitude=2.3522", uri);
        Assert.Contains("start_date=2026-06-01", uri);
        Assert.Contains("end_date=2026-06-01", uri);
        Assert.Contains("timezone=auto", uri);
    }
}
