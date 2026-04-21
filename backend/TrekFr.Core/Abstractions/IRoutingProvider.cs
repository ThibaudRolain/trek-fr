using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface IRoutingProvider
{
    Task<Track> GenerateRoundTripAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed = null,
        CancellationToken ct = default);

    Task<Track> RouteAsync(
        Coordinate from,
        Coordinate to,
        Profile profile,
        CancellationToken ct = default);
}
