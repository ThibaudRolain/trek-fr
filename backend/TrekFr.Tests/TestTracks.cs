using TrekFr.Core.Domain;

namespace TrekFr.Tests;

internal static class TestTracks
{
    /// <summary>
    /// Out-and-back de <paramref name="targetDistanceMeters"/> total : 3 points, aller-retour
    /// sur la latitude (≈ 111 km / deg). Utilisé par les fakes de routing pour satisfaire la
    /// tolérance ±20 % de GenerateRoundTrip indépendamment de la cible demandée.
    /// </summary>
    public static Track OutAndBack(Coordinate start, double targetDistanceMeters, Profile profile)
    {
        var dLat = (targetDistanceMeters / 2d) / 111_000d;
        return new Track(
            [start, new Coordinate(start.Latitude + dLat, start.Longitude, 100), start],
            profile);
    }
}
