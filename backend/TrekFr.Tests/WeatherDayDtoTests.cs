using System;
using TrekFr.Api.Tracks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using Xunit;

namespace TrekFr.Tests;

public class WeatherDayDtoTests
{
    private static WeatherSample Sample(int wmoCode, double precipMm, string summary = "—") => new(
        At: new Coordinate(48.85, 2.35),
        When: new DateTimeOffset(2026, 6, 1, 12, 0, 0, TimeSpan.Zero),
        TemperatureMinCelsius: 10.13,
        TemperatureMaxCelsius: 20.27,
        PrecipitationMm: precipMm,
        WindKmh: 12.3456,
        WmoCode: wmoCode,
        Summary: summary);

    // ---- Seuil 1 mm : les codes pluie légère deviennent "Couvert" si < 1 mm ----

    [Theory]
    [InlineData(51)]   // bruine légère
    [InlineData(55)]   // bruine dense
    [InlineData(61)]   // pluie légère
    [InlineData(63)]   // pluie
    [InlineData(65)]   // pluie forte
    [InlineData(67)]   // pluie verglaçante
    [InlineData(80)]   // averses légères
    [InlineData(81)]   // averses
    [InlineData(82)]   // averses violentes
    public void Rain_codes_under_1mm_are_downgraded_to_overcast(int rainCode)
    {
        var dto = WeatherDayDto.From(Sample(rainCode, precipMm: 0.3, summary: "Pluie légère"));
        Assert.Equal(3, dto.WmoCode);
        Assert.Equal("Couvert", dto.Summary);
    }

    [Theory]
    [InlineData(61, 1.0)]
    [InlineData(61, 1.5)]
    [InlineData(63, 5.0)]
    [InlineData(82, 20.0)]
    public void Rain_codes_at_or_above_1mm_are_kept(int rainCode, double precip)
    {
        var dto = WeatherDayDto.From(Sample(rainCode, precipMm: precip, summary: "Pluie"));
        Assert.Equal(rainCode, dto.WmoCode);
        Assert.Equal("Pluie", dto.Summary);
    }

    // ---- Orages et neige : jamais downgradés ----

    [Theory]
    [InlineData(95)]   // orage
    [InlineData(96)]   // orage avec grêle
    [InlineData(99)]   // orage avec grêle
    [InlineData(71)]   // neige légère
    [InlineData(73)]   // neige
    [InlineData(75)]   // neige forte
    [InlineData(77)]   // grains de neige
    [InlineData(85)]   // averses de neige légères
    [InlineData(86)]   // averses de neige fortes
    public void Storm_and_snow_codes_are_never_downgraded_even_with_zero_precip(int code)
    {
        var dto = WeatherDayDto.From(Sample(code, precipMm: 0, summary: "X"));
        Assert.Equal(code, dto.WmoCode);
    }

    // ---- Codes non-pluie intacts ----

    [Theory]
    [InlineData(0)]   // ciel clair
    [InlineData(3)]   // couvert
    [InlineData(45)]  // brouillard
    public void Non_rain_codes_are_untouched(int code)
    {
        var dto = WeatherDayDto.From(Sample(code, precipMm: 0, summary: "X"));
        Assert.Equal(code, dto.WmoCode);
    }

    // ---- Arrondis à 0.1 ----

    [Fact]
    public void Numbers_are_rounded_to_one_decimal()
    {
        var dto = WeatherDayDto.From(Sample(0, precipMm: 0.456, summary: "X"));
        Assert.Equal(10.1, dto.TempMinC);
        Assert.Equal(20.3, dto.TempMaxC);
        Assert.Equal(0.5, dto.PrecipitationMm);
        Assert.Equal(12.3, dto.WindKmh);
    }

    [Fact]
    public void Date_is_taken_from_When_local_midday()
    {
        var sample = Sample(0, 0) with { When = new DateTimeOffset(2026, 7, 15, 12, 0, 0, TimeSpan.FromHours(2)) };
        var dto = WeatherDayDto.From(sample);
        Assert.Equal(new DateOnly(2026, 7, 15), dto.Date);
    }
}
