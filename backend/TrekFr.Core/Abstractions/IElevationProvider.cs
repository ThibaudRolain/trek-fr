using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface IElevationProvider
{
    Task<IReadOnlyList<Coordinate>> EnrichAsync(
        IReadOnlyList<Coordinate> points,
        CancellationToken ct = default);
}
