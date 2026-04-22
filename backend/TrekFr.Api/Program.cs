using System.Text.Json.Serialization;
using TrekFr.Api.Tracks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.UseCases;
using TrekFr.Infrastructure.Communes;
using TrekFr.Infrastructure.Destinations;
using TrekFr.Infrastructure.Gpx;
using TrekFr.Infrastructure.OpenRouteService;
using TrekFr.Infrastructure.Stages;
using TrekFr.Infrastructure.Weather;

var builder = WebApplication.CreateBuilder(args);

const string AngularDevCors = "AngularDev";

builder.Services.AddOpenApi();

builder.Services.ConfigureHttpJsonOptions(options =>
{
    options.SerializerOptions.Converters.Add(new JsonStringEnumConverter(
        namingPolicy: System.Text.Json.JsonNamingPolicy.CamelCase,
        allowIntegerValues: true));
    options.SerializerOptions.PropertyNameCaseInsensitive = true;
});

builder.Services.AddCors(options =>
{
    options.AddPolicy(AngularDevCors, policy =>
    {
        policy.WithOrigins("http://localhost:4200")
              .AllowAnyHeader()
              .AllowAnyMethod();
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
builder.Services.AddSingleton<IRefugeProvider, NullRefugeProvider>();
builder.Services.AddSingleton<CommunesTownProvider>();
builder.Services.AddSingleton<ISleepSpotProvider, CompositeSleepSpotProvider>();
builder.Services.AddScoped<GenerateRoundTrip>();
builder.Services.AddScoped<RouteAToB>();
builder.Services.AddScoped<ProposeDestination>();
builder.Services.AddScoped<SplitIntoStages>();
builder.Services.AddScoped<GetWeatherForPoints>();

var app = builder.Build();

// Charge le dataset communes au démarrage plutôt que sur la première requête :
// la première /tracks/generate ou /tracks/weather paierait sinon ~500 ms de JSON parse.
_ = app.Services.GetRequiredService<CommuneDataset>();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.UseCors(AngularDevCors);
}

app.UseHttpsRedirection();

app.MapGet("/health", () => Results.Ok(new { status = "ok" }))
   .WithName("Health");

app.MapTracksEndpoints();

app.Run();

// Rend le type Program visible pour WebApplicationFactory<Program> côté tests.
public partial class Program;
