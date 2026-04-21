using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TrekFr.Core.Domain;
using TrekFr.Core.UseCases;
using TrekFr.Infrastructure.OpenRouteService;

namespace TrekFr.Api.Tracks;

public static class TracksEndpoints
{
    public static IEndpointRouteBuilder MapTracksEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/tracks").WithTags("Tracks");

        group.MapPost("/import", ImportGpxAsync)
            .DisableAntiforgery()
            .WithName("ImportGpx");

        group.MapPost("/generate", GenerateAsync)
            .WithName("GenerateTrack");

        return app;
    }

    private static async Task<IResult> ImportGpxAsync(
        IFormFile? gpx,
        Profile? profile,
        ImportGpxTrack useCase,
        CancellationToken ct)
    {
        if (gpx is null || gpx.Length == 0)
        {
            return Results.BadRequest(new { error = "missing gpx file" });
        }

        await using var stream = gpx.OpenReadStream();
        var imported = await Task.Run(() => useCase.Execute(stream, profile ?? Profile.Foot), ct);
        return Results.Ok(TrackResponse.From(imported));
    }

    private static async Task<IResult> GenerateAsync(
        TrackGenerateRequest request,
        GenerateRoundTrip roundTrip,
        RouteAToB aToB,
        CancellationToken ct)
    {
        if (request.Latitude is < -90 or > 90 || request.Longitude is < -180 or > 180)
        {
            return Results.BadRequest(new { error = "invalid start coordinates" });
        }

        var validation = request.Mode switch
        {
            TrackGenerationMode.AToB => ValidateAToB(request),
            TrackGenerationMode.RoundTrip => ValidateRoundTrip(request),
            _ => Results.BadRequest(new { error = "unknown mode" }),
        };
        if (validation is not null) return validation;

        try
        {
            var generated = request.Mode switch
            {
                TrackGenerationMode.AToB => await aToB.ExecuteAsync(
                    new Coordinate(request.Latitude, request.Longitude),
                    new Coordinate(request.EndLatitude!.Value, request.EndLongitude!.Value),
                    request.Profile,
                    ct),
                TrackGenerationMode.RoundTrip => await roundTrip.ExecuteAsync(
                    new Coordinate(request.Latitude, request.Longitude),
                    request.DistanceKm * 1000d,
                    request.Profile,
                    request.Seed,
                    ct),
                _ => throw new System.InvalidOperationException("Unreachable — mode was validated above."),
            };
            return Results.Ok(TrackResponse.From(generated));
        }
        catch (OpenRouteServiceException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "OpenRouteService error");
        }
    }

    private static IResult? ValidateRoundTrip(TrackGenerateRequest request)
    {
        if (request.DistanceKm is <= 0 or > 100)
        {
            return Results.BadRequest(new { error = "distance must be between 0 and 100 km (ORS round-trip limit)" });
        }
        return null;
    }

    private static IResult? ValidateAToB(TrackGenerateRequest request)
    {
        if (request.EndLatitude is not { } endLat || request.EndLongitude is not { } endLon)
        {
            // ProposeDestination (Phase B) fills this in; for now return 501 so the UI can show
            // an accurate "pose une arrivée manuellement" hint instead of a validation error.
            return Results.Problem(
                detail: "La proposition automatique de destination arrive dans une prochaine slice. Pose une arrivée manuellement pour l'instant.",
                statusCode: StatusCodes.Status501NotImplemented,
                title: "Fonctionnalité à venir");
        }
        if (endLat is < -90 or > 90 || endLon is < -180 or > 180)
        {
            return Results.BadRequest(new { error = "invalid end coordinates" });
        }
        return null;
    }
}
