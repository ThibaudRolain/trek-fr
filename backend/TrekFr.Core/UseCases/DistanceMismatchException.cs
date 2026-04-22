using System;

namespace TrekFr.Core.UseCases;

/// <summary>
/// ORS a renvoyé une trace dont la distance s'écarte trop de la cible demandée
/// (pour round-trip typiquement : zones isolées ou peu maillées où la graphe
/// ne supporte pas la boucle demandée, ORS renvoie alors "best effort" qui
/// peut être 5-10× off). Plutôt que d'afficher silencieusement cette trace,
/// on throw pour laisser l'utilisateur retuner.
/// </summary>
public sealed class DistanceMismatchException(double targetMeters, double bestActualMeters, int attempts)
    : Exception(BuildMessage(targetMeters, bestActualMeters, attempts))
{
    public double TargetMeters { get; } = targetMeters;
    public double BestActualMeters { get; } = bestActualMeters;
    public int Attempts { get; } = attempts;

    private static string BuildMessage(double target, double best, int attempts)
    {
        var targetKm = target / 1000d;
        var bestKm = best / 1000d;
        var ratio = best / target;
        var suggestion = ratio > 1.2
            ? $"Le réseau routier local ne permet pas de boucle aussi courte. Essaie **{bestKm:F0} km** (le meilleur trouvé) ou éloigne-toi vers une zone plus densément maillée (ville, village)."
            : $"Essaie une distance un peu différente, ou un autre point de départ.";
        return $"Aucune boucle dans la tolérance ±20 % de {targetKm:F0} km après {attempts} essais " +
               $"(meilleur résultat : {bestKm:F1} km). {suggestion}";
    }
}
