// @vitest-environment happy-dom
// The adapter's own tests (ADR-0350 section 13): React Testing Library mounts it. What the grid paints is
// the core's, tested there; here, that the adapter mounts the core once, hands it each render's props, and
// destroys it with the component.
import { createElement } from 'react';
import { StrictMode } from 'react';
import { cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import type { GridColumn } from '@ex-grid/core';
import { ExGrid } from '../src/ex-grid';

interface Trade {
    readonly book: string;
}

const columns: readonly GridColumn<Trade>[] = [{ name: 'Book', type: 'text', value: trade => trade.book, width: 120 }];

afterEach(() => { cleanup(); });

describe('ExGrid', () => {
    it('ADR-0350: the element React renders is the grid\'s instance root, drawn before the browser paints', () => {
        const { container } = render(createElement(ExGrid<Trade>, { columns, rows: [{ book: 'Rates' }] }));
        const root = container.firstElementChild;
        expect(root?.getAttribute('role')).toBe('grid');
        expect(root?.querySelector('.ex-header-cell')?.textContent).toBe('Book');
    });

    it('ADR-0350: a render hands the grid its props, and the same root keeps drawing', async () => {
        const { container, rerender } = render(createElement(ExGrid<Trade>, { columns, rows: [{ book: 'Rates' }] }));
        const root = container.firstElementChild;
        rerender(createElement(ExGrid<Trade>, { columns, rows: [{ book: 'Credit' }, { book: 'FX' }] }));
        await Promise.resolve();
        expect(container.firstElementChild).toBe(root);
        expect(root?.getAttribute('aria-rowcount')).toBe('3');
    });

    it('ADR-0018: unmounted, the grid is destroyed and its root emptied; under StrictMode one grid stands', () => {
        const { container, unmount } = render(createElement(StrictMode, null, createElement(ExGrid<Trade>, { columns, rows: [] })));
        const root = container.firstElementChild;
        expect(root?.querySelectorAll('.ex-scroller')).toHaveLength(1);
        unmount();
        expect(root?.childElementCount).toBe(0);
    });
});
