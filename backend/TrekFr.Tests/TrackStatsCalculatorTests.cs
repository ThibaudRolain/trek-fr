using TrekFr.Core.Domain;
using Xunit;

namespace TrekFr.Tests;

public class TrackStatsCalculatorTests
{
    [Fact]
    public void Empty_track_has_zero_stats()
    {
        var track = new Track([], Profile.Foot);
        var stats = TrackStatsCalculator.Compute(track);
        Assert.Equal(0d, stats.DistanceMeters);
        Assert.Equal(0d, stats.ElevationGainMeters);
        Assert.Equal(0d, stats.ElevationLossMeters);
    }

    [Fact]
    public void Single_point_track_has_zero_distance()
    {
        var track = new Track([new Coordinate(48.85, 2.35, 35)], Profile.Foot);
        var stats = TrackStatsCalculator.Compute(track);
        Assert.Equal(0d, stats.DistanceMeters);
    }

    [Fact]
    public void Haversine_Paris_to_Lyon_approx_393_km()
    {
        // Sanity : crow-fly Paris (48.8566, 2.3522) → Lyon (45.7640, 4.8357) ≈ 392 km.
        var track = new Track(
            [
                new Coordinate(48.8566, 2.3522),
                new Coordinate(45.7640, 4.8357),
            ],
            Profile.Foot);
        var stats = TrackStatsCalculator.Compute(track);
        Assert.InRange(stats.DistanceMeters / 1000d, 390d, 395d);
    }

    [Fact]
    public void Elevation_gain_respects_3m_threshold()
    {
        // Three bumps sous le seuil 3 m ne comptent pas ; un gain de 10 m compte.
        var track = new Track(
            [
                new Coordinate(48.0, 2.0, 100),
                new Coordinate(48.0001, 2.0, 101),   // +1 m — ignoré (< 3 m)
                new Coordinate(48.0002, 2.0, 102),   // +1 m cumulé 2 m — ignoré
                new Coordinate(48.0003, 2.0, 110),   // +10 m depuis ref 100 → gain 10
                new Coordinate(48.0004, 2.0, 108),   // -2 m — ignoré
                new Coordinate(48.0005, 2.0, 105),   // -5 m cumulé -5 depuis 110 → loss 5
            ],
            Profile.Foot);
        var stats = TrackStatsCalculator.Compute(track);
        Assert.Equal(10d, stats.ElevationGainMeters);
        Assert.Equal(5d, stats.ElevationLossMeters);
    }

    [Theory]
    [InlineData(Profile.Foot, 5.0, 600)]   // pas : 5 km/h à plat + 1h / 600 m D+
    [InlineData(Profile.Mtb, 15.0, 500)]   // VTT : 15 km/h à plat + 1h / 500 m D+
    [InlineData(Profile.Road, 22.0, 400)]  // route : 22 km/h à plat + 1h / 400 m D+
    public void Flat_10km_matches_profile_base_speed(Profile profile, double kmh, double _)
    {
        // 10 km à plat → 10 / kmh heures.
        var track = new Track(
            [
                new Coordinate(48.0, 2.0, 100),
                new Coordinate(48.0, 2.1344, 100), // ~10 km à la longitude de Paris
            ],
            profile);
        var stats = TrackStatsCalculator.Compute(track);
        var expectedHours = 10d / kmh;
        Assert.InRange(stats.EstimatedDuration.TotalHours, expectedHours * 0.95, expectedHours * 1.05);
    }

    [Fact]
    public void Foot_profile_adds_1h_per_600m_elevation_gain()
    {
        // 1 km à plat (~12 min) + 600 m D+ (~1 h Naismith) = ~1h12.
        var track = new Track(
            [
                new Coordinate(48.0, 2.0, 0),
                new Coordinate(48.0, 2.01344, 600), // ~1 km + 600 m D+
            ],
            Profile.Foot);
        var stats = TrackStatsCalculator.Compute(track);
        Assert.InRange(stats.EstimatedDuration.TotalHours, 1.1, 1.3);
    }
}
