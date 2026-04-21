namespace TrekFr.Core.Domain;

public readonly record struct Coordinate(double Latitude, double Longitude, double? Elevation = null);
