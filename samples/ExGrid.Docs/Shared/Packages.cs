namespace ExGrid.Docs;

/// <summary>A package of the family, as the Packages page lists it (ADR-0042, ADR-0110).</summary>
/// <param name="Product">The product ADR-0042's table puts it under: <c>ExGrid</c>, <c>The Snapshot</c>.</param>
/// <param name="Id">Its package id, which is its assembly's name.</param>
/// <param name="Description">Its assembly's description: the text nuget.org shows.</param>
/// <param name="DependsOn">The family's packages it depends on, each at exactly its own version.</param>
internal sealed record DocsPackage(string Product, string Id, string Description, IReadOnlyList<string> DependsOn)
{
    /// <summary>The package on nuget.org: the version a tag published, or the package's page.</summary>
    public string NuGetUrl => DocsBuild.Kind == DocsBuildKind.Tag
        ? $"https://www.nuget.org/packages/{Id}/{DocsBuild.Version}"
        : $"https://www.nuget.org/packages/{Id}";

    /// <summary>The package's readme in the repository, at the tag or the commit the site was built from.</summary>
    public string ReadmeUrl => DocsBuild.BlobUrl($"src/{Id}/README.md");
}

/// <summary>
/// The family's packages (ADR-0042), read when the site was built: by product, in ADR-0042's table order,
/// which the build checks is exactly what <c>release.yml</c> publishes; each with its assembly's
/// description and the family packages its project references.
/// </summary>
internal static partial class Packages
{
    /// <summary>Every package, in ADR-0042's order.</summary>
    public static IReadOnlyList<DocsPackage> All { get; } = Read();

    /// <summary>The products, in ADR-0042's order, each with its packages.</summary>
    public static IReadOnlyList<IGrouping<string, DocsPackage>> ByProduct { get; } = All.GroupBy(p => p.Product).ToArray();

    /// <summary>The page about a product, as ADR-0042's table names it; <see langword="null"/> when the site has none.</summary>
    public static string? PageOf(string product) => product switch
    {
        "ExGrid" => "exgrid",
        "ExSheet" => "exsheet",
        "ExPivot" => "expivot",
        "The Snapshot" or "Data" => "data",
        _ => null,
    };

    // The build writes this from ADR-0042, release.yml and the packages' assemblies and projects (ExGrid.Docs.Generator).
    private static partial DocsPackage[] Read();
}
