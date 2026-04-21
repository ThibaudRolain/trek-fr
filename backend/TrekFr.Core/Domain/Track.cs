using System.Collections.Generic;

namespace TrekFr.Core.Domain;

public sealed record Track(
    IReadOnlyList<Coordinate> Points,
    Profile Profile,
    string? Name = null);
