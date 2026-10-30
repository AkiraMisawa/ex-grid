import { useCallback, useSyncExternalStore } from 'react';
import {
    applyQuery, GridSource, type DistinctValues, type FetchingGridSource, type GridColumn, type GridFilter, type GridPage, type GridQuery,
    type SortSpec,
} from '@ex-grid/core';
import { sampleTrades, type Trade } from './Trades';

const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

export const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book, filterUi: 'both' },
    { name: 'Trader', type: 'text', value: t => t.trader, filterUi: 'valueList' },
    { name: 'Instrument', type: 'text', value: t => t.instrument, filterUi: 'both' },
    { name: 'Currency', type: 'text', value: t => t.currency, filterUi: 'valueList' },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number) },
    { name: 'Price', type: 'number', value: t => t.price },
    { name: 'TradeDate', type: 'date', value: t => t.tradeDate, header: 'Trade date', dateType: 'dateOnly' },
];

const latencyMs = 250;

// A round trip, or none once the source has asked for something else.
function delay(signal: AbortSignal): Promise<void> {
    signal.throwIfAborted();
    return new Promise<void>((resolve, reject) => {
        const abort = (): void => { clearTimeout(timer); reject(signal.reason); };
        const timer = setTimeout(() => { signal.removeEventListener('abort', abort); resolve(); }, latencyMs);
        signal.addEventListener('abort', abort, { once: true });
    });
}

/**
 * A trade server, simulated in the browser: 100,000 trades, answered a page at a time after a network delay. A real
 * one would run the query in its database; this one runs ExGrid's reference implementation, `applyQuery`, which is
 * what a server's answers are expected to match.
 */
export class TradeServer {
    readonly #trades = sampleTrades(100_000);
    #last: { readonly filter: GridFilter | undefined; readonly sorts: readonly SortSpec[]; readonly rows: readonly Trade[] } | undefined;
    #answered = 0;
    readonly #listeners = new Set<() => void>();

    /** How many pages the server has answered. */
    get answered(): number {
        return this.#answered;
    }

    /** Calls `listener` each time the server answers a page, until the returned function is called. */
    readonly subscribe = (listener: () => void): (() => void) => {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    };

    /** One page of the trades the query selects, in its order, and how many there are in all. */
    readonly page = async (query: GridQuery, signal: AbortSignal): Promise<GridPage<Trade>> => {
        await delay(signal);
        const result = this.#select(query.filter, query.sorts);
        const start = Math.min(query.range.start, result.length);
        const end = Math.min(start + query.range.count, result.length);
        this.#answered += 1;
        for (const listener of this.#listeners) listener();
        return { rows: result.slice(start, end), start, totalCount: result.length };
    };

    /** The distinct values of one column under a filter, for the filter panel's value list. */
    readonly distinct = async (name: string, filter: GridFilter | undefined, signal: AbortSignal): Promise<DistinctValues> => {
        await delay(signal);
        const column = columns.find(each => each.name === name);
        if (column === undefined) throw new RangeError(`The server has no column ${name}.`);
        // The columns with a value list hold text, which is told apart by value; a date column's list would key a day
        // by the day, since two objects can hold one. A missing value is the Blank, null.
        const values = new Set<unknown>();
        for (const trade of applyQuery(this.#trades, columns, filter, [])) {
            const value = column.value(trade);
            values.add(value === undefined ? null : value);
            if (values.size > 1_000) return { kind: 'tooMany' };
        }
        return { kind: 'values', values: [...values] };
    };

    // The last result is kept, so scrolling through one query does not sort the trades again. The source asks every
    // page of one query with the same Filter and Sort instances.
    #select(filter: GridFilter | undefined, sorts: readonly SortSpec[]): readonly Trade[] {
        const last = this.#last;
        if (last !== undefined && last.filter === filter && last.sorts === sorts) return last.rows;
        const rows = applyQuery(this.#trades, columns, filter, sorts);
        this.#last = { filter, sorts, rows };
        return rows;
    }
}

/** The server and the source over it: the source asks for one range at a time, reads ahead either side, and discards
 * an answer that arrives after the question changed. */
export function tradeDesk(): { readonly server: TradeServer; readonly source: FetchingGridSource<Trade> } {
    const server = new TradeServer();
    return { server, source: GridSource.fetch(server.page, { readAheadRows: 60, distinctValues: server.distinct }) };
}

const count = new Intl.NumberFormat('en-US');

/** How many trades the server holds under the query in force, and how many pages it has answered, followed as the
 * source changes. */
export function useServerCounts(desk: ReturnType<typeof tradeDesk>): { readonly total: string; readonly answered: number } {
    const { server, source } = desk;
    const subscribe = useCallback((listener: () => void) => source.subscribe(listener), [source]);
    const { totalCount } = useSyncExternalStore(subscribe, useCallback(() => source.snapshot(), [source]));
    const answered = useSyncExternalStore(server.subscribe, useCallback(() => server.answered, [server]));
    return { total: totalCount === undefined ? '…' : count.format(totalCount), answered };
}
