namespace TrekFr.Core.Domain;

public sealed record MhPoi(
    string CommuneName,
    int MonumentCount,
    Coordinate Location,
    double DistanceFromStartMeters,
    double DistanceFromTrackMeters);
