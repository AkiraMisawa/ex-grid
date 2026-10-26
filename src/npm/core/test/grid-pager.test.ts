// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the pager (ADR-0015). A page is the context the grid shows: the geometry runs over
// it alone, so a drag stops at its edge; the keyboard crosses pages; Ctrl+A and the corner select the page and the
// status line offers the whole result; the header's checkbox lines up the page and the whole result is offered too.
import { afterEach, describe, expect, it } from 'vitest';
import { markColumn, type GridColumn } from '../src/dom/grid-column';
import type { RowMarkIntent } from '../src/rows/row-marks';
import { GridSource } from '../src/source/grid-source';
import { PageGeometry } from '../src/viewport/page-geometry';
import { columns, mount, painted, q, rowsOf, scrollTo, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const prefix = (root: HTMLElement): string => root.id.replace(/root$/, '');
const pager = (root: HTMLElement): string => q(root, '.ex-pager').textContent;
// The status line's words; the line is taken out while it says nothing, which reads as no words.
const status = (root: HTMLElement): string => {
    const line = root.querySelector('.ex-status');
    return line === null ? '' : line.textContent;
};
const focus = (root: HTMLElement): string | null => root.getAttribute('aria-activedescendant');
const firstRow = (root: HTMLElement): string | undefined => rowsOf(root)[0]?.querySelector('[role="gridcell"]')?.id;
const paintedRow = (root: HTMLElement, row: number): boolean => root.querySelector(`[id="${prefix(root)}r${row}c0"]`) !== null;

function key(root: HTMLElement, name: string, init: KeyboardEventInit = {}): void {
    root.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init }));
}

function press(root: HTMLElement, x: number, y: number): void {
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        q(root, '.ex-viewport').dispatchEvent(event);
    }
}

function click(target: Element): void {
    for (const type of ['mousedown', 'mouseup', 'click']) {
        target.dispatchEvent(new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 }));
    }
}

const button = (root: HTMLElement, text: RegExp): HTMLElement => {
    const found = [...root.querySelectorAll<HTMLElement>('button')].find(each => text.test(each.textContent));
    if (found === undefined) throw new Error(`No button says ${String(text)}.`);
    return found;
};

describe('the pager', () => {
    it('ADR-0015: the geometry runs over the page alone, in the whole result\'s positions', () => {
        const page = new PageGeometry(50, { rowHeightPx: 24, viewportHeightPx: 240, totalRowCount: 20 });
        expect([page.sliceAt(0), page.offsetPxOf(55), page.rowAt(10_000), page.rowAt(0), page.scrollHeightPx])
            .toEqual([{ start: 50, count: 11 }, 120, 69, 50, 480]);
        expect(() => page.scrollTopToReveal(49, 0)).toThrow(RangeError);
    });

    it('ADR-0015: a page holds at least one row', () => {
        expect(() => mount({ columns, rows: trades(10), pageSize: 0 })).toThrow(/at least one row/);
        expect(() => mount({ columns, rows: trades(10), pageSize: 1.5 })).toThrow(/at least one row/);
    });

    it('ADR-0015: the pager turns pages from their top, each button disabled at its end, and the scrollbar spans the page', async () => {
        const { root } = mount({ columns, rows: trades(120), rowHeight: 24, viewportHeight: 480, pageSize: 50, showSelectionSummary: false });
        await painted();
        const [back, on] = [...q(root, '.ex-pager').querySelectorAll('button')];
        expect([pager(root), back?.disabled, on?.disabled]).toEqual(['‹1 / 3›', true, false]);
        expect(q(root, '.ex-spacer').style.height).toBe(`${24 + (50 * 24)}px`);

        q(root, '.ex-scroller').scrollTop = 300;
        q(root, '.ex-scroller').dispatchEvent(new Event('scroll'));
        await painted();
        on?.click();
        await painted();
        expect([pager(root), q(root, '.ex-scroller').scrollTop, firstRow(root)]).toEqual(['‹2 / 3›', 0, `${prefix(root)}r50c0`]);
        on?.click();
        await painted();
        expect([pager(root), back?.disabled, on?.disabled, q(root, '.ex-spacer').style.height]).toEqual(['‹3 / 3›', false, true, `${24 + (20 * 24)}px`]);
        expect(root.ownerDocument.activeElement).toBe(root);
    });

    it('ADR-0015: the keyboard crosses pages, and an extension spanning every row turns none', async () => {
        const { root } = mount({ columns, rows: trades(120), rowHeight: 24, viewportHeight: 480, pageSize: 50, showSelectionSummary: false });
        await painted();
        press(root, 10, 5);
        await painted();
        key(root, 'End', { ctrlKey: true });
        await painted();
        expect([focus(root), pager(root)]).toEqual([`${prefix(root)}r119c1`, '‹3 / 3›']);
        key(root, 'Home', { ctrlKey: true });
        await painted();
        key(root, 'ArrowDown', { ctrlKey: true, shiftKey: true });
        await painted();
        expect(pager(root)).toBe('‹1 / 3›');
        key(root, 'PageDown');
        await painted();
        expect(pager(root)).toBe('‹1 / 3›');
        for (let row = 0; row < 3; row++) key(root, 'PageDown');
        await painted();
        expect([focus(root), pager(root)]).toEqual([`${prefix(root)}r76c0`, '‹2 / 3›']);
    });

    it('ADR-0015: Ctrl+A selects the page, and the status line counts it and offers the whole result', async () => {
        const { root } = mount({ columns, rows: trades(120), rowHeight: 24, viewportHeight: 480, pageSize: 50, showSelectionSummary: false });
        await painted();
        press(root, 10, 5);
        await painted();
        expect(status(root)).toBe('1 cells selectedSelect all 120 rows');
        q(root, '.ex-pager button:last-child').click();
        await painted();
        expect(status(root)).toBe('1 cells selected (outside the visible range)Go to selectionSelect all 120 rows');

        key(root, 'a', { ctrlKey: true });
        await painted();
        expect([status(root), focus(root)]).toEqual(['100 cells selectedSelect all 120 rows', `${prefix(root)}r50c0`]);
        button(root, /^Select all/).click();
        await painted();
        expect([status(root), pager(root)]).toEqual(['240 cells selectedSelect all 120 rows', '‹2 / 3›']);
    });

    it('ADR-0015: Go to selection turns to the Focus\'s page', async () => {
        const { root } = mount({ columns, rows: trades(120), rowHeight: 24, viewportHeight: 480, pageSize: 50, showSelectionSummary: false });
        await painted();
        press(root, 10, 5 + (3 * 24));
        await painted();
        q(root, '.ex-pager button:last-child').click();
        await painted();
        button(root, /^Go to selection$/).click();
        await painted();
        expect([pager(root), status(root)]).toEqual(['‹1 / 3›', '1 cells selectedSelect all 120 rows']);
    });

    it('ADR-0015 ADR-0036: the Context Menu from the keyboard turns to the Focus\'s page and opens over it', async () => {
        const { root } = mount({ columns, rows: trades(120), rowHeight: 24, viewportHeight: 480, pageSize: 50, showSelectionSummary: false });
        await painted();
        press(root, 10, 5 + (3 * 24));
        await painted();
        q(root, '.ex-pager button:last-child').click();
        await painted();
        key(root, 'F10', { shiftKey: true });
        await painted();
        expect([pager(root), root.querySelector('[role="menu"]') === null]).toEqual(['‹1 / 3›', false]);
    });

    it('ADR-0015 ADR-0034: a turn commits an open edit first, and a Reject keeps the page and the editor', async () => {
        const rejecting = columns.map(column => ({ ...column, editable: true, validate: () => ({ kind: 'reject', message: 'not a number' }) as const }));
        const held = mount({ columns: rejecting, rows: trades(120), rowHeight: 24, viewportHeight: 480, pageSize: 50, showSelectionSummary: false, onEdit: () => undefined });
        await painted();
        press(held.root, 130, 5);
        await painted();
        key(q(held.root, '.ex-key-field'), 'F2');
        await painted();
        q(held.root, '.ex-pager button:last-child').click();
        await painted();
        expect([pager(held.root), held.root.querySelector('.ex-editor') === null, q(held.root, '.ex-message').textContent])
            .toEqual(['‹1 / 3›', false, 'not a number']);

        const committed: unknown[] = [];
        const editable = columns.map(column => ({ ...column, editable: true }));
        const { root } = mount({ columns: editable, rows: trades(120), rowHeight: 24, viewportHeight: 480, pageSize: 50, showSelectionSummary: false, onEdit: intent => { committed.push(intent); } });
        await painted();
        press(root, 130, 5);
        await painted();
        key(q(root, '.ex-key-field'), 'F2');
        await painted();
        const editor = q(root, '.ex-editor') as HTMLInputElement;
        editor.value = '5';
        editor.dispatchEvent(new Event('input', { bubbles: true }));
        q(root, '.ex-pager button:last-child').click();
        await painted();
        expect([pager(root), root.querySelector('.ex-editor'), committed.length]).toEqual(['‹2 / 3›', null, 1]);
    });

    it('ADR-0015 ADR-0034: a message on the last row of a page goes when the next page is shown', async () => {
        const { root, told } = mount({ columns, rows: trades(120), rowHeight: 24, viewportHeight: 480, pageSize: 10, showSelectionSummary: false, cellMessage: () => 'out of range' });
        await painted();
        press(root, 130, 5 + (9 * 24));
        await painted();
        told.clock.advance(300);
        await painted();
        expect(q(root, '.ex-message').textContent).toBe('out of range');
        q(root, '.ex-pager button:last-child').click();
        await painted();
        expect([pager(root), root.querySelector('.ex-message')]).toEqual(['‹2 / 12›', null]);
    });

    it('ADR-0015: a new page size keeps the first row shown at the top, and taking the pager away keeps the page\'s first', async () => {
        const options = { columns, rows: trades(120), rowHeight: 24, viewportHeight: 480, showSelectionSummary: false };
        const { root, view, told } = mount(options);
        await painted();
        // A jump that far is a fling, painted once the scroll has settled (ADR-0012).
        await scrollTo(root, 70 * 24);
        told.clock.advance(1000);
        await painted();
        expect(firstRow(root)).toBe(`${prefix(root)}r70c0`);
        view.update({ ...options, pageSize: 50 });
        await painted();
        expect([pager(root), firstRow(root)]).toEqual(['‹2 / 3›', `${prefix(root)}r70c0`]);
        view.update(options);
        await painted();
        expect([root.querySelector('.ex-pager'), firstRow(root)]).toEqual([null, `${prefix(root)}r50c0`]);
    });

    it('ADR-0015: a result that shrinks under the page shown shows its new last page from the top', async () => {
        const options = { columns, rowHeight: 24, viewportHeight: 480, pageSize: 50, showSelectionSummary: false };
        const { root, view } = mount({ ...options, rows: trades(150) });
        await painted();
        q(root, '.ex-pager button:last-child').click();
        q(root, '.ex-pager button:last-child').click();
        await painted();
        await scrollTo(root, 10 * 24);
        expect([pager(root), firstRow(root)]).toEqual(['‹3 / 3›', `${prefix(root)}r110c0`]);
        view.update({ ...options, rows: trades(60) });
        await painted();
        expect([pager(root), q(root, '.ex-scroller').scrollTop, firstRow(root)]).toEqual(['‹2 / 2›', 0, `${prefix(root)}r50c0`]);
    });

    it('ADR-0015 ADR-0036: the Context Menu from the keyboard reveals the Focus on the page it turns to', async () => {
        const { root, told } = mount({ columns, rows: trades(300), rowHeight: 24, viewportHeight: 480, pageSize: 100, showSelectionSummary: false });
        await painted();
        await scrollTo(root, 70 * 24);
        told.clock.advance(1000);
        await painted();
        press(root, 10, 5 + (15 * 24));
        await painted();
        expect(focus(root)).toBe(`${prefix(root)}r85c0`);
        q(root, '.ex-pager button:last-child').click();
        await painted();
        key(root, 'F10', { shiftKey: true });
        await painted();
        expect([pager(root), paintedRow(root, 85), root.querySelector('[role="menu"]') === null]).toEqual(['‹1 / 3›', true, false]);
    });

    it('ADR-0015: a new page size shows the page that holds the first row of the page shown', async () => {
        const { root, view } = mount({ columns, rows: trades(120), rowHeight: 24, viewportHeight: 480, pageSize: 50, showSelectionSummary: false });
        await painted();
        q(root, '.ex-pager button:last-child').click();
        q(root, '.ex-pager button:last-child').click();
        await painted();
        view.update({ columns, rows: trades(120), rowHeight: 24, viewportHeight: 480, pageSize: 10, showSelectionSummary: false });
        await painted();
        expect([pager(root), firstRow(root)]).toEqual(['‹11 / 12›', `${prefix(root)}r100c0`]);
    });

    it('ADR-0015 ADR-0043: the header\'s checkbox lines up the page it was pressed on, and the whole result is the explicit step past it', async () => {
        const mark: GridColumn<Trade> = markColumn<Trade>({ width: 40 });
        const source = GridSource.from(trades(120));
        const intents: RowMarkIntent<Trade>[] = [];
        const { marks } = source;
        const apply = marks.onMarkIntent.bind(marks);
        marks.onMarkIntent = intent => {
            intents.push(intent);
            return apply(intent);
        };
        const { root } = mount({ columns: [mark, ...columns], source, rowHeight: 24, viewportHeight: 480, pageSize: 50, showSelectionSummary: false });
        await painted();
        q(root, '.ex-pager button:last-child').click();
        await painted();
        const header = q(root, '.ex-header .ex-mark');
        expect(header.getAttribute('aria-checked')).toBe('false');
        click(header);
        await painted();
        expect(intents.at(-1)).toMatchObject({ kind: 'positions', ranges: [{ start: 50, count: 50 }] });
        expect(header.getAttribute('aria-checked')).toBe('true');
        expect(status(root)).toBe('50 markedMark all 120 rows');

        click(q(root, '.ex-mark-result'));
        await painted();
        expect(intents.at(-1)).toMatchObject({ kind: 'allRows', marked: true });
        expect(status(root)).toBe('120 markedMark all 120 rows');
    });
});
