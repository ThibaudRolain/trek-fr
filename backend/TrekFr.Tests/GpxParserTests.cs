using System.IO;
using System.Text;
using TrekFr.Core.Domain;
using TrekFr.Infrastructure.Gpx;
using Xunit;

namespace TrekFr.Tests;

public class GpxParserTests
{
    private static Stream StreamOf(string xml) => new MemoryStream(Encoding.UTF8.GetBytes(xml));

    [Fact]
    public void Parses_basic_trkpt_coordinates()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk>
                <name>Test</name>
                <trkseg>
                  <trkpt lat="48.85" lon="2.35"><ele>35.5</ele></trkpt>
                  <trkpt lat="48.86" lon="2.36"><ele>40.0</ele></trkpt>
                </trkseg>
              </trk>
            </gpx>
            """;
        var parser = new GpxParser();
        var track = parser.Parse(StreamOf(xml), Profile.Foot);

        Assert.Equal("Test", track.Name);
        Assert.Equal(Profile.Foot, track.Profile);
        Assert.Equal(2, track.Points.Count);
        Assert.Equal(48.85, track.Points[0].Latitude);
        Assert.Equal(2.35, track.Points[0].Longitude);
        Assert.Equal(35.5, track.Points[0].Elevation);
    }

    [Fact]
    public void Handles_trkpt_without_elevation()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg>
                <trkpt lat="48.85" lon="2.35"/>
              </trkseg></trk>
            </gpx>
            """;
        var parser = new GpxParser();
        var track = parser.Parse(StreamOf(xml), Profile.Foot);

        Assert.Single(track.Points);
        Assert.Null(track.Points[0].Elevation);
    }

    [Fact]
    public void Skips_trkpt_with_missing_lat_or_lon()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg>
                <trkpt lat="48.85"/>
                <trkpt lon="2.35"/>
                <trkpt lat="abc" lon="2.35"/>
                <trkpt lat="48.85" lon="2.35"><ele>35</ele></trkpt>
              </trkseg></trk>
            </gpx>
            """;
        var parser = new GpxParser();
        var track = parser.Parse(StreamOf(xml), Profile.Mtb);

        Assert.Single(track.Points);
        Assert.Equal(Profile.Mtb, track.Profile);
    }

    [Fact]
    public void Returns_null_name_when_absent_or_whitespace()
    {
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg>
                <trkpt lat="48.85" lon="2.35"/>
              </trkseg></trk>
            </gpx>
            """;
        var parser = new GpxParser();
        var track = parser.Parse(StreamOf(xml), Profile.Foot);
        Assert.Null(track.Name);
    }

    [Fact]
    public void Parses_comma_or_dot_decimals_via_invariant_culture()
    {
        // GPX utilise toujours le point ; on vérifie qu'une culture système à virgule ne casse rien.
        const string xml = """
            <?xml version="1.0" encoding="UTF-8"?>
            <gpx version="1.1" xmlns="http://www.topografix.com/GPX/1/1">
              <trk><trkseg>
                <trkpt lat="48.123456" lon="2.987654"><ele>100.5</ele></trkpt>
              </trkseg></trk>
            </gpx>
            """;
        var parser = new GpxParser();
        var track = parser.Parse(StreamOf(xml), Profile.Foot);
        Assert.Equal(48.123456, track.Points[0].Latitude);
        Assert.Equal(2.987654, track.Points[0].Longitude);
        Assert.Equal(100.5, track.Points[0].Elevation);
    }
}
