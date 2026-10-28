// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the Row Headings and a hidden header (ADR-0050, item 1). The band beside the rows
// and its corner, the Heading Mark over it (ADR-0008, 2026-10-13), the presses and the drag that select whole rows,
// and the corner that selects all; a header taken away. What the browser paints of them is layer 3's.
import { afterEach, describe, expect, it } from 'vitest';
import type { GridSelection } from '../src/selection/selection';
import type { GridPage } from '../src/source/fetching-source';
import { GridSource } from '../src/source/grid-source';
import type { GridColumn } from '../src/dom/grid-column';
import { columns, mount, painted, q, rowsOf, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

// A band of 40 px, then Book 40-160 and Notional 160-260; rows of 24 px under a header of 24.
const headings = (row: number): string => String(row + 1);
const rowY = (row: number): number => (row * 24) + 5;

function pointer(root: HTMLElement, type: string, x: number, y: number, init: MouseEventInit, on = '.ex-viewport'): MouseEvent {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, ...init });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, on).dispatchEvent(event);
    return event;
}

/** A press on the Row Heading of `row`, at the same client position, which the drag's later events are placed from. */
function pressHeading(root: HTMLElement, row: number, init: MouseEventInit = {}): MouseEvent {
    return pointer(root, 'mousedown', 10, rowY(row), { button: 0, buttons: 1, clientX: 10, clientY: rowY(row), ...init });
}

/** A release over the Row Heading of `row`, where a Heading drag places it from its client position. */
function release(root: HTMLElement, row: number): void {
    pointer(root, 'mouseup', 10, rowY(row), { button: 0, clientX: 10, clientY: rowY(row) });
}

function clickCorner(root: HTMLElement): void {
    for (const type of ['mousedown', 'mouseup', 'click']) {
        pointer(root, type, 10, 10, { button: 0, buttons: type === 'mousedown' ? 1 : 0, clientX: 10, clientY: -14 }, '.ex-header');
    }
}

function heard(): { readonly selections: GridSelection[]; readonly onSelectionChanged: (selection: GridSelection) => void } {
    const selections: GridSelection[] = [];
    return { selections, onSelectionChanged: selection => { selections.push(selection); } };
}

const wholeRows = (top: number, count: number): unknown => [{ topRow: top, leftColumn: 0, rowCount: count, columnCount: 2 }];
const states = (root: HTMLElement): string[] => [...root.querySelectorAll('.ex-viewport > .ex-heading-state')].map(marker => marker.className);

describe('Row Headings', () => {
    it('ADR-0050: none given, no band; given, every row and the header start with it, at the width declared', async () => {
        const rows = trades(100);
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(root.querySelector('.ex-row-heading, .ex-headings-corner, .ex-heading-state')).toBeNull();
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 40 });
        await painted();
        const painted0 = rowsOf(root);
        expect(painted0.slice(0, 3).map(row => row.firstElementChild?.outerHTML)).toEqual(['1', '2', '3'].map(label =>
            `<div class="ex-row-heading" style="width: 40px" aria-hidden="true">${label}</div>`));
        expect(q(root, '.ex-header').firstElementChild?.outerHTML)
            .toBe('<div class="ex-headings-corner" aria-hidden="true" style="width: 40px"></div>');
        // A hidden marker follows every painted row, unmarked while nothing is selected (ADR-0008).
        expect(states(root)).toHaveLength(painted0.length);
        expect(new Set(states(root))).toEqual(new Set(['ex-heading-state']));
        // The band names no column: the first column starts after it.
        expect(q(root, `[id$='-r0c0']`).getAttribute('aria-colindex')).toBe('1');
        expect(() => { view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 0 }); })
            .toThrow(/rowHeadingWidth is a finite, positive number/);
        expect(() => { view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, rowHeadings: 'n' as never }); })
            .toThrow(/rowHeadings is a function/);
    });

    it('ADR-0050/0028: undeclared, the width is the wider of the first and the last row\'s labels, and grows with the result', async () => {
        const { root, view } = mount({ columns, rows: trades(9), rowHeight: 24, viewportHeight: 480, rowHeadings: headings });
        await painted();
        const width = (): number => Number(/width: ([\d.]+)px/.exec(String(q(root, '.ex-headings-corner').getAttribute('style')))?.[1]);
        const narrow = width();
        expect(narrow).toBeGreaterThan(0);
        view.update({ columns, rows: trades(10), rowHeight: 24, viewportHeight: 480, rowHeadings: headings });
        await painted();
        expect(width()).toBeGreaterThan(narrow);
    });

    it('ADR-0090: the band is put on a Device Pixel first, and each column edge is counted from there, as the C# grid counts it', async () => {
        // At 2.25 a band of 37.6 px is 84.6 Device Pixels, painted as 85. A column of 99 px (222.75) then ends at 307.75,
        // painted as 308: 223 Device Pixels wide. Counted from the 84.6 declared, it would end at 307 and be 222 wide.
        const wide: readonly GridColumn<Trade>[] = columns.map(column => ({ ...column, width: 99 }));
        const { root } = mount({ columns: wide, rows: trades(10), rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 37.6 },
            { devicePixelRatio: 2.25 });
        await painted();
        expect(q(root, '.ex-headings-corner').getAttribute('style')).toBe(`width: ${85 / 2.25}px`);
        expect(q(root, `[id$='-r0c0']`).getAttribute('style')).toBe(`width: ${223 / 2.25}px`);
    });

    it('ADR-0050/0003: a new function with the same labels paints no row again; a label that changed paints its row', async () => {
        const rows = trades(100);
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 40 });
        await painted();
        const before = rowsOf(root);
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, rowHeadings: row => headings(row), rowHeadingWidth: 40 });
        await painted();
        const same = rowsOf(root);
        expect(same.every((row, i) => row === before[i])).toBe(true);
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, rowHeadings: row => (row === 1 ? 'two' : headings(row)), rowHeadingWidth: 40 });
        await painted();
        const after = rowsOf(root);
        expect(after.map((row, i) => row === before[i]).slice(0, 3)).toEqual([true, false, true]);
        expect(after[1]?.firstElementChild?.textContent).toBe('two');
    });

    it('ADR-0050: a Placeholder still wears its position\'s heading', async () => {
        const answers: ((page: GridPage<Trade>) => void)[] = [];
        const source = GridSource.fetch<Trade>(() => new Promise(resolve => { answers.push(resolve); }), { readAheadRows: 0 });
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 40 });
        await painted();
        answers[0]?.({ rows: trades(10), start: 0, totalCount: 5000 });
        await new Promise(resolve => { setTimeout(resolve, 0); });
        const waiting = rowsOf(root).filter(row => row.classList.contains('ex-placeholder'));
        expect(waiting.length).toBeGreaterThan(0);
        expect(waiting.map(row => row.children.length)).toEqual(waiting.map(() => 1));
        expect(waiting[0]?.firstElementChild?.textContent).toBe('11');
    });

    it('ADR-0050 item 1: a press selects the row with the Focus in the first column on screen; Shift extends, Ctrl adds and takes out', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 40, ...listener });
        await painted();
        pressHeading(root, 2);
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual(wholeRows(2, 1));
        expect(listener.selections.at(-1)?.focus).toEqual({ row: 2, column: 0 });
        // The Heading Mark: the row is marked, and marked whole (ADR-0008, D1).
        expect(states(root).slice(1, 4)).toEqual(['ex-heading-state', 'ex-heading-state ex-heading-marked ex-heading-whole', 'ex-heading-state']);
        release(root, 2);
        pressHeading(root, 4, { shiftKey: true });
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual(wholeRows(2, 3));
        release(root, 4);
        pressHeading(root, 7, { ctrlKey: true });
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual([...wholeRows(2, 3) as object[], ...wholeRows(7, 1) as object[]]);
        release(root, 7);
        pressHeading(root, 7, { ctrlKey: true });
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual(wholeRows(2, 3));
        // A press on a cell beside the band is a cell's, as ever.
        release(root, 7);
        pointer(root, 'mousedown', 50, rowY(1), { button: 0, buttons: 1 });
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual([{ topRow: 1, leftColumn: 0, rowCount: 1, columnCount: 1 }]);
    });

    it('ADR-0008: the Heading Mark draws one rectangle per run of marked rows over the band, and goes with the Selection', async () => {
        const { root, view } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 40 });
        await painted();
        expect(root.querySelector('.ex-heading-marks-rows')).toBeNull();
        pressHeading(root, 2);
        release(root, 2);
        pressHeading(root, 6, { ctrlKey: true });
        release(root, 6);
        await painted();
        // Two ranges, two runs: what the union costs depends on how many ranges there are, never on their size.
        expect([...q(root, '.ex-heading-marks-rows').children].map(mark => `${mark.className}|${String(mark.getAttribute('style'))}`)).toEqual([
            'ex-heading-mark ex-heading-mark-row|left: 0; top: 48px; width: 40px; height: 24px',
            'ex-heading-mark ex-heading-mark-row|left: 0; top: 144px; width: 40px; height: 24px',
        ]);
        // The band taken away takes its mark with it, as the header's part goes with the header.
        view.update({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(root.querySelector('.ex-heading-marks-rows')).toBeNull();
    });

    it('ADR-0050 item 1: a column drag after a Row Heading drag extends by column again', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 40, ...listener });
        await painted();
        pressHeading(root, 1);
        pointer(root, 'mousemove', 10, rowY(3), { buttons: 1, clientX: 10, clientY: rowY(3) });
        release(root, 3);
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual(wholeRows(1, 3));
        // Then a press on the header of Book and a drag onto Notional: whole columns, never whole rows.
        pointer(root, 'mousedown', 100, 10, { button: 0, buttons: 1, clientX: 100, clientY: 10 }, '.ex-header');
        pointer(root, 'mousemove', 200, 10, { buttons: 1, clientX: 200, clientY: 10 }, '.ex-header');
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual([{ topRow: 0, leftColumn: 0, rowCount: 1000, columnCount: 2 }]);
    });

    it('ADR-0050 item 1: a drag from a Row Heading extends by row, over the band or over the cells', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 40, ...listener });
        await painted();
        pressHeading(root, 1);
        // Over a cell three rows down: still whole rows, never a cell range.
        pointer(root, 'mousemove', 200, rowY(4), { buttons: 1, clientX: 200, clientY: rowY(4) });
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual(wholeRows(1, 4));
        // The Column Headings are what a move over the header band is over: a Row Heading drag learns nothing there.
        pointer(root, 'mousemove', 200, 10, { buttons: 1, clientX: 200, clientY: rowY(10) }, '.ex-header');
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual(wholeRows(1, 4));
        pointer(root, 'mouseup', 200, rowY(4), { button: 0, clientX: 200, clientY: rowY(4) });
        pointer(root, 'mousemove', 200, rowY(8), { buttons: 0, clientX: 200, clientY: rowY(8) });
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual(wholeRows(1, 4));
    });

    it('ADR-0050 item 1: a Row Heading drag held at the bottom edge scrolls down only, and the Extent follows the row under it', async () => {
        const wide = Array.from({ length: 12 }, (_, i) => ({ name: `C${i}`, type: 'number' as const, value: (trade: Trade) => trade.notional + i, width: 100 }));
        const listener = heard();
        const { root, told } = mount({
            columns: wide, rows: trades(1000), rowHeight: 24, viewportHeight: 480, viewportWidth: 400, rowHeadings: headings, rowHeadingWidth: 40, ...listener,
        });
        await painted();
        pressHeading(root, 0);
        // By the Viewport's bottom right corner, in both edge bands.
        pointer(root, 'mousemove', 3, 3, { buttons: 1, clientX: 395, clientY: rowY(0) + 446 });
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual([{ topRow: 0, leftColumn: 0, rowCount: 19, columnCount: 12 }]);
        const scroller = q(root, '.ex-scroller');
        told.clock.advance(50);
        await painted();
        expect(scroller.scrollLeft).toBe(0);
        expect(scroller.scrollTop).toBeGreaterThan(0);
        expect(listener.selections.at(-1)?.ranges[0]?.rowCount).toBeGreaterThan(19);
    });

    it('ADR-0050: a click on the corner selects all with the Focus on the top-left cell on screen, and marks the corner', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 40, ...listener });
        await painted();
        expect(q(root, '.ex-headings-corner').className).toBe('ex-headings-corner');
        clickCorner(root);
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual([{ topRow: 0, leftColumn: 0, rowCount: 1000, columnCount: 2 }]);
        expect(listener.selections.at(-1)?.focus).toEqual({ row: 0, column: 0 });
        expect(q(root, '.ex-headings-corner').className).toBe('ex-headings-corner ex-headings-corner-marked');
    });

    it('ADR-0050: a double click on a Row Heading opens no edit', async () => {
        const editable = columns.map(column => ({ ...column, editable: true }));
        const { root } = mount({ columns: editable, rows: trades(10), rowHeight: 24, viewportHeight: 480, rowHeadings: headings, rowHeadingWidth: 40, onEdit: () => undefined });
        await painted();
        pressHeading(root, 1);
        pointer(root, 'dblclick', 10, rowY(1), { button: 0 });
        await painted();
        expect(root.querySelector('.ex-editor')).toBeNull();
    });
});

describe('A hidden header', () => {
    it('ADR-0050 item 1: the band goes, the rows start at the Viewport\'s top and are counted from 1, and it comes back', async () => {
        const rows = trades(100);
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480, hideHeader: true });
        await painted();
        expect(root.querySelector('.ex-header')).toBeNull();
        expect(q(root, '.ex-viewport').getAttribute('style')).toMatch(/^top: 0px; transform/);
        expect(rowsOf(root)[0]?.getAttribute('aria-rowindex')).toBe('1');
        expect(root.getAttribute('aria-rowcount')).toBe('100');
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(q(root, '.ex-header').querySelectorAll('[role="columnheader"]')).toHaveLength(2);
        expect(rowsOf(root)[0]?.getAttribute('aria-rowindex')).toBe('2');
    });
});
