// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the row bands, painted as the C# core's overlay paints them. The hover band
// follows the row under the pointer and the Focus band the Focus's row; both are rectangles of the selection
// layers, beneath every tint, and no row learns of either, so no row is painted again (ADR-0008, ADR-0029,
// RR-11, UX-13).
import { describe, expect, it } from 'vitest';
import { defined } from '../src/must';
import { columns, mount, painted, q, rowsOf, scrollTo, trades } from './grid-harness';

function pointer(root: HTMLElement, type: string, x: number, y: number, init: MouseEventInit = {}): void {
    const event = new MouseEvent(type, { bubbles: true, cancelable: true, ...init });
    Object.defineProperty(event, 'offsetX', { value: x });
    Object.defineProperty(event, 'offsetY', { value: y });
    q(root, '.ex-viewport').dispatchEvent(event);
}

// Rows of 24 px; Book is 120 px wide and Notional 100.
const rowY = (row: number): number => (row * 24) + 5;

const pieces = (root: HTMLElement, layer: string): string[] =>
    [...root.querySelectorAll(`.ex-viewport > .${layer} > div`)].map(piece => piece.className);
const band = (root: HTMLElement, layer = 'ex-selection'): Element | null => root.querySelector(`.ex-viewport > .${layer} > .ex-hover-row`);
const paintId = (root: HTMLElement): string | null => q(root, '.ex-viewport').getAttribute('data-ex-paint');

describe('the hover band', () => {
    it('ADR-0029 UX-13 / RR-11: it follows the row under the pointer, a move within that row paints nothing, and no row is painted again', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, highlightHoverRow: true });
        await painted();
        const paint = paintId(root);
        pointer(root, 'mousemove', 10, rowY(3));
        await painted();
        const first = band(root);
        expect(first?.getAttribute('style')).toContain('top: 72px');
        pointer(root, 'mousemove', 150, rowY(3) + 10);
        await painted();
        expect(band(root)).toBe(first);
        pointer(root, 'mousemove', 10, rowY(5));
        await painted();
        expect([band(root)?.getAttribute('style')?.includes('top: 120px'), paintId(root)]).toEqual([true, paint]);
        root.dispatchEvent(new MouseEvent('mouseleave'));
        await painted();
        expect(band(root)).toBeNull();
    });

    it('ADR-0029: off unless asked, the pointer moving paints nothing; turned off, the band goes', async () => {
        const options = { columns, rows: trades(100), rowHeight: 24, viewportHeight: 480 };
        const { root, view } = mount(options);
        await painted();
        pointer(root, 'mousemove', 10, rowY(3));
        await painted();
        expect(band(root)).toBeNull();
        view.update({ ...options, highlightHoverRow: true });
        pointer(root, 'mousemove', 10, rowY(3));
        await painted();
        expect(band(root)).not.toBeNull();
        view.update(options);
        await painted();
        expect(band(root)).toBeNull();
    });

    it('ADR-0021 (fifth entry): the rows scrolled under a still pointer take the band away until the pointer moves', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, highlightHoverRow: true });
        await painted();
        pointer(root, 'mousemove', 10, rowY(3));
        await painted();
        await scrollTo(root, 240);
        expect(band(root)).toBeNull();
        pointer(root, 'mousemove', 10, rowY(3));
        await painted();
        expect(band(root)).not.toBeNull();
    });

    it('ADR-0029: on the header no row is under the pointer and the band goes; on an element of the Viewport\'s own it stands', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, highlightHoverRow: true });
        await painted();
        pointer(root, 'mousemove', 10, rowY(3));
        await painted();
        defined(rowsOf(root)[0], 'a painted row').dispatchEvent(new MouseEvent('mousemove', { bubbles: true }));
        await painted();
        expect(band(root)).not.toBeNull();
        q(root, '.ex-header').dispatchEvent(new MouseEvent('mousemove', { bubbles: true }));
        await painted();
        expect(band(root)).toBeNull();
    });

    it('ADR-0029: panning sideways moves no row under the pointer, so the band stays', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, highlightHoverRow: true });
        await painted();
        pointer(root, 'mousemove', 10, rowY(3));
        await painted();
        await scrollTo(root, 0, 40);
        expect(band(root)).not.toBeNull();
    });

    it('ADR-0029: a row the source has taken away has no band', async () => {
        const options = { columns, rows: trades(10), rowHeight: 24, viewportHeight: 480, highlightHoverRow: true };
        const { root, view } = mount(options);
        await painted();
        pointer(root, 'mousemove', 10, rowY(9));
        await painted();
        expect(band(root)).not.toBeNull();
        view.update({ ...options, rows: trades(9) });
        await painted();
        expect(band(root)).toBeNull();
    });

    it('ADR-0206: over Pinned Columns its pinned part is in the pinned shade, beneath every tint, as the Focus band\'s is', async () => {
        const { root } = mount({
            columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, pinnedColumnCount: 1, highlightHoverRow: true, highlightFocusRow: true,
        });
        await painted();
        pointer(root, 'mousemove', 10, rowY(4));
        await painted();
        expect(pieces(root, 'ex-shade-pinned')).toEqual(['ex-hover-row']);
        // A range of two rows and both columns: its tint lies over both bands, in either layer.
        pointer(root, 'mousedown', 10, rowY(1), { button: 0, buttons: 1 });
        pointer(root, 'mouseup', 10, rowY(1), { button: 0 });
        for (const name of ['ArrowDown', 'ArrowRight']) {
            root.dispatchEvent(new KeyboardEvent('keydown', { key: name, shiftKey: true, bubbles: true, cancelable: true }));
        }
        await painted();
        expect(pieces(root, 'ex-shade-pinned')).toEqual(['ex-hover-row', 'ex-focus-row', 'ex-range-tint']);
        expect(pieces(root, 'ex-selection').slice(0, 3)).toEqual(['ex-hover-row', 'ex-focus-row', 'ex-range-tint']);
    });
});

describe('the Focus band', () => {
    it('ADR-0008: asked for, it lies across the Focus\'s row and moves with it; with no Focus there is none', async () => {
        const options = { columns, rows: trades(100), rowHeight: 24, viewportHeight: 480, highlightFocusRow: true };
        const { root } = mount(options);
        await painted();
        expect(root.querySelector('.ex-focus-row')).toBeNull();
        pointer(root, 'mousedown', 10, rowY(2), { button: 0, buttons: 1 });
        pointer(root, 'mouseup', 10, rowY(2), { button: 0 });
        await painted();
        expect(root.querySelector('.ex-viewport > .ex-selection > .ex-focus-row')?.getAttribute('style')).toContain('top: 48px');
    });
});

// The Focus column band's pieces in a layer, each as the four lengths of its inline style.
interface Box { readonly left: string; readonly top: string; readonly width: string; readonly height: string }
const column = (root: HTMLElement, layer = 'ex-selection'): Box[] =>
    [...root.querySelectorAll<HTMLElement>(`.ex-viewport > .${layer} > .ex-focus-column`)].map(piece => ({
        left: piece.style.left, top: piece.style.top, width: piece.style.width, height: piece.style.height,
    }));
const box = (left: number, top: number, width: number, height: number): Box =>
    ({ left: `${String(left)}px`, top: `${String(top)}px`, width: `${String(width)}px`, height: `${String(height)}px` });
const press = (root: HTMLElement, x: number, row: number): void => {
    pointer(root, 'mousedown', x, rowY(row), { button: 0, buttons: 1 });
    pointer(root, 'mouseup', x, rowY(row), { button: 0 });
};
const key = (root: HTMLElement, name: string): void => {
    root.dispatchEvent(new KeyboardEvent('keydown', { key: name, bubbles: true, cancelable: true }));
};

describe('the Focus column band', () => {
    it('ADR-0008 (2026-11-03): asked for, it lies down the Focus\'s column over the painted rows and one past them, moves with it, and no row is painted again', async () => {
        const options = { columns, rows: trades(100), rowHeight: 24, viewportHeight: 480, highlightFocusColumn: true };
        const { root } = mount(options);
        await painted();
        expect(column(root)).toEqual([]);
        press(root, 150, 2);
        await painted();
        const paint = paintId(root);
        // The painted rows from the first, and the one past them that every rectangle reaches (ADR-0053).
        const heightPx = (rowsOf(root).length + 1) * 24;
        expect(column(root)).toEqual([box(120, 0, 100, heightPx)]);
        expect(root.querySelector('.ex-focus-row')).toBeNull();
        key(root, 'ArrowLeft');
        await painted();
        expect([column(root), paintId(root)]).toEqual([[box(0, 0, 120, heightPx)], paint]);
    });

    it('ADR-0008 (2026-11-03): with the Focus band as well the two cross, and the Focus\'s cell is tinted once', async () => {
        const options = { columns, rows: trades(100), rowHeight: 24, viewportHeight: 480, highlightFocusRow: true, highlightFocusColumn: true };
        const { root } = mount(options);
        await painted();
        press(root, 150, 3);
        await painted();
        const heightPx = (rowsOf(root).length + 1) * 24;
        expect(column(root)).toEqual([box(120, 0, 100, 72), box(120, 96, 100, heightPx - 96)]);
        expect(pieces(root, 'ex-selection').slice(0, 3)).toEqual(['ex-focus-row', 'ex-focus-column', 'ex-focus-column']);
        // On the first row, nothing lies above it.
        key(root, 'ArrowUp');
        key(root, 'ArrowUp');
        key(root, 'ArrowUp');
        await painted();
        expect(column(root)).toEqual([box(120, 24, 100, heightPx - 24)]);
    });

    it('ADR-0053: scrolled into a long grid, it is cut to the painted rows and one past each end', async () => {
        const { root } = mount({ columns, rows: trades(100_000), rowHeight: 24, viewportHeight: 480, highlightFocusColumn: true });
        await painted();
        press(root, 150, 2);
        await scrollTo(root, 24 * 50_000);
        const [piece] = column(root);
        expect([piece?.top, piece?.height]).toEqual(['-24px', `${String((rowsOf(root).length + 2) * 24)}px`]);
    });

    it('ADR-0206: on a Pinned Column it lies in the pinned shade, beneath every tint; turned off, it goes', async () => {
        const options = { columns, rows: trades(100), rowHeight: 24, viewportHeight: 480, pinnedColumnCount: 1, highlightFocusColumn: true };
        const { root, view } = mount(options);
        await painted();
        press(root, 10, 1);
        await painted();
        expect([column(root, 'ex-shade-pinned').length, column(root).length]).toEqual([1, 0]);
        view.update({ ...options, highlightFocusColumn: false });
        await painted();
        expect(root.querySelector('.ex-focus-column')).toBeNull();
    });

    it('ADR-0008: anything but true or false is refused by name', () => {
        expect(() => mount({ columns, rows: trades(1), highlightFocusColumn: 'yes' as unknown as boolean })).toThrow('highlightFocusColumn is true or false.');
    });
});
