import { useCallback, useState, useSyncExternalStore } from 'react';
import type { DistinctValues, GridFilter, GridSource, GridSourceState, RowRange, SortSpec } from '@ex-grid/core';
import { tradeAt, tradeCount } from './MillionTrades';
import type { Trade } from './Trades';

/** Rows read either side of the rows asked for. */
export const readAhead = 50;

/** How far short of the Window's edge the grid asks, when it asks early: never more than `readAhead`, or the answer
 * would not reach it and the next row would ask again. */
export const margin = 10;

const roundTripMs = 150;

/** How often the grid asked: with every row on screen held, and with rows on screen waiting as placeholders. */
export interface AskedCounts {
    readonly early: number;
    readonly late: number;
}

const covers = (start: number, count: number, range: RowRange): boolean => range.start >= start && range.start + range.count <= start + count;

const someTrades = (start: number, count: number): readonly Trade[] => Array.from({ length: count }, (_, at) => tradeAt(start + at));

/**
 * The million trades as a server would send them, a Window at a time, for a grid that may ask before its Viewport runs
 * out (ADR-0183): a Grid Source of the page's own. Each question is answered after a round trip with the rows asked for
 * and `readAhead` either side. A question in flight whose rows cover the rows on screen is kept rather than asked again
 * (ADR-0182): replacing it at every scroll would restart the round trip for as long as a key is held. It counts the
 * questions, and how many of them found every row on screen already held. A question asked early is asked quietly: the
 * grid is dimmed only while rows on screen wait for an answer.
 */
export class ReadAheadTrades implements GridSource<Trade> {
    readonly rowKey = (trade: Trade): number => trade.id;
    // A trade's id is never repeated, so the grid need not check.
    readonly vouchesDistinctRows = true;
    readonly #listeners = new Set<() => void>();
    #state: GridSourceState<Trade> = Object.freeze({
        window: someTrades(0, 100), windowStart: 0, totalCount: tradeCount, loading: false, rowSequenceVersion: 0, sorts: [], filter: undefined,
    });
    // The rows asked of the server and not yet answered, if any.
    #asked: { readonly start: number; readonly count: number } | undefined;
    #counts: AskedCounts = Object.freeze({ early: 0, late: 0 });

    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    }

    snapshot(): GridSourceState<Trade> {
        return this.#state;
    }

    /** The questions asked so far: the same object until another is asked. */
    counts(): AskedCounts {
        return this.#counts;
    }

    /** Starts the counts again. */
    resetCounts(): void {
        this.#counts = Object.freeze({ early: 0, late: 0 });
        this.#notify();
    }

    // This server neither sorts nor filters, and its columns say so: the grid never asks.
    setColumns(): void { /* The columns name nothing this server reads. */ }

    setSorts(sorts: readonly SortSpec[]): void {
        if (sorts.length > 0) throw new Error('This server does not sort.');
    }

    setFilter(filter: GridFilter | undefined): void {
        if (filter !== undefined) throw new Error('This server does not filter.');
    }

    /** The grid's Range Request: it names the rows on screen, early or not. */
    async needRange(range: RowRange): Promise<void> {
        const { window, windowStart } = this.#state;
        const held = covers(windowStart, window.length, range);
        const inFlight = this.#asked;
        if (inFlight !== undefined && covers(inFlight.start, inFlight.count, range)) {
            // Kept; but once the rows on screen leave the rows held, they wait for it.
            if (!held && !this.#state.loading) this.#take({ ...this.#state, loading: true });
            return;
        }
        const { early, late } = this.#counts;
        this.#counts = Object.freeze(held ? { early: early + 1, late } : { early, late: late + 1 });
        const start = Math.max(0, range.start - readAhead);
        const end = Math.min(tradeCount, range.start + range.count + readAhead);
        const asked = Object.freeze({ start, count: end - start });
        this.#asked = asked;
        this.#take({ ...this.#state, loading: !held });
        // Where a server call would go.
        await new Promise<void>(resolve => { setTimeout(resolve, roundTripMs); });
        // A newer question replaced this one while it waited: its answer would be stale.
        if (this.#asked !== asked) return;
        this.#asked = undefined;
        this.#take({ ...this.#state, window: someTrades(start, end - start), windowStart: start, loading: false });
    }

    /** The rows of a copy that reaches past the Window: asked of the server on their own. */
    async rows(range: RowRange, signal: AbortSignal): Promise<readonly Trade[]> {
        await new Promise<void>(resolve => { setTimeout(resolve, roundTripMs); });
        signal.throwIfAborted();
        return someTrades(range.start, Math.min(range.count, tradeCount - range.start));
    }

    distinctValues(): Promise<DistinctValues> {
        return Promise.resolve({ kind: 'tooMany' });
    }

    #take(state: GridSourceState<Trade>): void {
        this.#state = Object.freeze(state);
        this.#notify();
    }

    #notify(): void {
        for (const listener of this.#listeners) listener();
    }
}

/** The page's source, whether it asks early, and the counts under the grid, followed as the source asks. */
export function useAskEarly() {
    const [source] = useState(() => new ReadAheadTrades());
    // Off at first: the grid asks once the rows on screen are not all held, as by default.
    const [early, setEarly] = useState(false);
    const subscribe = useCallback((listener: () => void) => source.subscribe(listener), [source]);
    const counts = useSyncExternalStore(subscribe, useCallback(() => source.counts(), [source]));
    const toggle = useCallback((on: boolean) => {
        setEarly(on);
        source.resetCounts();
    }, [source]);
    return {
        source, early, toggle, rangeRequestMargin: early ? margin : 0,
        asked: `Asked ${String(counts.early + counts.late)} times: ${String(counts.early)} with every row on screen held, ${String(counts.late)} with rows on screen waiting as placeholders.`,
    };
}
