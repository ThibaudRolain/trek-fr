using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Hosting;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using TrekFr.Infrastructure.OpenRouteService;
using TrekFr.Infrastructure.Weather;

namespace TrekFr.Tests;

/// <summary>
/// WebApplicationFactory qui remplace les providers externes par des stubs déterministes.
/// Chaque test peut ajuster le comportement via les propriétés publiques des fakes.
/// </summary>
public sealed class TestWebApplicationFactory : WebApplicationFactory<Program>
{
    public FakeRoutingProvider Routing { get; } = new();
    public FakeWeatherProvider Weather { get; } = new();
    public FakeDestinationProposer Proposer { get; } = new();

    protected override IHost CreateHost(IHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            services.RemoveAll<IRoutingProvider>();
            services.RemoveAll<IWeatherProvider>();
            services.RemoveAll<IDestinationProposer>();
            services.AddSingleton<IRoutingProvider>(Routing);
            services.AddSingleton<IWeatherProvider>(Weather);
            services.AddSingleton<IDestinationProposer>(Proposer);
        });
        return base.CreateHost(builder);
    }
}

public sealed class FakeRoutingProvider : IRoutingProvider
{
    public Func<Coordinate, double, Profile, int?, Track> RoundTripResponder { get; set; } =
        (start, _, profile, _) => new Track(
            [start, new Coordinate(start.Latitude + 0.01, start.Longitude, 100), start],
            profile);

    public Func<Coordinate, Coordinate, Profile, Track> PointToPointResponder { get; set; } =
        (from, to, profile) => new Track([from, to], profile);

    public Exception? ThrowOnCall { get; set; }

    public Task<Track> GenerateRoundTripAsync(
        Coordinate start, double targetDistanceMeters, Profile profile, int? seed = null, CancellationToken ct = default)
    {
        if (ThrowOnCall is not null) throw ThrowOnCall;
        return Task.FromResult(RoundTripResponder(start, targetDistanceMeters, profile, seed));
    }

    public Task<Track> RouteAsync(Coordinate from, Coordinate to, Profile profile, CancellationToken ct = default)
    {
        if (ThrowOnCall is not null) throw ThrowOnCall;
        return Task.FromResult(PointToPointResponder(from, to, profile));
    }
}

public sealed class FakeWeatherProvider : IWeatherProvider
{
    public Exception? ThrowOnCall { get; set; }

    public Task<IReadOnlyList<WeatherSample>> GetForecastAsync(
        IReadOnlyList<Coordinate> points, DateOnly startDate, int days, CancellationToken ct = default)
    {
        if (ThrowOnCall is not null) throw ThrowOnCall;
        var samples = new List<WeatherSample>(points.Count * days);
        foreach (var p in points)
        {
            for (var d = 0; d < days; d++)
            {
                var date = startDate.AddDays(d);
                samples.Add(new WeatherSample(
                    At: p,
                    When: new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), TimeSpan.Zero),
                    TemperatureMinCelsius: 10,
                    TemperatureMaxCelsius: 20,
                    PrecipitationMm: 0,
                    WindKmh: 10,
                    WmoCode: 0,
                    Summary: "Ciel clair"));
            }
        }
        return Task.FromResult<IReadOnlyList<WeatherSample>>(samples);
    }
}

public sealed class FakeDestinationProposer : IDestinationProposer
{
    public ProposedDestination? Destination { get; set; } =
        new("TestVille", new Coordinate(48.0, 2.5), 500);

    public Task<ProposedDestination?> ProposeAsync(
        Coordinate start, double targetDistanceMeters, Profile profile, int? seed, CancellationToken ct = default) =>
        Task.FromResult(Destination);

    public Task<IReadOnlyList<ProposedDestination>> GetTopCandidatesAsync(
        Coordinate start, double targetDistanceMeters, Profile profile, int topN, CancellationToken ct = default) =>
        Task.FromResult<IReadOnlyList<ProposedDestination>>(
            Destination is null ? [] : [Destination]);
}
