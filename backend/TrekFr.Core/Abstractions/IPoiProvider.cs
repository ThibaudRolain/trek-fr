using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface IPoiProvider
{
    Task<IReadOnlyList<Poi>> FindNearAsync(
        IReadOnlyList<Coordinate> trackPoints,
        double bufferMeters,
        PoiCategory categories,
        CancellationToken ct = default);
}

[System.Flags]
public enum PoiCategory
{
    None = 0,
    Cultural = 1 << 0,
    Natural = 1 << 1,
    Viewpoint = 1 << 2,
    Water = 1 << 3,
    All = Cultural | Natural | Viewpoint | Water
}

public sealed record Poi(
    string Id,
    string Name,
    Coordinate Location,
    PoiCategory Category,
    string? Description,
    string? Url);
