using System;
using TrekFr.Core.Domain;

namespace TrekFr.Core.UseCases;

public sealed class ElevationOutOfRangeException(ElevationFilter filter, string context)
    : Exception(
        $"Aucune trace trouvée {filter.Describe()} pour {context}. Essaie d'élargir la plage de dénivelé ou de changer la distance cible.")
{
    public ElevationFilter Filter { get; } = filter;
    public string Context { get; } = context;
}
