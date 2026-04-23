using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using TrekFr.Core.UseCases;
using Xunit;

namespace TrekFr.Tests;

public class SplitIntoStagesTests
{
    private sealed class StubProvider(IReadOnlyList<SleepSpotCandidate> candidates) : ISleepSpotProvider
    {
        public Task<IReadOnlyList<SleepSpotCandidate>> FindAlongTrackAsync(
            IReadOnlyList<Coordinate> trackPoints, double bufferMeters, CancellationToken ct = default) =>
            Task.FromResult(candidates);
    }

    // Flat line at latitude 48, one point every `kmBetween` km.
    private static Track FlatLineTrack(int points, double kmBetween)
    {
        var lonStep = kmBetween / (111d * System.Math.Cos(48d * System.Math.PI / 180d));
        var list = new List<Coordinate>(points);
        for (var i = 0; i < points; i++)
        {
            list.Add(new Coordinate(48.0, 2.0 + i * lonStep, 100));
        }
        return new Track(list, Profile.Foot);
    }

    // ~35 km, 8 points at 5 km steps — juste au-delà de la limite 25 km/jour utilisée dans
    // les tests de split. Memoïsé pour éviter la reconstruction à chaque Fact.
    private static readonly Track Track35km = FlatLineTrack(points: 8, kmBetween: 5);

    [Fact]
    public async Task Short_track_below_limits_returns_single_arrival_stage()
    {
        var track = FlatLineTrack(points: 5, kmBetween: 2); // ~8 km, flat
        var useCase = new SplitIntoStages(new StubProvider([]));
        var opts = new StageOptions(
            MaxDistancePerDayMeters: 30_000,
            MaxElevationGainPerDay: 500,
            ArrivalName: "Final");

        var stages = await useCase.ExecuteAsync(track, opts);

        Assert.Single(stages);
        Assert.Equal(1, stages[0].Index);
        Assert.Equal("Final", stages[0].EndSleepSpot.Name);
        Assert.Equal(SleepSpotKind.Arrival, stages[0].EndSleepSpot.Kind);
        Assert.Null(stages[0].OffTrackDistanceMeters);
    }

    [Fact]
    public async Task Splits_long_track_at_town_candidate_near_pivot()
    {
        // ~35 km → split mid-way: stage 1 ≈ 20 km, stage 2 ≈ 15 km (final arrival).
        var track = Track35km;
        var candidate = new SleepSpotCandidate(
            Spot: new SleepSpot("MidTown", track.Points[4], SleepSpotKind.Town),
            NearestTrackIndex: 4,
            OffTrackDistanceMeters: 300,
            PatrimonyScore: 100);
        var useCase = new SplitIntoStages(new StubProvider([candidate]));
        var opts = new StageOptions(
            MaxDistancePerDayMeters: 25_000,
            MaxElevationGainPerDay: 10_000);

        var stages = await useCase.ExecuteAsync(track, opts);

        Assert.Equal(2, stages.Count);
        Assert.Equal("MidTown", stages[0].EndSleepSpot.Name);
        Assert.Equal(300d, stages[0].OffTrackDistanceMeters);
        Assert.Equal(SleepSpotKind.Arrival, stages[1].EndSleepSpot.Kind);
    }

    [Fact]
    public async Task Refuge_beats_town_even_with_lower_patrimony_score()
    {
        var track = Track35km;
        var town = new SleepSpotCandidate(
            new SleepSpot("Town", track.Points[4], SleepSpotKind.Town),
            NearestTrackIndex: 4, OffTrackDistanceMeters: 100, PatrimonyScore: 500);
        var refuge = new SleepSpotCandidate(
            new SleepSpot("Refuge", track.Points[4], SleepSpotKind.Refuge),
            NearestTrackIndex: 4, OffTrackDistanceMeters: 100, PatrimonyScore: 0);
        var useCase = new SplitIntoStages(new StubProvider([town, refuge]));
        var opts = new StageOptions(
            MaxDistancePerDayMeters: 25_000,
            MaxElevationGainPerDay: 10_000);

        var stages = await useCase.ExecuteAsync(track, opts);

        Assert.Equal("Refuge", stages[0].EndSleepSpot.Name);
    }

    [Fact]
    public async Task Throws_NoStageSleepSpot_when_no_candidate_in_window()
    {
        var track = Track35km;
        // Candidate hors fenêtre (index 7 = ~35 km, pivot cherche autour de 25 km).
        var faraway = new SleepSpotCandidate(
            new SleepSpot("Far", track.Points[7], SleepSpotKind.Town),
            NearestTrackIndex: 7, OffTrackDistanceMeters: 100, PatrimonyScore: 100);
        var useCase = new SplitIntoStages(new StubProvider([faraway]));
        var opts = new StageOptions(
            MaxDistancePerDayMeters: 25_000,
            MaxElevationGainPerDay: 10_000,
            WindowTolerance: 0.10);

        var ex = await Assert.ThrowsAsync<NoStageSleepSpotException>(() =>
            useCase.ExecuteAsync(track, opts));

        Assert.Equal(1, ex.StageIndex);
        Assert.True(ex.ApproxKmFromStart > 0);
    }

    [Fact]
    public async Task Throws_when_track_has_too_few_points()
    {
        var track = new Track([new Coordinate(48.0, 2.0)], Profile.Foot);
        var useCase = new SplitIntoStages(new StubProvider([]));
        var opts = new StageOptions(MaxDistancePerDayMeters: 10_000, MaxElevationGainPerDay: 500);

        await Assert.ThrowsAsync<System.ArgumentException>(() => useCase.ExecuteAsync(track, opts));
    }

    [Theory]
    [InlineData(0, 500)]
    [InlineData(10_000, 0)]
    public async Task Throws_on_non_positive_options(double maxDistance, double maxGain)
    {
        var track = FlatLineTrack(points: 3, kmBetween: 1);
        var useCase = new SplitIntoStages(new StubProvider([]));
        var opts = new StageOptions(
            MaxDistancePerDayMeters: maxDistance,
            MaxElevationGainPerDay: maxGain);

        await Assert.ThrowsAsync<System.ArgumentException>(() => useCase.ExecuteAsync(track, opts));
    }
}
