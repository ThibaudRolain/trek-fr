using System;

namespace TrekFr.Core.Domain;

public sealed record TrackStats(
    double DistanceMeters,
    double ElevationGainMeters,
    double ElevationLossMeters,
    TimeSpan EstimatedDuration);
