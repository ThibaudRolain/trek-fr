using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using TrekFr.Infrastructure.Destinations;

namespace TrekFr.Infrastructure.Stages;

/// <summary>
/// Source "Town" pour le découpage en étapes : les communes du dataset bundled (pop ≥ 200)
/// dont le point le plus proche de la trace est à ≤ bufferMeters.
/// </summary>
public sealed class CommunesTownProvider(CommunesDataset dataset)
{
    private const double MetersPerDegreeLatitude = 111_000d;

    public Task<IReadOnlyList<SleepSpotCandidate>> FindAsync(
        IReadOnlyList<Coordinate> trackPoints,
        double bufferMeters,
        CancellationToken ct = default)
    {
        if (trackPoints.Count == 0)
            return Task.FromResult<IReadOnlyList<SleepSpotCandidate>>([]);

        var (minLat, maxLat, minLon, maxLon) = TrackProximity.ComputeBBox(trackPoints);
        var midLatRad = (minLat + maxLat) / 2d * Math.PI / 180d;
        var dLat = bufferMeters / MetersPerDegreeLatitude;
        var dLon = bufferMeters / (MetersPerDegreeLatitude * Math.Max(Math.Cos(midLatRad), 0.1));

        var bboxMinLat = minLat - dLat;
        var bboxMaxLat = maxLat + dLat;
        var bboxMinLon = minLon - dLon;
        var bboxMaxLon = maxLon + dLon;

        var result = new List<SleepSpotCandidate>();
        foreach (var c in dataset.Communes)
        {
            if (c.Lat < bboxMinLat || c.Lat > bboxMaxLat) continue;
            if (c.Lon < bboxMinLon || c.Lon > bboxMaxLon) continue;

            var location = new Coordinate(c.Lat, c.Lon);
            var (idx, dist) = TrackProximity.FindNearest(trackPoints, location);
            if (dist > bufferMeters) continue;

            var spot = new SleepSpot(c.Name, location, SleepSpotKind.Town);
            result.Add(new SleepSpotCandidate(spot, idx, dist, c.Score));
        }
        return Task.FromResult<IReadOnlyList<SleepSpotCandidate>>(result);
    }
}
