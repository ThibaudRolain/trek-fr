using TrekFr.Core.Domain;
using TrekFr.Infrastructure.Stages;
using Xunit;

namespace TrekFr.Tests;

public class TrackProximityTests
{
    [Fact]
    public void FindNearest_returns_index_of_closest_point()
    {
        var track = new[]
        {
            new Coordinate(48.00, 2.00),
            new Coordinate(48.01, 2.01),
            new Coordinate(48.05, 2.05),
        };
        var (idx, dist) = TrackProximity.FindNearest(track, new Coordinate(48.02, 2.02));
        Assert.Equal(1, idx);
        Assert.True(dist > 0 && dist < 3_000);
    }

    [Fact]
    public void ComputeBBox_spans_min_max_on_both_axes()
    {
        var track = new[]
        {
            new Coordinate(48.0, 2.0),
            new Coordinate(49.0, 3.5),
            new Coordinate(47.5, 2.8),
        };
        var (minLat, maxLat, minLon, maxLon) = TrackProximity.ComputeBBox(track);
        Assert.Equal(47.5, minLat);
        Assert.Equal(49.0, maxLat);
        Assert.Equal(2.0, minLon);
        Assert.Equal(3.5, maxLon);
    }

    [Fact]
    public void Haversine_matches_known_Paris_Lyon_distance()
    {
        // Paris → Lyon ≈ 392 km.
        var d = TrackProximity.Haversine(
            new Coordinate(48.8566, 2.3522),
            new Coordinate(45.7640, 4.8357));
        Assert.InRange(d / 1000d, 390d, 395d);
    }
}
