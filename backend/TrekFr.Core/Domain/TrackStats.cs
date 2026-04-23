using System;
using System.Collections.Generic;

namespace TrekFr.Core.Domain;

public sealed record TrackStats(
    double DistanceMeters,
    double ElevationGainMeters,
    double ElevationLossMeters,
    TimeSpan EstimatedDuration,
    IReadOnlyList<SurfaceEntry>? Surface = null,
    IReadOnlyList<WayTypeEntry>? WayTypes = null);

public sealed record SurfaceEntry(int TypeId, double Amount, double Distance);

public sealed record WayTypeEntry(int TypeId, double Amount, double Distance);

public sealed record TrackExtras(
    IReadOnlyList<SurfaceEntry>? Surface,
    IReadOnlyList<WayTypeEntry>? WayTypes);
