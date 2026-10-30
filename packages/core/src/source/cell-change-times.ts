// A live source's Change Highlight (ADR-0141, on ADR-0067's rules and ADR-0068's declaration): when each
// cell's displayed text last changed, by Row Key and column name, and the one function the Consumer hands
// the grid. The source records here as it pairs a row's new version with the one it had; the grid asks here
// as it paints. The counterpart of C#'s CellChangeTimes.
//
// One function for the source's life: a row whose object changed paints and asks again, and no other row
// has anything new to ask, so the function never has to change.
//
// Memory is bounded on two sides. A key the source removes takes its times with it, and a time older than
// `keptFor` is let go, so a source whose every row changes once a day does not keep a time for each. What is
// let go is decided by data, never by when a paint happens to ask: times are let go only as new ones are
// recorded, and the function answers nothing for a time older than `keptFor` whether or not it has been let
// go yet, so a mark never depends on when it was asked for.
//
// Nothing is recorded until the function has been read: a source whose Consumer never asked for the Change
// Highlight compares nothing (ADR-0068: without the declaration, nothing changes).

import { textOf } from '../cells/display-text';
import type { QueryColumn } from '../query/columns';
import type { GridClock } from './grid-clock';
import type { RowKey, RowKeyOf } from './row-key';

/** When a cell's displayed text last changed, in milliseconds since the epoch, or `undefined` when it did not
 * change lately: the grid's `cellChangedAt`. */
export type CellChangedAt<Row> = (row: Row, column: string) => number | undefined;

/** How long a change time answers unless the Consumer says otherwise: sixty times the grid's default mark. */
export const defaultChangeTimesKeptFor = 60_000;

/** One pairing a publication found: a row that stood in the result and still does, its version then and the
 * version to compare it with. */
export interface ChangedPair<Row> {
    readonly key: RowKey;
    readonly old: Row;
    readonly new: Row;
}

export class CellChangeTimes<Row> {
    readonly #rowKey: RowKeyOf<Row>;
    readonly #clock: GridClock;
    readonly #times = new Map<RowKey, KeyTimes>();
    /** Every time recorded, oldest first, so that letting the old ones go costs what was recorded and never a
     * walk over everything kept. */
    readonly #recorded: { readonly key: RowKey; readonly at: number }[] = [];
    #recordedFrom = 0;
    #keptFor = defaultChangeTimesKeptFor;
    #wanted = false;
    readonly #lookup: CellChangedAt<Row>;

    constructor(rowKey: RowKeyOf<Row>, clock: GridClock) {
        this.#rowKey = rowKey;
        this.#clock = clock;
        this.#lookup = Object.freeze((row: Row, column: string) => this.#at(row, column));
    }

    /** The function the Consumer hands the grid. Reading it starts the recording. */
    get lookup(): CellChangedAt<Row> {
        this.#wanted = true;
        return this.#lookup;
    }

    /** Whether anyone has asked for the function, and so whether pairing is worth the comparison. */
    get wanted(): boolean {
        return this.#wanted;
    }

    /** How long, in milliseconds, a change time answers. A time kept for no time would mark nothing, so one
     * that is not more than zero is refused. */
    get keptFor(): number {
        return this.#keptFor;
    }

    set keptFor(milliseconds: number) {
        if (!(milliseconds > 0) || !Number.isFinite(milliseconds)) {
            throw new RangeError(`A change time is kept for a finite number of milliseconds above 0, not ${milliseconds}, `
                + 'or no cell is ever marked (ADR-0141, ADR-0068).');
        }
        this.#keptFor = milliseconds;
    }

    /**
     * Records what one publication found: for each of `pairs`, the columns whose displayed text differs
     * between the two versions; for each key of `appeared`, every column; and the keys of `removed`
     * forgotten. The text compared is what the cell displays (`textOf`), so a change the format hides is not
     * marked (ADR-0067). A column that carries no value (ADR-0020) is not asked.
     */
    record(columns: readonly QueryColumn<Row>[], pairs: readonly ChangedPair<Row>[], appeared: readonly RowKey[], removed: readonly RowKey[]): void {
        if (!this.#wanted) return;
        const now = this.#clock.now();
        // Compared before anything is kept: a format that throws leaves the times as they were.
        const changed: { readonly key: RowKey; readonly column: string }[] = [];
        for (const pair of pairs) {
            for (const column of columns) {
                if (column.queryable === false) continue;
                if (textOf(column, pair.old) !== textOf(column, pair.new)) changed.push({ key: pair.key, column: column.name });
            }
        }
        for (const key of removed) this.#times.delete(key);
        for (const { key, column } of changed) {
            this.#timesOf(key).changed(column, now);
            this.#recorded.push({ key, at: now });
        }
        for (const key of appeared) {
            this.#timesOf(key).appeared(now);
            this.#recorded.push({ key, at: now });
        }
        this.#letGo(now);
    }

    #timesOf(key: RowKey): KeyTimes {
        let times = this.#times.get(key);
        if (times === undefined) {
            times = new KeyTimes();
            this.#times.set(key, times);
        }
        return times;
    }

    #letGo(now: number): void {
        const horizon = now - this.#keptFor;
        while (this.#recordedFrom < this.#recorded.length) {
            const oldest = this.#recorded[this.#recordedFrom];
            if (oldest === undefined || oldest.at >= horizon) break;
            this.#recordedFrom++;
            const times = this.#times.get(oldest.key);
            if (times !== undefined && times.newest < horizon) this.#times.delete(oldest.key);
        }
        // The queue's front is closed once it is half the queue, so letting go costs, spread over the times
        // recorded, a constant each.
        if (this.#recordedFrom > 64 && this.#recordedFrom * 2 > this.#recorded.length) {
            this.#recorded.splice(0, this.#recordedFrom);
            this.#recordedFrom = 0;
        }
    }

    #at(row: Row, column: string): number | undefined {
        const at = this.#times.get(this.#rowKey(row))?.at(column);
        // A time older than the horizon answers nothing whether or not it has been let go yet, so what the grid
        // paints never depends on when the letting go happened.
        return at !== undefined && this.#clock.now() - at <= this.#keptFor ? at : undefined;
    }
}

/** One row's change times: when it appeared, if it did, and when each column whose text changed last
 * changed. */
class KeyTimes {
    #appeared: number | undefined;
    readonly #columns = new Map<string, number>();
    newest = Number.NEGATIVE_INFINITY;

    appeared(at: number): void {
        this.#appeared = at;
        this.newest = at;
    }

    changed(column: string, at: number): void {
        this.#columns.set(column, at);
        this.newest = at;
    }

    /** The later of when the row appeared and when the column last changed. */
    at(column: string): number | undefined {
        const changed = this.#columns.get(column);
        if (changed === undefined) return this.#appeared;
        return this.#appeared === undefined || changed > this.#appeared ? changed : this.#appeared;
    }
}
