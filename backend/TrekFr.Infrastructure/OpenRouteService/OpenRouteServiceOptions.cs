namespace TrekFr.Infrastructure.OpenRouteService;

public sealed class OpenRouteServiceOptions
{
    public const string SectionName = "OpenRouteService";

    public string BaseUrl { get; set; } = "https://api.openrouteservice.org";
    public string ApiKey { get; set; } = string.Empty;
}
