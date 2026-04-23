using TrekFr.Core.Domain;
using Xunit;

namespace TrekFr.Tests;

public class CommuneDatasetTests
{
    [Fact]
    public void Loads_a_non_empty_dataset()
    {
        Assert.NotEmpty(TestCommuneDataset.Instance.Entries);
        // Sanity : au moins quelques milliers de communes FR.
        Assert.True(TestCommuneDataset.Instance.Entries.Count > 5_000,
            $"Expected > 5 000 communes, got {TestCommuneDataset.Instance.Entries.Count}. Run BuildCommunes ?");
    }

    [Fact]
    public void FindNearest_returns_a_nearby_commune_for_coords_in_paris()
    {
        var start = new Coordinate(48.8566, 2.3522);
        var result = TestCommuneDataset.Instance.FindNearest(start);
        Assert.NotNull(result);
        // Le dataset peut contenir plusieurs quartiers/arrondissements autour de Paris —
        // on vérifie seulement que le FindNearest rend une commune à < 5 km.
        var dx = (result.Location.Latitude - start.Latitude) * 111_000d;
        var dy = (result.Location.Longitude - start.Longitude) * 111_000d * System.Math.Cos(start.Latitude * System.Math.PI / 180d);
        var approxMeters = System.Math.Sqrt(dx * dx + dy * dy);
        Assert.True(approxMeters < 5_000, $"Nearest was {result.Name} at ~{approxMeters:F0} m — expected <5 km.");
    }

    [Fact]
    public void FindNearest_respects_max_distance_km()
    {
        // Middle of Atlantic Ocean → aucune commune FR dans un rayon de 50 km.
        var result = TestCommuneDataset.Instance.FindNearest(new Coordinate(45.0, -30.0), maxDistanceKm: 50);
        Assert.Null(result);
    }

    [Fact]
    public void FindNearest_returns_null_when_beyond_cap()
    {
        // Himalaya → loin de toute commune FR.
        var result = TestCommuneDataset.Instance.FindNearest(new Coordinate(28.0, 85.0), maxDistanceKm: 10);
        Assert.Null(result);
    }

}
