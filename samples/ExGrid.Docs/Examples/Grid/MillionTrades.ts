import { useCallback, useSyncExternalStore } from 'react';
import { Temporal } from 'temporal-polyfill';
import { GridSource, type FetchingGridSource, type GridColumn, type GridPage, type GridQuery } from '@ex-grid/core';
import { mulberry32 } from './Seeded';
import type { Trade } from './Trades';

/** How many trades there are. */
export const tradeCount = 1_000_000;

const books = ['Rates', 'Credit', 'FX', 'Equities'];
const traders = ['Ishikawa', 'Novak', 'Osei', 'Laurent', 'Haddad', 'Kowalski'];
const instruments = ['UST 10Y', 'Bund 5Y', 'EUR/USD', 'USD/JPY', 'iTraxx Main', 'CDX IG', 'SPX Dec', 'NKY Mar'];
const currencies = ['USD', 'EUR', 'JPY', 'GBP'];
const firstDay = Temporal.PlainDate.from('2026-01-02');

// A trade made from its position, the same every time: the generator is seeded with the position, so a million
// trades are never all in memory.
export function tradeAt(index: number): Trade {
    const next = mulberry32(index);
    const pick = <T>(list: readonly T[]): T => list[Math.floor(next() * list.length)] as T;
    return {
        id: 1_000_001 + index,
        book: pick(books),
        trader: pick(traders),
        instrument: pick(instruments),
        currency: pick(currencies),
        notional: Math.round((next() * 49 + 1) * 10) * 100_000,
        price: Math.round((next() * 40 + 80) * 1000) / 1000,
        tradeDate: firstDay.add({ days: Math.floor(index / 4_000) }),
        confirmed: next() >= 0.25,
    };
}

const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

// This server neither sorts nor filters, so its columns say so: the grid offers no sort and no filter on them, rather
// than sending an order the answer would not follow.
export const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id, queryable: false },
    { name: 'Book', type: 'text', value: t => t.book, queryable: false },
    { name: 'Trader', type: 'text', value: t => t.trader, queryable: false },
    { name: 'Instrument', type: 'text', value: t => t.instrument, queryable: false },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number), queryable: false },
    { name: 'Price', type: 'number', value: t => t.price, queryable: false },
    { name: 'TradeDate', type: 'date', value: t => t.tradeDate, header: 'Trade date', dateType: 'dateOnly', queryable: false },
];

// Where a server call would go: it answers after 150 ms, or not at all once the grid has asked for other rows.
async function fetchTrades(query: GridQuery, signal: AbortSignal): Promise<GridPage<Trade>> {
    if (query.sorts.length > 0 || query.filter !== undefined) throw new Error('This server neither sorts nor filters.');
    await new Promise<void>((resolve, reject) => {
        const timer = setTimeout(resolve, 150);
        signal.addEventListener('abort', () => { clearTimeout(timer); reject(signal.reason); }, { once: true });
    });
    const start = Math.min(query.range.start, tradeCount);
    const end = Math.min(start + query.range.count, tradeCount);
    return { start, rows: Array.from({ length: end - start }, (_, at) => tradeAt(start + at)), totalCount: tradeCount };
}

/** A million trades fetched a page at a time. A trade's id names it across pages, so a row fetched again keeps its
 * row and only the text that changed is written. The source reads 50 rows either side of what the grid asks for, so
 * ordinary scrolling does not ask again at once. */
export function millionTrades(): FetchingGridSource<Trade> {
    return GridSource.fetch(fetchTrades, { rowKey: t => t.id, readAheadRows: 50 });
}

const count = new Intl.NumberFormat('en-US');

/** The line under the grid: which rows the source holds now, followed as the source changes. */
export function useHolding(source: FetchingGridSource<Trade>): string {
    const subscribe = useCallback((listener: () => void) => source.subscribe(listener), [source]);
    const snapshot = useCallback(() => source.snapshot(), [source]);
    const { window, windowStart } = useSyncExternalStore(subscribe, snapshot);
    if (window.length === 0) return 'Holding no rows yet.';
    return `Holding rows ${count.format(windowStart)} to ${count.format(windowStart + window.length - 1)} of ${count.format(tradeCount)}.`;
}
