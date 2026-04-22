using System;
using System.Collections.Generic;
using System.Linq;
using TrekFr.Core.Abstractions;
using TrekFr.Core.UseCases;

namespace TrekFr.Api.Tracks;

public sealed record TrackWeatherResponse(
    string Label,
    string? CommuneName,
    double Latitude,
    double Longitude,
    IReadOnlyList<WeatherDayDto> Forecast)
{
    public static TrackWeatherResponse From(PointForecast p) => new(
        p.Label,
        p.CommuneName,
        p.Location.Latitude,
        p.Location.Longitude,
        p.Forecast.Select(WeatherDayDto.From).ToList());
}

public sealed record WeatherDayDto(
    DateOnly Date,
    double TempMinC,
    double TempMaxC,
    double PrecipitationMm,
    double WindKmh,
    int WmoCode,
    string Summary)
{
    public static WeatherDayDto From(WeatherSample s) => new(
        DateOnly.FromDateTime(s.When.Date),
        Math.Round(s.TemperatureMinCelsius, 1),
        Math.Round(s.TemperatureMaxCelsius, 1),
        Math.Round(s.PrecipitationMm, 1),
        Math.Round(s.WindKmh, 1),
        s.WmoCode,
        s.Summary);
}
