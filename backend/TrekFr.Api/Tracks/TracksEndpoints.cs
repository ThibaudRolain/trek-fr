using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using TrekFr.Core.Domain;
using TrekFr.Core.UseCases;
using TrekFr.Infrastructure.Destinations;
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
        SplitIntoStages splitter,
        CommunesDataset communes,
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
        } ?? ValidateSplit(request);
        if (validation is not null) return validation;

        try
        {
            var start = new Coordinate(request.Latitude, request.Longitude);

            Track track;
            TrackStats stats;
            string? proposedName = null;

            switch (request.Mode)
            {
                case TrackGenerationMode.AToB
                    when request.EndLatitude is { } endLat && request.EndLongitude is { } endLon:
                {
                    var r = await aToB.ExecuteAsync(start, new Coordinate(endLat, endLon), request.Profile, ct);
                    (track, stats) = (r.Track, r.Stats);
                    break;
                }
                case TrackGenerationMode.AToB:
                {
                    var r = await propose.ExecuteAsync(start, request.DistanceKm * 1000d, request.Profile, request.Seed, ct);
                    (track, stats, proposedName) = (r.Track, r.Stats, r.Destination.Name);
                    break;
                }
                case TrackGenerationMode.RoundTrip:
                {
                    var r = await roundTrip.ExecuteAsync(start, request.DistanceKm * 1000d, request.Profile, request.Seed, ct);
                    (track, stats) = (r.Track, r.Stats);
                    break;
                }
                default:
                    return Results.BadRequest(new { error = "unknown mode" });
            }

            IReadOnlyList<Stage>? stages = null;
            List<WarningDto>? warnings = null;
            if (request.SplitStages)
            {
                var opts = new StageOptions(
                    MaxDistancePerDayMeters: request.StageDistanceKm!.Value * 1000d,
                    MaxElevationGainPerDay: request.StageElevationGain!.Value,
                    ArrivalName: proposedName ?? "Arrivée");
                try
                {
                    stages = await splitter.ExecuteAsync(track, opts, ct);
                }
                catch (NoStageSleepSpotException ex)
                {
                    var (nearest, distance) = communes.FindNearest(ex.PivotLocation);
                    warnings = [new WarningDto(ex.Message, nearest.Name, distance)];
                }
            }

            return Results.Ok(TrackResponse.From(track, stats, proposedName, stages, warnings));
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

    private static IResult? ValidateSplit(TrackGenerateRequest request)
    {
        if (!request.SplitStages) return null;
        if (request.StageDistanceKm is not (> 0 and <= 100))
        {
            return Results.BadRequest(new { error = "stageDistanceKm must be between 1 and 100" });
        }
        if (request.StageElevationGain is not (> 0 and <= 10_000))
        {
            return Results.BadRequest(new { error = "stageElevationGain must be between 1 and 10000 meters" });
        }
        return null;
    }
}
