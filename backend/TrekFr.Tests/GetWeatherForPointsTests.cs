using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using TrekFr.Core.UseCases;
using Xunit;

namespace TrekFr.Tests;

public class GetWeatherForPointsTests
{
    private sealed class FakeWeatherProvider : IWeatherProvider
    {
        public IReadOnlyList<Coordinate>? LastPoints { get; private set; }
        public DateOnly? LastStartDate { get; private set; }
        public int? LastDays { get; private set; }

        public Func<IReadOnlyList<Coordinate>, DateOnly, int, IReadOnlyList<WeatherSample>> Responder { get; set; } =
            (_, _, _) => Array.Empty<WeatherSample>();

        public Task<IReadOnlyList<WeatherSample>> GetForecastAsync(
            IReadOnlyList<Coordinate> points, DateOnly startDate, int days, CancellationToken ct = default)
        {
            LastPoints = points;
            LastStartDate = startDate;
            LastDays = days;
            return Task.FromResult(Responder(points, startDate, days));
        }
    }

    private sealed class FakeNearestCommuneFinder(Dictionary<Coordinate, Commune?> map) : INearestCommuneFinder
    {
        public Commune? FindNearest(Coordinate point, double maxDistanceKm = 50) =>
            map.TryGetValue(point, out var c) ? c : null;
    }

    private static WeatherSample Sample(Coordinate at, int dayOffset = 0, int code = 0) => new(
        At: at,
        When: new DateTimeOffset(2026, 6, 1 + dayOffset, 12, 0, 0, TimeSpan.Zero),
        TemperatureMinCelsius: 10,
        TemperatureMaxCelsius: 20,
        PrecipitationMm: 0,
        WindKmh: 10,
        WmoCode: code,
        Summary: "Ciel clair");

    [Fact]
    public async Task Empty_points_returns_empty_and_skips_provider()
    {
        var provider = new FakeWeatherProvider();
        var useCase = new GetWeatherForPoints(provider, new FakeNearestCommuneFinder([]));

        var result = await useCase.ExecuteAsync(Array.Empty<LabeledPoint>(), new DateOnly(2026, 6, 1), 3);

        Assert.Empty(result);
        Assert.Null(provider.LastPoints);
    }

    [Fact]
    public async Task Forwards_coordinates_startDate_and_days_to_provider()
    {
        var provider = new FakeWeatherProvider();
        var useCase = new GetWeatherForPoints(provider, new FakeNearestCommuneFinder([]));
        var points = new[]
        {
            new LabeledPoint("Start", new Coordinate(48.85, 2.35)),
            new LabeledPoint("End", new Coordinate(43.6, 1.44)),
        };

        await useCase.ExecuteAsync(points, new DateOnly(2026, 6, 15), 5);

        Assert.NotNull(provider.LastPoints);
        Assert.Equal(2, provider.LastPoints!.Count);
        Assert.Equal(points[0].Location, provider.LastPoints[0]);
        Assert.Equal(points[1].Location, provider.LastPoints[1]);
        Assert.Equal(new DateOnly(2026, 6, 15), provider.LastStartDate);
        Assert.Equal(5, provider.LastDays);
    }

    [Fact]
    public async Task Merges_samples_with_their_point_by_coordinate_equality()
    {
        var start = new Coordinate(48.85, 2.35);
        var end = new Coordinate(43.6, 1.44);
        var communeStart = new Commune("Paris", start, 2_000_000);
        var communeEnd = new Commune("Toulouse", end, 450_000);

        var provider = new FakeWeatherProvider
        {
            Responder = (_, _, _) => new[]
            {
                Sample(start, dayOffset: 0),
                Sample(start, dayOffset: 1),
                Sample(end, dayOffset: 0),
                Sample(end, dayOffset: 1),
            },
        };
        var finder = new FakeNearestCommuneFinder(new Dictionary<Coordinate, Commune?>
        {
            [start] = communeStart,
            [end] = communeEnd,
        });
        var useCase = new GetWeatherForPoints(provider, finder);

        var result = await useCase.ExecuteAsync(
            new[]
            {
                new LabeledPoint("Départ", start),
                new LabeledPoint("Arrivée", end),
            },
            new DateOnly(2026, 6, 1),
            2);

        Assert.Equal(2, result.Count);
        Assert.Equal("Départ", result[0].Label);
        Assert.Equal("Paris", result[0].CommuneName);
        Assert.Equal(2, result[0].Forecast.Count);
        Assert.All(result[0].Forecast, s => Assert.Equal(start, s.At));

        Assert.Equal("Arrivée", result[1].Label);
        Assert.Equal("Toulouse", result[1].CommuneName);
        Assert.Equal(2, result[1].Forecast.Count);
        Assert.All(result[1].Forecast, s => Assert.Equal(end, s.At));
    }

    [Fact]
    public async Task Point_with_no_nearest_commune_has_null_CommuneName()
    {
        var offshore = new Coordinate(45.0, -30.0);
        var provider = new FakeWeatherProvider
        {
            Responder = (_, _, _) => new[] { Sample(offshore) },
        };
        var useCase = new GetWeatherForPoints(provider, new FakeNearestCommuneFinder([]));

        var result = await useCase.ExecuteAsync(
            new[] { new LabeledPoint("Mer", offshore) },
            new DateOnly(2026, 6, 1),
            1);

        Assert.Single(result);
        Assert.Null(result[0].CommuneName);
        Assert.Single(result[0].Forecast);
    }
}
