namespace TrekFr.Core.Domain;

public sealed record ElevationFilter(double? MinGainMeters, double? MaxGainMeters)
{
    /// <summary>
    /// Tolérance élastique appliquée vers l'extérieur des bornes utilisateur : atteindre
    /// précisément un D+ cible via ORS est difficile (5 retries max), ±15 % rend le filtre
    /// utilisable sans sacrifier l'intention. Annoncé explicitement dans l'UI — pas silencieux.
    /// </summary>
    public const double ToleranceRatio = 0.15;

    public bool IsActive => MinGainMeters is not null || MaxGainMeters is not null;

    public bool Matches(double gainMeters)
    {
        if (MinGainMeters is { } min && gainMeters < min * (1 - ToleranceRatio)) return false;
        if (MaxGainMeters is { } max && gainMeters > max * (1 + ToleranceRatio)) return false;
        return true;
    }

    public string Describe()
    {
        const string t = " (±15 %)";
        return (MinGainMeters, MaxGainMeters) switch
        {
            ({ } min, { } max) => $"entre {min:F0} et {max:F0} m D+{t}",
            ({ } min, null) => $"au moins {min:F0} m D+{t}",
            (null, { } max) => $"au plus {max:F0} m D+{t}",
            _ => "aucun filtre D+",
        };
    }
}
