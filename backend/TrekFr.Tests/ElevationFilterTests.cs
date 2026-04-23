using TrekFr.Core.Domain;
using Xunit;

namespace TrekFr.Tests;

public class ElevationFilterTests
{
    [Fact]
    public void IsActive_false_when_both_bounds_null()
    {
        Assert.False(new ElevationFilter(null, null).IsActive);
    }

    [Theory]
    [InlineData(100d, null)]
    [InlineData(null, 500d)]
    [InlineData(100d, 500d)]
    public void IsActive_true_when_any_bound_set(object? min, object? max)
    {
        Assert.True(new ElevationFilter((double?)min, (double?)max).IsActive);
    }

    [Fact]
    public void Matches_always_true_when_no_bounds()
    {
        var f = new ElevationFilter(null, null);
        Assert.True(f.Matches(0));
        Assert.True(f.Matches(9_999));
    }

    [Theory]
    // min 500 → accepts down to 500 * 0.85 = 425
    [InlineData(500d, null, 425d, true)]
    [InlineData(500d, null, 424d, false)]
    [InlineData(500d, null, 500d, true)]
    [InlineData(500d, null, 10_000d, true)]
    // max 500 → accepts up to 500 * 1.15 = 575
    [InlineData(null, 500d, 575d, true)]
    [InlineData(null, 500d, 576d, false)]
    [InlineData(null, 500d, 0d, true)]
    // range [300, 700] with ±15 % → ~[255, 805] (boundaries are FP-sensitive, so values
    // stay a hair inside / outside to stay deterministic).
    [InlineData(300d, 700d, 254d, false)]
    [InlineData(300d, 700d, 260d, true)]
    [InlineData(300d, 700d, 500d, true)]
    [InlineData(300d, 700d, 800d, true)]
    [InlineData(300d, 700d, 810d, false)]
    public void Matches_respects_15_percent_tolerance(object? min, object? max, double gain, bool expected)
    {
        Assert.Equal(expected, new ElevationFilter((double?)min, (double?)max).Matches(gain));
    }

    [Fact]
    public void Describe_no_filter()
    {
        Assert.Equal("aucun filtre D+", new ElevationFilter(null, null).Describe());
    }

    [Fact]
    public void Describe_min_only()
    {
        Assert.Equal("au moins 300 m D+ (±15 %)", new ElevationFilter(300, null).Describe());
    }

    [Fact]
    public void Describe_max_only()
    {
        Assert.Equal("au plus 700 m D+ (±15 %)", new ElevationFilter(null, 700).Describe());
    }

    [Fact]
    public void Describe_range()
    {
        Assert.Equal("entre 300 et 700 m D+ (±15 %)", new ElevationFilter(300, 700).Describe());
    }
}
