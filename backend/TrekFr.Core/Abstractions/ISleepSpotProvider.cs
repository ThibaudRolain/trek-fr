using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface ISleepSpotProvider
{
    Task<IReadOnlyList<SleepSpotCandidate>> FindAlongTrackAsync(
        IReadOnlyList<Coordinate> trackPoints,
        double bufferMeters,
        CancellationToken ct = default);
}

public sealed record SleepSpotCandidate(
    SleepSpot Spot,
    int NearestTrackIndex,
    double OffTrackDistanceMeters,
    double PatrimonyScore);
