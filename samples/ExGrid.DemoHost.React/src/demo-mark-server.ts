// DemoMarkServer.cs in TypeScript: the server behind the Row Marks page (ADR-0043). A million rows, a Filter on
// the desk, rows that can be added and deleted "elsewhere", and the Consumer's half of the marks, the snapshot
// marker, the counts and the action, answered from the marks' steps, never from positions. Not a model of a real
// server's storage; a model of what one has to answer.
import { markColumn, RowMarkCounts, type GridColumn, type GridFilter, type GridPage, type GridQuery, type RowMarkAdapter, type RowMarkState } from '@ex-grid/core';
import { pick } from './demo-data';

/** A row of the Row Marks page: every value derives from its index, which is also its key. */
export interface DemoMarkRow {
    readonly index: number;
    readonly desk: string;
    readonly amount: number;
}

const desks = ['Alpha', 'Bravo', 'Charlie', 'Delta', 'Echo'] as const;

/** DemoMarkServer.Columns. */
export const markColumns: readonly GridColumn<DemoMarkRow>[] = Object.freeze([
    markColumn<DemoMarkRow>({ width: 40 }),
    { name: 'Index', type: 'number', value: (row: DemoMarkRow) => row.index, width: 100 },
    { name: 'Desk', type: 'text', value: (row: DemoMarkRow) => row.desk, width: 120 },
    { name: 'Amount', type: 'number', value: (row: DemoMarkRow) => row.amount, width: 140 },
]);

function deskOf(index: number): string {
    return pick(desks, index % desks.length);
}

function rowOf(index: number): DemoMarkRow {
    return Object.freeze({ index, desk: deskOf(index), amount: ((index * 7919) % 1_999_999) / 100 });
}

/** The one Filter this server understands: Desk equals a value. Anything else is refused rather than ignored: a
 * server that answered an unfiltered result under a Filter would be quietly wrong. */
function deskFilter(filter: GridFilter | undefined): string | undefined {
    if (filter === undefined) return undefined;
    const names = Object.keys(filter.columns);
    if (names.length === 0) return undefined;
    const clauses = filter.columns['Desk']?.clauses;
    const clause = clauses?.[0];
    if (names.length === 1 && clauses?.length === 1 && clause?.operator === 'equals' && typeof clause.value === 'string') return clause.value;
    throw new Error('The demo server filters on Desk equals a value, and on nothing else.');
}

function matches(index: number, desk: string | undefined): boolean {
    return desk === undefined || deskOf(index).toLowerCase() === desk.toLowerCase();
}

/**
 * When rows came and went, on one clock that a snapshot reads too: a snapshot's marker is the clock's reading, and a
 * row belongs to it when the row was added before it and not deleted before it. The rows the server starts with
 * were added at 0.
 */
class History {
    #clock = 0;
    readonly #added = new Map<number, number>();
    readonly #deleted = new Map<number, number>();

    tick(): number {
        return ++this.#clock;
    }

    add(index: number): void {
        this.#added.set(index, this.tick());
    }

    delete(index: number): void {
        this.#deleted.set(index, this.tick());
    }

    get deleted(): readonly number[] {
        return [...this.#deleted.keys()];
    }

    /** Whether the row existed when the clock read `asOf`. */
    existedAt(index: number, asOf: number): boolean {
        const added = this.#added.get(index);
        const deleted = this.#deleted.get(index);
        return (added === undefined || added < asOf) && (deleted === undefined || deleted > asOf);
    }
}

/** The server behind /marks. */
export class DemoMarkServer {
    readonly #live: number[] = [];
    readonly #history = new History();
    #next: number;
    #version = 0;
    #filtered: { readonly desk: string; readonly version: number; readonly rows: readonly number[] } | undefined;

    constructor(rows: number) {
        for (let i = 0; i < rows; i++) this.#live.push(i);
        this.#next = rows;
    }

    /** One page of the result under the Query, which never sorts: the server keeps its own order. */
    fetch(query: GridQuery): GridPage<DemoMarkRow> {
        if (query.sorts.length > 0) throw new Error('The demo server keeps its own order; it does not sort.');
        const result = this.#result(query.filter);
        const start = Math.min(query.range.start, result.length);
        const count = Math.min(query.range.count, result.length - start);
        return { rows: result.slice(start, start + count).map(rowOf), start, totalCount: result.length };
    }

    /** A row added elsewhere: it joins the result after any "all" already taken. */
    add(): void {
        this.#history.add(this.#next);
        this.#live.push(this.#next++);
        this.#version++;
    }

    /** A row deleted elsewhere: its mark can still name it until the screen hears. */
    delete(index: number): void {
        const at = this.#live.indexOf(index);
        if (at < 0) return;
        this.#live.splice(at, 1);
        this.#history.delete(index);
        this.#version++;
    }

    /** The Consumer's half of Row Marks. */
    adapter(): RowMarkAdapter<DemoMarkRow> {
        return {
            key: row => row.index,
            openSnapshot: () => Promise.resolve(this.#history.tick()),
            belongsTo: (row, snapshot) => this.#history.existedAt(row.index, Number(snapshot.asOf)) && matches(row.index, deskFilter(snapshot.filter)),
            count: (state, filter) => Promise.resolve(this.#count(state, filter)),
        };
    }

    /**
     * The action (ADR-0043): run over the marks by identity, verified against what exists now, and reported as it
     * went. A mark naming a row deleted elsewhere is counted as gone, never passed off as done.
     */
    approve(state: RowMarkState): { readonly applied: number; readonly gone: number } {
        const applied = marked(state, this.#live, this.#history).length;
        // The deleted rows the marks still name, by the same evaluation: a later step that unmarked one, or a
        // snapshot taken while it existed, decides as it does for a live row.
        const gone = marked(state, this.#history.deleted, this.#history).length;
        return { applied, gone };
    }

    #result(filter: GridFilter | undefined): readonly number[] {
        const desk = deskFilter(filter);
        if (desk === undefined) return this.#live;
        const cached = this.#filtered;
        if (cached !== undefined && cached.version === this.#version && cached.desk.toLowerCase() === desk.toLowerCase()) return cached.rows;
        const rows = this.#live.filter(index => matches(index, desk));
        this.#filtered = { desk, version: this.#version, rows };
        return rows;
    }

    #count(state: RowMarkState, filter: GridFilter | undefined): RowMarkCounts {
        const desk = deskFilter(filter);
        let inResult = 0;
        let total = 0;
        for (const index of marked(state, this.#live, this.#history)) {
            total++;
            if (matches(index, desk)) inResult++;
        }
        return new RowMarkCounts({ markedInResult: inResult, rowsInResult: this.#result(filter).length, markedOutsideResult: total - inResult });
    }
}

/** The marked live rows, as indices: the same evaluation the source makes per painted row, made here over the
 * whole data with each snapshot's desk read once. */
function marked(state: RowMarkState, rows: readonly number[], history: History): number[] {
    const keyed = new Map<number, { readonly step: number; readonly marked: boolean }>();
    const snapshots: { readonly step: number; readonly asOf: number; readonly desk: string | undefined; readonly marked: boolean }[] = [];
    state.steps.forEach((step, at) => {
        if (step.kind === 'oneKey') keyed.set(Number(step.key), { step: at, marked: step.marked });
        else snapshots.push({ step: at, asOf: Number(step.snapshot.asOf), desk: deskFilter(step.snapshot.filter), marked: step.marked });
    });
    return rows.filter(index => {
        const own = keyed.get(index);
        const step = own === undefined ? -1 : own.step;
        for (let s = snapshots.length - 1; s >= 0; s--) {
            const snapshot = snapshots[s];
            if (snapshot === undefined || snapshot.step <= step) break;
            if (history.existedAt(index, snapshot.asOf) && matches(index, snapshot.desk)) return snapshot.marked;
        }
        return own !== undefined && own.marked;
    });
}
