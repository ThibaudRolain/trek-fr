using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace TrekFr.Infrastructure.Destinations;

/// <summary>
/// Singleton wrapper around the bundled communes-fr.json dataset. Loaded once at startup
/// and shared by every consumer (destination proposer, town provider, etc.).
/// </summary>
public sealed class CommunesDataset
{
    private const string EmbeddedResourceName = "TrekFr.Infrastructure.Destinations.communes-fr.json";

    public IReadOnlyList<CommuneRecord> Communes { get; }

    public CommunesDataset()
    {
        Communes = LoadEmbedded();
    }

    private static IReadOnlyList<CommuneRecord> LoadEmbedded()
    {
        var assembly = typeof(CommunesDataset).Assembly;
        using var stream = assembly.GetManifestResourceStream(EmbeddedResourceName)
            ?? throw new InvalidOperationException(
                $"Embedded resource '{EmbeddedResourceName}' not found. Run backend/tools/BuildCommunes to generate communes-fr.json.");
        return JsonSerializer.Deserialize<List<CommuneRecord>>(stream)
            ?? throw new InvalidOperationException("communes-fr.json deserialized to null.");
    }
}
