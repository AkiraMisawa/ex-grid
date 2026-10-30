// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): what role a row plays, and the Row Stripe, as the grid paints them. A Row Kind is
// asked of the Consumer and painted, never decided (ADR-0024); a stripe is counted in the whole result, so it stays
// with its row (ADR-0038); and the Mark Column's "all" leaves group and total rows out (ADR-0043).
import { afterEach, describe, expect, it } from 'vitest';
import { markColumn, type GridColumn } from '../src/dom/grid-column';
import type { RowKind } from '../src/paint/row-classes';
import { GridSource } from '../src/source/grid-source';
import type { GridPage } from '../src/source/fetching-source';
import { columns, mount, painted, q, rowsOf, scrollTo, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const striped = (root: HTMLElement): boolean[] => rowsOf(root).map(row => row.classList.contains('ex-row-stripe'));
const indexOf = (row: HTMLElement): number => Number(row.getAttribute('aria-rowindex')) - 2;

// Every fifth row a group, and the one after it a total.
const kindOf = (row: Trade): RowKind => {
    const at = row.notional / 1000;
    if (at % 5 === 0) return 'group';
    return at % 5 === 1 ? 'total' : 'detail';
};

describe('Row Stripes', () => {
    it('ADR-0038: off unless given; given, every second row of the result wears one, and it stays with its row on scroll', async () => {
        const rows = trades(1000);
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(striped(root).some(Boolean)).toBe(false);
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, stripeRows: true });
        await painted();
        expect(striped(root).slice(0, 4)).toEqual([false, true, false, true]);
        // Scrolled by an odd count of rows, the stripe moves with the row, not the screen.
        await scrollTo(root, 24 * 7);
        const shown = rowsOf(root);
        expect(shown.map(row => row.classList.contains('ex-row-stripe'))).toEqual(shown.map(row => indexOf(row) % 2 === 1));
        expect(indexOf(shown[0] ?? root) % 2).toBe(1);
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, stripeRows: false });
        await painted();
        expect(striped(root).some(Boolean)).toBe(false);
        expect(() => { view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, stripeRows: 'yes' as unknown as boolean }); })
            .toThrow(/stripeRows is true or false/);
    });

    it('ADR-0038: a Placeholder wears the stripe its position will, and wears it again when stripes are turned on', async () => {
        const answers: ((page: GridPage<Trade>) => void)[] = [];
        const source = GridSource.fetch<Trade>(() => new Promise(resolve => { answers.push(resolve); }), { readAheadRows: 0 });
        const { root, view } = mount({ columns, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        answers[0]?.({ rows: trades(10), start: 0, totalCount: 5000 });
        await new Promise(resolve => { setTimeout(resolve, 0); });
        const waiting = (): HTMLElement[] => rowsOf(root).filter(row => row.classList.contains('ex-placeholder'));
        expect(waiting().length).toBeGreaterThan(2);
        expect(waiting().some(row => row.classList.contains('ex-row-stripe'))).toBe(false);
        view.update({ columns, source, rowHeight: 24, viewportHeight: 480, stripeRows: true });
        await painted();
        const placeholders = waiting();
        expect(placeholders.map(row => row.classList.contains('ex-row-stripe'))).toEqual(placeholders.map(row => indexOf(row) % 2 === 1));
        expect(placeholders.some(row => row.classList.contains('ex-row-stripe'))).toBe(true);
    });
});

describe('Row Kind', () => {
    it('ADR-0024: none given, every row is a detail row; given, each row wears the class of the role it was told', async () => {
        const rows = trades(100);
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(rowsOf(root).every(row => row.className === 'ex-row')).toBe(true);
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, rowKind: kindOf, stripeRows: true });
        await painted();
        expect(rowsOf(root).slice(0, 6).map(row => row.className)).toEqual([
            'ex-row ex-row-group', 'ex-row ex-row-total ex-row-stripe', 'ex-row', 'ex-row ex-row-stripe', 'ex-row',
            'ex-row ex-row-group ex-row-stripe',
        ]);
        expect(() => { view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, rowKind: 'group' as unknown as () => RowKind }); })
            .toThrow(/rowKind is a function of the row/);
    });

    it('ADR-0024, ADR-0003: the function\'s identity is the change signal: the same one paints nothing, a new one paints every row', async () => {
        const rows = trades(100);
        let groups = 0;
        const asked = (row: Trade): RowKind => (row.notional < groups * 1000 ? 'group' : 'detail');
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480, rowKind: asked });
        await painted();
        const before = rowsOf(root);
        // The same function, answering otherwise: nothing is painted again, as with cellState.
        groups = 3;
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, rowKind: asked });
        await painted();
        const after = rowsOf(root);
        expect(after.length).toBe(before.length);
        expect(after.every((row, i) => row === before[i])).toBe(true);
        expect(rowsOf(root).some(row => row.classList.contains('ex-row-group'))).toBe(false);
        // A new function is a new answer.
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, rowKind: row => asked(row) });
        await painted();
        expect(rowsOf(root).map(row => row.classList.contains('ex-row-group')).slice(0, 4)).toEqual([true, true, true, false]);
    });

    it('ADR-0043: a group or total row has no checkbox, and marking all leaves it out of the marks and the count', async () => {
        const marked: readonly GridColumn<Trade>[] = [markColumn<Trade>({ width: 40 }), ...columns];
        const rows = trades(10);
        const source = GridSource.from(rows);
        const { root, view } = mount({ columns: marked, source, rowHeight: 24, viewportHeight: 480, rowKind: kindOf });
        await painted();
        const boxes = rowsOf(root).map(row => row.querySelector('.ex-mark') !== null);
        expect(boxes).toEqual([false, false, true, true, true, false, false, true, true, true]);
        const header = q(root, '.ex-header .ex-mark');
        for (const type of ['mousedown', 'mouseup', 'click']) {
            header.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 }));
        }
        await painted();
        // Rows 0 and 5 are groups, 1 and 6 totals: six detail rows of ten.
        expect(root.querySelector('.ex-status .ex-mark-count')?.textContent).toBe('6 marked');
        expect(header.getAttribute('aria-checked')).toBe('true');
        expect(rows.filter(row => source.marks.isMarked(row)).map(row => row.notional / 1000)).toEqual([2, 3, 4, 7, 8, 9]);
        expect(source.marks.counts.rowsInResult).toBe(6);
        // Without a Row Kind, the same marks count every row, and whoever shows the counts hears that they moved.
        let heard = 0;
        const unsubscribe = source.marks.subscribe(() => { heard += 1; });
        view.update({ columns: marked, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(heard).toBeGreaterThan(0);
        expect(q(root, '.ex-header .ex-mark').getAttribute('aria-checked')).toBe('mixed');
        // A grid gone leaves no Row Kind of its own on the marks it held.
        view.update({ columns: marked, source, rowHeight: 24, viewportHeight: 480, rowKind: kindOf });
        await painted();
        expect(source.marks.counts.rowsInResult).toBe(6);
        view.destroy();
        expect(source.marks.counts.rowsInResult).toBe(10);
        unsubscribe();
    });
});
