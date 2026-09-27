using System.Reflection;
using ExGrid.Components;
using ExSheet;
using ExSheet.Engine;
using Xunit;

namespace ExSheet.Components.Tests;

/// <summary>
/// SH-1's component half (ADR-0046/0047): ExSheet references the engine and ExGrid, and neither
/// of them references ExSheet. The engine's own half — it references nothing of ours — is pinned
/// in <c>tests/ExSheet.Engine.Tests</c>.
/// </summary>
public class ReferenceDirectionTests
{
    private static readonly Assembly Component = typeof(SheetRow).Assembly;

    [Fact] // ADR-0046: ExSheet → ExSheet.Engine and ExSheet → ExGrid, and nothing else of ours
    public void The_component_references_the_engine_and_the_grid()
    {
        var ours = Component.GetReferencedAssemblies()
            .Select(a => a.Name!)
            .Where(name => name.StartsWith("ExGrid", StringComparison.Ordinal) || name.StartsWith("ExSheet", StringComparison.Ordinal))
            .Order(StringComparer.Ordinal)
            .ToList();

        Assert.Equal(["ExGrid", "ExSheet.Engine"], ours);
    }

    [Fact] // ADR-0046: nothing ExSheet depends on references ExSheet
    public void Neither_the_engine_nor_the_grid_references_the_component()
    {
        foreach (var assembly in new[] { typeof(Sheet).Assembly, typeof(ExGrid<>).Assembly })
        {
            Assert.DoesNotContain(assembly.GetReferencedAssemblies(), a => a.Name == Component.GetName().Name);
        }
    }
}
