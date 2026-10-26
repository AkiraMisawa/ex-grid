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
        description: 'Template Columns and Action Columns, what the value alone cannot say (ADR-0020); Cell State and Row Kind join as the Script Core comes to them.',
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
]);
