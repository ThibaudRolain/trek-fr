using TrekFr.Core.Domain;

namespace TrekFr.Api.Tracks;

public enum TrackGenerationMode
{
    RoundTrip,
    AToB,
}

public sealed record TrackGenerateRequest(
    double Latitude,
    double Longitude,
    double DistanceKm,
    Profile Profile = Profile.Foot,
    int? Seed = null,
    TrackGenerationMode Mode = TrackGenerationMode.RoundTrip,
    double? EndLatitude = null,
    double? EndLongitude = null,
    bool SplitStages = false,
    double? StageDistanceKm = null,
    double? StageElevationGain = null);
