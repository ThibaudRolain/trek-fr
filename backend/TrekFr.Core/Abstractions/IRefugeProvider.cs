using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface IRefugeProvider
{
    Task<IReadOnlyList<Refuge>> FindNearAsync(
        IReadOnlyList<Coordinate> trackPoints,
        double bufferMeters,
        CancellationToken ct = default);
}

public sealed record Refuge(
    string Id,
    string Name,
    Coordinate Location,
    string? Type,
    int? Capacity,
    string? Url);
