using System.Text.Json.Serialization;
using TrekFr.Api.Tracks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.UseCases;
using TrekFr.Infrastructure.Communes;
using TrekFr.Infrastructure.Destinations;
using TrekFr.Infrastructure.Gpx;
using TrekFr.Infrastructure.OpenRouteService;
using TrekFr.Infrastructure.Weather;

var builder = WebApplication.CreateBuilder(args);

const string AngularDevCors = "AngularDev";
const string ProductionCors = "Production";

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(
        namingPolicy: System.Text.Json.JsonNamingPolicy.CamelCase,
        allowIntegerValues: true));
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

// CORS prod : liste d'origines séparées par des virgules, via Cors:AllowedOrigins
// (config nested binding) ou via l'env var ALLOWED_ORIGINS (pratique sur Fly/compose).
var allowedOrigins = (builder.Configuration["Cors:AllowedOrigins"]
                      ?? builder.Configuration["ALLOWED_ORIGINS"]
                      ?? string.Empty)
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevCors, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
    });

    options.AddPolicy(ProductionCors, policy =>
    {
        if (allowedOrigins.Length > 0)
        {
            policy.WithOrigins(allowedOrigins)
                  .AllowAnyHeader()
                  .AllowAnyMethod();
        }
    });
});

builder.Services.AddSingleton<IGpxParser, GpxParser>();
builder.Services.AddScoped<ImportGpxTrack>();

builder.Services
    .AddOptions<OpenRouteServiceOptions>()
    .Bind(builder.Configuration.GetSection(OpenRouteServiceOptions.SectionName))
    .ValidateOnStart();

builder.Services.AddHttpClient<IRoutingProvider, OpenRouteServiceRouter>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenRouteServiceOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(30);
});

builder.Services
    .AddOptions<OpenMeteoOptions>()
    .Bind(builder.Configuration.GetSection(OpenMeteoOptions.SectionName));

builder.Services.AddHttpClient<IWeatherProvider, OpenMeteoWeatherProvider>((sp, client) =>
{
    var options = sp.GetRequiredService<Microsoft.Extensions.Options.IOptions<OpenMeteoOptions>>().Value;
    client.BaseAddress = new Uri(options.BaseUrl);
    client.Timeout = TimeSpan.FromSeconds(10);
});

builder.Services.AddSingleton<CommuneDataset>();
builder.Services.AddSingleton<INearestCommuneFinder>(sp => sp.GetRequiredService<CommuneDataset>());
builder.Services.AddSingleton<IDestinationProposer, CommunesDestinationProposer>();
builder.Services.AddScoped<GenerateRoundTrip>();
builder.Services.AddScoped<RouteAToB>();
builder.Services.AddScoped<ProposeDestination>();
builder.Services.AddScoped<GetWeatherForPoints>();

var app = builder.Build();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(AngularDevCors);
    app.UseHttpsRedirection();
}
else
{
    app.UseCors(ProductionCors);
    // Pas de UseHttpsRedirection en conteneur : la plateforme (Fly/proxy) termine TLS
    // et l'app écoute en HTTP sur :8080. Forcer la redirection créerait une boucle.
}

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health");

app.MapTracksEndpoints();

app.Run();
