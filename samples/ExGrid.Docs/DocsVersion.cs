using System.Reflection;

namespace ExGrid.Docs;

/// <summary>The version the site states: the tag it was published from, or the commit.</summary>
public static class DocsVersion
{
    /// <summary>The version, as the build was given it.</summary>
    public static string Value { get; } = typeof(DocsVersion).Assembly
        .GetCustomAttributes<AssemblyMetadataAttribute>()
        .FirstOrDefault(a => a.Key == "DocsVersion")?.Value ?? "dev";
}
