namespace ExGrid.Docs.Layout;

/// <summary>What a navigation entry leads to, and so how it opens.</summary>
public enum DocsLinkKind
{
    /// <summary>A page of this site, opened in place.</summary>
    Page,

    /// <summary>A Showcase of this site, which fills the window: opened in a new tab.</summary>
    Showcase,

    /// <summary>A file or folder of the repository, at the tag or the commit the site was built from;
    /// a folder's path ends in <c>/</c>.</summary>
    Repository,

    /// <summary>An address outside the site.</summary>
    External,
}

/// <summary>One entry in the navigation.</summary>
/// <param name="Title">What the navigation calls it.</param>
/// <param name="Href">Its address: a route relative to the site's base, a path from the repository's
/// root, or a whole address, as <paramref name="Kind"/> says. A literal, so the build can check that a
/// page has it (ADR-0110).</param>
/// <param name="Kind">What it leads to.</param>
public sealed record DocsLink(string Title, string Href, DocsLinkKind Kind = DocsLinkKind.Page)
{
    /// <summary>Whether it names a route of this site.</summary>
    public bool IsRoute => Kind is DocsLinkKind.Page or DocsLinkKind.Showcase;

    /// <summary>The address a link to it carries.</summary>
    public string Url => Kind switch
    {
        DocsLinkKind.Repository when Href.EndsWith('/') => DocsBuild.TreeUrl(Href.TrimEnd('/')),
        DocsLinkKind.Repository => DocsBuild.BlobUrl(Href),
        _ => Href,
    };

    /// <summary>The tab it opens in: its own for anything but a page of the site.</summary>
    public string? Target => Kind == DocsLinkKind.Page ? null : "_blank";
}

/// <summary>Which part of the site a group belongs to (ADR-0110).</summary>
public enum DocsNavSection
{
    /// <summary>The topic pages: getting started and each product. "Previous" and "next" walk these.</summary>
    Docs,

    /// <summary>The Showcases, which fill the window.</summary>
    Showcases,

    /// <summary>The reference pages that cross the products.</summary>
    Reference,

    /// <summary>The project around the products: its records and how to take part.</summary>
    Project,
}

/// <summary>A group of entries in the drawer.</summary>
/// <param name="Title">The group's heading.</param>
/// <param name="Section">The part of the site it belongs to.</param>
/// <param name="Links">Its entries, in reading order.</param>
public sealed record DocsGroup(string Title, DocsNavSection Section, IReadOnlyList<DocsLink> Links);

/// <summary>A product, as the app bar's Products menu offers it.</summary>
/// <param name="Link">Its first page.</param>
/// <param name="Caption">One line that says what it is.</param>
public sealed record DocsProduct(DocsLink Link, string Caption);

/// <summary>
/// The site's navigation (ADR-0110), the one ordered list: the drawer draws its groups, the app bar
/// and the footer take their links from it, and each topic page's "previous" and "next" follow the
/// Docs groups. The build reads this class: an entry whose route no page has, and a page that no
/// group lists and <see cref="Unlisted"/> does not name, are errors. An entry appears only with a
/// page behind it.
/// </summary>
public static class DocsNav
{
    /// <summary>Every group, in order.</summary>
    public static IReadOnlyList<DocsGroup> Groups { get; } =
    [
        new("Getting started", DocsNavSection.Docs,
        [
            new("Introduction", ""),
            new("Installation", "getting-started"),
            new("Packages", "getting-started/packages"),
            new("Feature matrix", "features"),
            new("Design", "design"),
        ]),
        new("ExGrid", DocsNavSection.Docs,
        [
            new("Overview", "exgrid"),
            new("Columns", "exgrid/columns"),
            new("Selection and keyboard", "exgrid/selection"),
            new("Sorting, filtering and find", "exgrid/sorting-filtering"),
            new("Editing", "exgrid/editing"),
            new("Clipboard", "exgrid/clipboard"),
            new("Data sources", "exgrid/data"),
            new("Appearance", "exgrid/appearance"),
        ]),
        new("ExSheet", DocsNavSection.Docs,
        [
            new("Overview", "exsheet"),
            new("Formulas", "exsheet/formulas"),
            new("Functions", "exsheet/functions"),
            new("Formatting", "exsheet/formatting"),
            new("Linked Tables", "exsheet/linked-tables"),
        ]),
        new("ExPivot", DocsNavSection.Docs,
        [
            new("Overview", "expivot"),
            new("Layout", "expivot/layout"),
            new("Values", "expivot/values"),
            new("Sources", "expivot/sources"),
            new("Live data", "expivot/live"),
        ]),
        new("Data", DocsNavSection.Docs,
        [
            new("Snapshot", "data"),
            new("Apache Arrow", "data/arrow"),
        ]),
        new("Showcases", DocsNavSection.Showcases,
        [
            new("All showcases", "showcases"),
            new("Trade blotter", "showcase/blotter", DocsLinkKind.Showcase),
            new("Budget sheet", "showcase/budget", DocsLinkKind.Showcase),
            new("Sales analysis", "showcase/sales", DocsLinkKind.Showcase),
            new("Exposure profile", "showcase/exposure", DocsLinkKind.Showcase),
            new("Curves", "showcase/curves", DocsLinkKind.Showcase),
        ]),
        new("Reference", DocsNavSection.Reference,
        [
            new("Keyboard", "reference/keyboard"),
            new("API reference", "reference/api"),
        ]),
        new("Project", DocsNavSection.Project,
        [
            new("Release notes", "release-notes"),
            new("Roadmap", "roadmap"),
            new("Decision records", "docs/adr/", DocsLinkKind.Repository),
            new("Contributing", "CONTRIBUTING.md", DocsLinkKind.Repository),
            new("Report a problem", DocsBuild.Repository + "/issues", DocsLinkKind.External),
            new("Licence", "LICENSE", DocsLinkKind.Repository),
        ]),
    ];

    /// <summary>
    /// The routes no group lists on purpose, because a reader reaches them only by their address: the
    /// figures <c>tools/readme-media</c> draws from the product, and the page that says an address has
    /// no page. A route ending in <c>*</c> names every route it begins.
    /// </summary>
    public static IReadOnlyList<string> Unlisted { get; } =
    [
        "figure/*",
        "{*path}",
    ];

    /// <summary>The app bar's first link.</summary>
    public static DocsLink GetStarted { get; } = new("Get started", "getting-started");

    /// <summary>The app bar's Products menu, and the footer's: each product's first page.</summary>
    public static IReadOnlyList<DocsProduct> Products { get; } =
    [
        new(new("ExGrid", "exgrid"), "The grid: a spreadsheet's selection, keys and clipboard over a million rows"),
        new(new("ExSheet", "exsheet"), "A sheet with Formulas, drawn by ExGrid"),
        new(new("ExPivot", "expivot"), "Excel's PivotTable, drawn by ExGrid"),
        new(new("Data", "data"), "The Snapshot, the family's immutable data, and Apache Arrow"),
    ];

    /// <summary>The Products menu's last entry, under the products, and the footer's (ADR-0110).</summary>
    public static DocsProduct FeatureMatrix { get; } =
        new(new("Feature matrix", "features"), "Where the family runs, and what each MudBlazor Wrapper draws");

    /// <summary>The app bar's link to the Showcases.</summary>
    public static DocsLink Showcases { get; } = new("Showcases", "showcases");

    /// <summary>The app bar's link to the Release Notes (ADR-0190).</summary>
    public static DocsLink ReleaseNotes { get; } = new("Release notes", "release-notes");

    /// <summary>The app bar's link to the Roadmap (ADR-0191).</summary>
    public static DocsLink Roadmap { get; } = new("Roadmap", "roadmap");

    /// <summary>
    /// The topic pages in reading order, which "previous" and "next" walk: the Docs groups' pages, so
    /// that the last page of one product leads to the first of the next, and never into a Showcase or
    /// the project's records.
    /// </summary>
    public static IReadOnlyList<DocsLink> Reading { get; } = Groups
        .Where(g => g.Section == DocsNavSection.Docs)
        .SelectMany(g => g.Links)
        .Where(l => l.Kind == DocsLinkKind.Page)
        .ToArray();

    /// <summary>The group that lists <paramref name="route"/>, if one does.</summary>
    /// <param name="route">A route relative to the site's base, without a query or an anchor.</param>
    public static DocsGroup? GroupOf(string route) =>
        Groups.FirstOrDefault(g => g.Links.Any(l => l.IsRoute && string.Equals(l.Href, route, StringComparison.OrdinalIgnoreCase)));
}
