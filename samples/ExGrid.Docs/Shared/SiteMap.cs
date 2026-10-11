using ExGrid.Docs.Layout;

namespace ExGrid.Docs;

/// <summary>A routed page of the site, as the build read it from its markup (ADR-0110).</summary>
/// <param name="Path">Its file, from the site's folder: <c>Pages/Grid/GridOverview.razor</c>.</param>
/// <param name="Routes">Its routes, relative to the site's base: <c>exgrid</c>; the start page's is empty.</param>
/// <param name="Product">The product its header names, if any.</param>
/// <param name="Title">Its title, if it has one.</param>
/// <param name="Lead">The sentence or two under its title, if any.</param>
/// <param name="Sections">Its sections, in order.</param>
public sealed record SitePage(
    string Path,
    IReadOnlyList<string> Routes,
    string? Product,
    string? Title,
    string? Lead,
    IReadOnlyList<SiteSection> Sections)
{
    /// <summary>Its first route.</summary>
    public string Route => Routes[0];
}

/// <summary>A section of a page.</summary>
/// <param name="Title">Its heading.</param>
/// <param name="Anchor">The anchor its heading carries, the one a link to it names.</param>
/// <param name="Examples">Its Examples, in order.</param>
/// <param name="Types">The types its API tables list, as the page names them: <c>ExGrid&lt;&gt;</c>.</param>
public sealed record SiteSection(
    string Title,
    string Anchor,
    IReadOnlyList<SiteExample> Examples,
    IReadOnlyList<string> Types);

/// <summary>An Example of a section.</summary>
/// <param name="Title">Its heading, if it has one.</param>
/// <param name="HasMud">Whether it also runs under MudBlazor's Chrome.</param>
public sealed record SiteExample(string? Title, bool HasMud);

/// <summary>What a search result is.</summary>
public enum SiteSearchKind
{
    /// <summary>A page.</summary>
    Page,

    /// <summary>A section of a page.</summary>
    Section,

    /// <summary>An Example.</summary>
    Example,

    /// <summary>A type a page's API table lists.</summary>
    Type,

    /// <summary>A line of the Release Notes, or a Change Note (ADR-0190).</summary>
    ReleaseNote,

    /// <summary>An item of the Roadmap: a ticket, a row of a scope table, or a row written by hand (ADR-0191).</summary>
    Roadmap,

    /// <summary>A function ExSheet supports, on the Functions page.</summary>
    Function,

    /// <summary>A key of the Keyboard reference.</summary>
    Key,
}

/// <summary>A place the search can lead to.</summary>
/// <param name="Title">What it is called.</param>
/// <param name="Context">Where it is: the group, the page and the section.</param>
/// <param name="Href">Its address, relative to the site's base, with the section's anchor.</param>
/// <param name="Kind">What it is.</param>
public sealed record SiteSearchEntry(string Title, string Context, string Href, SiteSearchKind Kind);

/// <summary>
/// The site map (ADR-0110): every routed page with its sections, Examples and API tables, read from
/// the pages' markup when the site is built. The build checks it against <see cref="DocsNav"/>, and
/// the frame reads it: the "On this page" rail, the source link and the search. Nothing is registered
/// at runtime, so a page's rail is whole on its first render.
/// </summary>
internal static partial class SiteMap
{
    /// <summary>
    /// Every routed page, by file. The Release notes page's sections are its versions, which it renders
    /// from the notes rather than writes as sections of its own (ADR-0190); the Functions page's are the
    /// catalogue's categories, which it renders from the build's reading of the catalogue.
    /// </summary>
    public static IReadOnlyList<SitePage> Pages { get; } = Read()
        .Select(p => p.Routes.Contains(ReleaseNotes.Route, StringComparer.OrdinalIgnoreCase)
            ? p with { Sections = ReleaseNotes.PageSections().ToArray() }
            : p.Routes.Contains(FunctionCatalogue.Route, StringComparer.OrdinalIgnoreCase)
                ? p with { Sections = [.. p.Sections, .. FunctionCatalogue.PageSections()] }
                : p)
        .ToArray();

    private static readonly Dictionary<string, SitePage> ByRoute = Pages
        .SelectMany(p => p.Routes.Select(r => (Route: r, Page: p)))
        .ToDictionary(x => x.Route, x => x.Page, StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// What the search leads to: each page the navigation lists, each of its sections, each titled
    /// Example and each type an API table lists, in the navigation's order; then each line of the
    /// Release Notes, each item of the Roadmap, each function ExSheet supports and each key of the
    /// Keyboard reference.
    /// </summary>
    public static IReadOnlyList<SiteSearchEntry> Entries { get; } = BuildEntries();

    /// <summary>The page with <paramref name="route"/>, if the site has one.</summary>
    /// <param name="route">A route relative to the site's base, without a query or an anchor.</param>
    public static SitePage? Find(string route) =>
        ByRoute.TryGetValue(route.Trim('/'), out var page) ? page : null;

    /// <summary>
    /// The places whose title or context hold every word of <paramref name="text"/>, best first: a
    /// title that begins with the text, then one that holds it, then one that holds every word, then
    /// the rest; within each, pages before sections, sections before Examples, Examples before types.
    /// Spaces and punctuation do not count in the comparison of the whole, so "selection summary"
    /// finds <c>SelectionSummary</c> first.
    /// </summary>
    public static IEnumerable<SiteSearchEntry> Search(string? text)
    {
        var words = (text ?? "").Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (words.Length == 0)
            return [];
        var whole = Letters(text!);
        return Entries
            .Select((entry, order) => (entry, order, title: Letters(entry.Title)))
            .Where(x => words.All(w =>
                x.entry.Title.Contains(w, StringComparison.OrdinalIgnoreCase)
                || x.entry.Context.Contains(w, StringComparison.OrdinalIgnoreCase)))
            .OrderBy(x => x.title.StartsWith(whole, StringComparison.Ordinal) ? 0
                : x.title.Contains(whole, StringComparison.Ordinal) ? 1
                : words.All(w => x.entry.Title.Contains(w, StringComparison.OrdinalIgnoreCase)) ? 2
                : 3)
            .ThenBy(x => x.entry.Kind)
            .ThenBy(x => x.order)
            .Select(x => x.entry);
    }

    // The letters and digits of a text, in lower case.
    private static string Letters(string text) =>
        new(text.Where(char.IsLetterOrDigit).Select(char.ToLowerInvariant).ToArray());

    // The build writes this from the pages (ExGrid.Docs.Generator).
    private static partial SitePage[] Read();

    private static SiteSearchEntry[] BuildEntries()
    {
        var entries = new List<SiteSearchEntry>();
        foreach (var group in DocsNav.Groups)
        {
            foreach (var link in group.Links.Where(l => l.IsRoute))
            {
                var page = Find(link.Href);
                entries.Add(new(link.Title, group.Title, link.Href, SiteSearchKind.Page));
                if (page is null)
                    continue;
                var where = $"{group.Title} › {link.Title}";
                foreach (var section in page.Sections)
                {
                    var href = $"{link.Href}#{section.Anchor}";
                    entries.Add(new(section.Title, where, href, SiteSearchKind.Section));
                    foreach (var example in section.Examples.Where(e => e.Title is not null))
                        entries.Add(new(example.Title!, $"{where} › {section.Title}", href, SiteSearchKind.Example));
                    foreach (var type in section.Types)
                        entries.Add(new(TypeTitle(type), $"API table · {where}", href, SiteSearchKind.Type));
                }
            }
        }
        entries.AddRange(ReleaseNotes.SearchEntries());
        entries.AddRange(Roadmap.SearchEntries());
        entries.AddRange(FunctionCatalogue.SearchEntries());
        entries.AddRange(Keyboard.SearchEntries());
        return entries.ToArray();
    }

    // ExGrid.Clipboard.PastePlan as PastePlan; ExGrid<> as it is.
    private static string TypeTitle(string type)
    {
        var generic = type.IndexOf('<');
        var name = generic < 0 ? type : type[..generic];
        var dot = name.LastIndexOf('.');
        return dot < 0 ? type : type[(dot + 1)..];
    }
}
