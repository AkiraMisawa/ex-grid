// @vitest-environment happy-dom
// A Window holds no row twice (ADR-0003, ADR-0141): a Window no source vouches for is walked whole as it arrives, and
// the painted rows are checked whatever was vouched, so a null row, a null key or a repeat is refused by name, naming
// its positions, rather than painted as two places the grid cannot tell apart (ERR-4, LV-2, LV-10).
import { afterEach, describe, expect, it } from 'vitest';
import type { GridSource, GridSourceState } from '../src/source/grid-source';
import type { RowKeyOf } from '../src/source/row-key';
import { columns, mount, painted, rowsOf, trades, unmountAll, type Trade } from './grid-harness';

afterEach(() => { unmountAll(); });

/** A Consumer's own source over the rows it is shown, which vouches for them only where it is told to. */
class OwnSource implements GridSource<Trade> {
    readonly #listeners = new Set<() => void>();
    readonly rowKey: RowKeyOf<Trade> | undefined;
    readonly vouchesDistinctRows: boolean;
    #rows: readonly Trade[];
    #version = 0;

    constructor(rows: readonly Trade[], { rowKey, vouches = false }: { readonly rowKey?: RowKeyOf<Trade>; readonly vouches?: boolean } = {}) {
        this.#rows = rows;
        this.rowKey = rowKey;
        this.vouchesDistinctRows = vouches;
    }

    show(rows: readonly Trade[]): void {
        this.#rows = rows;
        this.#version += 1;
        for (const listener of this.#listeners) listener();
    }

    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    }

    snapshot(): GridSourceState<Trade> {
        return { window: this.#rows, windowStart: 0, totalCount: this.#rows.length, loading: false, rowSequenceVersion: this.#version, sorts: [], filter: undefined };
    }

    setColumns(): void { /* nothing is queried */ }
    setSorts(): void { throw new Error('Not sorted.'); }
    setFilter(): void { throw new Error('Not filtered.'); }
    needRange(): Promise<void> { return Promise.resolve(); }
    rows(): Promise<readonly Trade[]> { return Promise.resolve(this.#rows); }
    distinctValues(): Promise<never> { return Promise.reject(new Error('No value lists.')); }
}

const byBook: RowKeyOf<Trade> = trade => trade.book;
const grid = (source: OwnSource): ReturnType<typeof mount<Trade>> => mount({ columns, source, rowHeight: 24, viewportHeight: 480 });

describe('a Window that holds a row twice', () => {
    it('ERR-4 / ADR-0003: one object twice in a Window no source vouches for is refused, naming both positions, though one is off the screen', () => {
        const rows = trades(200);
        expect(() => grid(new OwnSource([...rows, rows[3] as Trade])))
            .toThrow('The rows at positions 3 and 200 are the same object');
    });

    it('ERR-4 / ADR-0003: a null row is refused, naming its position', () => {
        const rows = trades(5) as (Trade | null)[];
        rows[2] = null;
        expect(() => grid(new OwnSource(rows as Trade[]))).toThrow('The row at position 2 is null');
    });

    it('LV-2 / ADR-0140: a Row Key that repeats, or a null one, is refused by name, naming the key and its positions', () => {
        const rows = trades(5);
        expect(() => grid(new OwnSource([...rows, { book: 'Book-1', notional: 7 }], { rowKey: byBook })))
            .toThrow("The rows at positions 1 and 5 both have the Row Key 'Book-1'");
        expect(() => grid(new OwnSource([...rows, { book: null as unknown as string, notional: 7 }], { rowKey: byBook })))
            .toThrow('The row at position 5 has a null Row Key');
    });

    it('LV-10 / ADR-0141: a Window that is vouched for is not walked whole, and its painted rows still are', async () => {
        const rows = trades(200);
        // A repeat the screen never shows is the vouching party's: the grid paints the rows it shows.
        const source = new OwnSource([...rows, { book: 'Book-3', notional: 7 }], { rowKey: byBook, vouches: true });
        const { root } = grid(source);
        await painted();
        expect(rowsOf(root).length).toBeGreaterThan(0);
        // The same promise broken where it is painted is refused by name.
        expect(() => grid(new OwnSource([...trades(5), { book: 'Book-3', notional: 7 }], { rowKey: byBook, vouches: true })))
            .toThrow("The rows at positions 3 and 5 both have the Row Key 'Book-3'");
    });

    it('ADR-0141: a new Window from the source is checked as it arrives', () => {
        const rows = trades(5);
        const source = new OwnSource(rows);
        grid(source);
        expect(() => { source.show([...rows, rows[0] as Trade]); }).toThrow('The rows at positions 0 and 5 are the same object');
    });
});
