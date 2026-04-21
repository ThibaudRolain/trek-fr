using System.Text.Json.Serialization;

namespace TrekFr.Tools.BuildCommunes;

public sealed record CommuneRaw(string WikidataId, string Name, double Lat, double Lon, int? Population);

public sealed record CommuneEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon,
    [property: JsonPropertyName("pop")] int Population,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("mh")] int MonumentsHistoriques,
    [property: JsonPropertyName("pbv")] bool IsPlusBeauVillage,
    [property: JsonPropertyName("vah")] bool IsVilleArtHistoire);
