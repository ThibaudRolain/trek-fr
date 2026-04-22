using System.IO;
using System.Text;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;
using TrekFr.Core.UseCases;
using TrekFr.Infrastructure.Gpx;
using Xunit;

namespace TrekFr.Tests;

public class ImportGpxTrackTests
{
    private sealed class FakeGpxParser(Track track) : IGpxParser
    {
        public Stream? LastStream { get; private set; }
        public Profile? LastProfile { get; private set; }

        public Track Parse(Stream gpxStream, Profile profile)
        {
            LastStream = gpxStream;
            LastProfile = profile;
            return track;
        }
    }

    [Fact]
    public void Forwards_stream_and_profile_to_parser()
    {
        var track = new Track([new Coordinate(48.85, 2.35)], Profile.Road);
        var parser = new FakeGpxParser(track);
        var useCase = new ImportGpxTrack(parser);
        using var stream = new MemoryStream();

        useCase.Execute(stream, Profile.Road);

        Assert.Same(stream, parser.LastStream);
        Assert.Equal(Profile.Road, parser.LastProfile);
    }

    [Fact]
    public void Returns_imported_track_with_computed_stats()
    {
        var track = new Track(
            [new Coordinate(48.0, 2.0, 100), new Coordinate(48.0, 2.1344, 100)],
            Profile.Foot);
        var useCase = new ImportGpxTrack(new FakeGpxParser(track));
        using var stream = new MemoryStream();

        var result = useCase.Execute(stream, Profile.Foot);

        Assert.Same(track, result.Track);
        Assert.InRange(result.Stats.DistanceMeters / 1000d, 9.5, 10.5);
    }

    [Fact]
    public void Integrates_with_real_GpxParser()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><name>Integration</name><trkseg>
                <trkpt lat="48.85" lon="2.35"><ele>35</ele></trkpt>
                <trkpt lat="48.86" lon="2.36"><ele>50</ele></trkpt>
              </trkseg></trk>
            </gpx>
            """;
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(xml));
        var useCase = new ImportGpxTrack(new GpxParser());

        var result = useCase.Execute(stream, Profile.Foot);

        Assert.Equal("Integration", result.Track.Name);
        Assert.Equal(2, result.Track.Points.Count);
        Assert.True(result.Stats.DistanceMeters > 0);
    }
}
