using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Options;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Infrastructure.OpenRouteService;

public sealed class OpenRouteServiceRouter(
    HttpClient http,
    IOptions<OpenRouteServiceOptions> options) : IRoutingProvider
{
    private readonly OpenRouteServiceOptions _options = options.Value;

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

    public async Task<(Track Track, TrackExtras? Extras)> GenerateRoundTripAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed = null,
        CancellationToken ct = default)
    {
        var orsProfile = ToOrsProfile(profile);
        var request = new OrsDirectionsRequest
        {
            Coordinates = [[start.Longitude, start.Latitude]],
            Elevation = true,
            ExtraInfo = ["waytype", "surface"],
            Options = new OrsOptions
            {
                RoundTrip = new OrsRoundTrip
                {
                    Length = targetDistanceMeters,
                    Points = EstimatePoints(targetDistanceMeters),
                    Seed = seed,
                },
            },
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, $"/v2/directions/{orsProfile}/geojson")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        msg.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey);

        return await SendAndParseAsync(msg, profile, "round_trip", ct);
    }

    public async Task<(Track Track, TrackExtras? Extras)> RouteAsync(
        Coordinate from,
        Coordinate to,
        Profile profile,
        CancellationToken ct = default)
    {
        var orsProfile = ToOrsProfile(profile);
        var request = new OrsDirectionsRequest
        {
            Coordinates =
            [
                [from.Longitude, from.Latitude],
                [to.Longitude, to.Latitude],
            ],
            Elevation = true,
            ExtraInfo = ["waytype", "surface"],
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, $"/v2/directions/{orsProfile}/geojson")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        msg.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey);

        return await SendAndParseAsync(msg, profile, "point-to-point", ct);
    }

    private async Task<(Track Track, TrackExtras? Extras)> SendAndParseAsync(
        HttpRequestMessage msg, Profile profile, string operation, CancellationToken ct)
    {
        using var response = await http.SendAsync(msg, ct);
        if (!response.IsSuccessStatusCode)
        {
            await ThrowFromErrorResponseAsync(response, operation, ct);
        }

        var payload = await response.Content.ReadFromJsonAsync<OrsGeoJsonResponse>(JsonOptions, ct)
                      ?? throw new OpenRouteServiceException("ORS returned empty payload.");

        var feature = payload.Features is { Count: > 0 }
            ? payload.Features[0]
            : throw new OpenRouteServiceException("ORS response has no feature.");

        var coords = feature.Geometry?.Coordinates
            ?? throw new OpenRouteServiceException("ORS feature has no coordinates.");

        var points = new List<Coordinate>(coords.Count);
        foreach (var c in coords)
        {
            if (c.Count < 2) continue;
            double? elevation = c.Count >= 3 ? c[2] : null;
            points.Add(new Coordinate(c[1], c[0], elevation));
        }

        var extras = ParseExtras(feature.Properties);
        return (new Track(points, profile), extras);
    }

    public static TrackExtras? ParseExtras(OrsProperties? properties)
    {
        if (properties?.Extras is null) return null;

        var surface = ParseSummary<SurfaceEntry>(properties.Extras, "surface",
            e => new SurfaceEntry((int)e.Value, e.Amount, e.Distance));
        var waytypes = ParseSummary<WayTypeEntry>(properties.Extras, "waytype",
            e => new WayTypeEntry((int)e.Value, e.Amount, e.Distance));

        if (surface is null && waytypes is null) return null;
        return new TrackExtras(surface, waytypes);
    }

    private static List<T>? ParseSummary<T>(
        Dictionary<string, OrsExtraData> extras, string key, Func<OrsSummaryEntry, T> map)
    {
        if (!extras.TryGetValue(key, out var data)) return null;
        if (data.Summary is not { Count: > 0 }) return null;

        var result = new List<T>(data.Summary.Count);
        foreach (var e in data.Summary) result.Add(map(e));
        return result;
    }

    /// <summary>
    /// ORS renvoie un JSON { error: { code, message } } sur 4xx. Les codes 2010 / 2099
    /// correspondent à "point pas routable" — c'est une erreur utilisateur (clic trop loin
    /// d'une route), pas une panne upstream. On throw une exception dédiée pour permettre
    /// à l'API de renvoyer un 400 actionnable au lieu d'un 502 avec JSON technique.
    /// </summary>
    private static async Task ThrowFromErrorResponseAsync(HttpResponseMessage response, string context, CancellationToken ct)
    {
        var body = await response.Content.ReadAsStringAsync(ct);

        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error) && error.ValueKind == JsonValueKind.Object)
            {
                int? code = error.TryGetProperty("code", out var c) && c.ValueKind == JsonValueKind.Number ? c.GetInt32() : null;
                var message = error.TryGetProperty("message", out var m) ? m.GetString() : null;

                if (code is 2010 or 2099 || (message?.Contains("routable point", StringComparison.OrdinalIgnoreCase) ?? false))
                {
                    throw new NonRoutablePointException(
                        "Point non routable : l'un des points cliqués est trop loin d'un chemin ou d'une route connue par OpenRouteService. Rapproche-toi d'un sentier, d'une route ou d'un village et réessaie.");
                }
            }
        }
        catch (JsonException)
        {
            // ORS has returned non-JSON — fall through to the generic error.
        }

        throw new OpenRouteServiceException(
            $"ORS {context} failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
    }

    private static string ToOrsProfile(Profile profile) => profile switch
    {
        Profile.Foot => "foot-hiking",
        Profile.Mtb => "cycling-mountain",
        Profile.Road => "cycling-road",
        _ => "foot-hiking",
    };

    private static int EstimatePoints(double targetDistanceMeters)
    {
        var km = targetDistanceMeters / 1000d;
        return Math.Clamp((int)Math.Round(km / 3d) + 3, 3, 10);
    }

    private sealed class OrsDirectionsRequest
    {
        [JsonPropertyName("coordinates")]
        public required double[][] Coordinates { get; init; }

        [JsonPropertyName("elevation")]
        public bool Elevation { get; init; }

        [JsonPropertyName("extra_info")]
        public string[]? ExtraInfo { get; init; }

        [JsonPropertyName("options")]
        public OrsOptions? Options { get; init; }
    }

    private sealed class OrsOptions
    {
        [JsonPropertyName("round_trip")]
        public OrsRoundTrip? RoundTrip { get; init; }
    }

    private sealed class OrsRoundTrip
    {
        [JsonPropertyName("length")]
        public double Length { get; init; }

        [JsonPropertyName("points")]
        public int Points { get; init; }

        [JsonPropertyName("seed")]
        public int? Seed { get; init; }
    }

    private sealed class OrsGeoJsonResponse
    {
        [JsonPropertyName("features")]
        public List<OrsFeature>? Features { get; init; }
    }

    private sealed class OrsFeature
    {
        [JsonPropertyName("geometry")]
        public OrsGeometry? Geometry { get; init; }

        [JsonPropertyName("properties")]
        public OrsProperties? Properties { get; init; }
    }

    private sealed class OrsGeometry
    {
        [JsonPropertyName("coordinates")]
        public List<List<double>>? Coordinates { get; init; }
    }
}

public sealed class OrsProperties
{
    [JsonPropertyName("extras")]
    public Dictionary<string, OrsExtraData>? Extras { get; init; }
}

public sealed class OrsExtraData
{
    [JsonPropertyName("summary")]
    public List<OrsSummaryEntry>? Summary { get; init; }
}

public sealed class OrsSummaryEntry
{
    [JsonPropertyName("value")]
    public double Value { get; init; }

    [JsonPropertyName("amount")]
    public double Amount { get; init; }

    [JsonPropertyName("distance")]
    public double Distance { get; init; }
}

public sealed class OpenRouteServiceException(string message) : Exception(message);

/// <summary>
/// Un des points cliqués est trop loin d'un chemin routable — erreur utilisateur, pas
/// une panne backend. L'API doit renvoyer 400 avec un message actionnable.
/// </summary>
public sealed class NonRoutablePointException(string message) : Exception(message);
