import { GridSource, type GridColumn, type SelectionRange } from '@ex-grid/core';
import type { ExGridApi } from '@ex-grid/react';
import { sampleTrades, type Trade } from './Trades';

export const currencies: readonly string[] = ['USD', 'EUR', 'JPY', 'GBP'];
export const source = GridSource.from(sampleTrades());
const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

export const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book },
    { name: 'Trader', type: 'text', value: t => t.trader },
    { name: 'Instrument', type: 'text', value: t => t.instrument },
    { name: 'Currency', type: 'text', value: t => t.currency },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number) },
];

/**
 * Selects the trades in `currency` and says what it did. The rows are read in the order the grid shows now, and
 * placed under the version that order has: a sort or a filter in between would make the positions name other
 * rows, and the grid would refuse them.
 *
 * Each run of neighbouring rows is one range across every column. The grid joins nothing: rows that stand together
 * are one range because they are handed as one, and every range handed costs a little on each paint.
 */
export function selectWhere(grid: ExGridApi<Trade> | null, currency: string): string {
    const { window, windowStart, rowSequenceVersion } = source.snapshot();
    const ranges: SelectionRange[] = [];
    let trades = 0;
    window.forEach((trade, at) => {
        if (trade.currency !== currency) return;
        trades += 1;
        const row = windowStart + at;
        const last = ranges.at(-1);
        if (last !== undefined && last.topRow + last.rowCount === row) ranges[ranges.length - 1] = { ...last, rowCount: last.rowCount + 1 };
        else ranges.push({ topRow: row, leftColumn: 0, rowCount: 1, columnCount: columns.length });
    });
    const [first] = ranges;
    if (grid === null || first === undefined) return `No trade shown is in ${currency}.`;
    // The first match takes the Focus, which the grid scrolls into view.
    return grid.placeSelection(ranges, { row: first.topRow, column: 0 }, rowSequenceVersion)
        ? `${String(trades)} trades in ${String(ranges.length)} range(s)`
        : 'The grid placed nothing: the order moved, or an open edit was held.';
}
