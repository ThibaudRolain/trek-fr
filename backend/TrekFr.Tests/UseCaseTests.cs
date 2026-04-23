using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using TrekFr.Core.UseCases;
using Xunit;

namespace TrekFr.Tests;

public class UseCaseTests
{
    private sealed class FakeRouter : IRoutingProvider
    {
        public Coordinate? LastStart { get; private set; }
        public Coordinate? LastFrom { get; private set; }
        public Coordinate? LastTo { get; private set; }
        public double? LastDistance { get; private set; }
        public int? LastSeed { get; private set; }
        public Profile? LastProfile { get; private set; }

        public Func<Track>? RoundTripTrack { get; set; }
        public Func<Track>? PointToPointTrack { get; set; }

        public Task<(Track Track, TrackExtras? Extras)> GenerateRoundTripAsync(
            Coordinate start, double targetDistanceMeters, Profile profile, int? seed = null, CancellationToken ct = default)
        {
            LastStart = start;
            LastDistance = targetDistanceMeters;
            LastSeed = seed;
            LastProfile = profile;
            var track = RoundTripTrack?.Invoke() ?? DefaultRoundTrip(start, targetDistanceMeters, profile);
            return Task.FromResult<(Track, TrackExtras?)>((track, null));
        }

        private static Track DefaultRoundTrip(Coordinate start, double distanceMeters, Profile p) => new(
            new List<Coordinate>
            {
                start,
                new(start.Latitude + distanceMeters / 222_222d, start.Longitude, 100),
                start,
            },
            p);

        public Task<(Track Track, TrackExtras? Extras)> RouteAsync(Coordinate from, Coordinate to, Profile profile, CancellationToken ct = default)
        {
            LastFrom = from;
            LastTo = to;
            LastProfile = profile;
            return Task.FromResult<(Track, TrackExtras?)>((PointToPointTrack?.Invoke() ?? DefaultTrack(profile), null));
        }

        private static Track DefaultTrack(Profile p) => new(
            new List<Coordinate>
            {
                new(48.85, 2.35, 100),
                new(48.86, 2.36, 110),
            },
            p);
    }

    private sealed class FakeProposer(ProposedDestination? destination) : IDestinationProposer
    {
        public Coordinate? LastStart { get; private set; }
        public double? LastDistance { get; private set; }
        public int? LastSeed { get; private set; }

        public Task<ProposedDestination?> ProposeAsync(
            Coordinate start, double targetDistanceMeters, Profile profile, int? seed, CancellationToken ct = default)
        {
            LastStart = start;
            LastDistance = targetDistanceMeters;
            LastSeed = seed;
            return Task.FromResult<ProposedDestination?>(destination);
        }

        public Task<IReadOnlyList<ProposedDestination>> GetTopCandidatesAsync(
            Coordinate start, double targetDistanceMeters, Profile profile, int topN, CancellationToken ct = default) =>
            Task.FromResult<IReadOnlyList<ProposedDestination>>(
                destination is null ? [] : [destination]);
    }

    // ---- RouteAToB ----

    [Fact]
    public async Task RouteAToB_calls_router_and_computes_stats()
    {
        var router = new FakeRouter();
        var useCase = new RouteAToB(router);

        var result = await useCase.ExecuteAsync(
            new Coordinate(48.85, 2.35), new Coordinate(45.76, 4.84), Profile.Foot);

        Assert.Equal(new Coordinate(48.85, 2.35), router.LastFrom);
        Assert.Equal(new Coordinate(45.76, 4.84), router.LastTo);
        Assert.Equal(Profile.Foot, router.LastProfile);
        Assert.NotNull(result.Stats);
        Assert.True(result.Stats.DistanceMeters > 0);
    }

    // ---- GenerateRoundTrip ----

    [Fact]
    public async Task GenerateRoundTrip_forwards_seed_and_distance_to_router()
    {
        var router = new FakeRouter();
        var useCase = new GenerateRoundTrip(router);

        await useCase.ExecuteAsync(new Coordinate(48.85, 2.35), 12_500d, Profile.Mtb, seed: 7);

        Assert.Equal(new Coordinate(48.85, 2.35), router.LastStart);
        Assert.Equal(12_500d, router.LastDistance);
        Assert.Equal(7, router.LastSeed);
        Assert.Equal(Profile.Mtb, router.LastProfile);
    }

    [Fact]
    public async Task GenerateRoundTrip_returns_stats_from_the_returned_track()
    {
        var router = new FakeRouter
        {
            RoundTripTrack = () => new Track(
                new List<Coordinate>
                {
                    new(48.0, 2.0, 100),
                    new(48.0, 2.1344, 100), // ~10 km plat
                    new(48.0, 2.0, 100),    // retour
                },
                Profile.Foot),
        };
        var useCase = new GenerateRoundTrip(router);

        var result = await useCase.ExecuteAsync(new Coordinate(48.0, 2.0), 20_000d, Profile.Foot, seed: null);

        Assert.InRange(result.Stats.DistanceMeters / 1000d, 19d, 21d);
        Assert.Equal(0d, result.Stats.ElevationGainMeters);
    }

    // ---- GenerateVariantsAsync ----

    [Fact]
    public async Task GenerateVariantsAsync_returns_three_variants_with_distinct_seeds()
    {
        var router = new FakeRouter();
        var useCase = new GenerateRoundTrip(router);

        var variants = await useCase.GenerateVariantsAsync(
            new Coordinate(48.85, 2.35), 20_000d, Profile.Foot);

        Assert.Equal(3, variants.Count);
        Assert.Equal(3, variants.Select(v => v.Seed).Distinct().Count());
    }

    [Fact]
    public async Task GenerateVariantsAsync_sorted_by_proximity_to_target()
    {
        var callIndex = 0;
        // call 0 → 30 km (50 % overshoot), calls 1–2 → 18 km and 22 km
        var distances = new[] { 30_000d, 18_000d, 22_000d };
        var router = new FakeRouter
        {
            RoundTripTrack = () =>
            {
                var d = distances[Math.Min(callIndex, distances.Length - 1)];
                callIndex++;
                return TestTracks.OutAndBack(new Coordinate(48.85, 2.35), d, Profile.Foot);
            },
        };
        var useCase = new GenerateRoundTrip(router);
        const double target = 20_000d;

        var variants = await useCase.GenerateVariantsAsync(
            new Coordinate(48.85, 2.35), target, Profile.Foot);

        Assert.Equal(3, variants.Count);
        Assert.True(
            Math.Abs(variants[0].Stats.DistanceMeters - target) <=
            Math.Abs(variants[2].Stats.DistanceMeters - target),
            "First variant must be at least as close to target as the last variant.");
    }

    // ---- ProposeDestination ----

    [Fact]
    public async Task ProposeDestination_happy_path_returns_track_stats_and_destination()
    {
        var destination = new ProposedDestination("Vézelay", new Coordinate(47.47, 3.74), 420);
        var proposer = new FakeProposer(destination);
        var router = new FakeRouter();
        var useCase = new ProposeDestination(proposer, router);

        var result = await useCase.ExecuteAsync(
            new Coordinate(47.0, 3.7), 30_000d, Profile.Foot, seed: 3);

        Assert.Equal(destination, result.Destination);
        Assert.True(result.Stats.DistanceMeters > 0);
        // Le routeur doit être appelé de start → destination (pas l'inverse).
        Assert.Equal(new Coordinate(47.0, 3.7), router.LastFrom);
        Assert.Equal(destination.Location, router.LastTo);
        Assert.Equal(3, proposer.LastSeed);
        Assert.Equal(30_000d, proposer.LastDistance);
    }

    [Fact]
    public async Task ProposeDestination_throws_when_no_candidate()
    {
        var proposer = new FakeProposer(destination: null);
        var router = new FakeRouter();
        var useCase = new ProposeDestination(proposer, router);

        await Assert.ThrowsAsync<NoDestinationCandidateException>(() =>
            useCase.ExecuteAsync(new Coordinate(45.0, -30.0), 5_000d, Profile.Foot, seed: null));
    }
}
