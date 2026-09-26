namespace ExGrid.DemoPages;

/// <summary>One demo page: where it is, what it is called, and what it is for.</summary>
/// <param name="Route">The route without its leading slash — the form an <c>href</c> takes
/// under the host's base address.</param>
/// <param name="Title">The page's name, as the index and its navigation bar show it.</param>
/// <param name="Group">The theme the index files it under.</param>
/// <param name="Description">One line saying what the page demonstrates.</param>
public sealed record DemoPage(string Route, string Title, string Group, string Description);

/// <summary>
/// Every demo page, in the order the index shows them. The one list the index at / and
/// every page's navigation bar are drawn from, so a page added here appears in both, and a
/// page left out of it is caught by layer 3 (navigation.spec.mjs), which compares this
/// list with the routes the pages declare.
/// </summary>
public static class DemoPageList
{
    public static readonly IReadOnlyList<DemoPage> All =
    [
        new("identity", "Row identity", "Rows and data",
            "Two independent grids on one page; a replaced row instance repaints, an in-place rewrite does not (ADR-0003, ADR-0018)."),
        new("virtual", "Virtualised", "Rows and data",
            "100,000 rows the grid never holds: Range Requests, Placeholders and a Consumer that answers late (ADR-0001, ADR-0004)."),
        new("wide", "Wide", "Rows and data",
            "Rows and 100 columns virtualised on both axes, with pinned columns; compare with horizontal virtualisation off (ADR-0004)."),
        new("fetch", "Fetching source", "Rows and data",
            "GridSource.Fetch over a slow server: only the range you land on is painted, stale answers are discarded (ADR-0025)."),
        new("shared", "Shared data", "Rows and data",
            "One store, a Grid Source per user; on the Server host a change reaches every tab, a sort reaches only one (ADR-0018)."),
        new("cells", "Cells and rows", "Cells and rows",
            "Cell State, Row Kind, Template Columns and Action Columns — what the value alone cannot say (ADR-0006, ADR-0020, ADR-0024)."),
        new("marks", "Row Marks", "Cells and rows",
            "A Mark Column over 1,000,000 fetched rows: mark all, marks that survive a filter, and an action over them (ADR-0043)."),
        new("stripes", "Row Stripes", "Cells and rows",
            "Stripes decided from each row's position in the whole result, so they stay with the row as it scrolls (ADR-0038)."),
        new("features", "Features", "Interaction",
            "Editing, the clipboard, sorting, filtering and Header Groups on one page, with every notification written out."),
        new("sizing", "Sizing", "Layout",
            "Column width gestures and a box that narrows until pinning is suspended (ADR-0016, ADR-0045)."),
        new("fill", "Fill", "Layout",
            "A Viewport that takes its parent's height, and the warning when the parent has none (ADR-0028)."),
        new("lifecycle", "Lifecycle", "Layout",
            "Mount and dispose a grid repeatedly; nothing it attached stays behind (ADR-0018, ADR-0021)."),
        new("mud", "MudBlazor Wrapper", "MudBlazor",
            "The Wrapper verified against its contract: Material papers, the Wrapper's editor and loading bar (ADR-0030)."),
        new("mud-app", "MudBlazor application", "MudBlazor",
            "An ordinary MudBlazor app — drawer, tabs, a dialog, dark mode — with grids placed where such an app places them."),
        new("inspectors", "Row inspectors", "Row inspectors",
            "An Action Column and Row Marks opening a row's inspector — modal or floating — and where the keyboard goes (ADR-0020, ADR-0043)."),
        new("inspector-edits", "Inspector edits", "Row inspectors",
            "Approving and noting a row from its inspector while the store changes underneath: banner, refusal, versioned notes."),
    ];

    /// <summary>The entry for a base-relative path, ignoring any query or fragment, or null
    /// for the index and for anything not listed.</summary>
    public static DemoPage? For(string baseRelativePath)
    {
        var route = baseRelativePath.Split('?', '#')[0].Trim('/');
        return All.FirstOrDefault(page => page.Route == route);
    }
}
