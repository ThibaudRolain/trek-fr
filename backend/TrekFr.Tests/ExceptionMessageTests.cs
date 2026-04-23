using TrekFr.Core.Domain;
using TrekFr.Core.UseCases;
using Xunit;

namespace TrekFr.Tests;

public class ExceptionMessageTests
{
    [Fact]
    public void DistanceMismatch_short_loop_suggests_using_best_found()
    {
        // best 50 km for target 20 km → ratio 2.5, message should include the "**50 km**" suggestion.
        var ex = new DistanceMismatchException(targetMeters: 20_000, bestActualMeters: 50_000, attempts: 10);
        Assert.Contains("20 km", ex.Message);
        Assert.Contains("**50 km**", ex.Message);
        Assert.Contains("10 essais", ex.Message);
    }

    [Fact]
    public void DistanceMismatch_close_to_target_falls_back_to_generic_suggestion()
    {
        // best 18 km for target 20 km → ratio 0.9, hits the else branch.
        var ex = new DistanceMismatchException(targetMeters: 20_000, bestActualMeters: 18_000, attempts: 10);
        Assert.Contains("Essaie une distance un peu différente", ex.Message);
        Assert.DoesNotContain("**", ex.Message); // no bold km suggestion in this branch
    }

    [Fact]
    public void DistanceMismatch_exposes_input_fields()
    {
        var ex = new DistanceMismatchException(targetMeters: 20_000, bestActualMeters: 50_000, attempts: 10);
        Assert.Equal(20_000d, ex.TargetMeters);
        Assert.Equal(50_000d, ex.BestActualMeters);
        Assert.Equal(10, ex.Attempts);
    }

    [Fact]
    public void ElevationOutOfRange_message_includes_filter_describe_and_context()
    {
        var filter = new ElevationFilter(300, 700);
        var ex = new ElevationOutOfRangeException(filter, "un round-trip à cette distance");
        Assert.Contains("entre 300 et 700 m D+", ex.Message);
        Assert.Contains("un round-trip à cette distance", ex.Message);
        Assert.Same(filter, ex.Filter);
        Assert.Equal("un round-trip à cette distance", ex.Context);
    }
}
