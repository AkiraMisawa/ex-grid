// @vitest-environment happy-dom
// The adapter's own tests (ADR-0370 section 1): React Testing Library mounts it. What the pivot draws and
// decides is its Script Core's, tested there; here, that the adapter mounts the pivot once, hands it each
// render's props, passes the browser's language as its locale, and destroys it with the component.
import { createElement } from 'react';
import { act, cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { FetchingPivotSource, PivotSourceFeatures, RecordPivotSource, createPivotLayout, pivotRecordField } from '@ex-grid/pivot-engine';
import { ExPivot } from '../src/ex-pivot';

interface Trade {
    readonly region: string;
    readonly amount: number;
}

const fields = [
    pivotRecordField<Trade>({ name: 'Region', type: 'text', value: trade => trade.region }),
    pivotRecordField<Trade>({ name: 'Amount', type: 'number', value: trade => trade.amount }),
];

const trades: readonly Trade[] = Array.from({ length: 1200 }, (_, i) => ({ region: `Region ${i}`, amount: i }));

const source = new RecordPivotSource(trades, fields);

afterEach(() => { cleanup(); });

async function settled(): Promise<void> {
    await act(async () => {
        for (let i = 0; i < 20; i++) await Promise.resolve();
        await new Promise(resolve => { setTimeout(resolve, 0); });
    });
}

describe('ExPivot', () => {
    it('ADR-0370 section 1: the element React renders is the pivot\'s root, the report drawn by ExGrid beside the Field List', async () => {
        const { container } = render(createElement(ExPivot, { source, layout: createPivotLayout({ rows: ['Region'], values: ['Amount'] }) }));
        const root = container.firstElementChild;
        expect(root?.classList.contains('ex-pivot')).toBe(true);
        expect(root?.querySelector('.ex-pivot-pane-title')?.textContent).toBe('PivotTable Fields');
        await settled();
        // The report's 1,200 Items and its Grand Total, under the header: the grid the core drew.
        expect(root?.querySelector('.ex-grid')?.getAttribute('aria-rowcount')).toBe('1202');
    });

    it('ADR-0370 section 1: a render hands the pivot its props, and the same root keeps drawing', async () => {
        const { container, rerender } = render(createElement(ExPivot, { source, showFieldList: true }));
        const root = container.firstElementChild;
        rerender(createElement(ExPivot, { source, showFieldList: false }));
        await settled();
        expect(container.firstElementChild).toBe(root);
        expect(root?.querySelector<HTMLElement>('.ex-pivot-field-list')?.hidden).toBe(true);
    });

    it('ADR-0370 section 10: given no locale, the pivot writes counts in the browser\'s language, read once when it is made', async () => {
        Object.defineProperty(navigator, 'language', { value: 'de-DE', configurable: true });
        try {
            const counted = new FetchingPivotSource(source.fields, new PivotSourceFeatures(['sum'], false, true), {
                aggregate: (query, signal) => source.aggregate(query, signal),
                details: (query, signal) => source.details(query, signal),
                items: (query, signal) => source.items(query, signal),
                itemCounts: signal => source.itemCounts(signal),
            });
            const { container } = render(createElement(ExPivot, { source: counted }));
            await settled();
            const region = container.querySelector('.ex-pivot-field[data-ex-field="Region"] .ex-pivot-field-count');
            expect(region?.textContent).toBe('1.200');
        } finally {
            Reflect.deleteProperty(navigator, 'language');
        }
    });

    it('ADR-0370 section 1: unmounting destroys the pivot, which empties its root', () => {
        const { container, unmount } = render(createElement(ExPivot, { source }));
        const root = container.firstElementChild;
        unmount();
        expect(root?.childElementCount).toBe(0);
    });
});
