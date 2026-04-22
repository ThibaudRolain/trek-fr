namespace TrekFr.Infrastructure.Weather;

/// <summary>
/// WMO weather interpretation codes (Open-Meteo convention). Mapping FR pour l'affichage.
/// https://open-meteo.com/en/docs (section "WMO Weather interpretation codes").
/// </summary>
internal static class WmoCodeMap
{
    public static string LabelFr(int code) => code switch
    {
        0 => "Ciel clair",
        1 => "Peu nuageux",
        2 => "Partiellement nuageux",
        3 => "Couvert",
        45 or 48 => "Brouillard",
        51 => "Bruine légère",
        53 => "Bruine",
        55 => "Bruine dense",
        56 or 57 => "Bruine verglaçante",
        61 => "Pluie légère",
        63 => "Pluie",
        65 => "Pluie forte",
        66 or 67 => "Pluie verglaçante",
        71 => "Neige légère",
        73 => "Neige",
        75 => "Neige forte",
        77 => "Grains de neige",
        80 => "Averses légères",
        81 => "Averses",
        82 => "Averses violentes",
        85 => "Averses de neige légères",
        86 => "Averses de neige fortes",
        95 => "Orage",
        96 or 99 => "Orage avec grêle",
        _ => "—",
    };
}
