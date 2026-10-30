using System.Text.RegularExpressions;
using ExSheet.Engine;

namespace ExGrid.Docs;

/// <summary>A category of ExSheet's function catalogue, as the Functions page groups the functions.</summary>
/// <param name="Name">Its heading in the catalogue: <c>Aggregation</c>.</param>
/// <param name="Anchor">The anchor its section on the page carries.</param>
/// <param name="Functions">Its functions, in the catalogue's order.</param>
internal sealed record FunctionCategory(string Name, string Anchor, IReadOnlyList<CataloguedFunction> Functions);

/// <summary>A row of the catalogue: a function's name and its status there.</summary>
/// <param name="Name">The name in upper case: <c>SUMIFS</c>.</param>
/// <param name="Status">The catalogue's status: <c>Supported</c>, <c>Ready</c>, <c>Observe</c>, <c>Decide</c> or <c>Blocked</c>.</param>
internal sealed record CataloguedFunction(string Name, string Status);

/// <summary>A function the engine does not support yet, and where the Roadmap places the work on it.</summary>
/// <param name="Name">Its name.</param>
/// <param name="Category">The catalogue's category it is under.</param>
/// <param name="RoadmapHref">The Roadmap's section that lists its ticket, or the Roadmap when no ticket names it.</param>
/// <param name="RoadmapTitle">What the link says.</param>
internal sealed record PendingFunction(string Name, string Category, string RoadmapHref, string RoadmapTitle);

/// <summary>
/// ExSheet's functions as the Functions page lists them (ADR-0110, ADR-0047): each supported function's
/// signature and description from the engine's own list, <see cref="DeclaredFunction.All"/>, grouped by the
/// categories of the catalogue, <c>docs/specs/exsheet-functions/spec.md</c>, which the build read. The engine
/// decides what is supported; the catalogue only where each function belongs. A function the engine declares
/// and the catalogue does not place is listed under its own heading, never dropped.
/// </summary>
internal static partial class FunctionCatalogue
{
    /// <summary>The page's route.</summary>
    public const string Route = "exsheet/functions";

    /// <summary>The heading of the functions the engine supports and the catalogue does not place.</summary>
    public const string UncataloguedTitle = "Not catalogued yet";

    /// <summary>The heading of the functions the engine does not support yet.</summary>
    public const string PendingTitle = "Not supported yet";

    /// <summary>The catalogue's categories, in its order.</summary>
    public static IReadOnlyList<FunctionCategory> Categories { get; } = Read();

    /// <summary>Each category with the supported functions it holds, in the catalogue's order.</summary>
    public static IReadOnlyList<(FunctionCategory Category, IReadOnlyList<DeclaredFunction> Functions)> Supported { get; } =
        Categories
            .Select(c => (c, (IReadOnlyList<DeclaredFunction>)c.Functions
                .Select(f => DeclaredFunction.Find(f.Name))
                .Where(f => f is not null)
                .Select(f => f!)
                .ToArray()))
            .Where(c => c.Item2.Count > 0)
            .ToArray();

    /// <summary>The functions the engine supports that no category of the catalogue places.</summary>
    public static IReadOnlyList<DeclaredFunction> Uncatalogued { get; } = DeclaredFunction.All
        .Where(f => !Categories.Any(c => c.Functions.Any(g => g.Name == f.Name)))
        .ToArray();

    /// <summary>The functions the catalogue lists and the engine does not support yet, each with its place on the Roadmap.</summary>
    public static IReadOnlyList<PendingFunction> Pending { get; } = Categories
        .SelectMany(c => c.Functions.Where(f => DeclaredFunction.Find(f.Name) is null).Select(f => Place(f.Name, c.Name)))
        .ToArray();

    /// <summary>The page's sections, for its rail and the search: each category, then those not catalogued, then those not supported yet.</summary>
    public static IEnumerable<SiteSection> PageSections()
    {
        foreach (var (category, _) in Supported)
            yield return new SiteSection(category.Name, category.Anchor, [], []);
        if (Uncatalogued.Count > 0)
            yield return new SiteSection(UncataloguedTitle, DocsAnchor.Of(UncataloguedTitle), [], []);
        if (Pending.Count > 0)
            yield return new SiteSection(PendingTitle, DocsAnchor.Of(PendingTitle), [], []);
    }

    /// <summary>Each supported function, as a place the search leads to.</summary>
    public static IEnumerable<SiteSearchEntry> SearchEntries()
    {
        foreach (var (category, functions) in Supported)
        {
            foreach (var function in functions)
                yield return new SiteSearchEntry(function.Name, $"ExSheet › Functions › {category.Name}", $"{Route}#{category.Anchor}", SiteSearchKind.Function);
        }
        foreach (var function in Uncatalogued)
            yield return new SiteSearchEntry(function.Name, $"ExSheet › Functions › {UncataloguedTitle}", $"{Route}#{DocsAnchor.Of(UncataloguedTitle)}", SiteSearchKind.Function);
    }

    /// <summary>Whether <paramref name="function"/> matches the filter: its name, or a word of what it returns.</summary>
    public static bool Matches(DeclaredFunction function, string? filter) =>
        string.IsNullOrWhiteSpace(filter)
        || function.Name.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase)
        || function.Description.Contains(filter.Trim(), StringComparison.OrdinalIgnoreCase);

    // The Roadmap's section whose ticket names the function as a word: "RAND" in "RAND and RANDBETWEEN",
    // never in "RANDBETWEEN" alone.
    private static PendingFunction Place(string name, string category)
    {
        var word = new Regex(@"(?<![A-Z0-9]|[A-Z0-9]\.)" + Regex.Escape(name) + @"(?![A-Z0-9]|\.[A-Z0-9])", RegexOptions.CultureInvariant);
        var ticket = Roadmap.Tickets.FirstOrDefault(t => t.Feature == "exsheet-functions" && (word.IsMatch(t.Title) || word.IsMatch(t.Sentence)));
        if (ticket is null)
            return new PendingFunction(name, category, Roadmap.Route, "The Roadmap");
        var section = ticket.State == RoadmapState.Decided ? Roadmap.Decided : Roadmap.Waiting;
        return new PendingFunction(name, category, $"{Roadmap.Route}#{DocsAnchor.Of(section)}", $"Roadmap: {section}");
    }

    // The build writes this from the catalogue (ExGrid.Docs.Generator).
    private static partial FunctionCategory[] Read();
}
