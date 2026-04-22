namespace TrekFr.Core.Domain;

public sealed record ElevationFilter(double? MinGainMeters, double? MaxGainMeters)
{
    public bool IsActive => MinGainMeters is not null || MaxGainMeters is not null;

    public bool Matches(double gainMeters)
    {
        if (MinGainMeters is { } min && gainMeters < min) return false;
        if (MaxGainMeters is { } max && gainMeters > max) return false;
        return true;
    }

    public string Describe()
    {
        return (MinGainMeters, MaxGainMeters) switch
        {
            ({ } min, { } max) => $"entre {min:F0} et {max:F0} m D+",
            ({ } min, null) => $"au moins {min:F0} m D+",
            (null, { } max) => $"au plus {max:F0} m D+",
            _ => "aucun filtre D+",
        };
    }
}
