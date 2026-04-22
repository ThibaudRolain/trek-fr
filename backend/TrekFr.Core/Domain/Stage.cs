using System.Collections.Generic;

namespace TrekFr.Core.Domain;

public sealed record Stage(
    int Index,
    IReadOnlyList<Coordinate> Points,
    TrackStats Stats,
    SleepSpot EndSleepSpot,
    double? OffTrackDistanceMeters);

public sealed record SleepSpot(
    string Name,
    Coordinate Location,
    SleepSpotKind Kind);

public enum SleepSpotKind
{
    Refuge,
    Town,
    Arrival,
}
