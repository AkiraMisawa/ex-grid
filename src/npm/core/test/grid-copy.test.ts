// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): a copy from the grid (ADR-0005). The browser's copy command fires the copy
// event on the root; a Selection the Window holds is written on the event, one past it by the asynchronous
// route, and a refused one writes nothing and is said by name.
import { describe, expect, it } from 'vitest';
import { Temporal } from 'temporal-polyfill';
import { defined } from '../src/must';
import type { GridColumn } from '../src/dom/grid-column';
import type { GridRefusal } from '../src/dom/grid-refusal';
import type { GridPage } from '../src/source/fetching-source';
import { GridSource } from '../src/source/grid-source';
import { mount, painted, q, trades, type Trade } from './grid-harness';

interface Dated extends Trade {
    readonly confirmed: boolean;
    readonly tradeDate: Temporal.PlainDateTime;
}

// Notional shows two places and is too narrow for them: what is painted is ####, what is copied never is.
const columns: readonly GridColumn<Dated>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120 },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 40, format: value => (value as number).toFixed(2) },
    { name: 'Confirmed', type: 'boolean', value: trade => trade.confirmed, width: 80 },
    { name: 'TradeDate', type: 'date', value: trade => trade.tradeDate, width: 160 },
];

const firstDay = Temporal.PlainDateTime.from('2026-01-01T09:30:00');
const dated = (count: number): readonly Dated[] =>
    trades(count).map((trade, i) => ({ ...trade, notional: trade.notional + 0.5, confirmed: i % 2 === 0, tradeDate: firstDay.add({ days: i }) }));

// Rows of 24 px under a header of 24.
const rowY = (row: number): number => (row * 24) + 5;

function press(root: HTMLElement, x: number, y: number, init: MouseEventInit = {}): void {
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0, ...init });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        q(root, '.ex-viewport').dispatchEvent(event);
    }
}

function key(root: HTMLElement, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    root.dispatchEvent(event);
    return event;
}

/** The copy event the browser's copy command fires, with the data it carries out. */
function copy(target: HTMLElement): { readonly event: Event; readonly data: Map<string, string> } {
    const data = new Map<string, string>();
    const event = new Event('copy', { bubbles: true, cancelable: true });
    Object.defineProperty(event, 'clipboardData', { value: { setData: (type: string, value: string) => { data.set(type, value); } } });
    target.dispatchEvent(event);
    return { event, data };
}

function heard(): { readonly refusals: GridRefusal[]; readonly onRefused: (refusal: GridRefusal) => void } {
    const refusals: GridRefusal[] = [];
    return { refusals, onRefused: refusal => { refusals.push(refusal); } };
}

const settle = (): Promise<void> => new Promise(resolve => { setTimeout(resolve, 0); });

describe('a copy from the grid', () => {
    it('ADR-0005 CP-4/CP-5: a Selection the Window holds is written on the copy event, the shown text and the raw value, never ####', async () => {
        const listener = heard();
        const { root, told } = mount({ columns, rows: dated(100), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, 130, rowY(0));
        key(root, 'ArrowDown', { shiftKey: true });
        key(root, 'ArrowRight', { shiftKey: true });
        key(root, 'ArrowRight', { shiftKey: true });
        await painted();
        expect(q(root, '[id$="r1c1"]').textContent).toMatch(/^#+$/);

        const { event, data } = copy(root);
        expect(event.defaultPrevented).toBe(true);
        expect(data.get('text/plain')).toBe('0.50\tTrue\t2026-01-01 09:30:00\r\n1000.50\tFalse\t2026-01-02 09:30:00\r\n');
        expect(data.get('text/html')).toBe('<table data-ex-grid="invariant">'
            + '<tr><td>0.5</td><td>TRUE</td><td>2026-01-01T09:30:00</td></tr>'
            + '<tr><td>1000.5</td><td>FALSE</td><td>2026-01-02T09:30:00</td></tr></table>');
        // The event route asks no permission: the asynchronous one was never taken.
        expect([told.clipboard, told.writing, listener.refusals]).toEqual([[], 0, []]);
    });

    it('ADR-0005 CP-1/CP-3: a misaligned Selection writes nothing, says why in the live region and is heard by name', async () => {
        const listener = heard();
        const { root, told } = mount({ columns, rows: dated(100), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, 130, rowY(0));
        press(root, 210, rowY(1), { ctrlKey: true });
        await painted();

        const { event, data } = copy(root);
        // Left alone, the default copy of a user-select: none root carries nothing: the clipboard keeps what it held.
        expect([event.defaultPrevented, data.size, told.clipboard]).toEqual([false, 0, []]);
        expect(listener.refusals).toEqual([{ kind: 'copy', reason: 'misalignedShape' }]);
        expect(q(root, '.ex-announce').textContent)
            .toBe('Nothing was copied: the selected ranges do not line up into one block. Select ranges that share their rows or their columns.');
    });

    it('ADR-0005: no Selection, and one past the cap, are refused; a Selection exactly on the cap copies', async () => {
        const listener = heard();
        const options = { columns, rows: dated(100), rowHeight: 24, viewportHeight: 480, copyCellCap: 2, ...listener } as const;
        const { root, view } = mount(options);
        await painted();
        expect(copy(root).data.size).toBe(0);
        press(root, 10, rowY(0));
        key(root, 'ArrowDown', { shiftKey: true });
        await painted();
        expect(copy(root).data.get('text/plain')).toBe('Book-0\r\nBook-1\r\n');
        key(root, 'ArrowDown', { shiftKey: true });
        await painted();
        expect(copy(root).data.size).toBe(0);
        expect(listener.refusals).toEqual([{ kind: 'copy', reason: 'emptySelection' }, { kind: 'copy', reason: 'tooLarge' }]);
        expect(q(root, '.ex-announce').textContent).toBe('Nothing was copied: the selection holds more cells than one copy carries. Export the data instead.');
        // A cap below one cell would switch copying off, which the cap is not for.
        expect(() => { view.update({ ...options, copyCellCap: 0 }); }).toThrow(/copyCellCap is a whole number of at least one cell/);
    });

    it('ADR-0005 CP-7: a Selection past the Window is gathered from the source and written whole by the asynchronous route', async () => {
        const answers: ((page: GridPage<Dated>) => void)[] = [];
        const all = dated(5000);
        const source = GridSource.fetch<Dated>(() => new Promise(resolve => { answers.push(resolve); }), { readAheadRows: 0 });
        const { root, told } = mount({ columns, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        defined(answers.shift(), 'the first question')({ rows: all.slice(0, 40), start: 0, totalCount: 5000 });
        await settle();
        press(root, 10, rowY(0));
        key(root, 'ArrowDown', { ctrlKey: true, shiftKey: true });
        await painted();
        answers.length = 0;

        const { event, data } = copy(root);
        expect([event.defaultPrevented, data.size]).toEqual([true, 0]);
        await painted();
        // Writing, the copy holds the Settled Mark (ADR-0220).
        expect([told.writing, root.hasAttribute('data-ex-settled')]).toEqual([1, false]);
        defined(answers.shift(), 'the copy\'s question')({ rows: all, start: 0, totalCount: 5000 });
        await settle();
        const written = defined(told.clipboard[0], 'the copy written');
        const lines = written.text.split('\r\n');
        expect(lines).toHaveLength(5001);
        expect([lines[0], lines[4999]]).toEqual(['Book-0', 'Book-4999']);
        expect(told.writing).toBe(0);
    });

    it('ADR-0005: rows the source answers short refuse the copy whole, and a write the browser refuses is said too (CP-23)', async () => {
        const listener = heard();
        const answers: ((page: GridPage<Dated>) => void)[] = [];
        const all = dated(5000);
        const source = GridSource.fetch<Dated>(() => new Promise(resolve => { answers.push(resolve); }), { readAheadRows: 0 });
        const { root, told } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        defined(answers.shift(), 'the first question')({ rows: all.slice(0, 40), start: 0, totalCount: 5000 });
        await settle();
        press(root, 10, rowY(0));
        key(root, 'ArrowDown', { ctrlKey: true, shiftKey: true });
        await painted();
        answers.length = 0;

        copy(root);
        await painted();
        // The result ended early: the rows it has are the truth, and a copy of them would be a part.
        defined(answers.shift(), 'the copy\'s question')({ rows: all.slice(0, 4000), start: 0, totalCount: 4000 });
        await settle();
        expect([told.clipboard, told.writing, listener.refusals]).toEqual([[], 0, [{ kind: 'copy', reason: 'rowsUnavailable' }]]);

        told.clipboardRefused = true;
        press(root, 10, rowY(0));
        key(root, 'ArrowDown', { shiftKey: true });
        await painted();
        // Within the Window the event route writes, and the browser never refuses it: a copy past the Window
        // takes the asynchronous route, which the browser may.
        key(root, 'ArrowDown', { ctrlKey: true, shiftKey: true });
        await painted();
        copy(root);
        await painted();
        defined(answers.at(-1), 'the second copy\'s question')({ rows: all, start: 0, totalCount: 5000 });
        await settle();
        expect(listener.refusals.at(-1)).toEqual({ kind: 'copy', reason: 'clipboardUnavailable' });
        expect(q(root, '.ex-announce').textContent).toBe('Nothing was copied: the browser refused the clipboard, which still holds what it held before.');
    });

    it('ADR-0005, spine 6: a newer copy supersedes one still gathering, and the order moving under a gather refuses it', async () => {
        const listener = heard();
        const answers: ((page: GridPage<Dated>) => void)[] = [];
        const all = dated(5000);
        const source = GridSource.fetch<Dated>(() => new Promise(resolve => { answers.push(resolve); }), { readAheadRows: 0 });
        const { root, told } = mount({ columns, source, rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        defined(answers.shift(), 'the first question')({ rows: all.slice(0, 40), start: 0, totalCount: 5000 });
        await settle();
        press(root, 10, rowY(0));
        key(root, 'ArrowDown', { ctrlKey: true, shiftKey: true });
        await painted();
        answers.length = 0;
        copy(root);
        await painted();
        const slow = defined(answers.shift(), 'the slow copy\'s question');

        // A cell copied meanwhile is written on the event; the slow copy's rows, arriving after it, write nothing.
        press(root, 10, rowY(1));
        await painted();
        expect(copy(root).data.get('text/plain')).toBe('Book-1\r\n');
        slow({ rows: all, start: 0, totalCount: 5000 });
        await settle();
        expect([told.clipboard, told.writing, listener.refusals]).toEqual([[], 0, []]);

        // A Sort asked for while a copy gathers moves the rows it was taken over: refused, never two orders.
        key(root, 'ArrowDown', { ctrlKey: true, shiftKey: true });
        await painted();
        answers.length = 0;
        copy(root);
        await painted();
        const gathering = defined(answers.shift(), 'the copy\'s question');
        source.setSorts([{ column: 'Book', direction: 'descending' }]);
        gathering({ rows: all, start: 0, totalCount: 5000 });
        await settle();
        expect([told.clipboard, listener.refusals]).toEqual([[], [{ kind: 'copy', reason: 'rowsUnavailable' }]]);
    });

    it('A11Y-16: a refusal said again is emptied and said in the next frame, and stands down the Selection\'s description', async () => {
        const listener = heard();
        const { root, told } = mount({ columns, rows: dated(100), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, 130, rowY(0));
        press(root, 210, rowY(1), { ctrlKey: true });
        await painted();
        copy(root);
        const words = 'Nothing was copied: the selected ranges do not line up into one block. Select ranges that share their rows or their columns.';
        // The description of the two ranges, owed after the settle delay, never says over the refusal.
        told.clock.advance(1000);
        expect(q(root, '.ex-announce').textContent).toBe(words);
        copy(root);
        expect([q(root, '.ex-announce').textContent, root.hasAttribute('data-ex-settled')]).toEqual(['', false]);
        told.frame();
        expect([q(root, '.ex-announce').textContent, root.hasAttribute('data-ex-settled')]).toEqual([words, true]);
        expect(listener.refusals).toHaveLength(2);
    });

    it('ADR-0005/0020: a copy aimed at something inside the root is not the grid\'s', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: dated(10), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, 10, rowY(0));
        const { event, data } = copy(q(root, '.ex-header'));
        expect([event.defaultPrevented, data.size, listener.refusals]).toEqual([false, 0, []]);
    });
});

const dashes = (root: HTMLElement): HTMLElement[] => [...root.querySelectorAll<HTMLElement>('.ex-copied-range')];
const escapeLeaves = (root: HTMLElement): boolean => q(root, '[data-ex-answered]').hasAttribute('data-ex-escape-leaves');

describe('the Copied Range', () => {
    it('ADR-0170 CP-26/CP-28: a copy that lands is outlined over the Focus; Escape takes the outline away, and only the next Escape leaves', async () => {
        const { root } = mount({ columns, rows: dated(100), rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 130, rowY(0));
        key(root, 'ArrowDown', { shiftKey: true });
        await painted();
        expect([dashes(root), escapeLeaves(root)]).toEqual([[], true]);

        copy(root);
        await painted();
        // After the Focus, so the dashes lie over the Selection's outline the copy shares.
        const layer = q(root, '.ex-selection');
        expect(dashes(root)).toEqual([layer.lastElementChild]);
        expect(layer.lastElementChild?.previousElementSibling?.classList.contains('ex-focus')).toBe(true);
        expect(escapeLeaves(root)).toBe(false);

        // The first Escape peels the outline and keeps the grid's keys: Tab still moves inside the Selection.
        key(root, 'Escape');
        await painted();
        expect([dashes(root), escapeLeaves(root)]).toEqual([[], true]);
        expect(key(root, 'Tab').defaultPrevented).toBe(true);
        key(root, 'Escape');
        expect(key(root, 'Tab').defaultPrevented).toBe(false);
    });

    it('ADR-0170 CP-27: the grid\'s own write is counted and keeps the outline, any other change drops it; a refused copy leaves it', async () => {
        const { root, told } = mount({ columns, rows: dated(100), rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 10, rowY(0));
        copy(root);
        await painted();
        told.clipboardChanged();
        await painted();
        expect(dashes(root)).toHaveLength(1);

        // A Selection that does not line up is refused: the clipboard still holds the copy, outlined.
        press(root, 130, rowY(2), { ctrlKey: true });
        await painted();
        expect(copy(root).data.size).toBe(0);
        await painted();
        expect(dashes(root)).toHaveLength(1);

        // Another application's write, another grid's, a text field's.
        told.clipboardChanged();
        await painted();
        expect(dashes(root)).toEqual([]);
    });

    it('ADR-0170 CP-29/CP-30: a copied cell that comes to read otherwise drops the outline; a row replaced with the same copied text keeps it', async () => {
        const rows = dated(100);
        const source = GridSource.from(rows);
        const { root } = mount({ columns, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 10, rowY(0));
        copy(root);
        await painted();

        // Another instance whose Book, the copied cell, reads as it did.
        const first = defined(rows[0], 'the first row');
        const renotioned = { ...first, notional: 1 };
        source.replaceRow(first, renotioned);
        await painted();
        expect(dashes(root)).toHaveLength(1);

        source.replaceRow(renotioned, { ...renotioned, book: 'Book-moved' });
        await painted();
        expect(dashes(root)).toEqual([]);
    });

    it('ADR-0170 / ADR-0011: the order moving or the columns changing drops the outline, as it drops the Selection', async () => {
        const source = GridSource.from(dated(100));
        const options = { columns, source, rowHeight: 24, viewportHeight: 480 } as const;
        const { root, view } = mount(options);
        await painted();
        press(root, 10, rowY(0));
        copy(root);
        await painted();
        source.setSorts([{ column: 'Book', direction: 'descending' }]);
        await painted();
        expect(dashes(root)).toEqual([]);

        press(root, 10, rowY(0));
        copy(root);
        await painted();
        view.update({ ...options, columns: columns.toReversed() });
        await painted();
        expect(dashes(root)).toEqual([]);
    });

    it('ADR-0170: each rectangle of a copy of several ranges is outlined', async () => {
        const { root } = mount({ columns, rows: dated(100), rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 10, rowY(0));
        press(root, 10, rowY(2), { ctrlKey: true });
        copy(root);
        await painted();
        expect(dashes(root)).toHaveLength(2);
    });

    it('ADR-0170 CP-7: a copy past the Window is outlined only once its write lands', async () => {
        const answers: ((page: GridPage<Dated>) => void)[] = [];
        const all = dated(5000);
        const source = GridSource.fetch<Dated>(() => new Promise(resolve => { answers.push(resolve); }), { readAheadRows: 0 });
        const { root, told } = mount({ columns, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        defined(answers.shift(), 'the first question')({ rows: all.slice(0, 40), start: 0, totalCount: 5000 });
        await settle();
        press(root, 10, rowY(0));
        key(root, 'ArrowDown', { ctrlKey: true, shiftKey: true });
        await painted();
        answers.length = 0;

        copy(root);
        await painted();
        expect(dashes(root)).toEqual([]);
        defined(answers.shift(), 'the copy\'s question')({ rows: all, start: 0, totalCount: 5000 });
        await settle();
        expect([told.clipboard.length, told.writing, dashes(root).length]).toEqual([1, 0, 1]);
    });

    it('ADR-0170 CP-23: a write the browser refuses gives back its count, so the next change from elsewhere still drops an outline', async () => {
        const answers: ((page: GridPage<Dated>) => void)[] = [];
        const all = dated(5000);
        const source = GridSource.fetch<Dated>(() => new Promise(resolve => { answers.push(resolve); }), { readAheadRows: 0 });
        const { root, told } = mount({ columns, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        defined(answers.shift(), 'the first question')({ rows: all.slice(0, 40), start: 0, totalCount: 5000 });
        await settle();
        press(root, 10, rowY(0));
        key(root, 'ArrowDown', { ctrlKey: true, shiftKey: true });
        await painted();
        answers.length = 0;
        told.clipboardRefused = true;
        copy(root);
        await painted();
        defined(answers.shift(), 'the copy\'s question')({ rows: all, start: 0, totalCount: 5000 });
        await settle();
        expect([told.clipboard, dashes(root)]).toEqual([[], []]);

        // Back in the Window: written on the event, counted once, and reported once.
        key(root, 'ArrowUp', { ctrlKey: true });
        await painted();
        copy(root);
        told.clipboardChanged();
        await painted();
        expect(dashes(root)).toHaveLength(1);
        told.clipboardChanged();
        await painted();
        expect(dashes(root)).toEqual([]);
    });

    it('ADR-0170 CP-32: a browser that cannot say the clipboard changed outlines nothing, and the copy still lands', async () => {
        const { root } = mount({ columns, rows: dated(100), rowHeight: 24, viewportHeight: 480 }, { clipboardWatched: false });
        await painted();
        press(root, 10, rowY(0));
        const { data } = copy(root);
        await painted();
        expect([data.get('text/plain'), dashes(root), escapeLeaves(root)]).toEqual(['Book-0\r\n', [], true]);
    });
});
