using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using TrekFr.Core.Domain;

namespace TrekFr.Core.Abstractions;

public interface IGeocoder
{
    Task<IReadOnlyList<GeocodeResult>> SearchAsync(
        string query,
        int limit = 5,
        CancellationToken ct = default);
}

public sealed record GeocodeResult(
    string DisplayName,
    Coordinate Location,
    string? CountryCode,
    string? Region);
