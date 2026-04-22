using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface IDestinationProposer
{
    Task<ProposedDestination?> ProposeAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int? seed,
        CancellationToken ct = default);
}

public sealed record ProposedDestination(string Name, Coordinate Location, int Population);
