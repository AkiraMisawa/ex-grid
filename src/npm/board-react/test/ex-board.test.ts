// @vitest-environment happy-dom
// The adapter's own tests (ADR-0370 section 1): React Testing Library mounts it. What the Board draws and decides is
// its Script Core's, tested there; here, that the adapter mounts the Board once, hands it each render's props, passes
// the browser's language as its locale, and destroys it with the component.
import { createElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { boardInput, readBoard, type BoardReading } from '@ex-grid/board';
import type { Snapshot } from '@ex-grid/data';
import { ExBoard, type ExBoardChrome } from '../src/ex-board';

const board: BoardReading = readBoard(`{"version": 1, "title": "Desk review",
  "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]}},
  "views": [{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "pnl", "format": "#,##0"}]}},
            {"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}},
            {"id": "curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}],
  "layout": [{"views": ["blotter", "byDesk"]}, {"views": ["curve"]}]}`);

function data(): Record<string, Snapshot> {
    const trades = boardInput(board, 'trades');
    if (trades === undefined) throw new Error('No input trades.');
    return { trades: trades.fromJson('[{"desk": "Rates", "pnl": 1200, "day": "2026-10-19"}, {"desk": "FX", "pnl": -50, "day": "2026-10-20"}]') };
}

afterEach(() => { cleanup(); });

/** Lets every answer already given land: all of it is microtasks, which a macrotask comes after. */
async function settled(): Promise<void> {
    await act(async () => {
        await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

describe('ExBoard', () => {
    it('ADR-0370 section 1: the element React renders is the Board\'s root, its views drawn by ExGrid, ExPivot and ExChart', async () => {
        const { container } = render(createElement(ExBoard, { board, data: data(), locale: 'invariant' }));
        const root = container.firstElementChild;
        await settled();
        expect(root?.classList.contains('ex-board')).toBe(true);
        expect(root?.querySelector('[data-ex-view="blotter"] .ex-grid')).not.toBeNull();
        expect(root?.querySelector('[data-ex-view="byDesk"] .ex-pivot')).not.toBeNull();
        expect(root?.querySelector('[data-ex-view="curve"] svg')).not.toBeNull();
    });

    it('ADR-0370 section 1: a render hands the Board its props, and the same root keeps drawing', async () => {
        const rows = data();
        const { container, rerender } = render(createElement(ExBoard, { board, data: rows, locale: 'invariant', width: 1000 }));
        const root = container.firstElementChild;
        const grid = root?.querySelector('.ex-grid');
        rerender(createElement(ExBoard, { board, data: rows, locale: 'invariant', width: 600 }));
        await settled();
        expect(container.firstElementChild).toBe(root);
        expect(root?.querySelector('.ex-grid')).toBe(grid);
    });

    it('ADR-0370 section 10: given no locale, the Board asks the browser, and refuses one with no language a pivot writes in', () => {
        Object.defineProperty(navigator, 'languages', { value: Object.freeze(['xx-YY']), configurable: true });
        try {
            expect(() => render(createElement(ExBoard, { board, data: data() }))).toThrow(/pass locale/u);
            const { container } = render(createElement(ExBoard, { board, data: data(), locale: 'de-DE' }));
            expect(container.firstElementChild?.classList.contains('ex-board')).toBe(true);
        } finally {
            Reflect.deleteProperty(navigator, 'languages');
        }
    });

    it('ADR-0390: each kind\'s Chrome renders its parts into the views of that kind, and handed again draws them once', async () => {
        let drawn = 0;
        const chrome: ExBoardChrome = {
            pivot: { toolbar: () => createElement('div', { className: 'own-pivot-toolbar' }, 'Pivot tools') },
            chart: { toolbar: () => { drawn++; return createElement('div', { className: 'own-chart-toolbar' }, 'Chart tools'); } },
        };
        const rows = data();
        const { container, rerender } = render(createElement(ExBoard, { board, data: rows, locale: 'invariant', chrome }));
        await settled();
        const root = container.firstElementChild;
        expect(root?.querySelector('[data-ex-view="byDesk"] .own-pivot-toolbar')?.textContent).toBe('Pivot tools');
        expect(root?.querySelector('[data-ex-view="curve"] .own-chart-toolbar')?.textContent).toBe('Chart tools');
        expect(root?.querySelector('[data-ex-view="blotter"] .own-pivot-toolbar')).toBeNull();
        const once = drawn;
        rerender(createElement(ExBoard, { board, data: rows, locale: 'invariant', chrome }));
        await settled();
        expect(drawn).toBe(once);
    });

    it('ADR-0390: a kind the Board has not is refused by name', () => {
        expect(() => render(createElement(ExBoard, { board, data: data(), locale: 'invariant', chrome: { sheet: {} } as unknown as ExBoardChrome })))
            .toThrow(/no kind 'sheet'/u);
    });

    it('ADR-0370 section 1: unmounting destroys the Board, which empties its root', () => {
        const { container, unmount } = render(createElement(ExBoard, { board, data: data(), locale: 'invariant' }));
        const root = container.firstElementChild;
        unmount();
        expect(root?.childElementCount).toBe(0);
    });
});
