// What a Sheet reports and refuses (C#'s SheetColumnWidth.cs, SheetChange.cs, SheetRefusal.cs and
// RepeatedKeyException.cs; ADR-0046, ADR-0047, ADR-0049, ADR-0050).
import { CellAddress } from './cell-address';
import { formatRoundTrip } from './dotnet';
import type { Value } from './value';

/**
 * What recorded a column's width (ADR-0046, 2026-09-28; SH-26). `widenedByEntry`: an entry that widened the column,
 * which a longer entry widens again (CW-028, third run). `setByUser`: a drag, a size to fit, a command, or a column
 * inserted to the right of a recorded width; an entry never widens it.
 */
export type SheetColumnWidthKind = 'widenedByEntry' | 'setByUser';

/**
 * A width recorded on a column (ADR-0046): how wide it is, in characters of the default font (Excel's unit,
 * ADR-0047), and what recorded it. Both kinds are marked custom, as Excel's file marks a column an entry widened as
 * well as one the user set (`customWidth`; CW-018, CW-027..029).
 */
export class SheetColumnWidth {
/** How wide the column is, in characters of the default font. */
    readonly width: number;

/** What recorded it. */
    readonly kind: SheetColumnWidthKind;

    constructor(width: number, kind: SheetColumnWidthKind) {
        this.width = width;
        this.kind = kind;
    }

    /** Whether Excel's file marks the width custom (`customWidth`): every recorded width is, whichever its kind. */
    get isCustom(): boolean {
        return true;
    }

    /** Whether the user set the width, so an entry never widens the column. */
    get isSetByUser(): boolean {
        return this.kind === 'setByUser';
    }

/** Whether `other` is the same width, recorded the same way. */
    equals(other: SheetColumnWidth): boolean {
        return this.width === other.width && this.kind === other.kind;
    }
}

/**
 * What one change to a Sheet did, reported after its recalculation has completed. Every Value it names is already
 * published, and all of them come from that one recalculation (ADR-0047).
 */
export class SheetChange {
    /** The cells whose Value changed (a cell becoming blank included), in row-major order. */
    readonly valueChanges: readonly CellAddress[];
    /** The Formula cells this change recomputed, in row-major order: the changed cells and the cells that depend on them, and no other (ADR-0047). */
    readonly recalculated: readonly CellAddress[];
    /** The rows whose painted content may differ — a Value, an Entry or a cell's formatting changed — in ascending order, each once. */
    readonly rows: readonly number[];
    /** The columns whose recorded width changed (ADR-0046) — set, cleared back to the default, or moved by an insertion or deletion — in ascending order, each once. */
    readonly columns: readonly number[];
    /**
     * Whether the Cell Format shown in rows `rows` does not name may have changed (ADR-0071): after a whole row's or
     * column's Cell Format set, cleared or moved, or rows or columns inserted or deleted. A component that paints a
     * Cell Format then reads again what each row it paints shows.
     */
    readonly reformatsUnnamedRows: boolean;

    /** @internal */
    constructor(valueChanges: readonly CellAddress[], recalculated: readonly CellAddress[], rows: readonly number[],
        columns: readonly number[] = [], reformatsUnnamedRows = false) {
        this.valueChanges = valueChanges;
        this.recalculated = recalculated;
        this.rows = rows;
        this.columns = columns;
        this.reformatsUnnamedRows = reformatsUnnamedRows;
    }

    /** A change that changed nothing. @internal */
    static readonly none = new SheetChange([], [], []);

    /** This change, also reporting that it may have reformatted rows it does not name. @internal */
    reformattingUnnamedRows(): SheetChange {
        return this.reformatsUnnamedRows ? this : new SheetChange(this.valueChanges, this.recalculated, this.rows, this.columns, true);
    }

    /** Several changes made as one, such as the parts of one step. @internal */
    static merge(changes: Iterable<SheetChange>): SheetChange {
        const list = [...changes];
        const valueChanges = distinctAddresses(list.flatMap(c => c.valueChanges));
        const recalculated = distinctAddresses(list.flatMap(c => c.recalculated));
        const rows = distinctNumbers(list.flatMap(c => c.rows));
        const columns = distinctNumbers(list.flatMap(c => c.columns));
        const reformats = list.some(c => c.reformatsUnnamedRows);
        return valueChanges.length === 0 && recalculated.length === 0 && rows.length === 0 && columns.length === 0 && !reformats
            ? SheetChange.none
            : new SheetChange(valueChanges, recalculated, rows, columns, reformats);
    }
}

/** Addresses each once, in row-major order. @internal */
export function distinctAddresses(addresses: Iterable<CellAddress>): CellAddress[] {
    const keys = new Set<number>();
    for (const address of addresses) keys.add(address.key);
    return [...keys].toSorted((a, b) => a - b).map(key => CellAddress.fromKey(key));
}

/** Numbers each once, ascending. @internal */
export function distinctNumbers(numbers: Iterable<number>): number[] {
    return [...new Set(numbers)].toSorted((a, b) => a - b);
}

/**
 * Why a Sheet refused an operation. A refusal changes nothing: the engine says an operation cannot be done rather
 * than do something close to it (ADR-0047, ADR-0050).
 *
 * - `entriesWouldLeaveSheet`: an insertion would push a cell holding an Entry off the Sheet's edge.
 * - `blockWouldLeaveSheet`: a block written at a place would run past the Sheet's edge (ADR-0050).
 * - `waitingForData`: a copy reaches a cell whose Value is `#GETTING_DATA` (ADR-0049).
 * - `fillPatternNotSupported`: a fill whose source is a pattern ExSheet does not continue (ADR-0050).
 * - `fillShapeNotSupported`: a fill whose target does not extend its source along one axis (ADR-0050).
 * - `editIsOpen`: a command that changes the Sheet, given while an edit is open (ADR-0048, ADR-0049).
 * - `sheetNameTaken`: a Sheet would be given a name another Sheet of the Workbook has, without regard to case (ADR-0331).
 * - `lastSheet`: the Workbook's last Sheet would be deleted (ADR-0331).
 * - `sheetNotNamed`: a command or an address that names no Sheet, given while the Workbook holds several (ADR-0331 §6).
 * - `selectionMoved`: a change made from the Focus cell's Cell Format away from the Sheet, set once the Selection, or the
 *   Sheet shown, is no longer the one it was read from (ADR-0100).
 * - `focusFormatChanged`: such a change, set once the Focus cell's Cell Format is no longer the one it was made from
 *   (ADR-0100).
 */
export type SheetRefusalReason = 'entriesWouldLeaveSheet' | 'blockWouldLeaveSheet' | 'waitingForData' | 'fillPatternNotSupported'
    | 'fillShapeNotSupported' | 'editIsOpen' | 'sheetNameTaken' | 'lastSheet' | 'sheetNotNamed' | 'selectionMoved' | 'focusFormatChanged';

/** An operation a Sheet refused, and why, with the refusal in words naming what caused it. Nothing was changed. */
export class SheetRefusal {
/** Why it was refused. */
    readonly reason: SheetRefusalReason;

/** The refusal in words, naming what caused it. */
    readonly message: string;

    constructor(reason: SheetRefusalReason, message: string) {
        this.reason = reason;
        this.message = message;
    }

/** Whether `other` is the same refusal, for the same reason and in the same words. */
    equals(other: SheetRefusal): boolean {
        return this.reason === other.reason && this.message === other.message;
    }
}

/** Thrown when an operation the caller asked for is refused. Nothing was changed. */
export class SheetRefusedError extends Error {
/** The refusal. */
    readonly refusal: SheetRefusal;

    constructor(refusal: SheetRefusal) {
        super(refusal.message);
        this.refusal = refusal;
        this.name = 'SheetRefusedError';
    }
}

/**
 * Thrown when a snapshot of a Linked Table declared with a key is refused because a key repeats in it (ADR-0049,
 * 2026-09-30): two rows whose keys `XLOOKUP`'s exact match cannot tell apart. Case does not tell keys apart, and a
 * blank key is not a key. The snapshot is not taken, and the previous one is not kept: the table waits again, and
 * every Formula that reads it shows `#GETTING_DATA` until a snapshot in which no key repeats is pushed.
 */
export class RepeatedKeyError extends Error {
    /** The table's name, as declared. */
    readonly table: string;
    /** The key column's name, as declared; for a key of several columns, their names joined by `, `. */
    readonly keyColumn: string;
    /** The key that repeats, as the first of its rows holds it; for a key of several columns, its first column's Value. */
    readonly key: Value;
    /** The key's columns, as declared (ADR-0058, amended 2026-10-03). */
    readonly keyColumns: readonly string[];
    /** The key that repeats, one Value per key column, as the first of its rows holds it. */
    readonly keys: readonly Value[];
    /** The first row of the snapshot, from 0, that holds the key. */
    readonly firstRow: number;
    /** The row of the snapshot, from 0, that holds it again. */
    readonly secondRow: number;
    /** What the table's going back to waiting changed: its readers, and what depends on them, now show `#GETTING_DATA`. */
    readonly change: SheetChange;
    /** The key as the second of its rows holds it, one Value per key column, which the message names. @internal */
    readonly repeats: readonly Value[];

    /** @internal */
    constructor(table: string, keyColumns: readonly string[], keys: readonly Value[], firstRow: number, repeats: readonly Value[], secondRow: number,
        change: SheetChange) {
        super(describe(table, keyColumns, keys, firstRow, repeats, secondRow));
        this.name = 'RepeatedKeyError';
        this.table = table;
        this.keyColumns = keyColumns;
        this.keys = keys;
        this.keyColumn = keyColumns.join(', ');
        const first = keys[0];
        if (first === undefined) throw new Error('Internal: a repeated key has at least one column.');
        this.key = first;
        this.firstRow = firstRow;
        this.secondRow = secondRow;
        this.change = change;
        this.repeats = repeats;
    }
}

function describe(table: string, keyColumns: readonly string[], keys: readonly Value[], firstRow: number, repeats: readonly Value[], secondRow: number): string {
    const key = keys.map(written).join(', ');
    const repeat = repeats.map(written).join(', ');
    const same = keys.length === repeats.length && keys.every((k, i) => repeats[i]?.equals(k) === true);
    const held = same
        ? `holds ${key} in rows ${String(firstRow)} and ${String(secondRow)}`
        : `holds ${key} in row ${String(firstRow)} and ${repeat} in row ${String(secondRow)}, which ${keys.length === 1 ? 'XLOOKUP' : 'its lookup'} does not tell apart`;
    const columns = keyColumns.length === 1 ? `its key column '${String(keyColumns[0])}'` : `its key columns ${keyColumns.map(c => `'${c}'`).join(', ')}`;
    return `The snapshot of '${table}' is refused: ${columns} ${held}. The table waits for a snapshot in which no key repeats, and every Formula that reads it shows #GETTING_DATA.`;
}

// As a Formula writes the constant (ADR-0058): text quoted, a number invariant, a boolean in capitals.
function written(key: Value): string {
    switch (key.kind) {
        case 'text': return `"${key.text.replaceAll('"', '""')}"`;
        case 'number': return formatRoundTrip(key.number);
        case 'boolean':
        case 'error':
            return key.boolean ? 'TRUE' : 'FALSE';
        default: throw new Error(`Internal: ${String(key.kind)} is no kind of Value.`);
    }
}
