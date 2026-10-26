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
        route: 'board', title: 'A Board', group: 'ExBoard',
        description: 'A Board read from JSON: a blotter, a pivot by trader and a chart by day over one checked input, and a limits grid over a second; the blotter\'s selected rows narrow the other three by desk, and a dropped selection restores them (ADR-0340).',
    },
]);
