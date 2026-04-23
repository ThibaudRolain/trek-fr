using TrekFr.Infrastructure.Communes;

namespace TrekFr.Tests;

/// <summary>
/// Singleton-per-process : le dataset est un embedded resource de ~35k communes, parsé
/// à chaque instanciation. xUnit parallélise les classes, donc `new CommuneDataset()` par
/// classe multiplie le coût. Un seul holder static suffit pour les tests en lecture seule.
/// </summary>
internal static class TestCommuneDataset
{
    public static readonly CommuneDataset Instance = new();
}
