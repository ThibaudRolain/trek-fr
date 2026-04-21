using System.IO;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class ImportGpxTrack(IGpxParser parser)
{
    public ImportedTrack Execute(Stream gpxStream, Profile profile)
    {
        var track = parser.Parse(gpxStream, profile);
        var stats = TrackStatsCalculator.Compute(track);
        return new ImportedTrack(track, stats);
    }
}

public sealed record ImportedTrack(Track Track, TrackStats Stats);
