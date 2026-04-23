using TrekFr.Core.Domain;
using TrekFr.Infrastructure.Communes;
using TrekFr.Infrastructure.Destinations;
using Xunit;

namespace TrekFr.Tests;

public class CommunesDestinationProposerTests
{
    private static readonly CommunesDestinationProposer Proposer = new(TestCommuneDataset.Instance);

    private static double CrowFlyMeters(double lat1, double lon1, double lat2, double lon2)
    {
        const double r = 6_371_000d;
        static double Rad(double d) => d * System.Math.PI / 180d;
        var dLat = Rad(lat2 - lat1);
        var dLon = Rad(lon2 - lon1);
        var a = System.Math.Sin(dLat / 2) * System.Math.Sin(dLat / 2)
              + System.Math.Cos(Rad(lat1)) * System.Math.Cos(Rad(lat2))
              * System.Math.Sin(dLon / 2) * System.Math.Sin(dLon / 2);
        return 2 * r * System.Math.Asin(System.Math.Min(1, System.Math.Sqrt(a)));
    }

    [Fact]
    public async Task Proposes_something_within_tolerance_of_target_distance()
    {
        // Départ Lyon, cible 15 km → on devrait tomber sur une commune à ~15 km (±10 %).
        var start = new Coordinate(45.7640, 4.8357);
        var result = await Proposer.ProposeAsync(start, 15_000d, Profile.Foot, seed: 1);

        Assert.NotNull(result);
        var crowFlyKm = CrowFlyMeters(
            start.Latitude, start.Longitude,
            result.Location.Latitude, result.Location.Longitude) / 1000d;
        Assert.InRange(crowFlyKm, 13.5, 16.5);
    }

    [Fact]
    public async Task Seeded_pick_is_reproducible()
    {
        var start = new Coordinate(45.7640, 4.8357);
        var a = await Proposer.ProposeAsync(start, 15_000d, Profile.Foot, seed: 42);
        var b = await Proposer.ProposeAsync(start, 15_000d, Profile.Foot, seed: 42);

        Assert.NotNull(a);
        Assert.NotNull(b);
        Assert.Equal(a.Name, b.Name);
        Assert.Equal(a.Location.Latitude, b.Location.Latitude);
    }

    [Fact]
    public async Task Returns_null_when_no_candidate_in_tolerance_band()
    {
        // Atlantic Ocean au large des côtes françaises → rien dans [4.5 ; 5.5] km.
        var start = new Coordinate(45.0, -30.0);
        var result = await Proposer.ProposeAsync(start, 5_000d, Profile.Foot, seed: 1);
        Assert.Null(result);
    }

    [Fact]
    public async Task Different_seeds_can_pick_different_candidates()
    {
        // Lyon à 15 km : il y a plein de candidats, on devrait voir des différences.
        var start = new Coordinate(45.7640, 4.8357);
        var seen = new HashSet<string>();
        for (int seed = 0; seed < 20; seed++)
        {
            var result = await Proposer.ProposeAsync(start, 15_000d, Profile.Foot, seed);
            if (result is not null) seen.Add(result.Name);
        }
        // On attend au moins 2 noms distincts sur 20 seeds — le top 5 a normalement au
        // moins 2-3 candidats dans cette zone. Sinon, le dataset est suspicieux.
        Assert.True(seen.Count >= 2, $"Only {seen.Count} distinct candidates — dataset suspect ?");
    }
}
