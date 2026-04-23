using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface IMhPoiProvider
{
    Task<IReadOnlyList<MhPoi>> FindAlongTrackAsync(
        IReadOnlyList<Coordinate> trackPoints,
        CancellationToken ct = default);
}
