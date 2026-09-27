using ExSheet.Engine;
using Xunit;

namespace ExSheet.Engine.Tests;

public class PackageBoundaryTests
{
    [Fact] // ADR-0047 (SH-1): ExSheet.Engine references nothing of ours and no Blazor package
    public void The_engine_references_only_the_base_class_library()
    {
        var referenced = typeof(Sheet).Assembly.GetReferencedAssemblies().Select(a => a.Name!).ToArray();

        Assert.All(referenced, name => Assert.True(
            name == "System" || name.StartsWith("System.", StringComparison.Ordinal) || name == "netstandard",
            $"ExSheet.Engine references {name}."));
    }
}
