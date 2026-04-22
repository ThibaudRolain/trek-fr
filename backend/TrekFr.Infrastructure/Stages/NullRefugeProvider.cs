using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Infrastructure.Stages;

/// <summary>
/// Placeholder until a real source (Overpass OSM refuge=alpine_hut/wilderness_hut/…) is wired up.
/// Keeps IRefugeProvider as an explicit seam without blocking the stage-splitting flow.
/// </summary>
public sealed class NullRefugeProvider : IRefugeProvider
{
    private static readonly IReadOnlyList<Refuge> Empty = [];

    public Task<IReadOnlyList<Refuge>> FindNearAsync(
        IReadOnlyList<Coordinate> trackPoints,
        double bufferMeters,
        CancellationToken ct = default)
        => Task.FromResult(Empty);
}
