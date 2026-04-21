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

        try
        {
            GeneratedTrack generated = request.Mode switch
            {
                TrackGenerationMode.AToB => await ExecuteAToB(request, aToB, ct),
                _ => await ExecuteRoundTrip(request, roundTrip, ct),
            };
            return Results.Ok(TrackResponse.From(generated));
        }
        catch (BadRequestException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (OpenRouteServiceException ex)
        {
            return Results.Problem(
                detail: ex.Message,
                statusCode: StatusCodes.Status502BadGateway,
                title: "OpenRouteService error");
        }
    }

    private static Task<GeneratedTrack> ExecuteRoundTrip(
        TrackGenerateRequest request,
        GenerateRoundTrip useCase,
        CancellationToken ct)
    {
        if (request.DistanceKm <= 0 || request.DistanceKm > 100)
        {
            throw new BadRequestException("distance must be between 0 and 100 km (ORS round-trip limit)");
        }

        return useCase.ExecuteAsync(
            new Coordinate(request.Latitude, request.Longitude),
            request.DistanceKm * 1000d,
            request.Profile,
            request.Seed,
            ct);
    }

    private static Task<GeneratedTrack> ExecuteAToB(
        TrackGenerateRequest request,
        RouteAToB useCase,
        CancellationToken ct)
    {
        if (request.EndLatitude is not { } endLat || request.EndLongitude is not { } endLon)
        {
            throw new BadRequestException("endLatitude and endLongitude are required in A→B mode");
        }
        if (endLat is < -90 or > 90 || endLon is < -180 or > 180)
        {
            throw new BadRequestException("invalid end coordinates");
        }

        return useCase.ExecuteAsync(
            new Coordinate(request.Latitude, request.Longitude),
            new Coordinate(endLat, endLon),
            request.Profile,
            ct);
    }

    private sealed class BadRequestException(string message) : System.Exception(message);
}
