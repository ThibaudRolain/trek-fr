using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Xml.Linq;
using TrekFr.Core.Abstractions;
using TrekFr.Core.Domain;

namespace TrekFr.Infrastructure.Gpx;

public sealed class GpxParser : IGpxParser
{
    public Track Parse(Stream gpxStream, Profile profile)
    {
        var doc = XDocument.Load(gpxStream);
        var name = doc.Descendants()
            .FirstOrDefault(e => e.Name.LocalName == "trk")
            ?.Elements()
            .FirstOrDefault(e => e.Name.LocalName == "name")
            ?.Value;

        var points = new List<Coordinate>();
        foreach (var trkpt in doc.Descendants().Where(e => e.Name.LocalName == "trkpt"))
        {
            var latAttr = trkpt.Attribute("lat");
            var lonAttr = trkpt.Attribute("lon");
            if (latAttr is null || lonAttr is null) continue;
            if (!double.TryParse(latAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lat)) continue;
            if (!double.TryParse(lonAttr.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var lon)) continue;

            double? elevation = null;
            var eleEl = trkpt.Elements().FirstOrDefault(e => e.Name.LocalName == "ele");
            if (eleEl is not null &&
                double.TryParse(eleEl.Value, NumberStyles.Float, CultureInfo.InvariantCulture, out var ele))
            {
                elevation = ele;
            }

            points.Add(new Coordinate(lat, lon, elevation));
        }

        return new Track(points, profile, string.IsNullOrWhiteSpace(name) ? null : name);
    }
}
