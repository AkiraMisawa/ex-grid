// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the Selection Summary in the DOM (ADR-0130). The grid asks and whoever holds the
// data answers; the status line shows only the answer to the current question, in Excel's order, and stands
// whenever the grid can summarise (SM-2, SM-4, SM-9).
import { afterEach, describe, expect, it } from 'vitest';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridOptions } from '../src/dom/grid-view';
import { rowsMovedUnder, SummaryQuestions, type SelectionSummary } from '../src/dom/selection-summary';
import { cell } from '../src/selection/cell';
import { selectionRange, type SelectionRange } from '../src/selection/range';
import { GridSource } from '../src/source/grid-source';
import { answeredSummary, declinedSummary, summaryOf, type GridSummaryRequest, type GridSummaryResult } from '../src/summary/grid-summary';
import { columns, mount, painted, q, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

/** Lets an answer that is already in land, and the pass it asks for run. */
async function answered(): Promise<void> {
    await new Promise(resolve => { setTimeout(resolve, 0); });
}

const box = (root: HTMLElement): HTMLElement => q(root, '.ex-status .ex-summary');
const figures = (root: HTMLElement): string[] => [...box(root).querySelectorAll('.ex-summary-figure')].map(figure => figure.textContent);

const valueOf = (held: readonly GridColumn<Trade>[]) => (name: string): ((row: Trade) => unknown) | undefined =>
    held.find(column => column.name === name)?.value;

/** An answerer that holds every question until the test answers it. */
function holding(): {
    readonly asked: { readonly request: GridSummaryRequest; readonly signal: AbortSignal; readonly answer: (result: GridSummaryResult) => void }[];
    readonly onSummarize: NonNullable<GridOptions<Trade>['onSummarize']>;
} {
    const asked: { request: GridSummaryRequest; signal: AbortSignal; answer: (result: GridSummaryResult) => void }[] = [];
    return {
        asked,
        onSummarize: async (request, signal) => new Promise(answer => { asked.push({ request, signal, answer }); }),
    };
}

function select(view: ReturnType<typeof mount<Trade>>['view'], range: SelectionRange, focusRow = range.topRow, focusColumn = range.leftColumn): void {
    expect(view.placeSelection(range, cell(focusRow, focusColumn), view.readView().rowSequenceVersion)).toBe(true);
}

// Whole numbers held as bigints, summed exactly, in a format of the test's.
const exactly = (format: (value: unknown) => string): readonly GridColumn<Trade>[] => [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120 },
    { name: 'Notional', type: 'number', value: trade => BigInt(trade.notional), width: 100, format },
];

// A format written for the column's bigints, which a figure's number does not reach.
function bigintOnly(value: unknown): string {
    if (typeof value !== 'bigint') throw new TypeError('This format takes a bigint.');
    return `${value}n`;
}

// A format that takes numbers too, to two places.
const either = (value: unknown): string => (typeof value === 'bigint' ? `${value}` : (value as number).toFixed(2));

describe('The Selection Summary', () => {
    it('SM-9 / ADR-0130: over a source that summarises the strip stands before any selection, and two cells show Average, Count and Sum in Excel\'s order', async () => {
        const { root, view } = mount({ columns, source: GridSource.from(trades(10)), rowHeight: 24 });
        await painted();
        expect(view.canSummarize).toBe(true);
        expect(box(root).textContent).toBe('');

        select(view, selectionRange(1, 1, 3, 1));
        await answered();
        expect(figures(root)).toEqual(['Average: 2000', 'Count: 3', 'Sum: 6000']);

        // One cell is no question: the figures go.
        select(view, selectionRange(2, 1, 1, 1));
        await answered();
        expect(box(root).className).toBe('ex-summary');
        expect(figures(root)).toEqual([]);
    });

    it('SM-2 / ADR-0130: a new selection clears the figures at once for the pending words, cancels the question it replaced, and its late answer is shown nowhere', async () => {
        const { asked, onSummarize } = holding();
        const rows = trades(10);
        const heard: SelectionSummary[] = [];
        const { root, view } = mount({ columns, rows, rowHeight: 24, onSummarize, onSelectionSummaryChanged: summary => { heard.push(summary); } });
        await painted();

        select(view, selectionRange(0, 1, 2, 1));
        await painted();
        expect(box(root).className).toBe('ex-summary ex-summary-pending');
        expect(box(root).textContent).toBe('Calculating…');
        expect(asked).toHaveLength(1);
        expect(asked[0]?.request).toMatchObject({ figures: ['average', 'count', 'sum'], columns: ['Book', 'Notional'], focus: cell(0, 1) });

        select(view, selectionRange(0, 1, 3, 1));
        await painted();
        expect(asked[0]?.signal.aborted).toBe(true);
        expect(asked).toHaveLength(2);

        // The replaced question answered late: its two rows' figures are shown nowhere.
        asked[0]?.answer(summaryOf(rows, asked[0].request, valueOf(columns)));
        await answered();
        expect(box(root).textContent).toBe('Calculating…');

        asked[1]?.answer(summaryOf(rows, asked[1].request, valueOf(columns)));
        await answered();
        expect(figures(root)).toEqual(['Average: 1000', 'Count: 3', 'Sum: 3000']);
        expect(heard.map(summary => summary.status)).toEqual(['pending', 'pending', 'answered']);
        expect(heard[2]?.result).toMatchObject({ kind: 'answered' });
    });

    it('SM-4 / ADR-0130: a declined question shows its reason in place of the figures, and no figure with it', async () => {
        const { asked, onSummarize } = holding();
        const { root, view } = mount({ columns, rows: trades(10), rowHeight: 24, onSummarize });
        await painted();
        select(view, selectionRange(0, 0, 5, 2));
        await painted();
        asked[0]?.answer(declinedSummary('Not summed: this page sums 4 cells at most.'));
        await answered();
        expect(box(root).className).toBe('ex-summary ex-summary-declined');
        expect(box(root).textContent).toBe('Not summed: this page sums 4 cells at most.');
        expect(figures(root)).toEqual([]);
    });

    it('ADR-0130: counts are invariant whole numbers, the other figures take the Focus column\'s format, and a figure that is not a number is #NUM!', async () => {
        const formatted: readonly GridColumn<Trade>[] = [
            { name: 'Book', type: 'text', value: trade => trade.book, width: 120 },
            { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100, format: value => `$${(value as number).toFixed(2)}` },
        ];
        const { root, view } = mount({ columns: formatted, source: GridSource.from(trades(10)), rowHeight: 24 });
        await painted();
        select(view, selectionRange(1, 0, 3, 2), 1, 1);
        await answered();
        expect(figures(root)).toEqual(['Average: $2000.00', 'Count: 6', 'Sum: $6000.00']);

        // With the Focus on text the figures are their own.
        select(view, selectionRange(1, 0, 3, 2), 1, 0);
        await answered();
        expect(figures(root)).toEqual(['Average: 2000', 'Count: 6', 'Sum: 6000']);

        // An answerer's own text wins; a figure that overflowed says #NUM! rather than going missing.
        const { asked, onSummarize } = holding();
        const pushed = mount({ columns: formatted, rows: trades(10), rowHeight: 24, onSummarize });
        await painted();
        select(pushed.view, selectionRange(0, 1, 2, 1));
        await painted();
        asked[0]?.answer(answeredSummary({
            average: { kind: 'number', number: 1.5, exact: undefined },
            count: { kind: 'number', number: 2, exact: undefined },
            sum: { kind: 'error', error: 'notANumber' },
        }, { average: 'one and a half' }));
        await answered();
        expect(figures(pushed.root)).toEqual(['Average: one and a half', 'Count: 2', 'Sum: #NUM!']);
    });

    it('ADR-0130: a format that cannot take a number, and a figure past a double\'s exact integers, show the figure\'s own text', async () => {
        const huge: readonly Trade[] = [{ book: 'A', notional: 2 ** 53 }, { book: 'B', notional: 2 ** 53 }, { book: 'C', notional: 1 }];
        const source = GridSource.from(huge);
        const { root, view } = mount({ columns: exactly(bigintOnly), source, rowHeight: 24 });
        await painted();
        select(view, selectionRange(1, 1, 2, 1));
        await answered();
        expect(figures(root)).toEqual(['Average: 4503599627370496.5', 'Count: 2', 'Sum: 9007199254740993']);

        // A format that takes numbers too: the Sum, 2^54 + 1, would be shown as 2^54 by it, so its exact text is.
        view.update({ columns: exactly(either), source, rowHeight: 24 });
        await painted();
        select(view, selectionRange(0, 1, 3, 1));
        await answered();
        expect(figures(root)).toEqual(['Average: 6004799503160661.6666666667', 'Count: 3', 'Sum: 18014398509481985']);
        select(view, selectionRange(2, 0, 1, 2), 2, 1);
        await answered();
        expect(figures(root)).toEqual(['Average: 1.00', 'Count: 2', 'Sum: 1.00']);
    });

    it('ADR-0130: summaryFigures are shown in Excel\'s order, none asks nothing, and a name that is no figure is refused', async () => {
        const { asked, onSummarize } = holding();
        const rows = trades(10);
        const { root, view } = mount({ columns, rows, rowHeight: 24, onSummarize, summaryFigures: ['sum', 'max', 'numericalCount'] });
        await painted();
        select(view, selectionRange(0, 1, 3, 1));
        await painted();
        expect(asked[0]?.request.figures).toEqual(['numericalCount', 'max', 'sum']);
        asked[0]?.answer(summaryOf(rows, asked[0].request, valueOf(columns)));
        await answered();
        expect(figures(root)).toEqual(['Numerical Count: 3', 'Max: 2000', 'Sum: 3000']);

        view.update({ columns, rows, rowHeight: 24, onSummarize, summaryFigures: [] });
        await painted();
        expect(asked).toHaveLength(1);
        expect(box(root).textContent).toBe('');

        expect(() => { view.update({ columns, rows, onSummarize, summaryFigures: ['median' as 'sum'] }); }).toThrow(/"median", which is none of the figures/);
    });

    it('ADR-0130: showSelectionSummary off leaves no strip, a listener is still told, and with nobody listening nothing is asked', async () => {
        const { asked, onSummarize } = holding();
        const rows = trades(10);
        const heard: SelectionSummary[] = [];
        const { root, view } = mount({ columns, rows, rowHeight: 24, onSummarize, showSelectionSummary: false });
        await painted();
        expect(root.querySelector('.ex-status')).toBeNull();
        select(view, selectionRange(0, 1, 2, 1));
        await painted();
        expect(asked).toHaveLength(0);

        view.update({ columns, rows, rowHeight: 24, onSummarize, showSelectionSummary: false, onSelectionSummaryChanged: summary => { heard.push(summary); } });
        await painted();
        expect(asked).toHaveLength(1);
        expect(root.querySelector('.ex-status')).toBeNull();
        expect(heard.map(summary => summary.status)).toEqual(['pending']);
    });

    it('ADR-0130: onSummarize beside a source that summarises is refused by name, and over rows it answers in place of the grid\'s own source', () => {
        const { onSummarize } = holding();
        expect(() => mount({ columns, source: GridSource.from(trades(3)), onSummarize })).toThrow(/silently win/);
        const { view } = mount({ columns, rows: trades(3), onSummarize });
        expect(view.canSummarize).toBe(true);
    });

    it('ADR-0130: a selected row replaced, refreshSummary and a write handed over each ask again; a refresh with no question asks nothing', async () => {
        const rows = trades(10);
        const source = GridSource.from(rows);
        let asks = 0;
        const answer = source.summarize.bind(source);
        source.summarize = async (request, signal) => {
            asks += 1;
            return answer(request, signal);
        };
        const cleared: unknown[] = [];
        const editable = columns.map(column => (column.name === 'Notional' ? { ...column, editable: true } : column));
        const { root, view } = mount({ columns: editable, source, rowHeight: 24, onClear: intent => { cleared.push(intent); } });
        await painted();
        view.refreshSummary();
        await answered();
        expect(asks).toBe(0);

        select(view, selectionRange(1, 1, 2, 1));
        await answered();
        expect(asks).toBe(1);
        expect(figures(root)).toContain('Sum: 3000');

        // A row outside the selection, replaced: the figures stand.
        source.replaceRow(rows[5] as Trade, { book: 'Book-5', notional: 1 });
        source.publishGathered();
        await answered();
        expect(asks).toBe(1);

        source.replaceRow(rows[1] as Trade, { book: 'Book-1', notional: 7000 });
        source.publishGathered();
        await answered();
        expect(asks).toBe(2);
        expect(figures(root)).toContain('Sum: 9000');

        view.refreshSummary();
        await answered();
        expect(asks).toBe(3);

        // Delete hands a Clear Intent over the selection: whatever the Consumer writes, the figures are asked again.
        root.dispatchEvent(new KeyboardEvent('keydown', { key: 'Delete', bubbles: true, cancelable: true }));
        await answered();
        expect(cleared).toHaveLength(1);
        expect(asks).toBe(4);
    });
});

const row = (n: number): { readonly n: number } => ({ n });

describe('The rows under a Selection Summary', () => {
    const window = [row(0), row(1), row(2), row(3)];

    it('ADR-0130: a Window that only scrolled moves nothing; a selected row replaced, or a different row count, does', () => {
        const ranges = [selectionRange(1, 0, 2, 1)];
        const before = { window, windowStart: 10, totalCount: 100 };
        expect(rowsMovedUnder(ranges.map(r => selectionRange(r.topRow + 10, 0, 2, 1)), before, { window: window.slice(1), windowStart: 11, totalCount: 100 })).toBe(false);
        expect(rowsMovedUnder(ranges, { window, windowStart: 0, totalCount: 4 }, { window, windowStart: 0, totalCount: 5 })).toBe(true);
        const replaced = [window[0], row(1), window[2], window[3]] as typeof window;
        expect(rowsMovedUnder(ranges, { window, windowStart: 0, totalCount: 4 }, { window: replaced, windowStart: 0, totalCount: 4 })).toBe(true);
        // A row outside the ranges replaced moves nothing under them.
        const outside = [row(0), window[1], window[2], window[3]] as typeof window;
        expect(rowsMovedUnder(ranges, { window, windowStart: 0, totalCount: 4 }, { window: outside, windowStart: 0, totalCount: 4 })).toBe(false);
    });

    it('ADR-0130: an answer that is not one to the question is the answerer\'s defect, named rather than shown in part', async () => {
        let landed = 0;
        const questions = new SummaryQuestions(() => { landed += 1; });
        questions.review({
            ranges: [selectionRange(0, 0, 2, 1)], focus: cell(0, 0), binding: {}, rowSequenceVersion: 0, columns: ['A'], figures: ['count', 'sum'], rowsStamp: 0,
        });
        await expect(questions.settle(() => async () => answeredSummary({ sum: { kind: 'empty' } }), undefined))
            .rejects.toThrow(/answered the figures \[sum\], and was asked for \[count, sum\]/);
        expect(landed).toBe(0);
    });
});

const rightClick = (target: HTMLElement): MouseEvent => {
    const event = new MouseEvent('contextmenu', { bubbles: true, cancelable: true, button: 2 });
    target.dispatchEvent(event);
    return event;
};
const menuItems = (root: HTMLElement): HTMLButtonElement[] => [...root.querySelectorAll<HTMLButtonElement>('.ex-summary-menu [role=menuitemcheckbox]')];
const ticks = (root: HTMLElement): [string, string | null][] =>
    menuItems(root).map(item => [item.textContent.replace('✓', ''), item.getAttribute('aria-checked')]);

function press(name: string): void {
    document.activeElement?.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
}

describe('The figures menu', () => {
    it('SM-9 / ADR-0130: a right-click on the status line opens Excel\'s six figures, ticked while shown; a press reports the choice in Excel\'s order, and the ticks follow the figures handed back', async () => {
        const chosen: (readonly string[])[] = [];
        const options: GridOptions<Trade> = {
            columns, source: GridSource.from(trades(10)), rowHeight: 24,
            onSummaryFiguresChanged: choice => { chosen.push(choice); },
        };
        const { root, view } = mount(options);
        await painted();
        expect(box(root).getAttribute('data-ex-claims')).toBe('contextmenu');
        expect(rightClick(box(root)).defaultPrevented).toBe(true);
        await painted();
        const menu = q(root, '.ex-popover-consumer .ex-summary-menu');
        expect([menu.getAttribute('role'), menu.getAttribute('aria-label'), q(root, '.ex-popover-consumer').getAttribute('aria-label')])
            .toEqual(['menu', 'Status bar figures', 'Status bar figures']);
        expect(ticks(root)).toEqual([
            ['Average', 'true'], ['Count', 'true'], ['Numerical Count', 'false'], ['Min', 'false'], ['Max', 'false'], ['Sum', 'true'],
        ]);
        expect(document.activeElement).toBe(menuItems(root)[0]);

        menuItems(root)[3]?.click();
        menuItems(root)[0]?.click();
        await painted();
        // The grid holds no choice, and the menu stays. Two presses before the figures come back build one on the
        // other, however the Consumer is timed.
        expect(chosen).toEqual([['average', 'count', 'min', 'sum'], ['count', 'min', 'sum']]);
        expect(ticks(root).map(([, checked]) => checked)).toEqual(['true', 'true', 'false', 'false', 'false', 'true']);

        view.update({ ...options, summaryFigures: ['min', 'count', 'sum'] });
        await painted();
        expect(ticks(root).map(([, checked]) => checked)).toEqual(['false', 'true', 'false', 'true', 'false', 'true']);

        // A menu's keys step through the items and wrap.
        press('ArrowUp');
        expect(document.activeElement).toBe(menuItems(root)[5]);
        press('Home');
        press('ArrowDown');
        expect(document.activeElement).toBe(menuItems(root)[1]);

        // Escape closes it as any popover, and the keyboard is back on the grid.
        document.activeElement?.dispatchEvent(new KeyboardEvent('keydown', { key: 'Escape', bubbles: true, cancelable: true }));
        await painted();
        expect([root.querySelector('.ex-summary-menu'), root.contains(document.activeElement)]).toEqual([null, true]);
    });

    it('ADR-0130, ADR-0036: an open edit commits before the menu opens, and the menu goes once nobody hears the choice', async () => {
        const edits: string[] = [];
        const options: GridOptions<Trade> = {
            columns: [{ name: 'Book', type: 'text', value: trade => trade.book, width: 120, editable: true }],
            source: GridSource.from(trades(10)), rowHeight: 24,
            onEdit: intent => { edits.push(intent.value); },
            onSummaryFiguresChanged: () => undefined,
        };
        const { root, view } = mount(options);
        await painted();
        expect(view.placeSelection(selectionRange(1, 0, 1, 1), cell(1, 0), view.readView().rowSequenceVersion)).toBe(true);
        await painted();
        view.focus();
        document.activeElement?.dispatchEvent(new KeyboardEvent('keydown', { key: 'X', bubbles: true, cancelable: true }));
        await painted();
        expect(root.querySelector('.ex-editor')).not.toBeNull();
        rightClick(box(root));
        await painted();
        expect([edits, root.querySelector('.ex-editor'), menuItems(root).length]).toEqual([['X'], null, 6]);

        const { onSummaryFiguresChanged: _withdrawn, ...withdrawn } = options;
        view.update(withdrawn);
        await painted();
        expect([root.querySelector('.ex-summary-menu'), root.contains(document.activeElement)]).toEqual([null, true]);
    });

    it('ADR-0130: with nobody hearing a change of figures, the right-click on the status line is the browser\'s', async () => {
        const { root } = mount({ columns, source: GridSource.from(trades(10)), rowHeight: 24 });
        await painted();
        expect([box(root).hasAttribute('data-ex-claims'), rightClick(box(root)).defaultPrevented]).toEqual([false, false]);
        await painted();
        expect(root.querySelector('.ex-summary-menu')).toBeNull();
    });
});
