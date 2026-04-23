using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using TrekFr.Infrastructure.Stages;

namespace TrekFr.Infrastructure.Communes;

/// <summary>
/// Retourne les communes traversées par la trace qui possèdent des monuments historiques
/// (données Mérimée, agrégées par commune dans communes-fr.json).
/// </summary>
public sealed class MhPoiProvider(CommuneDataset dataset) : IMhPoiProvider
{
    private const double BufferMeters = 2_000d;
    private const int MaxResults = 20;
    private const double MetersPerDegLat = 111_000d;

    public Task<IReadOnlyList<MhPoi>> FindAlongTrackAsync(
        IReadOnlyList<Coordinate> trackPoints,
        CancellationToken ct = default)
    {
        if (trackPoints.Count == 0)
            return Task.FromResult<IReadOnlyList<MhPoi>>([]);

        var (minLat, maxLat, minLon, maxLon) = TrackProximity.ComputeBBox(trackPoints);
        var midLatRad = (minLat + maxLat) / 2d * Math.PI / 180d;
        var dLat = BufferMeters / MetersPerDegLat;
        var dLon = BufferMeters / (MetersPerDegLat * Math.Max(Math.Cos(midLatRad), 0.1));

        var bboxMinLat = minLat - dLat;
        var bboxMaxLat = maxLat + dLat;
        var bboxMinLon = minLon - dLon;
        var bboxMaxLon = maxLon + dLon;

        var start = trackPoints[0];
        var results = new List<MhPoi>();

        foreach (var entry in dataset.Entries)
        {
            if ((entry.MonumentsHistoriques ?? 0) <= 0) continue;
            if (entry.Lat < bboxMinLat || entry.Lat > bboxMaxLat) continue;
            if (entry.Lon < bboxMinLon || entry.Lon > bboxMaxLon) continue;

            var location = new Coordinate(entry.Lat, entry.Lon);
            var (_, distFromTrack) = TrackProximity.FindNearest(trackPoints, location);
            if (distFromTrack > BufferMeters) continue;

            var distFromStart = TrackProximity.Haversine(start, location);
            results.Add(new MhPoi(entry.Name, entry.MonumentsHistoriques ?? 0, location, distFromStart, distFromTrack));
        }

        var sorted = results
            .OrderBy(p => p.DistanceFromStartMeters)
            .Take(MaxResults)
            .ToList();

        return Task.FromResult<IReadOnlyList<MhPoi>>(sorted);
    }
}
