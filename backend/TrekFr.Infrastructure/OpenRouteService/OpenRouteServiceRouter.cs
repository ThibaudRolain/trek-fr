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

    public async Task<Track> GenerateRoundTripAsync(
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

        using var response = await http.SendAsync(msg, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new OpenRouteServiceException(
                $"ORS round_trip failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
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

        return new Track(points, profile);
    }

    public async Task<Track> RouteAsync(
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
        };

        using var msg = new HttpRequestMessage(HttpMethod.Post, $"/v2/directions/{orsProfile}/geojson")
        {
            Content = JsonContent.Create(request, options: JsonOptions),
        };
        msg.Headers.TryAddWithoutValidation("Authorization", _options.ApiKey);

        using var response = await http.SendAsync(msg, ct);
        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(ct);
            throw new OpenRouteServiceException(
                $"ORS point-to-point failed: {(int)response.StatusCode} {response.ReasonPhrase}. Body: {body}");
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

        return new Track(points, profile);
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
    }

    private sealed class OrsGeometry
    {
        [JsonPropertyName("coordinates")]
        public List<List<double>>? Coordinates { get; init; }
    }
}

public sealed class OpenRouteServiceException(string message) : Exception(message);
