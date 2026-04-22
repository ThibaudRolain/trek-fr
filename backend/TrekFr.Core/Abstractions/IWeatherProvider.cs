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
        DateOnly startDate,
        int days,
        CancellationToken ct = default);
}

public sealed record WeatherSample(
    Coordinate At,
    DateTimeOffset When,
    double TemperatureMinCelsius,
    double TemperatureMaxCelsius,
    double PrecipitationMm,
    double WindKmh,
    int WmoCode,
    string Summary);
