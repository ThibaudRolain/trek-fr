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
    // Seuil en dessous duquel un code "pluie" (non-orage, non-neige) est rendu comme "Couvert"
    // pour éviter d'annoncer "pluie légère" à 0.3 mm/jour. Orages et neige toujours rendus.
    private const double RainThresholdMm = 1d;

    public static WeatherDayDto From(WeatherSample s)
    {
        var wmoCode = s.WmoCode;
        var summary = s.Summary;
        if (IsLightPrecipRainCode(wmoCode) && s.PrecipitationMm < RainThresholdMm)
        {
            wmoCode = 3;
            summary = "Couvert";
        }
        return new WeatherDayDto(
            DateOnly.FromDateTime(s.When.Date),
            Math.Round(s.TemperatureMinCelsius, 1),
            Math.Round(s.TemperatureMaxCelsius, 1),
            Math.Round(s.PrecipitationMm, 1),
            Math.Round(s.WindKmh, 1),
            wmoCode,
            summary);
    }

    private static bool IsLightPrecipRainCode(int code) =>
        code is (>= 51 and <= 67) or (>= 80 and <= 82);
}
