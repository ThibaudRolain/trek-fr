using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface IWeatherProvider
{
    Task<IReadOnlyList<WeatherSample>> GetForecastAsync(
        IReadOnlyList<Coordinate> points,
        DateOnly date,
        CancellationToken ct = default);
}

public sealed record WeatherSample(
    Coordinate At,
    DateTimeOffset When,
    double TemperatureCelsius,
    double PrecipitationMm,
    double WindKmh,
    string Summary);
