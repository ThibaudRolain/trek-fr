using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Infrastructure.Weather;

/// <summary>
/// Implémente IWeatherProvider via l'API publique gratuite Open-Meteo
/// (pas d'auth, limite ~10k req/jour). Supporte le batch multi-points en un seul appel.
/// </summary>
public sealed class OpenMeteoWeatherProvider(
    HttpClient http,
    IOptions<OpenMeteoOptions> options) : IWeatherProvider
{
    private readonly OpenMeteoOptions _options = options.Value;

    public async Task<IReadOnlyList<WeatherSample>> GetForecastAsync(
        IReadOnlyList<Coordinate> points,
        DateOnly startDate,
        int days,
        CancellationToken ct = default)
    {
        if (points.Count == 0) return Array.Empty<WeatherSample>();
        if (days is < 1 or > 16) throw new ArgumentOutOfRangeException(nameof(days), "days must be 1..16");

        var endDate = startDate.AddDays(days - 1);
        var lats = string.Join(",", points.Select(p => p.Latitude.ToString("0.####", CultureInfo.InvariantCulture)));
        var lons = string.Join(",", points.Select(p => p.Longitude.ToString("0.####", CultureInfo.InvariantCulture)));

        var url = $"/v1/forecast?latitude={lats}&longitude={lons}"
            + $"&start_date={startDate:yyyy-MM-dd}&end_date={endDate:yyyy-MM-dd}"
            + "&daily=temperature_2m_max,temperature_2m_min,precipitation_sum,wind_speed_10m_max,weather_code"
            + "&timezone=auto";

        using var resp = await http.GetAsync(url, ct);
        if (!resp.IsSuccessStatusCode)
        {
            var body = await resp.Content.ReadAsStringAsync(ct);
            throw new OpenMeteoException(
                $"Open-Meteo failed: {(int)resp.StatusCode} {resp.ReasonPhrase}. Body: {body}");
        }

        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);
        var root = doc.RootElement;

        // Open-Meteo renvoie un objet pour 1 point, un tableau pour N>=2.
        var pointResponses = root.ValueKind == JsonValueKind.Array
            ? root.EnumerateArray().ToList()
            : new List<JsonElement> { root };

        if (pointResponses.Count != points.Count)
        {
            throw new OpenMeteoException(
                $"Open-Meteo returned {pointResponses.Count} responses for {points.Count} points.");
        }

        var samples = new List<WeatherSample>(points.Count * days);
        for (var i = 0; i < points.Count; i++)
        {
            ParsePointInto(samples, points[i], pointResponses[i]);
        }
        return samples;
    }

    private static void ParsePointInto(List<WeatherSample> acc, Coordinate at, JsonElement response)
    {
        if (!response.TryGetProperty("daily", out var daily)) return;

        var times = daily.GetProperty("time");
        var tmax = daily.GetProperty("temperature_2m_max");
        var tmin = daily.GetProperty("temperature_2m_min");
        var precip = daily.GetProperty("precipitation_sum");
        var wind = daily.GetProperty("wind_speed_10m_max");
        var codes = daily.GetProperty("weather_code");

        var utcOffsetSeconds = response.TryGetProperty("utc_offset_seconds", out var off)
            ? off.GetInt32() : 0;
        var offset = TimeSpan.FromSeconds(utcOffsetSeconds);

        var count = times.GetArrayLength();
        for (var d = 0; d < count; d++)
        {
            if (!TryGetDouble(tmax[d], out var max) ||
                !TryGetDouble(tmin[d], out var min) ||
                !TryGetDouble(precip[d], out var pmm) ||
                !TryGetDouble(wind[d], out var w) ||
                codes[d].ValueKind != JsonValueKind.Number)
            {
                continue; // skip days with missing core fields (Open-Meteo peut renvoyer null)
            }

            var date = DateOnly.ParseExact(times[d].GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
            var midday = new DateTimeOffset(date.ToDateTime(new TimeOnly(12, 0)), offset);
            var code = codes[d].GetInt32();

            acc.Add(new WeatherSample(
                At: at,
                When: midday,
                TemperatureMinCelsius: min,
                TemperatureMaxCelsius: max,
                PrecipitationMm: pmm,
                WindKmh: w,
                WmoCode: code,
                Summary: WmoCodeMap.LabelFr(code)));
        }
    }

    private static bool TryGetDouble(JsonElement e, out double value)
    {
        if (e.ValueKind == JsonValueKind.Number)
        {
            value = e.GetDouble();
            return true;
        }
        value = 0;
        return false;
    }
}

public sealed class OpenMeteoException(string message) : Exception(message);
