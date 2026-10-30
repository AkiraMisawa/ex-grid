// @vitest-environment happy-dom
// Layer 2 (ADR-0030, ADR-0028): what a design-system Wrapper hands every grid inside it. The widths of the font its
// stylesheet sets reach the #### arithmetic, and its Density, hover band and Row Stripes stand where the grid gives
// none of its own; a value the grid is given beats the Wrapper's, and the Wrapper's beats the preset.
import { afterEach, describe, expect, it } from 'vitest';
import { CellTextMetrics } from '../src/columns/cell-text-metrics';
import type { GridColumn } from '../src/dom/grid-column';
import { GridMetrics } from '../src/grid-metrics';
import { GridPresentationDefaults } from '../src/grid-presentation-defaults';
import { mount, painted, rowsOf, trades, unmountAll, type Trade } from './grid-harness';

afterEach(unmountAll);

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120 },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 80 },
];
const rows = trades(40);
// A face whose digits are three times the preset's: 9000 no longer fits 80 pixels.
const wide = { wideWidthPx: 30, digitWidthPx: 30, narrowWidthPx: 15, fontSizePx: 14 } as const;
const hashed = (root: HTMLElement, row: number): boolean => rowsOf(root)[row]?.children[1]?.querySelector('.ex-hashes') !== null;
const heightOf = (root: HTMLElement): string => root.style.getPropertyValue('--ex-row-height');

describe('a Wrapper\'s presentation defaults', () => {
    it('ADR-0030 / ADR-0028: the Wrapper\'s Density stands where the grid gives none, and the grid\'s own beats it', async () => {
        const defaults = new GridPresentationDefaults({ ...wide, density: 'comfortable' });
        const { root, view } = mount({ columns, rows, viewportHeight: 480, presentationDefaults: defaults });
        await painted();
        expect(heightOf(root)).toBe(`${GridMetrics.resolve({ density: 'comfortable' }).rowHeightPx}px`);
        view.update({ columns, rows, viewportHeight: 480, presentationDefaults: defaults, density: 'compact' });
        await painted();
        expect(heightOf(root)).toBe(`${GridMetrics.resolve({ density: 'compact' }).rowHeightPx}px`);
    });

    it('ADR-0030 / ADR-0016: the Wrapper\'s widths are what #### is judged by, and Cell Metrics given to the grid beat them', async () => {
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480 });
        await painted();
        expect(hashed(root, 9)).toBe(false);
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, presentationDefaults: new GridPresentationDefaults(wide) });
        await painted();
        expect(hashed(root, 9)).toBe(true);
        view.update({
            columns, rows, rowHeight: 24, viewportHeight: 480, presentationDefaults: new GridPresentationDefaults(wide),
            cellMetrics: new CellTextMetrics({ wideWidthPx: 8, digitWidthPx: 7, narrowWidthPx: 4, cellHorizontalPaddingPx: 8 }),
        });
        await painted();
        expect(hashed(root, 9)).toBe(false);
    });

    it('ADR-0030 / ADR-0038: the Wrapper\'s Row Stripes stand where the grid gives none, and the grid\'s false beats them', async () => {
        const defaults = new GridPresentationDefaults({ ...wide, stripeRows: true });
        const { root, view } = mount({ columns, rows, rowHeight: 24, viewportHeight: 480, presentationDefaults: defaults });
        await painted();
        expect(rowsOf(root).slice(0, 2).map(row => row.classList.contains('ex-row-stripe'))).toEqual([false, true]);
        view.update({ columns, rows, rowHeight: 24, viewportHeight: 480, presentationDefaults: defaults, stripeRows: false });
        await painted();
        expect(rowsOf(root).some(row => row.classList.contains('ex-row-stripe'))).toBe(false);
    });

    it('ADR-0030: either is refused by name when it is not what a Wrapper makes', () => {
        // Plain widths, as a caller without the types might hand them.
        const plain: unknown = { ...wide };
        expect(() => mount({ columns, rows, presentationDefaults: plain as never })).toThrow(/presentationDefaults is a GridPresentationDefaults/u);
        expect(() => mount({ columns, rows, cellMetrics: plain as never })).toThrow(/cellMetrics is a CellTextMetrics/u);
    });
});
