using ExGrid.Docs.Layout;

namespace ExGrid.Docs;

/// <summary>A row of the Feature matrix's "Where it runs", written by hand with the records that decide it.</summary>
/// <param name="Topic">What it is about: <c>Browsers</c>.</param>
/// <param name="Text">What holds, in plain text.</param>
/// <param name="Cites">The records that decide it: <c>ADR-0017</c>, <c>DoD §24</c>.</param>
internal sealed record MatrixFact(string Topic, string Text, IReadOnlyList<string> Cites);

/// <summary>A Wrapper whose Chrome the Feature matrix reads.</summary>
/// <param name="Package">Its package: <c>ExGrid.MudBlazor</c>.</param>
/// <param name="Chrome">Its Chrome's full type name: <c>ExGrid.MudBlazor.MudGridChrome</c>.</param>
/// <param name="Seams">The full name of the Chrome interface whose own seams it is read for: <c>ExGrid.Chrome.IGridChrome</c>.</param>
/// <param name="Cites">The records that decide what it draws.</param>
internal sealed record MatrixWrapper(string Package, string Chrome, string Seams, IReadOnlyList<string> Cites);

/// <summary>What a reader calls a seam of a Chrome interface, and the decision that made it.</summary>
/// <param name="Seam">The interface and its member: <c>IGridChrome.FilterPanel</c>.</param>
/// <param name="Title">What a reader calls the surface: <c>The filter panel</c>.</param>
/// <param name="Cites">The records that decide it.</param>
internal sealed record MatrixSurface(string Seam, string Title, IReadOnlyList<string> Cites);

/// <summary>A seam of a Wrapper's Chrome interface, as the build read it from the Wrapper's assembly.</summary>
/// <param name="Package">The Wrapper.</param>
/// <param name="Seam">The interface and its member: <c>IGridChrome.FilterPanel</c>.</param>
/// <param name="Filled">Whether the Wrapper's Chrome draws it; otherwise the product's built-in Chrome does.</param>
internal sealed record MatrixSeam(string Package, string Seam, bool Filled);

/// <summary>A row of the Feature matrix's documentation tables: a section of a product's page.</summary>
/// <param name="Page">The page's title.</param>
/// <param name="Section">The section's title.</param>
/// <param name="Href">The section, relative to the site's base.</param>
/// <param name="Route">The page's route.</param>
/// <param name="Anchor">The section's anchor.</param>
/// <param name="Examples">How many Examples it holds.</param>
/// <param name="WithMud">How many of them also run under MudBlazor's Chrome.</param>
internal sealed record MatrixSection(string Page, string Section, string Href, string Route, string Anchor, int Examples, int WithMud);

/// <summary>
/// The Feature matrix (ADR-0110): where the family runs, written by hand in <see cref="FeatureMatrixByHand"/>
/// with the records that decide it; which surfaces each Wrapper draws, read from the Wrappers' assemblies
/// when the site was built; and, from the site map, what each product's pages hold and under which Chrome.
/// </summary>
internal static partial class FeatureMatrix
{
    /// <summary>The page's route.</summary>
    public const string Route = "features";

    /// <summary>Every seam of every Wrapper's Chrome interface, in the interface's order.</summary>
    public static IReadOnlyList<MatrixSeam> Seams { get; } = ReadSeams();

    private static readonly Dictionary<string, DocsCite> Cites = ReadCites().ToDictionary(c => c.Name, StringComparer.Ordinal);

    private static readonly Dictionary<string, MatrixSurface> Surfaces =
        FeatureMatrixByHand.Surfaces.ToDictionary(s => s.Seam, StringComparer.Ordinal);

    /// <summary>The record <paramref name="cite"/> names. The build refuses a citation that leads nowhere.</summary>
    public static DocsCite Cite(string cite) =>
        Cites.TryGetValue(cite, out var found) ? found : new DocsCite(cite, cite, "docs", null);

    /// <summary>The records a row cites.</summary>
    public static IReadOnlyList<DocsCite> CitesOf(IEnumerable<string> cites) => cites.Select(Cite).ToArray();

    /// <summary>What a reader calls a seam. The build refuses a seam with no title.</summary>
    public static MatrixSurface SurfaceOf(string seam) =>
        Surfaces.TryGetValue(seam, out var surface) ? surface : new MatrixSurface(seam, seam, []);

    /// <summary>
    /// What each product's pages hold, from the site map: one row per section of each page the product's
    /// group lists, with its Examples and how many run under MudBlazor's Chrome too. The Functions page is
    /// a reference page, counted as the functions it lists rather than as its categories.
    /// </summary>
    public static IReadOnlyList<MatrixSection> SectionsOf(string product)
    {
        var group = DocsNav.Groups.FirstOrDefault(g => g.Section == DocsNavSection.Docs && g.Title == product);
        if (group is null)
            return [];
        var rows = new List<MatrixSection>();
        foreach (var link in group.Links.Where(l => l.Kind == DocsLinkKind.Page))
        {
            if (string.Equals(link.Href, FunctionCatalogue.Route, StringComparison.OrdinalIgnoreCase))
                continue;
            if (SiteMap.Find(link.Href) is not { } page)
                continue;
            foreach (var section in page.Sections)
            {
                rows.Add(new MatrixSection(link.Title, section.Title, $"{link.Href}#{section.Anchor}", link.Href, section.Anchor,
                    section.Examples.Count, section.Examples.Count(e => e.HasMud)));
            }
        }
        return rows;
    }

    // The build writes these from FeatureMatrixByHand and the Wrappers' assemblies (ExGrid.Docs.Generator).
    private static partial MatrixSeam[] ReadSeams();

    private static partial DocsCite[] ReadCites();
}
