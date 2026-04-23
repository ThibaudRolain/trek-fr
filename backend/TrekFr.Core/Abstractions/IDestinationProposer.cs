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

    /// <summary>
    /// Renvoie les meilleurs candidats (triés par score décroissant) dans la tolérance distance.
    /// Utilisé quand l'appelant doit filtrer chaque candidat (ex: filtre dénivelé qui nécessite
    /// d'appeler le routing provider pour chaque candidat).
    /// </summary>
    Task<IReadOnlyList<ProposedDestination>> GetTopCandidatesAsync(
        Coordinate start,
        double targetDistanceMeters,
        Profile profile,
        int topN,
        CancellationToken ct = default);
}

public sealed record ProposedDestination(
    string Name,
    Coordinate Location,
    int Population,
    int? MonumentsHistoriques = null,
    bool IsPlusBeauVillage = false,
    bool IsVilleArtHistoire = false);
