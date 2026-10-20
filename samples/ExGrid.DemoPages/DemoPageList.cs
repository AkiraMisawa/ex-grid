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
        new("grid-live", "Live grid", "Rows and data",
            "A live Window over the demo API server's trades through GridSource.Fetch: its hub says the trades changed, the source reads its Window again by Row Key, and the cells whose values moved are marked for a moment (ADR-0068, ADR-0069, ADR-0141)."),
        new("grid-live-local", "Live grid in process", "Rows and data",
            "A million trades held in this process through GridSource.From with a Row Key, amended by the page's own timer as Change Batches four times a second; the source gathers them, brings its result up to date from the rows that changed, and marks the cells whose values moved (ADR-0141)."),
        new("cells", "Cells and rows", "Cells and rows",
            "Cell State, Row Kind, Template Columns and Action Columns — what the value alone cannot say (ADR-0006, ADR-0020, ADR-0024)."),
        new("marks", "Row Marks", "Cells and rows",
            "A Mark Column over 1,000,000 fetched rows: mark all, marks that survive a filter, and an action over them (ADR-0043)."),
        new("stripes", "Row Stripes", "Cells and rows",
            "Stripes decided from each row's position in the whole result, so they stay with the row as it scrolls (ADR-0038)."),
        new("tones", "Tone and Cell State", "Cells and rows",
            "A tone rule's gains and losses in a theme's colours, and a Cell State that outranks them on the same cell (ADR-0006, ADR-0029)."),
        new("appearance", "Cell appearance", "Cells and rows",
            "A per-cell Font, Fill and Borders as Excel paints them; bold judged by bold widths, italic never cut (ADR-0050, ADR-0071)."),
        new("features", "Features", "Interaction",
            "Editing, the clipboard, sorting, filtering and Header Groups on one page, with every notification written out."),
        new("sizing", "Sizing", "Layout",
            "Column width gestures and a box that narrows until pinning is suspended (ADR-0016, ADR-0045)."),
        new("stretch", "Stretch", "Layout",
            "A Viewport that takes its parent's height, and the warning when the parent has none (ADR-0028)."),
        new("density", "Density", "Layout",
            "A density preset changed while the grid is on screen: rows, header band and Selection follow the resolved Grid Metrics together (ADR-0027, ADR-0028)."),
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
        new("sheet", "Sheet", "ExSheet",
            "ExSheet drawn by ExGrid: Formulas with completion and pointing, the Formula Bar, fill, paste, insertion, and a Linked Table read from the positions grid beside it (ADR-0046, ADR-0049, ADR-0051)."),
        new("pointing", "Pointing", "ExSheet",
            "A Sheet pointing into a positions grid through a Pointing Scope: a press writes a lookup by key, and the arrow keys move it inside the grid, which scrolls to follow (ADR-0058). With ?narrow the grid is laid out narrower than its columns, and shows both of its own scrollbars."),
        new("sheets", "Two sheets", "ExSheet",
            "Two ExSheets on one page: keys, completion, undo and the Formula Bar stay with the Sheet that has the keyboard, and each outlines a Linked Table's columns only in the grid its page wired to it (ADR-0018, ADR-0048, ADR-0057)."),
        new("pivot", "Pivot", "ExPivot",
            "The basics of Excel's PivotTable drawn by ExGrid: fields declared with PivotFields.Of, Month as a part of the trade date, the Fields pane, the Layout menu, Show Details in tabs, and Excel's Japanese words; ?chrome=mud dresses it in MudBlazor (ADR-0059, ADR-0060, ADR-0061, ADR-0062)."),
        new("pivot-csv", "Pivot over a CSV", "ExPivot",
            "A CSV read under a declared Schema, and an unknown file under a suggested one once you confirm it: choosing a file, progress, Cancel, a malformed row refused by name, then the pivot (ADR-0064)."),
        new("pivot-db", "Pivot over a database", "ExPivot",
            "The demo API server's SQLite trades two ways, side by side: read over Arrow into a Snapshot the page pivots, and asked of the server's own Pivot Source, which answers in SQL (ADR-0065, ADR-0066, ADR-0069)."),
        new("pivot-live", "Live pivots", "ExPivot",
            "Change Batches folded into the bundled source by the page's own timer, and a server whose data its live updates keep moving; changed values are marked in both, and a server cut off leaves a Stale Report (ADR-0067, ADR-0068)."),
        new("pivot-risk", "Pivot risk", "ExPivot",
            "A rate-delta report: desks and curves in Rows, tenors in Columns, ordered ON, TN, 1W … 30Y by an Order Key, with 18M and 1Y6M side by side (ADR-0060)."),
        new("chart", "Charts", "ExChart",
            "An exposure profile in two panels and a hazard curve drawn as steps: the tooltip lists every series at the pointer's date, the legend hides a series, a drag narrows the range, and Next run replaces the data keeping what the user changed (ADR-0252, ADR-0270 to ADR-0273)."),
        new("chart-bridge", "Charting a selection", "ExChart",
            "A curve history charted from the grid's context menu: series in columns along the dates or in rows across the tenors, Add to chart, a refusal by name, a chart that keeps its rows through a recalculation and a deleted row, and a click that moves the grid's Focus (ADR-0258)."),
        new("chart-dock", "Charts under MudBlazor", "ExChart",
            "ExChart.MudBlazor: the Chart Dock beside the grid opening each chart made from the selection as a tab, and a chart in the grid Wrapper's paper with the MudBlazor toolbar, download menu and context menu, the theme's colours leading its series (ADR-0258, ADR-0281)."),
        new("scatter", "Scatter and fitted lines", "ExChart",
            "CDS spreads against leverage beside their grid, with the least-squares and the Theil–Sen line; Old Faithful; and PRML's polynomial curve fitting of degree 0, 1, 3 and 9. The nearest point's tooltip, a click to the row, a rectangle that narrows both axes, a logarithmic spread axis (ADR-0290, ADR-0291)."),
        new("chart-candles", "Candles and intervals", "ExChart",
            "Ten years of a synthetic index as candles with volume and a moving average: rising hollow and falling filled, merged into months to fit and back to days when narrowed, on calendar time or packed sessions, in three colour conventions; a yield curve with a Bid–Ask band and forecasts with 95% CI whiskers; strategies' risk and return with intervals both ways (ADR-0300 to ADR-0304)."),
        new("chart-showcase/exposure", "Exposure profile showcase", "ExChart",
            "The Docs Site's Exposure profile Showcase: a netting set's EPE, ENE, CVA and PFE before and after a trade, charted from the grid's selection into tabs, CVA per year over its buckets, and a recalculation the charts follow; ?chrome=mud for MudBlazor (ADR-0280)."),
        new("chart-showcase/curves", "Curves showcase", "ExChart",
            "The Docs Site's Curves Showcase: a CDS curve with its hazard rates as steps, CDS curves charted from rows across tenors, and a rate curve with its interpolation named, switched in the bar; ?chrome=mud for MudBlazor (ADR-0280)."),
        new("chart-showcase/fx-volatility", "FX volatility showcase", "ExChart",
            "The Docs Site's FX volatility Showcase: EURUSD quotes beside the 3D surface made from them, missing quotes filled by the page's spline and said so, sliced by a click and saved as SVG or PNG, with the term structure, the smile and the deposits and forwards switched in the bar; ?chrome=mud for MudBlazor (ADR-0280)."),
        new("chart-showcase/cds-spread", "CDS spread vs leverage showcase", "ExChart",
            "The Docs Site's CDS spread vs leverage Showcase: two hundred names in a grid beside their scatter, the least-squares and the Theil–Sen line with their coefficients in the Notes, a logarithmic spread switched in the bar, a rectangle that narrows both axes and a click to the row; ?chrome=mud for MudBlazor (ADR-0280, ADR-0290, ADR-0291)."),
        new("chart-showcase/equity-index", "Equity index showcase", "ExChart",
            "The Docs Site's Equity index daily bars Showcase: ten years of an index beside its candles, volume and 25-day average, merged into months and back to days, packed or on calendar time, in three colour conventions, a click to the row, and a Bid–Ask band and 95% CI whiskers in a second view; ?chrome=mud for MudBlazor (ADR-0280, ADR-0300 to ADR-0304)."),
        new("surface", "3D surface", "ExChart",
            "An FX volatility surface turned by dragging or with the arrow keys, zoomed and panned, read at the grid point nearest the pointer, sliced into a smile or a term structure by a click, with a hole drawn as a hole (ADR-0274)."),
    ];

    /// <summary>The entry for a base-relative path, ignoring any query or fragment, or null
    /// for the index and for anything not listed.</summary>
    public static DemoPage? For(string baseRelativePath)
    {
        var route = baseRelativePath.Split('?', '#')[0].Trim('/');
        return All.FirstOrDefault(page => page.Route == route);
    }
}
