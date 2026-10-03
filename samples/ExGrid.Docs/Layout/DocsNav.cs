namespace ExGrid.Docs.Layout;

/// <summary>One page in the navigation.</summary>
/// <param name="Title">What the navigation calls it.</param>
/// <param name="Href">Its address, relative to the site's base.</param>
public sealed record DocsLink(string Title, string Href);

/// <summary>A group of pages in the navigation.</summary>
/// <param name="Title">The group's heading.</param>
/// <param name="Links">Its pages, in reading order.</param>
public sealed record DocsGroup(string Title, IReadOnlyList<DocsLink> Links);

/// <summary>The site's navigation, in reading order: the drawer draws it, and each page's
/// "previous" and "next" follow it.</summary>
public static class DocsNav
{
    /// <summary>Every group, in order.</summary>
    public static IReadOnlyList<DocsGroup> Groups { get; } =
    [
        new("Getting started",
        [
            new("Introduction", ""),
            new("Installation", "getting-started"),
            new("Design", "design"),
        ]),
        new("ExGrid",
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
        new("ExSheet",
        [
            new("Overview", "exsheet"),
            new("Formulas", "exsheet/formulas"),
            new("Formatting", "exsheet/formatting"),
            new("Linked Tables", "exsheet/linked-tables"),
        ]),
        new("ExPivot",
        [
            new("Overview", "expivot"),
            new("Layout", "expivot/layout"),
            new("Values", "expivot/values"),
            new("Sources", "expivot/sources"),
            new("Live data", "expivot/live"),
        ]),
        new("Data",
        [
            new("Snapshot", "data"),
            new("Apache Arrow", "data/arrow"),
        ]),
        new("Showcases",
        [
            new("Trade blotter", "showcase/blotter"),
            new("Budget sheet", "showcase/budget"),
            new("Sales analysis", "showcase/sales"),
        ]),
    ];

    /// <summary>Every page, in reading order.</summary>
    public static IReadOnlyList<DocsLink> All { get; } = Groups.SelectMany(g => g.Links).ToArray();
}
