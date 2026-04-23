using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Logging;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using TrekFr.Core.UseCases;
using TrekFr.Infrastructure.Communes;
using TrekFr.Infrastructure.OpenRouteService;
using TrekFr.Infrastructure.Weather;

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

        group.MapPost("/weather", GetWeatherAsync)
            .WithName("GetTrackWeather");

        return app;
    }

    private static async Task<IResult> GetWeatherAsync(
        TrackWeatherRequest request,
        GetWeatherForPoints useCase,
        ILogger<GetWeatherForPoints> logger,
        CancellationToken ct)
    {
        if (request.Points is null || request.Points.Count == 0)
        {
            return Results.BadRequest(new { error = "at least one point is required" });
        }
        if (request.Points.Count > 10)
        {
            return Results.BadRequest(new { error = "too many points (max 10)" });
        }
        if (request.Days is < 1 or > 16)
        {
            return Results.BadRequest(new { error = "days must be between 1 and 16" });
        }
        foreach (var p in request.Points)
        {
            if (p.Latitude is < -90 or > 90 || p.Longitude is < -180 or > 180)
            {
                return Results.BadRequest(new { error = $"invalid coordinates for label '{p.Label}'" });
            }
        }

        var startDate = request.StartDate ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var labeled = request.Points
            .Select(p => new LabeledPoint(p.Label, new Coordinate(p.Latitude, p.Longitude)))
            .ToList();

        try
        {
            var forecasts = await useCase.ExecuteAsync(labeled, startDate, request.Days, ct);
            return Results.Ok(forecasts.Select(TrackWeatherResponse.From).ToList());
        }
        catch (OpenMeteoException ex)
        {
            logger.LogWarning(ex, "Open-Meteo request failed");
            return UpstreamBadGateway(ex, "Open-Meteo error");
        }
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
        CommuneDataset communes,
        IMhPoiProvider mhPois,
        ILogger<GenerateRoundTrip> logger,
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
            var filter = new ElevationFilter(request.MinElevationGainMeters, request.MaxElevationGainMeters);

            Track track;
            TrackStats stats;
            ProposedDestination? proposedDestination = null;
            IReadOnlyList<TrackVariantDto>? variants = null;

            switch (request.Mode)
            {
                case TrackGenerationMode.AToB
                    when request.EndLatitude is { } endLat && request.EndLongitude is { } endLon:
                {
                    var r = await aToB.ExecuteAsync(start, new Coordinate(endLat, endLon), request.Profile, filter, ct);
                    (track, stats) = (r.Track, r.Stats);
                    break;
                }
                case TrackGenerationMode.AToB:
                {
                    var r = await propose.ExecuteAsync(start, request.DistanceKm * 1000d, request.Profile, request.Seed, filter, ct);
                    (track, stats, proposedDestination) = (r.Track, r.Stats, r.Destination);
                    break;
                }
                case TrackGenerationMode.RoundTrip when request.Seed is null && (filter is null || !filter.IsActive):
                {
                    // Première génération sans seed ni filtre D+ : on génère plusieurs variantes
                    // et on expose toutes au front pour permettre le cycle local (sans appel ORS).
                    var all = await roundTrip.GenerateVariantsAsync(start, request.DistanceKm * 1000d, request.Profile, ct);
                    var best = all[0];
                    (track, stats) = (best.Track, best.Stats);
                    var profile = track.Profile.ToString().ToLowerInvariant();
                    variants = all.Select(v => TrackVariantDto.From(v, profile)).ToList();
                    break;
                }
                case TrackGenerationMode.RoundTrip:
                {
                    var r = await roundTrip.ExecuteAsync(start, request.DistanceKm * 1000d, request.Profile, request.Seed, filter, ct);
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
                    ArrivalName: proposedDestination?.Name ?? "Arrivée");
                try
                {
                    stages = await splitter.ExecuteAsync(track, opts, ct);
                }
                catch (NoStageSleepSpotException ex)
                {
                    var nearest = communes.FindNearestWithDistance(ex.PivotLocation);
                    warnings = nearest is null
                        ? [new WarningDto(ex.Message)]
                        : [new WarningDto(ex.Message, nearest.Value.Commune.Name, nearest.Value.DistanceMeters)];
                }
            }

            IReadOnlyList<PoiOnRouteDto>? pois = null;
            try
            {
                var mhList = await mhPois.FindAlongTrackAsync(track.Points, ct);
                if (mhList.Count > 0)
                    pois = mhList.Select(PoiOnRouteDto.From).ToList();
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "MH POI lookup failed — continuing without POIs");
                warnings ??= [];
                warnings.Add(new WarningDto("Enrichissement patrimoine indisponible."));
            }

            return Results.Ok(TrackResponse.From(track, stats, proposedDestination, stages, warnings, pois: pois, variants: variants));
        }
        catch (NoDestinationCandidateException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (ElevationOutOfRangeException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (DistanceMismatchException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (NonRoutablePointException ex)
        {
            return Results.BadRequest(new { error = ex.Message });
        }
        catch (OpenRouteServiceException ex)
        {
            return UpstreamBadGateway(ex, "OpenRouteService error");
        }
    }

    private static IResult UpstreamBadGateway(Exception ex, string title) =>
        Results.Problem(detail: ex.Message, statusCode: StatusCodes.Status502BadGateway, title: title);

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
        bool hasEndPoint = request.EndLatitude is not null && request.EndLongitude is not null;

        if (hasEndPoint)
        {
            if (request.EndLatitude is < -90 or > 90 || request.EndLongitude is < -180 or > 180)
            {
                return Results.BadRequest(new { error = "invalid end coordinates" });
            }
            return null;
        }

        if (request.DistanceKm is <= 0 or > 200)
        {
            return Results.BadRequest(new { error = "distance must be between 1 and 200 km" });
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
