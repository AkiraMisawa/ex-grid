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
