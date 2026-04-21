using System.Text.Json.Serialization;

namespace TrekFr.Tools.BuildCommunes;

public sealed record CommuneRaw(string WikidataId, string Name, double Lat, double Lon, int? Population, string? InseeCode);

/// <summary>
/// Bundle of heritage signals used by <see cref="CommuneScorer"/>. Grouped into a single record
/// so adding a new signal later (POI density, weather, etc.) doesn't cascade into every caller.
/// </summary>
public sealed record HeritageSignals(
    IReadOnlyDictionary<string, int> WikidataHeritageCounts,
    IReadOnlyDictionary<string, int>? MerimeeCountsByInsee,
    IReadOnlySet<string> PlusBeauxVillages,
    IReadOnlySet<string> VillesArtHistoire);

public sealed record CommuneEntry(
    [property: JsonPropertyName("name")] string Name,
    [property: JsonPropertyName("lat")] double Lat,
    [property: JsonPropertyName("lon")] double Lon,
    [property: JsonPropertyName("pop")] int Population,
    [property: JsonPropertyName("score")] double Score,
    [property: JsonPropertyName("mh")] int MonumentsHistoriques,
    [property: JsonPropertyName("pbv")] bool IsPlusBeauVillage,
    [property: JsonPropertyName("vah")] bool IsVilleArtHistoire);
