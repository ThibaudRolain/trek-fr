using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using TrekFr.Infrastructure.Communes;
using TrekFr.Infrastructure.Stages;
using Xunit;

namespace TrekFr.Tests;

public class CommunesTownProviderTests
{
    [Fact]
    public async Task Empty_track_returns_empty_list()
    {
        var provider = new CommunesTownProvider(TestCommuneDataset.Instance);
        var result = await provider.FindAsync([], bufferMeters: 2_000);
        Assert.Empty(result);
    }

    [Fact]
    public async Task Finds_communes_near_a_track_through_Paris()
    {
        var provider = new CommunesTownProvider(TestCommuneDataset.Instance);
        var track = new[]
        {
            new Coordinate(48.85, 2.30),
            new Coordinate(48.86, 2.34),
            new Coordinate(48.87, 2.38),
        };

        var result = await provider.FindAsync(track, bufferMeters: 2_000);

        Assert.NotEmpty(result);
        Assert.All(result, c => Assert.Equal(SleepSpotKind.Town, c.Spot.Kind));
        Assert.All(result, c => Assert.True(c.OffTrackDistanceMeters <= 2_000));
        Assert.All(result, c => Assert.InRange(c.NearestTrackIndex, 0, track.Length - 1));
    }

    [Fact]
    public async Task Buffer_limits_results_to_geographic_proximity()
    {
        var provider = new CommunesTownProvider(TestCommuneDataset.Instance);
        var track = new[]
        {
            new Coordinate(48.85, 2.30),
            new Coordinate(48.86, 2.34),
        };

        var tight = await provider.FindAsync(track, bufferMeters: 200);
        var loose = await provider.FindAsync(track, bufferMeters: 5_000);

        Assert.True(loose.Count >= tight.Count,
            $"Expected ≥ candidates with a looser buffer (tight={tight.Count}, loose={loose.Count}).");
    }

    [Fact]
    public async Task Ocean_track_returns_no_candidates()
    {
        var provider = new CommunesTownProvider(TestCommuneDataset.Instance);
        var track = new[]
        {
            new Coordinate(45.0, -30.0),
            new Coordinate(45.1, -30.0),
        };

        var result = await provider.FindAsync(track, bufferMeters: 5_000);

        Assert.Empty(result);
    }
}

public class CompositeSleepSpotProviderTests
{
    private sealed class StubRefugeProvider(IReadOnlyList<Refuge> refuges) : IRefugeProvider
    {
        public Task<IReadOnlyList<Refuge>> FindNearAsync(
            IReadOnlyList<Coordinate> trackPoints, double bufferMeters, CancellationToken ct = default) =>
            Task.FromResult(refuges);
    }

    [Fact]
    public async Task Merges_refuges_and_town_candidates()
    {
        var refugeLoc = new Coordinate(48.85, 2.30);
        var refuges = new Refuge[]
        {
            new("r1", "Cabane Test", refugeLoc, "refuge", Capacity: null, Url: null),
        };
        var composite = new CompositeSleepSpotProvider(
            new StubRefugeProvider(refuges),
            new CommunesTownProvider(TestCommuneDataset.Instance));

        var track = new[]
        {
            new Coordinate(48.85, 2.30),
            new Coordinate(48.86, 2.34),
        };

        var result = await composite.FindAlongTrackAsync(track, bufferMeters: 2_000);

        Assert.Contains(result, c => c.Spot.Kind == SleepSpotKind.Refuge && c.Spot.Name == "Cabane Test");
        Assert.Contains(result, c => c.Spot.Kind == SleepSpotKind.Town);
    }

    [Fact]
    public async Task Refuge_outside_buffer_is_dropped()
    {
        var faraway = new Coordinate(60.0, 20.0);
        var refuges = new Refuge[]
        {
            new("r1", "Far", faraway, "refuge", null, null),
        };
        var composite = new CompositeSleepSpotProvider(
            new StubRefugeProvider(refuges),
            new CommunesTownProvider(TestCommuneDataset.Instance));

        var track = new[]
        {
            new Coordinate(48.85, 2.30),
            new Coordinate(48.86, 2.34),
        };

        var result = await composite.FindAlongTrackAsync(track, bufferMeters: 2_000);

        Assert.DoesNotContain(result, c => c.Spot.Name == "Far");
    }
}

public class NullRefugeProviderTests
{
    [Fact]
    public async Task Always_returns_empty()
    {
        var provider = new NullRefugeProvider();
        var track = new[] { new Coordinate(48.0, 2.0), new Coordinate(49.0, 3.0) };
        var result = await provider.FindNearAsync(track, bufferMeters: 10_000);
        Assert.Empty(result);
    }
}
