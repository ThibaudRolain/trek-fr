using System.Text.Json.Serialization;

namespace TrekFr.Infrastructure.Destinations;

public sealed record CommuneRecord(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon,
    [property: JsonPropertyName("pop")] int Population,
    [property: JsonPropertyName("score")] double Score);
