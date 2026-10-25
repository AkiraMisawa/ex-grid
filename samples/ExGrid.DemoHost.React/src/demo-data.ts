// The demo's data, as samples/ExGrid.DemoPages/DemoData.cs makes it: the same seeds and the same arithmetic,
// so the three hosts paint the same values.
import type { GridColumn } from '@ex-grid/core';
import { Temporal } from 'temporal-polyfill';
import { DotNetRandom } from './dotnet-random';

/** A trade, as DemoData.cs's DemoTrade. */
export interface DemoTrade {
    readonly id: number;
    readonly book: string;
    readonly trader: string;
    readonly notional: number;
    readonly tradeDate: Temporal.PlainDateTime;
    readonly confirmed: boolean;
}

/** A row of the wide page, which holds only its position and derives every value from it. */
export interface DemoWideRow {
    readonly index: number;
}

const books = ['Rates', 'Credit', 'FX', 'Equity', 'Commodity'] as const;
const traders = ['Ito', 'Marsh', 'Okafor', 'Petrov', 'Silva'] as const;

/** The item at a position the arithmetic has already brought inside the list. */
export function pick<T>(list: readonly T[], at: number): T {
    const value = list[at];
    if (value === undefined) throw new Error(`Internal: no item at ${at}.`);
    return value;
}

/** The first trade date every dataset counts its days from. */
export const firstDay = Temporal.PlainDateTime.from('2026-01-01T00:00:00');

/** One shared, reference-stable columns array: rebuilt per render it would repaint every row (ADR-0003). */
export const tradeColumns: readonly GridColumn<DemoTrade>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (r: DemoTrade) => r.book },
    { name: 'Trader', type: 'text', value: (r: DemoTrade) => r.trader },
    { name: 'Notional', type: 'number', value: (r: DemoTrade) => r.notional },
    { name: 'TradeDate', type: 'date', value: (r: DemoTrade) => r.tradeDate, header: 'Trade date' },
    { name: 'Confirmed', type: 'boolean', value: (r: DemoTrade) => r.confirmed },
]);

/** Deterministic rows, so a reload paints the same data: DemoData.Window. */
export function tradeWindow(count: number, seed: number): readonly DemoTrade[] {
    const random = new DotNetRandom(seed);
    return Object.freeze(Array.from({ length: count }, (_, i) => {
        const book = `${pick(books, random.next(books.length))}-${String(i + 1).padStart(3, '0')}`;
        const trader = pick(traders, random.next(traders.length));
        const notional = (random.next(-500, 2_000) * 1_000) + random.next(0, 100);
        const tradeDate = firstDay.add({ days: random.next(0, 240) });
        const confirmed = random.next(2) === 0;
        return Object.freeze({ id: 0, book, trader, notional, tradeDate, confirmed });
    }));
}

/** ((row·7919 + metric·104729) mod 1,999,999) / 100, as DemoData.Metric computes it in decimal. */
function metric(rowIndex: number, column: number): number {
    return ((rowIndex * 7919 + column * 104_729) % 1_999_999) / 100;
}

/** A hundred columns, the first two identifiers and the rest metrics: DemoData.WideColumns. */
export const wideColumns: readonly GridColumn<DemoWideRow>[] = Object.freeze([
    { name: 'Key', type: 'text', value: (r: DemoWideRow) => `K-${String(r.index).padStart(6, '0')}`, width: 140 },
    { name: 'Book', type: 'text', value: (r: DemoWideRow) => pick(books, r.index % books.length), width: 110 },
    ...Array.from({ length: 98 }, (_, i): GridColumn<DemoWideRow> => {
        const column = i + 2;
        return { name: `M${String(column - 1).padStart(2, '0')}`, type: 'number', value: (r: DemoWideRow) => metric(r.index, column), width: 91 };
    }),
]);
