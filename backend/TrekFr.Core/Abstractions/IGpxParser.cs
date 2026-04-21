using System.IO;
using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface IGpxParser
{
    Track Parse(Stream gpxStream, Profile profile);
}
