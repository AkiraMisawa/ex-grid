// The pages this host serves, in the index's order: the entries of DemoPageList.cs that the Script Core
// draws so far. A page joins as the core comes to draw what it shows (ADR-0350 section 4).

/** One demo page: where it is, what it is called, and what it is for. */
export interface DemoPage {
    readonly route: string;
    readonly title: string;
    readonly group: string;
    readonly description: string;
}

export const demoPages: readonly DemoPage[] = Object.freeze([
    {
        route: 'virtual', title: 'Virtualised', group: 'Rows and data',
        description: '100,000 rows the grid never holds: Range Requests, Placeholders and a Consumer that answers late (ADR-0001, ADR-0004).',
    },
    {
        route: 'fetch', title: 'Fetching source', group: 'Rows and data',
        description: 'GridSource.fetch over a slow server: only the range you land on is painted, stale answers are discarded (ADR-0025).',
    },
    {
        route: 'wide', title: 'Wide', group: 'Rows and data',
        description: 'Rows and 100 columns virtualised on both axes, with pinned columns; compare with horizontal virtualisation off (ADR-0004).',
    },
    {
        route: 'features', title: 'Features', group: 'Interaction',
        description: 'Sorting, the Selection, copying and Header Groups on one page; editing, paste and filtering, with the status lines that report them, join as the Script Core comes to them.',
    },
    {
        route: 'cells', title: 'Cells and rows', group: 'Cells and rows',
        description: 'Cell State, Row Kind, Template Columns and Action Columns — what the value alone cannot say (ADR-0006, ADR-0020, ADR-0024).',
    },
    {
        route: 'stripes', title: 'Row Stripes', group: 'Cells and rows',
        description: 'Stripes decided from each row\'s position in the whole result, so they stay with the row as it scrolls (ADR-0038).',
    },
    {
        route: 'tones', title: 'Tone and Cell State', group: 'Cells and rows',
        description: 'A tone rule\'s gains and losses in a theme\'s colours, and a Cell State that outranks them on the same cell (ADR-0006, ADR-0029).',
    },
    {
        route: 'appearance', title: 'Cell appearance', group: 'Cells and rows',
        description: 'A per-cell Font, Fill and Borders as Excel paints them; bold judged by bold widths, italic never cut (ADR-0050, ADR-0071).',
    },
    {
        route: 'sizing', title: 'Sizing', group: 'Layout',
        description: 'Column width gestures and a box that narrows until pinning is suspended (ADR-0016, ADR-0045).',
    },
    {
        route: 'stretch', title: 'Stretch', group: 'Layout',
        description: 'A Viewport that takes its parent\'s box: a sized box, the rest of a flex column, a box that follows the window, and a parent with no height (ADR-0028).',
    },
    {
        route: 'density', title: 'Density', group: 'Layout',
        description: 'A density preset changed while the grid is on screen: rows, header band and Selection follow the resolved Grid Metrics together (ADR-0027, ADR-0028).',
    },
    {
        route: 'lifecycle', title: 'Lifecycle', group: 'Layout',
        description: 'Mount and dispose a grid repeatedly; nothing it attached stays behind (ADR-0018, ADR-0021).',
    },
    {
        route: 'marks', title: 'Row Marks', group: 'Cells and rows',
        description: 'A Mark Column over 1,000,000 fetched rows: mark all, marks that survive a filter, and an action over them (ADR-0043).',
    },
    {
        route: 'filter-panel', title: 'Filter panel', group: 'Interaction',
        description: 'The filter panel\'s value list, Blank entry, search, condition form and TooMany under the built-in Chrome, with the Filter it applied written out (ADR-0009, WR-1).',
    },
    {
        route: 'chart', title: 'Charts', group: 'ExChart',
        description: 'An exposure profile in two panels and a hazard curve drawn as steps: the tooltip lists every series at the pointer\'s date, the legend hides a series, a drag narrows the range, and Next run replaces the data keeping what the user changed (ADR-0252, ADR-0270 to ADR-0273).',
    },
    {
        route: 'chart-bridge', title: 'Charting a selection', group: 'ExChart',
        description: 'A curve history charted from the grid\'s context menu: series in columns along the dates or in rows across the tenors, Add to chart, a refusal by name, a chart that keeps its rows through a recalculation and a deleted row, and a click that moves the grid\'s Focus (ADR-0258).',
    },
    {
        route: 'scatter', title: 'Scatter and fitted lines', group: 'ExChart',
        description: 'CDS spreads against leverage beside their grid, with the least-squares and the Theil–Sen line; Old Faithful; and PRML\'s polynomial curve fitting of degree 0, 1, 3 and 9. The nearest point\'s tooltip, a click to the row, a rectangle that narrows both axes, a logarithmic spread axis (ADR-0290, ADR-0291).',
    },
    {
        route: 'chart-spec', title: 'Charts from a Chart Spec', group: 'ExChart',
        description: 'An American put\'s 4-step lattice, as two tables a capability would return, drawn from the Chart Spec in the field: coloured by value, sized by the chance of reaching each node, its branches as edges; the tool\'s answer under the field, and a spec with numbers in it refused with its path and reason (ADR-0320, ADR-0321).',
    },
    {
        route: 'chart-candles', title: 'Candles and intervals', group: 'ExChart',
        description: 'Ten years of a synthetic index as candles with volume and a moving average: rising hollow and falling filled, merged into months to fit and back to days when narrowed, on calendar time or packed sessions, in three colour conventions; a yield curve with a Bid–Ask band and forecasts with 95% CI whiskers; strategies\' risk and return with intervals both ways (ADR-0300 to ADR-0304).',
    },
    {
        route: 'surface', title: '3D surface', group: 'ExChart',
        description: 'An FX volatility surface turned by dragging or with the arrow keys, zoomed and panned, read at the grid point nearest the pointer, sliced into a smile or a term structure by a click, with a hole drawn as a hole (ADR-0274).',
    },
    {
        route: 'pivot', title: 'Pivot', group: 'ExPivot',
        description: 'Excel\'s PivotTable drawn by ExGrid: Month as a part of the trade date, the Fields pane with drag and drop, saving the layout, and Excel\'s Japanese words (ADR-0059, ADR-0060, ADR-0061).',
    },
    {
        route: 'pivot-risk', title: 'Pivot risk', group: 'ExPivot',
        description: 'A rate-delta report by tenor: desks and curves in Rows, tenors in Columns, ordered ON to 30Y by the page\'s Order Key, with 18M and 1Y6M two Items side by side (ADR-0060).',
    },
    {
        route: 'pivot-csv', title: 'Pivot over a CSV', group: 'ExPivot',
        description: 'A CSV read under a declared Schema, and an unknown file under a suggested one once you confirm it: choosing a file, progress, Cancel, a malformed row refused by name, then the pivot (ADR-0064).',
    },
    {
        route: 'pivot-db', title: 'Pivot over a database', group: 'ExPivot',
        description: 'The demo API server\'s SQLite trades two ways, side by side: read over Arrow into a Snapshot and pivoted here, and asked of the server\'s Pivot Source, which answers each question\'s Leaf Aggregates in SQL (ADR-0066, ADR-0069, ADR-0370).',
    },
    {
        route: 'sheet', title: 'Sheet', group: 'ExSheet',
        description: 'ExSheet drawn by ExGrid: Formulas, editing, paste, Delete, the fill keys and undo, with ?lang=ja for the Sheet\'s Japanese (ADR-0046, ADR-0370).',
    },
    {
        route: 'chart-showcase/exposure', title: 'Exposure profile showcase', group: 'ExChart',
        description: 'The Docs Site\'s Exposure profile Showcase: a netting set\'s EPE, ENE, CVA and PFE before and after a trade, charted from the grid\'s selection into tabs, CVA per year over its buckets, and a recalculation the charts follow (ADR-0280).',
    },
    {
        route: 'chart-showcase/cds-spread', title: 'CDS spread vs leverage showcase', group: 'ExChart',
        description: 'The Docs Site\'s CDS spread vs leverage Showcase: two hundred names in a grid beside their scatter, the least-squares and the Theil–Sen line with their coefficients in the Notes, a logarithmic spread switched in the bar, a rectangle that narrows both axes and a click to the row (ADR-0280, ADR-0290, ADR-0291).',
    },
    {
        route: 'chart-showcase/fx-volatility', title: 'FX volatility showcase', group: 'ExChart',
        description: 'The Docs Site\'s FX volatility Showcase: EURUSD quotes beside the 3D surface made from them, missing quotes filled by the page\'s spline and said so, sliced by a click and saved as SVG or PNG, with the term structure, the smile and the deposits and forwards switched in the bar (ADR-0280).',
    },
    {
        route: 'chart-showcase/equity-index', title: 'Equity index showcase', group: 'ExChart',
        description: 'The Docs Site\'s Equity index daily bars Showcase: ten years of an index beside its candles, volume and 25-day average, merged into months and back to days, packed or on calendar time, in three colour conventions, a click to the row, and a Bid–Ask band and 95% CI whiskers in a second view (ADR-0280, ADR-0300 to ADR-0304).',
    },
    {
        route: 'chart-showcase/binomial-lattice', title: 'Binomial lattice showcase', group: 'ExChart',
        description: 'The Docs Site\'s Binomial lattice Showcase: an option priced on a CRR lattice, its nodes in a grid and on a chart coloured by value with early exercise ringed, the lattice price by N against Black–Scholes, and a light/dark switch in the bar (ADR-0280).',
    },
    {
        route: 'chart-showcase/curves', title: 'Curves showcase', group: 'ExChart',
        description: 'The Docs Site\'s Curves Showcase: a CDS curve with its hazard rates as steps, CDS curves charted from rows across tenors, and a rate curve with its interpolation named, switched in the bar (ADR-0280).',
    },
    {
        route: 'board', title: 'A Board', group: 'ExBoard',
        description: 'A Board read from JSON: a blotter, a pivot by trader and a chart by day over one checked input, and a limits grid over a second; the blotter\'s selected rows narrow the other three by desk, and a dropped selection restores them (ADR-0340).',
    },
]);
