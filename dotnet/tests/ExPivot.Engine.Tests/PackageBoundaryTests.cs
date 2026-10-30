using ExPivot.Engine;
using Xunit;

namespace ExPivot.Engine.Tests;

/// <summary>The engine's package boundary (ADR-0059/0060). The Pivot Layout's saved form is held on both
/// engines by the shared cases (<c>tests/cases/pivot/layout-json.json</c>).</summary>
public class PackageBoundaryTests
{
    [Fact] // ADR-0059/0064 (PV-1): the engine references the base class library and ExGrid.Data, and nothing else
    public void The_engine_references_only_the_base_class_library_and_the_data_package()
    {
        var referenced = typeof(PivotEngine).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.All(referenced, name => Assert.True(
            name == "System" || name.StartsWith("System.", StringComparison.Ordinal) || name == "netstandard" || name == "ExGrid.Data",
            $"ExPivot.Engine references {name}."));
        Assert.Contains("ExGrid.Data", referenced);
    }
}
