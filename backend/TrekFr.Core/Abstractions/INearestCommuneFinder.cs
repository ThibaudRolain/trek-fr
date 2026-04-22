using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface INearestCommuneFinder
{
    Commune? FindNearest(Coordinate point, double maxDistanceKm = 50);
}
