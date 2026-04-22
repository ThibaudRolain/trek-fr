using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class GetWeatherForPoints(
    IWeatherProvider weather,
    INearestCommuneFinder nearest)
{
    public async Task<IReadOnlyList<PointForecast>> ExecuteAsync(
        IReadOnlyList<LabeledPoint> points,
        DateOnly startDate,
        int days,
        CancellationToken ct = default)
    {
        if (points.Count == 0) return Array.Empty<PointForecast>();

        var coords = points.Select(p => p.Location).ToList();
        var samples = await weather.GetForecastAsync(coords, startDate, days, ct);

        var result = new List<PointForecast>(points.Count);
        foreach (var p in points)
        {
            var commune = nearest.FindNearest(p.Location);
            var pSamples = samples.Where(s => s.At.Equals(p.Location)).ToList();
            result.Add(new PointForecast(p.Label, commune?.Name, p.Location, pSamples));
        }
        return result;
    }
}

public sealed record LabeledPoint(string Label, Coordinate Location);

public sealed record PointForecast(
    string Label,
    string? CommuneName,
    Coordinate Location,
    IReadOnlyList<WeatherSample> Forecast);
