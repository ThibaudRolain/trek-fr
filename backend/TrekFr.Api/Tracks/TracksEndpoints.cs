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
        ProposeDestination propose,
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
            var start = new Coordinate(request.Latitude, request.Longitude);
            return request.Mode switch
            {
                TrackGenerationMode.AToB when request.EndLatitude is { } endLat && request.EndLongitude is { } endLon
                    => Results.Ok(TrackResponse.From(
                        await aToB.ExecuteAsync(start, new Coordinate(endLat, endLon), request.Profile, ct))),
                TrackGenerationMode.AToB
                    => Results.Ok(TrackResponse.From(
                        await propose.ExecuteAsync(start, request.DistanceKm * 1000d, request.Profile, request.Seed, ct))),
                TrackGenerationMode.RoundTrip
                    => Results.Ok(TrackResponse.From(
                        await roundTrip.ExecuteAsync(start, request.DistanceKm * 1000d, request.Profile, request.Seed, ct))),
                _ => Results.BadRequest(new { error = "unknown mode" }),
            };
        }
        catch (NoDestinationCandidateException ex)
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
        // Distance is required in both sub-modes (routing needs it for the propose path,
        // and it's a sanity check for the explicit-end path).
        if (request.DistanceKm is <= 0 or > 200)
        {
            return Results.BadRequest(new { error = "distance must be between 1 and 200 km" });
        }
        if (request.EndLatitude is { } endLat && request.EndLongitude is { } endLon)
        {
            if (endLat is < -90 or > 90 || endLon is < -180 or > 180)
            {
                return Results.BadRequest(new { error = "invalid end coordinates" });
            }
        }
        return null;
    }
}
