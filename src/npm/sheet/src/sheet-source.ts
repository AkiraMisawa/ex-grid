// The Grid Source a Sheet is to ExGrid (ADR-0046, ADR-0350 section 16): 1,048,576 rows that are places, of which
// it holds a Window of row instances around what the grid shows. It neither sorts nor filters: a Sheet's rows are
// places, and its columns are not queryable. Its Row Sequence Version moves only when another Sheet is shown,
// since that is the only time a position comes to mean another row (ADR-0011).

import type { DistinctValues, GridFilter, GridFindRequest, GridFindResult, GridSource, GridSourceState, QueryColumn, RowRange, SortSpec } from '@ex-grid/core';
import { Sheet, type SheetChange } from '@ex-grid/sheet-engine';
import { sheetFindStep } from './sheet-find';
import { SheetRow } from './sheet-row';

/** A Grid Source over the Sheet shown. */
export class SheetSource implements GridSource<SheetRow> {
    readonly #listeners = new Set<() => void>();
    #sheet: Sheet;
    #version = 0;
    // The Sheet shown under the version before, which a grid that has not yet taken the newer rows still paints.
    #before: { readonly version: number; readonly sheet: Sheet } | undefined;
    #state: GridSourceState<SheetRow>;

    // One step's answer over the Sheet the step was asked of: the Sheet's own walk unless one is given, as a Sheet
    // searching Within the Workbook gives (ADR-0332 §5).
    readonly #step: (sheet: Sheet, request: GridFindRequest) => Promise<GridFindResult>;

    constructor(sheet: Sheet, step: (sheet: Sheet, request: GridFindRequest) => Promise<GridFindResult> = (over, request) => Promise.resolve(sheetFindStep(over, request))) {
        this.#sheet = sheet;
        this.#step = step;
        this.#state = SheetSource.#stateOf([], 0, 0);
    }

    /** The Sheet the rows are of. */
    get sheet(): Sheet { return this.#sheet; }

    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    }

    snapshot(): GridSourceState<SheetRow> {
        return this.#state;
    }

    setColumns(_columns: readonly QueryColumn<SheetRow>[]): void {
        // The columns name no Sort and no Filter here; their widths and declarations are the grid's.
    }

    setSorts(sorts: readonly SortSpec[]): void {
        if (sorts.length > 0) throw new Error("A Sheet's rows are places, and are not sorted (ADR-0046).");
    }

    setFilter(filter: GridFilter | undefined): void {
        if (filter !== undefined) throw new Error("A Sheet's rows are places, and are not filtered (ADR-0046).");
    }

    needRange(range: RowRange): Promise<void> {
        const { window, windowStart } = this.#state;
        if (range.start >= windowStart && range.start + range.count <= windowStart + window.length) return Promise.resolve();
        // A screen above and below what the grid shows, so a short scroll is answered from the rows in hand.
        const start = Math.max(0, range.start - range.count);
        const end = Math.min(Sheet.rowCount, range.start + 2 * range.count);
        const rows = Array.from({ length: Math.max(0, end - start) }, (_, i) => this.#rowAt(start + i));
        this.#state = SheetSource.#stateOf(rows, start, this.#version);
        // Heard after the grid's paint that asked, never inside it.
        return Promise.resolve().then(() => { this.#publish(); });
    }

    rows(range: RowRange, signal: AbortSignal): Promise<readonly SheetRow[]> {
        signal.throwIfAborted();
        const end = Math.min(Sheet.rowCount, range.start + range.count);
        return Promise.resolve(Array.from({ length: Math.max(0, end - range.start) }, (_, i) => this.#rowAt(range.start + i)));
    }

    // The row at `index`: the Window's instance where the Window holds it, so a row read twice is one instance
    // (ADR-0003), else a new one.
    #rowAt(index: number): SheetRow {
        const { window, windowStart } = this.#state;
        const at = index - windowStart;
        const kept = at >= 0 && at < window.length ? window[at] : undefined;
        return kept ?? new SheetRow(this.#sheet, index);
    }

    /** One Find step over the Sheet its Row Sequence Version names (ADR-0055): the one shown, or the one before it,
     * which a grid that has not yet taken the newer rows still paints, and whose answer it discards by the version
     * once it has. A step asked under an older version is given up, never answered "not found" about cells nobody
     * searched. */
    find(request: GridFindRequest, signal: AbortSignal): Promise<GridFindResult> {
        signal.throwIfAborted();
        const before = this.#before;
        const sheet = request.rowSequenceVersion === this.#version ? this.#sheet
            : request.rowSequenceVersion === before?.version ? before.sheet : undefined;
        if (sheet === undefined) return Promise.reject(new DOMException('The step was asked of a Sheet shown before the last one.', 'AbortError'));
        return this.#step(sheet, request);
    }

    distinctValues(_column: string, _signal: AbortSignal): Promise<DistinctValues> {
        return Promise.reject(new Error("A Sheet's columns are not queryable, so they offer no values to filter by (ADR-0046)."));
    }

    /** Shows another Sheet, or the same Sheet read whole: every position names a row of it now, so the Row
     * Sequence Version moves (ADR-0011). */
    show(sheet: Sheet): void {
        this.#before = { version: this.#version, sheet: this.#sheet };
        this.#sheet = sheet;
        this.#version++;
        const { window, windowStart } = this.#state;
        const rows = window.map((_, i) => new SheetRow(sheet, windowStart + i));
        this.#state = SheetSource.#stateOf(rows, windowStart, this.#version);
        this.#publish();
    }

    /** Takes what the Sheet shown changed: a new instance for every row the changes name, the same instance for
     * every other row, so only the changed rows are painted again (ADR-0003, SH-4). Answers whether any row in
     * the Window was renewed. */
    changed(changes: readonly SheetChange[]): boolean {
        const { window, windowStart } = this.#state;
        let rows: SheetRow[] | undefined;
        for (const change of changes) {
            for (const row of change.rows) {
                const at = row - windowStart;
                if (at < 0 || at >= window.length) continue;
                rows ??= [...window];
                rows[at] = new SheetRow(this.#sheet, row);
            }
        }
        if (rows === undefined) return false;
        this.#state = SheetSource.#stateOf(rows, windowStart, this.#version);
        this.#publish();
        return true;
    }

    /** Renews every row in the Window, so each is read again: a change the engine names no row of, such as a Linked
     * Table gone back to waiting on another Sheet's account (ADR-0049). */
    renewWindow(): void {
        const { window, windowStart } = this.#state;
        if (window.length === 0) return;
        this.#state = SheetSource.#stateOf(window.map((_, i) => new SheetRow(this.#sheet, windowStart + i)), windowStart, this.#version);
        this.#publish();
    }

    #publish(): void {
        // A listener may stop hearing as it hears.
        for (const listener of Array.from(this.#listeners)) listener();
    }

    static #stateOf(window: readonly SheetRow[], windowStart: number, version: number): GridSourceState<SheetRow> {
        return Object.freeze({
            window: Object.freeze(window),
            windowStart,
            totalCount: Sheet.rowCount,
            loading: false,
            rowSequenceVersion: version,
            sorts: Object.freeze([]),
            filter: undefined,
        });
    }
}
