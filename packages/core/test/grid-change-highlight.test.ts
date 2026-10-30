// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the Change Highlight (ADR-0068). The Consumer says when a value cell's shown value
// last changed; the grid paints `ex-changed` until that time plus the duration, then takes it away in one step,
// with one timer for the earliest end and only the rows whose marks ended painted again.
import { afterEach, describe, expect, it } from 'vitest';
import type { GridOptions } from '../src/dom/grid-view';
import { columns, mount, painted, q, rowsOf, trades, unmountAll, type Trade } from './grid-harness';

afterEach(() => { unmountAll(); });

const cellAt = (root: HTMLElement, row: number, column: number): HTMLElement =>
    q(root, `.ex-viewport > .ex-row[aria-rowindex="${row + 2}"] > [aria-colindex="${column + 1}"]`);

const marked = (root: HTMLElement): string[] =>
    [...root.querySelectorAll<HTMLElement>('.ex-viewport .ex-changed')].map(cell => cell.id.replace(/^.*r(\d+)c(\d+)$/u, '$1:$2'));

const rows = trades(3);

function grid(options: Partial<GridOptions<Trade>>): ReturnType<typeof mount<Trade>> {
    return mount<Trade>({ columns, rows, rowHeight: 24, viewportHeight: 480, ...options });
}

/** Book-0's Notional changed at 0, and Book-2's Book at 400: a change time is taken as given. */
const twoChanges: GridOptions<Trade>['cellChangedAt'] = (trade, column) =>
    trade.book === 'Book-0' && column === 'Notional' ? 0 : trade.book === 'Book-2' && column === 'Book' ? 400 : undefined;

/** Book-0's Notional changed at 0, and Book-1's changes at 10, still to come at first. */
const pastAndComing: GridOptions<Trade>['cellChangedAt'] = (trade, column) =>
    column !== 'Notional' ? undefined : trade.book === 'Book-0' ? 0 : trade.book === 'Book-1' ? 10 : undefined;

describe('The Change Highlight (ADR-0068)', () => {
    it('ADR-0068: a changed value cell is marked until its change time plus the duration, then unmarked in one step; only the rows whose marks ended paint again, and no timer stands after', async () => {
        const { root, told } = grid({ cellChangedAt: twoChanges });
        await painted();
        expect(marked(root)).toEqual(['0:1', '2:0']);
        expect(cellAt(root, 0, 1).className).toBe('ex-cell ex-cell-numeric ex-changed');
        const before = rowsOf(root);

        told.clock.advance(999);
        await painted();
        expect(marked(root)).toEqual(['0:1', '2:0']);
        told.clock.advance(1);
        await painted();
        expect(marked(root)).toEqual(['2:0']);
        const after = rowsOf(root);
        expect([after[0] === before[0], after[1] === before[1], after[2] === before[2]]).toEqual([false, true, true]);

        told.clock.advance(400);
        await painted();
        expect(marked(root)).toEqual([]);
        expect(told.clock.pending).toBe(0);
    });

    it('ADR-0068: nothing is asked without the declaration; with it, each painted value cell is asked once, and the function\'s identity is the change signal', async () => {
        let asked = 0;
        const book1: { at: number | undefined } = { at: undefined };
        const changedAt: GridOptions<Trade>['cellChangedAt'] = (trade, column) => {
            asked++;
            return trade.book === 'Book-1' && column === 'Book' ? book1.at : undefined;
        };
        const { root, view, told } = grid({});
        await painted();
        expect([marked(root), told.clock.pending]).toEqual([[], 0]);
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, cellChangedAt: changedAt });
        await painted();
        expect([asked, marked(root)]).toEqual([6, []]);
        // The same function answering otherwise leaves the marks on screen where they are (ADR-0003).
        book1.at = 0;
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, cellChangedAt: changedAt });
        await painted();
        expect([asked, marked(root)]).toEqual([6, []]);
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, cellChangedAt: (trade, column) => changedAt(trade, column) });
        await painted();
        expect(marked(root)).toEqual(['1:0']);
        expect(() => { view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, cellChangedAt: 0 as never }); })
            .toThrow(/cellChangedAt is a function \(ADR-0068\)/u);
    });

    it('ADR-0068: the duration is the Consumer\'s: zero marks only a change still to come, and a negative one is refused', async () => {
        const changedAt = pastAndComing;
        const { root, view, told } = grid({ cellChangedAt: changedAt, changeHighlightDuration: 0 });
        await painted();
        expect(marked(root)).toEqual(['1:1']);
        told.clock.advance(10);
        await painted();
        expect(marked(root)).toEqual([]);
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, cellChangedAt: changedAt, changeHighlightDuration: 5000 });
        await painted();
        expect(marked(root)).toEqual(['0:1', '1:1']);
        expect(() => { view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, cellChangedAt: changedAt, changeHighlightDuration: -1 }); })
            .toThrow(/changeHighlightDuration is a finite number of zero or more milliseconds/u);
        expect(() => { view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, cellChangedAt: changedAt, changeHighlightDuration: Number.POSITIVE_INFINITY }); })
            .toThrow(/changeHighlightDuration is a finite number/u);
    });
});
