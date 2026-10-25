// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the Selection in the DOM. A press, the keys, the selection layers, the
// reveal and its repaint, the live region and the marks on the root, to the DOM contract the C# core
// writes (ADR-0008, ADR-0011, ADR-0012, ADR-0033, ADR-0052, ADR-0206).
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import type { GridSelection } from '../src/selection/selection';
import type { GridPage } from '../src/source/fetching-source';
import { GridSource } from '../src/source/grid-source';
import { columns, mount, painted, q, scrollTo, trades, type Trade } from './grid-harness';

/** A press on the Viewport at its own offsets, which is what the grid reads (ADR-0012). */
function press(root: HTMLElement, x: number, y: number, init: MouseEventInit = {}): MouseEvent {
    return pointer(root, 'mousedown', x, y, { button: 0, buttons: 1, ...init });
}

function pointer(root: HTMLElement, type: string, x: number, y: number, init: MouseEventInit): MouseEvent {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, ...init });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-viewport').dispatchEvent(event);
    return event;
}

function key(root: HTMLElement, name: string, init: KeyboardEventInit = {}): KeyboardEvent {
    const event = new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true, ...init });
    root.dispatchEvent(event);
    return event;
}

const layer = (root: HTMLElement, name: string): string[] =>
    [...root.querySelectorAll(`.ex-viewport > .${name} > div`)].map(piece => `${piece.className} | ${defined(piece.getAttribute('style'), 'a layer piece\'s style')}`);
const firstChildren = (root: HTMLElement): string[] =>
    [...q(root, '.ex-viewport').children].slice(0, 3).map(child => child.className);
const prefix = (root: HTMLElement): string => root.id.replace(/root$/, '');

// Rows of 24 px under a header of 24; Book is 120 px wide and Notional 100.
const rowY = (row: number): number => (row * 24) + 5;

function heard(): { readonly selections: GridSelection[]; readonly onSelectionChanged: (selection: GridSelection) => void } {
    const selections: GridSelection[] = [];
    return { selections, onSelectionChanged: selection => { selections.push(selection); } };
}

describe('the Selection in the grid', () => {
    it('ADR-0012: a press paints the Focus outline over its cell, names it on the root, and is heard once', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        expect(firstChildren(root)).toEqual(['ex-selection-pinned', 'ex-selection', 'ex-row']);
        expect(root.hasAttribute('aria-activedescendant')).toBe(false);

        press(root, 130, rowY(2));
        await painted();
        expect(layer(root, 'ex-selection')).toEqual(['ex-focus | left: 120px; top: 48px; width: 100px; height: 24px']);
        expect(layer(root, 'ex-selection-pinned')).toEqual([]);
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r2c1`);
        expect(root.getAttribute('data-ex-follow-focus')).toBe(`${prefix(root)}r2c1`);
        expect([root.getAttribute('data-ex-follow'), root.getAttribute('data-ex-reveal')]).toEqual(['0', '0']);
        expect(q(root, '[data-ex-answered]').getAttribute('data-ex-answered')).toBe('1');
        expect(listener.selections.map(selection => selection.focus)).toEqual([{ row: 2, column: 1 }]);
        // One cell is the Focus by another name: nothing is announced, and nothing is owed.
        expect(q(root, '.ex-announce').textContent).toBe('');
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
    });

    it('ADR-0052, ADR-0033: Shift+arrows extend the range, heard once a pass, and announced once it stands still', async () => {
        const listener = heard();
        const { root, told } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, 10, rowY(2));
        await painted();
        const down = key(root, 'ArrowDown', { shiftKey: true });
        key(root, 'ArrowRight', { shiftKey: true });
        await painted();
        expect(down.defaultPrevented).toBe(true);
        expect(listener.selections).toHaveLength(2);
        expect(layer(root, 'ex-selection').map(piece => piece.split(' | ')[0])).toEqual(['ex-range-tint', 'ex-range ex-range-single', 'ex-focus']);
        expect(layer(root, 'ex-selection')[1]).toBe('ex-range ex-range-single | left: 0px; top: 48px; width: 220px; height: 48px; --ex-outline-in-l: 1');
        // Owed for the settle delay: the mark says so, and the region is still quiet.
        expect(q(root, '.ex-announce').textContent).toBe('');
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        told.clock.advance(150);
        expect(q(root, '.ex-announce').textContent).toBe('2 rows by 2 columns selected, Book 3 to Notional 4');
        // The page follow the keys asked for is still owed, until the task after the next frame.
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        told.frame();
        told.clock.advance(0);
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
        // Collapsing to one cell empties the sentence, silently.
        key(root, 'ArrowUp');
        await painted();
        told.frame();
        told.clock.advance(0);
        expect(q(root, '.ex-announce').textContent).toBe('');
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
    });

    it('ADR-0012: the first key with nothing selected places the Focus on the first cell on screen, without its step', async () => {
        const { root, told } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        await scrollTo(root, 2400);
        // Past the fling's settle, so the cells are painted.
        told.clock.advance(150);
        await painted();
        key(root, 'ArrowDown');
        await painted();
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r100c0`);
        expect(root.getAttribute('data-ex-reveal')).toBe('0');
        expect(q(root, '.ex-scroller').scrollTop).toBe(2400);
    });

    it('ADR-0012: Ctrl+End reveals the last cell in the pass that paints it, and its repaint moves a pixel and back unheard', async () => {
        const { root, told } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 10, rowY(0));
        await painted();
        key(root, 'End', { ctrlKey: true });
        await painted();
        const scroller = q(root, '.ex-scroller');
        const viewport = q(root, '.ex-viewport');
        // 1000 rows of 24 px in the 456 px the header leaves.
        expect(scroller.scrollTop).toBe(23_544);
        expect(root.getAttribute('data-ex-reveal')).toBe('1');
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r999c1`);
        const first = viewport.getAttribute('data-ex-first-row');
        expect(root.hasAttribute('data-ex-settled')).toBe(false);

        told.frame();
        expect(scroller.scrollTop).toBe(23_543);
        scroller.dispatchEvent(new Event('scroll'));
        await painted();
        expect(viewport.getAttribute('data-ex-first-row')).toBe(first);
        told.frame();
        expect(scroller.scrollTop).toBe(23_544);
        expect(told.framesAsked).toBe(0);
        // The page follow runs in the task after the frame that paints the move, once the repaint is over.
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        told.clock.advance(0);
        expect(root.getAttribute('data-ex-follow')).toBe('1');
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
    });

    it('ADR-0012: a scroll the user makes during a reveal\'s repaint stands, and the grid paints where it stands', async () => {
        const { root, told } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 10, rowY(0));
        await painted();
        key(root, 'End', { ctrlKey: true });
        await painted();
        told.frame();
        const scroller = q(root, '.ex-scroller');
        scroller.scrollTop = 12_000;
        scroller.dispatchEvent(new Event('scroll'));
        told.frame();
        await painted();
        expect(scroller.scrollTop).toBe(12_000);
        expect(q(root, '.ex-viewport').getAttribute('data-ex-first-row')).toBe('500');
    });

    it('ADR-0011: new rows drop the Selection, and the Consumer hears it empty', async () => {
        const listener = heard();
        const options = { columns, rowHeight: 24, viewportHeight: 480, ...listener };
        const { root, view } = mount({ ...options, rows: trades(1000) });
        await painted();
        press(root, 10, rowY(1));
        await painted();
        view.update({ ...options, rows: trades(1000) });
        await painted();
        expect(listener.selections.map(selection => selection.isEmpty)).toEqual([false, true]);
        expect(layer(root, 'ex-selection')).toEqual([]);
        expect(root.hasAttribute('aria-activedescendant')).toBe(false);
        expect(root.hasAttribute('data-ex-follow-focus')).toBe(false);
    });

    it('ADR-0012: Escape releases Tab, so the next Tab is the browser\'s, and only that one', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 10, rowY(0));
        await painted();
        expect(key(root, 'Escape').defaultPrevented).toBe(true);
        expect(key(root, 'Shift').defaultPrevented).toBe(false);
        expect(key(root, 'Tab').defaultPrevented).toBe(false);
        expect(key(root, 'Tab').defaultPrevented).toBe(true);
        await painted();
        expect(root.getAttribute('aria-activedescendant')).toBe(`${prefix(root)}r0c1`);
    });

    it('ADR-0206: the pinned shade layer stands only while a range tints a Pinned Column', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, pinnedColumnCount: 1 });
        await painted();
        press(root, 10, rowY(0));
        // The Focus's own cell is never tinted: the pinned part has to hold another.
        key(root, 'ArrowRight', { shiftKey: true });
        await painted();
        expect(firstChildren(root)).toEqual(['ex-selection-pinned', 'ex-selection', 'ex-row']);
        key(root, 'ArrowDown', { shiftKey: true });
        await painted();
        expect(firstChildren(root)).toEqual(['ex-shade-pinned', 'ex-selection-pinned', 'ex-selection']);
        expect(layer(root, 'ex-shade-pinned').map(piece => piece.split(' | ')[0])).toEqual(['ex-range-tint']);
        const paint = q(root, '.ex-viewport').getAttribute('data-ex-paint');
        key(root, 'ArrowDown');
        await painted();
        expect(firstChildren(root)).toEqual(['ex-selection-pinned', 'ex-selection', 'ex-row']);
        // A layer coming or going paints no cell (ADR-0003).
        expect(q(root, '.ex-viewport').getAttribute('data-ex-paint')).toBe(paint);
    });

    it('ADR-0008 (2026-10-13): the Heading Mark marks the header of every column the Selection reaches, the Pinned Columns\' part in a layer first in the band', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, pinnedColumnCount: 1 });
        await painted();
        const headerCells = (): HTMLElement[] => [...root.querySelectorAll<HTMLElement>('.ex-header > .ex-header-cell')];
        const marks = (): string[] => [...q(root, '.ex-header').children]
            .filter(child => !child.classList.contains('ex-header-cell'))
            .map(child => (child.classList.contains('ex-heading-marks-pinned')
                ? `pinned: ${[...child.children].map(mark => defined(mark.getAttribute('style'), 'a mark\'s style')).join(', ')}`
                : `${child.className} | ${defined(child.getAttribute('style'), 'a mark\'s style')}`));
        const [book, notional] = headerCells();
        expect(marks()).toEqual([]);

        press(root, 130, rowY(2));
        await painted();
        expect(headerCells().map(cell => cell.className)).toEqual(['ex-header-cell ex-pinned', 'ex-header-cell ex-heading-marked']);
        expect(marks()).toEqual(['ex-heading-mark ex-heading-mark-column | left: 120px; top: 0px; width: 100px; height: 24px']);

        key(root, 'ArrowLeft', { shiftKey: true });
        await painted();
        // The pinned layer is first in the band, so its place in the flow is the band's left edge (ADR-0004).
        expect(q(root, '.ex-header').firstElementChild?.className).toBe('ex-heading-marks-pinned');
        expect(marks()).toEqual([
            'pinned: left: 0px; top: 0px; width: 120px; height: 24px',
            'ex-heading-mark ex-heading-mark-column | left: 120px; top: 0px; width: 100px; height: 24px',
        ]);

        key(root, 'a', { ctrlKey: true });
        await painted();
        // Every row is selected: both columns are covered whole (D1), on the same header cells (ADR-0003).
        expect(headerCells()).toEqual([book, notional]);
        expect(headerCells().map(cell => cell.className)).toEqual([
            'ex-header-cell ex-pinned ex-heading-marked ex-heading-whole', 'ex-header-cell ex-heading-marked ex-heading-whole',
        ]);

        // The Focus stayed on Notional: a key that collapses leaves its column alone marked, and the pinned
        // layer goes with the last pinned mark.
        key(root, 'ArrowDown');
        await painted();
        expect(marks()).toEqual(['ex-heading-mark ex-heading-mark-column | left: 120px; top: 0px; width: 100px; height: 24px']);
        expect(headerCells().map(cell => cell.className)).toEqual(['ex-header-cell ex-pinned', 'ex-header-cell ex-heading-marked']);
    });

    it('ADR-0012: a drag extends to the cell under the pointer while the button is down, and ends when it is up', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, 10, rowY(1));
        pointer(root, 'mousemove', 130, rowY(3), { buttons: 1 });
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual([{ topRow: 1, leftColumn: 0, rowCount: 3, columnCount: 2 }]);
        pointer(root, 'mousemove', 130, rowY(5), { buttons: 0 });
        pointer(root, 'mousemove', 130, rowY(7), { buttons: 1 });
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual([{ topRow: 1, leftColumn: 0, rowCount: 3, columnCount: 2 }]);
    });
    it('ADR-0080: DOM focus leaving the grid ends a release of Tab, so a Tab after coming back cycles', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 10, rowY(0));
        await painted();
        key(root, 'Escape');
        const outside = document.createElement('button');
        document.body.append(outside);
        root.dispatchEvent(new FocusEvent('focusout', { relatedTarget: outside }));
        expect(key(root, 'Tab').defaultPrevented).toBe(true);
    });

    it('ADR-0012: a drag released over the header ends, and a later move with the button down extends nothing', async () => {
        const listener = heard();
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, ...listener });
        await painted();
        press(root, 10, rowY(1));
        q(root, '.ex-header').dispatchEvent(new MouseEvent('mouseup', { bubbles: true, button: 0 }));
        pointer(root, 'mousemove', 130, rowY(4), { buttons: 1 });
        await painted();
        expect(listener.selections.at(-1)?.ranges).toEqual([{ topRow: 1, leftColumn: 0, rowCount: 1, columnCount: 1 }]);
    });

    it('ADR-0220: only a press the grid answered, and its release, count as inputs answered', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480 });
        await painted();
        press(root, 10, rowY(1), { button: 2, buttons: 2 });
        pointer(root, 'mouseup', 10, rowY(1), { button: 2 });
        await painted();
        expect(q(root, '[data-ex-answered]').getAttribute('data-ex-answered')).toBe('0');
        press(root, 10, rowY(1));
        pointer(root, 'mouseup', 10, rowY(1), { button: 0 });
        await painted();
        expect(q(root, '[data-ex-answered]').getAttribute('data-ex-answered')).toBe('2');
    });

    it('ADR-0204, ADR-0220: a page follow waiting for its row to arrive is owed, and the grid is not settled until it runs', async () => {
        const answers: ((page: GridPage<Trade>) => void)[] = [];
        const source = GridSource.fetch<Trade>(() => new Promise(resolve => { answers.push(resolve); }), { readAheadRows: 0 });
        const { root, told } = mount({ columns, source, rowHeight: 24, viewportHeight: 480 });
        await painted();
        defined(answers.shift(), 'the first question')({ rows: trades(100), start: 0, totalCount: 5000 });
        await new Promise(resolve => { setTimeout(resolve, 0); });
        press(root, 10, rowY(0));
        await painted();
        key(root, 'End', { ctrlKey: true });
        await painted();
        told.frame();
        told.frame();
        expect(root.getAttribute('data-ex-follow')).toBe('0');
        expect(root.hasAttribute('data-ex-settled')).toBe(false);
        const last = trades(5000).slice(4980);
        defined(answers.at(-1), 'the question for the last rows')({ rows: last, start: 4980, totalCount: 5000 });
        await new Promise(resolve => { setTimeout(resolve, 0); });
        expect(root.getAttribute('data-ex-follow')).toBe('1');
        told.frame();
        told.clock.advance(0);
        expect(root.hasAttribute('data-ex-settled')).toBe(true);
    });
});
