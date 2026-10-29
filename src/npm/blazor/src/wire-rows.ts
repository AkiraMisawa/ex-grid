// Rows and columns as .NET sends them (ADR-0350 sections 3 and 15). A row is .NET's: the bridge holds the
// values its columns read of it, and an id by which .NET finds the row again when the grid hands one back in
// an event. The same row, sent again with the same values, is the same object, so the core skips it as it
// skips any row that did not change (ADR-0003); a row the grid no longer holds anywhere is let go on .NET's
// side too, once the browser has collected it, so .NET never keeps a row the grid can still hand back
// without, nor holds every row it ever sent.

import {
    actionColumn, markColumn, type CellAlign, type CellAppearance, type CellState, type CellTone, type ColumnType, type DateType, type EditVerdict, type FilterUi,
    type GridAction, type GridColumn, type GridColumnWidth, type GridTemplate, type RowKey, type RowKind,
} from '@ex-grid/core';
import { rowKeyOf, valueKey, valueOf } from './wire-values';

/** A row of .NET's as the grid holds it: the values its columns read, and the id .NET knows it by. */
export interface BlazorRow {
    /** The id .NET sent the row with, by which an event names it back. */
    readonly id: number;
    /** The row's Row Key, where the source names its rows by one (ADR-0140). */
    readonly key: RowKey | undefined;
    /** The value of each column, in the columns' order. */
    readonly cells: readonly unknown[];
    /** The Cell State of each column, in the columns' order, where .NET answers one other than Normal for any of
     * them (ADR-0006): a row whose states change is another object, so the core paints it again (ADR-0003). */
    readonly states: readonly CellState[] | undefined;
    /** The row read as the table's row, for a grid of a Pointing Scope (ADR-0430): its Values as .NET's `SheetWire`
     * writes a Linked Table's row, which the Scope reads. */
    readonly table: readonly unknown[] | undefined;
    /** The row's Row Kind where the Consumer answers one (ADR-0024): `undefined` is a detail row. */
    readonly kind: RowKind | undefined;
    /** Whether the row is marked, where the grid has Row Marks (ADR-0043): `undefined` where it has none. */
    readonly marked: boolean | undefined;
    /** Each cell's Font, Fill and Borders, in the columns' order, where the Consumer answers one other than none for
     * any of them (ADR-0050 item 15): `null` is none. */
    readonly appearances: readonly (CellAppearance | null)[] | undefined;
    /** When each cell's text last changed, in the columns' order, on the browser's clock, where the Consumer answers a
     * time for any of them (ADR-0068): `null` is no change lately. */
    readonly changedAt: readonly (number | null)[] | undefined;
}

/** A row as .NET sends it: its id, its Row Key where the source has one, its cells, for each column .NET
 * formats, the text of the cell's value, `null` where it does not, and the Cell State of each cell, where the
 * Consumer answers one and it is not Normal for every cell of the row. */
export interface WireRow {
    readonly i: number;
    readonly k?: unknown;
    readonly c: readonly unknown[];
    readonly t?: readonly (string | null)[];
    readonly s?: readonly CellState[];
    readonly p?: readonly unknown[];
    /** The Row Kind, where it is not a detail row (ADR-0024). */
    readonly r?: string;
    /** Whether the row is marked, where the grid has Row Marks (ADR-0043). */
    readonly m?: boolean;
    /** For each column whose values .NET tones, the tone of the cell's value, `null` where the column tones nothing or
     * the cell is Blank (ADR-0006). */
    readonly o?: readonly (CellTone | null)[];
    /** Each cell's appearance, where the Consumer answers one other than none for any cell of the row. */
    readonly a?: readonly (CellAppearance | null)[];
    /** When each cell's text last changed, in milliseconds since the epoch on .NET's clock, where the Consumer answers
     * a time for any cell of the row (ADR-0068). */
    readonly h?: readonly (number | null)[];
}

/** A column as .NET declares it. `format`, where .NET formats the column's values and sends each cell's text with
 * them, names that format and the culture it writes in: another name is another format, whose texts are new. */
export interface WireColumn {
    readonly name: string;
    readonly type: ColumnType;
    readonly header: string;
    readonly width: GridColumnWidth;
    readonly editable: boolean;
    readonly align: CellAlign;
    readonly headerAlign: CellAlign;
    readonly filterUi: FilterUi;
    readonly dateType: DateType;
    readonly queryable: boolean;
    readonly format: string | null;
    /** Whether .NET judges a commit into the column (ADR-0034): the core asks, and the commit is pending until .NET
     * has answered (ADR-0380 section 5). */
    readonly validates?: true;
    readonly actions?: readonly GridAction[];
    /** Whether each cell's content is the column's Razor fragment, which .NET draws from the row (ADR-0020, ADR-0410
     * section 6). */
    readonly template?: true;
    /** Whether .NET tones the column's values (ADR-0006): each row carries the tone of its value, as a formatted column
     * carries its text. */
    readonly tone?: true;
    /** Whether the column is the Mark Column (ADR-0043). */
    readonly mark?: true;
}

/** .NET's verdict on a commit into `column` of `row`, asked across the wire: `signal` aborts as the user cancels the
 * commit (ADR-0380 section 3). */
export type VerdictOf = (row: BlazorRow, column: string, text: string, signal: AbortSignal) => Promise<EditVerdict>;

/** One column's texts, by the value they are the text of. C#'s format is a function of the value alone, so a
 * value is shown one way wherever it stands. A text is kept while a row the grid can still read holds its value,
 * or the column's last value list does, and no longer: scrolling a million values past leaves only the shown. */
class FormatTexts {
    readonly #texts = new Map<string, { readonly text: string; holders: number }>();
    readonly #column: string;
    readonly #what: 'formats' | 'tones';

    /** `what` names what .NET answers of each value, a text or a tone (ADR-0006): each is a function of the value alone. */
    constructor(column: string, what: 'formats' | 'tones' = 'formats') {
        this.#column = column;
        this.#what = what;
    }

    /** Checks `text` against the one held for `value`, and answers the value's key, to hold it by. */
    record(value: unknown, text: string): string {
        const key = valueKey(value);
        const held = this.#texts.get(key);
        // The bridge reads a value less finely than .NET held it where a decimal's scale is lost to a double:
        // two texts for one value would leave the grid to pick one, and it does not pick (the spine's first rule).
        if (held === undefined) {
            this.#texts.set(key, { text, holders: 0 });
        } else if (held.text !== text) {
            throw new Error(`Column '${this.#column}' ${this.#what} one value two ways, '${held.text}' and '${text}': the grid reads them as one value `
                + `(a decimal's trailing zeros do not cross to the browser), so its ${this.#what === 'formats' ? 'text' : 'tone'} must not depend on them.`);
        }
        return key;
    }

    hold(key: string): void {
        const held = this.#texts.get(key);
        if (held === undefined) throw new Error(`Internal: column '${this.#column}' held a text it never recorded.`);
        held.holders += 1;
    }

    letGo(key: string): void {
        const held = this.#texts.get(key);
        if (held === undefined) throw new Error(`Internal: column '${this.#column}' let go of a text it does not hold.`);
        held.holders -= 1;
        if (held.holders === 0) this.#texts.delete(key);
    }

    textOf(value: unknown): string {
        const held = this.#texts.get(valueKey(value));
        if (held === undefined) throw new Error(`Internal: column '${this.#column}' was asked the text of a value no row it holds was sent with.`);
        return held.text;
    }

    /** How many texts are kept, for the tests. */
    get size(): number {
        return this.#texts.size;
    }
}

/** The texts one row object, or one value list, holds: each once, whatever number of times it arrives. */
class TextHolding {
    readonly #held = new Map<FormatTexts, Set<string>>();

    hold(texts: FormatTexts, value: unknown, text: string): void {
        const key = texts.record(value, text);
        let keys = this.#held.get(texts);
        if (keys === undefined) {
            keys = new Set();
            this.#held.set(texts, keys);
        }
        if (keys.has(key)) return;
        keys.add(key);
        texts.hold(key);
    }

    letGo(): void {
        for (const [texts, keys] of this.#held) {
            for (const key of keys) texts.letGo(key);
        }
        this.#held.clear();
    }
}

interface HeldColumn {
    readonly signature: string;
    readonly column: GridColumn<BlazorRow>;
    readonly texts: FormatTexts | undefined;
    readonly tones: FormatTexts | undefined;
}

/** The columns as the core takes them, the same objects while .NET declares them the same. */
export class ColumnSet {
    #held: readonly HeldColumn[] = [];
    #indexes = new Map<GridColumn<BlazorRow>, number>();
    #named = new Map<string, number>();
    #version = 0;
    readonly #verdict: VerdictOf | undefined;
    readonly #template: GridTemplate<BlazorRow> | undefined;
    readonly #valueLists = new Map<string, TextHolding>();
    readonly #awaited: { readonly version: number; readonly reached: () => void }[] = [];

    /** `verdict` asks .NET to judge a commit, for the columns it says it validates; `template` mounts a Template cell's
     * content, which .NET draws, for the columns it says it templates. */
    constructor(verdict?: VerdictOf, template?: GridTemplate<BlazorRow>) {
        this.#verdict = verdict;
        this.#template = template;
    }

    /** Takes .NET's declarations, numbered `version` by .NET each time they change: a column declared as before is
     * the object it was, so the core keeps its Auto width and repaints nothing (ADR-0003, ADR-0016). */
    take(declared: readonly WireColumn[], version: number): void {
        if (!Number.isSafeInteger(version) || version < this.#version) throw new TypeError(`.NET sent columns numbered ${version} after ${this.#version}.`);
        this.#version = version;
        for (let i = this.#awaited.length - 1; i >= 0; i -= 1) {
            const awaited = this.#awaited[i];
            if (awaited === undefined || awaited.version > version) continue;
            this.#awaited.splice(i, 1);
            awaited.reached();
        }
        const before = new Map(this.#held.map(held => [held.signature, held]));
        this.#held = Object.freeze(declared.map((wire, index) => {
            const signature = JSON.stringify(wire);
            const kept = before.get(signature);
            if (kept !== undefined && this.#held.indexOf(kept) === index) return kept;
            return columnOf(wire, index, signature, this.#verdict, this.#template);
        }));
        this.#indexes = new Map(this.#held.map((held, index) => [held.column, index]));
        this.#named = new Map(this.#held.map((held, index) => [held.column.name, index]));
    }

    /** Where the column named `name` stands among the columns taken last. */
    indexNamed(name: string): number {
        const index = this.#named.get(name);
        if (index === undefined) throw new Error(`Internal: the grid named column '${name}', which is not among the columns .NET declared.`);
        return index;
    }

    /** Where `column` stands among the columns taken last, as the core hands one back. */
    indexOf(column: GridColumn<BlazorRow>): number {
        const index = this.#indexes.get(column);
        if (index === undefined) throw new Error(`Internal: the grid named column '${column.name}', which is not among the columns .NET declared.`);
        return index;
    }

    /** Settles once the declarations numbered `version` or later have been taken, or `signal` gives up: the
     * caller reads which. .NET sends columns before it reads anything by them, but an answer to a question can
     * overtake them while the bridge holds what .NET sends (`attach`), and is then read by them once they arrive,
     * not asked again meanwhile. */
    async reached(version: number, signal: AbortSignal): Promise<void> {
        if (version <= this.#version || signal.aborted) return;
        await new Promise<void>(resolve => {
            const awaited = { version, reached: () => { signal.removeEventListener('abort', abort); resolve(); } };
            const abort = (): void => {
                const at = this.#awaited.indexOf(awaited);
                if (at >= 0) this.#awaited.splice(at, 1);
                resolve();
            };
            signal.addEventListener('abort', abort, { once: true });
            this.#awaited.push(awaited);
        });
    }

    /** The number of the declarations taken last: rows .NET read by other ones are not read by these. */
    get version(): number {
        return this.#version;
    }

    /** The columns, for the core's options. */
    get columns(): readonly GridColumn<BlazorRow>[] {
        return this.#held.map(held => held.column);
    }

    /** Holds, in `holding`, the texts and the tones a row was sent with. */
    holdTexts(cells: readonly unknown[], texts: readonly (string | null)[] | undefined, holding: TextHolding, tones?: readonly (CellTone | null)[]): void {
        if (texts !== undefined) {
            if (texts.length !== this.#held.length) throw new TypeError(`.NET sent ${texts.length} texts for a row of ${this.#held.length} columns.`);
            this.#held.forEach((held, index) => {
                const text = texts[index];
                if (text === undefined || text === null) return;
                if (held.texts === undefined) throw new TypeError(`.NET sent a text for column '${held.column.name}', which it does not format.`);
                if (cells[index] === null) throw new TypeError(`.NET sent a text for a Blank of column '${held.column.name}'.`);
                holding.hold(held.texts, cells[index], text);
            });
        }
        if (tones !== undefined) {
            if (tones.length !== this.#held.length) throw new TypeError(`.NET sent ${tones.length} tones for a row of ${this.#held.length} columns.`);
            this.#held.forEach((held, index) => {
                const tone = tones[index];
                if (tone === undefined || tone === null) return;
                if (held.tones === undefined) throw new TypeError(`.NET sent a tone for column '${held.column.name}', which it does not tone.`);
                if (cells[index] === null) throw new TypeError(`.NET sent a tone for a Blank of column '${held.column.name}'.`);
                holding.hold(held.tones, cells[index], tone);
            });
        }
    }

    /** Holds the texts of a column's values, sent without their rows (the filter's value list), in place of the
     * list before it: the filter panel shows one list of a column at a time. */
    holdColumnTexts(name: string, values: readonly unknown[], texts: readonly unknown[]): void {
        const held = this.#held.find(each => each.column.name === name);
        const format = held?.texts;
        if (format === undefined) throw new TypeError(`.NET sent texts for column '${name}', which it does not format.`);
        if (texts.length !== values.length) throw new TypeError(`.NET sent ${texts.length} texts for ${values.length} values of column '${name}'.`);
        const holding = new TextHolding();
        values.forEach((value, index) => {
            const text = texts[index];
            if (text === null) return;
            if (typeof text !== 'string') throw new TypeError(`.NET sent a text for column '${name}' that is not text.`);
            holding.hold(format, value, text);
        });
        this.#valueLists.get(name)?.letGo();
        this.#valueLists.set(name, holding);
    }

    /** How many texts column `name` keeps, for the tests. */
    textCount(name: string): number {
        const held = this.#held.find(each => each.column.name === name);
        if (held?.texts === undefined) throw new Error(`Column '${name}' keeps no texts.`);
        return held.texts.size;
    }

    get count(): number {
        return this.#held.length;
    }
}

function columnOf(
    wire: WireColumn, index: number, signature: string, verdict: VerdictOf | undefined, template: GridTemplate<BlazorRow> | undefined,
): HeldColumn {
    if (wire.actions !== undefined) {
        return { signature, column: actionColumn(wire.name, wire.actions, { header: wire.header, width: wire.width }), texts: undefined, tones: undefined };
    }
    if (wire.mark === true) return { signature, column: markColumn({ name: wire.name, header: wire.header, width: wire.width }), texts: undefined, tones: undefined };
    const texts = wire.format === null ? undefined : new FormatTexts(wire.name);
    const tones = wire.tone === true ? new FormatTexts(wire.name, 'tones') : undefined;
    if (wire.validates === true && verdict === undefined) throw new TypeError(`.NET declared column '${wire.name}' validates, and nothing here asks it.`);
    if (wire.template === true && template === undefined) throw new TypeError(`.NET declared column '${wire.name}' a Template, and nothing here mounts it.`);
    const { name } = wire;
    const column: GridColumn<BlazorRow> = Object.freeze({
        name: wire.name,
        type: wire.type,
        header: wire.header,
        width: wire.width,
        editable: wire.editable,
        align: wire.align,
        headerAlign: wire.headerAlign,
        filterUi: wire.filterUi,
        queryable: wire.queryable,
        ...(wire.type === 'date' ? { dateType: wire.dateType } : {}),
        value: (row: BlazorRow) => cellOf(row, index),
        ...(texts === undefined ? {} : { format: (value: unknown) => texts.textOf(value) }),
        // A tone .NET read of the value, as a text it formatted: the core asks it of a value a painted row holds.
        ...(tones === undefined ? {} : { tone: (value: unknown) => toneOf(tones.textOf(value)) }),
        ...(wire.validates === true && verdict !== undefined
            ? { validate: (row: BlazorRow, text: string, signal: AbortSignal) => verdict(row, name, text, signal) }
            : {}),
        ...(wire.template === true && template !== undefined ? { template } : {}),
    });
    return { signature, column, texts, tones };
}

function toneOf(text: string): CellTone {
    if (text === 'none' || text === 'positive' || text === 'negative') return text;
    throw new TypeError(`Internal: .NET sent the tone '${text}'.`);
}

function cellOf(row: BlazorRow, index: number): unknown {
    if (index >= row.cells.length) throw new Error(`Internal: a row of ${row.cells.length} cells was read at column ${index}.`);
    return row.cells[index];
}

/** What the bridge needs of the browser to let rows go: a registry that says when one was collected. */
export interface RowCollector {
    /** Registers `row`, made as the bridge's object number `held`. */
    register(row: BlazorRow, held: number): void;
}

interface HeldRow {
    readonly row: WeakRef<BlazorRow>;
    readonly signature: string;
}

/**
 * The rows .NET sent, by id. A row sent again with the same key and values is the same object; one whose values
 * changed is a new one. An id is let go to .NET once no object of it is left, together with the number of the last
 * message the bridge took in: .NET keeps a row it sent again after that, which the bridge has yet to read.
 */
export class RowSet {
    readonly #held = new Map<number, HeldRow>();
    readonly #live = new Map<number, number>();
    readonly #collector: RowCollector;
    readonly #release: (ids: readonly number[], seen: number) => void;
    // The messages taken in past `#seen`, which .NET may send in another order than it numbered them.
    readonly #ahead = new Set<number>();
    // Ids of messages given up, let go once every message up to the one they came in has been taken in.
    readonly #givenUp: { readonly sequence: number; readonly ids: readonly number[] }[] = [];
    #pending: number[] = [];
    #seen = 0;
    #stopped = false;
    // Each row object made, by the number the collector says it by: the row it is, and the texts it holds.
    readonly #objects = new Map<number, { readonly id: number; readonly holding: TextHolding }>();
    readonly #holdings = new WeakMap<BlazorRow, TextHolding>();
    #made = 0;

    constructor(makeCollector: (collected: (held: number) => void) => RowCollector, release: (ids: readonly number[], seen: number) => void) {
        this.#collector = makeCollector(held => { this.#collected(held); });
        this.#release = release;
    }

    /** Stops letting rows go: the grid is gone, and .NET with the object it would tell. */
    stop(): void {
        this.#stopped = true;
        this.#pending = [];
    }

    /** Reads a message's rows, numbered `sequence`. `clockOffset` is what the browser's clock reads ahead of .NET's as
     * the message is taken in: a change time .NET read on its own clock is the browser's once it is added, so a server
     * whose clock runs apart from the user's marks a change for as long as one in the browser's own would be (ADR-0068). */
    read(rows: readonly WireRow[], sequence: number, columns: ColumnSet, clockOffset = 0): readonly BlazorRow[] {
        const read = Object.freeze(rows.map(wire => this.#rowOf(wire, columns, clockOffset)));
        this.#took(sequence);
        return read;
    }

    /** Takes in a message whose rows the grid no longer asks for: no object is made of them, and those of its
     * rows the grid holds no object of are let go, as if made and collected. */
    discard(rows: readonly WireRow[], sequence: number): void {
        this.#givenUp.push({ sequence, ids: rows.map(row => row.i) });
        this.#took(sequence);
    }

    // `#seen` is the last message before which every message has been taken in, so .NET lets a row go only when
    // no message it sent the row in is still on its way: one numbered past it may be, and then .NET keeps the row.
    #took(sequence: number): void {
        if (!Number.isSafeInteger(sequence) || sequence < 1) throw new TypeError(`.NET numbered a message ${sequence}.`);
        if (sequence <= this.#seen || this.#ahead.has(sequence)) throw new TypeError(`.NET sent message ${sequence} twice.`);
        this.#ahead.add(sequence);
        while (this.#ahead.delete(this.#seen + 1)) this.#seen += 1;
        for (let i = this.#givenUp.length - 1; i >= 0; i -= 1) {
            const given = this.#givenUp[i];
            if (given === undefined || given.sequence > this.#seen) continue;
            this.#givenUp.splice(i, 1);
            for (const id of given.ids) {
                if (!this.#live.has(id)) this.#letGo(id);
            }
        }
    }

    #rowOf(wire: WireRow, columns: ColumnSet, clockOffset: number): BlazorRow {
        if (!Number.isSafeInteger(wire.i)) throw new TypeError(`.NET sent a row whose id is ${String(wire.i)}.`);
        if (wire.c.length !== columns.count) throw new TypeError(`.NET sent a row of ${wire.c.length} cells for ${columns.count} columns.`);
        for (const [name, perCell] of [['Cell States', wire.s], ['appearances', wire.a], ['change times', wire.h]] as const) {
            if (perCell !== undefined && perCell.length !== columns.count) throw new TypeError(`.NET sent ${perCell.length} ${name} for ${columns.count} columns.`);
        }
        const cells = Object.freeze(wire.c.map(valueOf));
        // The change times as .NET read them: a row sent again with the same times is the row it was, on the clock it was
        // first taken in by, so a message's own offset never makes a row new (ADR-0003).
        const signature = JSON.stringify([wire.k, wire.c, wire.s, wire.p, wire.r, wire.m, wire.o, wire.a, wire.h]);
        const held = this.#held.get(wire.i);
        const kept = held?.signature === signature ? held.row.deref() : undefined;
        if (kept !== undefined) {
            // Columns declared since may format the row's values: the object holds their texts too.
            const holding = this.#holdings.get(kept);
            if (holding === undefined) throw new Error(`Internal: row ${wire.i} is held without its texts.`);
            columns.holdTexts(cells, wire.t, holding, wire.o);
            return kept;
        }
        const holding = new TextHolding();
        columns.holdTexts(cells, wire.t, holding, wire.o);
        const kind = wire.r;
        if (kind !== undefined && kind !== 'group' && kind !== 'total') throw new TypeError(`.NET sent a row of the kind '${kind}'.`);
        const row: BlazorRow = Object.freeze({
            id: wire.i, key: wire.k === undefined ? undefined : rowKeyOf(wire.k), cells, states: wire.s === undefined ? undefined : Object.freeze([...wire.s]),
            table: wire.p === undefined ? undefined : Object.freeze(wire.p.slice()),
            kind,
            marked: wire.m,
            appearances: wire.a === undefined ? undefined : Object.freeze([...wire.a]),
            changedAt: wire.h === undefined ? undefined : Object.freeze(wire.h.map(at => (at === null ? null : at + clockOffset))),
        });
        this.#held.set(wire.i, { row: new WeakRef(row), signature });
        this.#live.set(wire.i, (this.#live.get(wire.i) ?? 0) + 1); // oxlint-disable-line ex-grid/no-stand-in-default -- an id not yet live has no objects
        this.#made += 1;
        this.#objects.set(this.#made, { id: wire.i, holding });
        this.#holdings.set(row, holding);
        this.#collector.register(row, this.#made);
        return row;
    }

    #collected(made: number): void {
        const object = this.#objects.get(made);
        if (object === undefined) throw new Error(`Internal: row object ${made} was collected without being made.`);
        this.#objects.delete(made);
        object.holding.letGo();
        const { id } = object;
        const live = this.#live.get(id);
        if (live === undefined) throw new Error(`Internal: row ${id} was collected without being counted.`);
        if (live > 1) {
            this.#live.set(id, live - 1);
            return;
        }
        this.#live.delete(id);
        this.#held.delete(id);
        this.#letGo(id);
    }

    #letGo(id: number): void {
        if (this.#stopped) return;
        if (this.#pending.length === 0) {
            queueMicrotask(() => {
                const ids = this.#pending;
                this.#pending = [];
                if (!this.#stopped && ids.length > 0) this.#release(ids, this.#seen);
            });
        }
        this.#pending.push(id);
    }
}
