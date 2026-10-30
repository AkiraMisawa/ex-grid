// A grid built from a source's shape (ADR-0342): C#'s ShapedGrid over a Snapshot, or over pages fetched from a source
// held where the data lives (ADR-0343). Each column the spec shows reads its value from the row's Snapshot and writes
// it through its format code, the kind's default where it writes none.

import {
    engineFilterOf, exactDecimalDigits, FormatCode, gridNumberOf, GridViewStateJsonError, InMemoryGridSource, plainDateTimeOf, significantDigits,
    type GridColumn, type GridColumnWidth, type GridFilter, type SortSpec,
} from '@ex-grid/core';
import { BoardRefusedError, ticksOfTemporal, type DataShapeColumn, type Snapshot, type SnapshotColumn, type SnapshotKind } from '@ex-grid/data';
import { refusedAt, type GridViewSpecReading, type ShapedColumn } from './grid-view-spec';

/** The format of an Integer column that writes none. */
export const integerFormat = '#,##0';
/** The format of a Date column that writes none, whose values are all midnights. */
export const dateFormat = 'yyyy-mm-dd';
/** The format of a Date column that writes none, a value of which has a time. */
export const dateTimeFormat = 'yyyy-mm-dd hh:mm:ss';

/** A row of a grid built from a source's shape: where it stands in its Snapshot. */
export interface ShapedRow {
    /** The Snapshot it is a row of: the grid's whole Snapshot, or, in a grid whose rows are fetched, the page it came
     * back in. */
    readonly snapshot: Snapshot;
    /** Its position there. */
    readonly at: number;
}

/** The rows of `snapshot`, in its order. */
export function shapedRows(snapshot: Snapshot): readonly ShapedRow[] {
    return Object.freeze(Array.from({ length: snapshot.rowCount }, (_, at) => Object.freeze({ snapshot, at })));
}

/**
 * The grid's columns for `spec` over `snapshot`, in the spec's order, at the View State's widths, writing their
 * text in `locale` (`invariant` by default, ADR-0370 section 10). Refused, with a {@link @ex-grid/data#BoardRefusedError} naming
 * the input: a column the Snapshot does not hold, or holds as another kind than the input declares; and a Decimal
 * with more significant digits than a grid's number shows, which it would show rounded (spine rule 1).
 */
export function shapedColumns(snapshot: Snapshot, spec: GridViewSpecReading, locale = 'invariant'): readonly GridColumn<ShapedRow>[] {
    return Object.freeze(spec.columns.map(shown => {
        const held = snapshot.columns.find(column => column.name === shown.column);
        if (held === undefined) {
            throw new BoardRefusedError(spec.data, `Input '${spec.data}' declares column '${shown.column}', which its source does not have.`);
        }
        if (held.kind !== shown.kind) {
            throw new BoardRefusedError(spec.data, `Input '${spec.data}', column '${shown.column}': declared ${shown.kind}, and its source holds ${held.kind}.`);
        }
        checkDigits(shown, held, snapshot.rowCount);
        return shapedColumn(shown, held.kind === 'date' && hasTime(held, snapshot.rowCount), widthOf(spec, shown), locale);
    }));
}

/**
 * The grid's columns for `spec` over rows fetched a page at a time (ADR-0343), each reading its value from the page the
 * row came back in. A Date column the spec writes no format for shows its time ({@link dateTimeFormat}): a shape does
 * not say whether a page will hold one. Each page is checked with {@link checkShapedPage} before its rows are shown.
 */
export function fetchedColumns(spec: GridViewSpecReading, locale = 'invariant'): readonly GridColumn<ShapedRow>[] {
    return Object.freeze(spec.columns.map(shown => shapedColumn(shown, true, widthOf(spec, shown), locale)));
}

/**
 * Checks a page a source answered for a fetched grid (ADR-0343) as a whole Snapshot is checked: one lacking a column
 * the spec shows, or `alsoRead` names (the columns its links and outputs read, which it may not show), holding one of
 * another kind, or holding a Decimal a grid would show rounded, is refused with a
 * {@link @ex-grid/data#BoardRefusedError}, and so fails that fetch.
 */
export function checkShapedPage(page: Snapshot, spec: GridViewSpecReading, alsoRead: readonly DataShapeColumn[] = []): void {
    for (const read of alsoRead) {
        if (spec.columns.some(shown => shown.column === read.name)) continue;
        const held = page.columns.find(column => column.name === read.name);
        if (held === undefined) {
            throw new BoardRefusedError(spec.data, `Input '${spec.data}' declares column '${read.name}', which a page its source answered does not have.`);
        }
        if (held.kind !== read.kind) {
            throw new BoardRefusedError(spec.data, `Input '${spec.data}', column '${read.name}': declared ${read.kind}, and a page its source answered holds ${held.kind}.`);
        }
    }
    for (const shown of spec.columns) {
        const held = page.columns.find(column => column.name === shown.column);
        if (held === undefined) {
            throw new BoardRefusedError(spec.data, `Input '${spec.data}' declares column '${shown.column}', which a page its source answered does not have.`);
        }
        if (held.kind !== shown.kind) {
            throw new BoardRefusedError(spec.data, `Input '${spec.data}', column '${shown.column}': declared ${shown.kind}, and a page its source answered holds ${held.kind}.`);
        }
        checkDigits(shown, held, page.rowCount);
    }
}

function widthOf(spec: GridViewSpecReading, shown: ShapedColumn): GridColumnWidth {
    const state = spec.viewState.columns.find(column => column.column === shown.column);
    if (state === undefined) throw new Error(`Internal: the View State does not size column '${shown.column}'.`);
    return state.width;
}

// A Decimal with more significant digits than a grid's number shows would be shown rounded (spine rule 1).
function checkDigits(shown: ShapedColumn, held: SnapshotColumn, rowCount: number): void {
    if (held.kind !== 'decimal') return;
    for (let row = 0; row < rowCount; row++) {
        const value = held.valueAt(row);
        if (value !== null && significantDigits(value) > exactDecimalDigits) {
            throw new BoardRefusedError(shown.column, `Column '${shown.column}' holds a value of ${significantDigits(value)} significant digits, `
                + `and a grid shows a number to ${exactDecimalDigits}; it would be shown rounded.`);
        }
    }
}

/** A grid built from a source's shape over a Snapshot (ADR-0342): C#'s ShapedGrid. */
export interface ShapedGrid {
    /** The Snapshot its rows are of. */
    readonly snapshot: Snapshot;
    /** The spec it was built from. */
    readonly spec: GridViewSpecReading;
    /** The columns, in the spec's order at the View State's widths. */
    readonly columns: readonly GridColumn<ShapedRow>[];
    /** The rows, in the Snapshot's order. */
    readonly rows: readonly ShapedRow[];
    /** The grid's source over the rows, under the spec's sort and filter, keyed by each row's position. */
    readonly source: InMemoryGridSource<ShapedRow>;
    /** How many leading columns the spec pins. */
    readonly pinnedColumnCount: number;
}

/**
 * The grid `spec` describes over `snapshot`, C#'s `ShapedGrid.Of`: {@link shapedColumns} in `locale`, the
 * Snapshot's rows, and a source sorting and filtering them as the spec says, with `GridSource.from`'s semantics. Refused,
 * with a {@link @ex-grid/data#BoardRefusedError} named under `path` (the grid's place in a Board, empty for the whole):
 * whatever {@link shapedColumns} refuses, a sort by a column the grid does not show, and a filter whose operands the
 * columns cannot compare.
 */
export function shapedGrid(snapshot: Snapshot, spec: GridViewSpecReading, locale = 'invariant', path = ''): ShapedGrid {
    const columns = shapedColumns(snapshot, spec, locale);
    const declared = new Map(columns.map(column => [column.name, column]));
    checkSortsShown(spec.viewState.sorts, declared, path);
    const filter = engineFilterAt(spec.viewState.filter, declared, path);
    const rows = shapedRows(snapshot);
    const source = InMemoryGridSource.from(rows, { rowKey: row => row.at });
    source.setColumns(columns);
    source.setSorts(spec.viewState.sorts);
    source.setFilter(filter);
    return Object.freeze({ snapshot, spec, columns, rows, source, pinnedColumnCount: spec.viewState.pinnedColumnCount });
}

/** @internal The spec's reader reads a sort against every column the input declares, as a server's grid may sort by
 * any; a grid over a Snapshot sorts by those it shows (C#'s GridSource.From refuses the rest by name). */
export function checkSortsShown(sorts: readonly SortSpec[], shown: ReadonlyMap<string, GridColumn<ShapedRow>>, path: string): void {
    sorts.forEach((sort, i) => {
        if (!shown.has(sort.column)) {
            throw new BoardRefusedError(`${path === '' ? '' : `${path}.`}sorts[${i}]`, `The grid sorts by column '${sort.column}', which it does not show.`);
        }
    });
}

/** @internal The spec's filter as the engine compares it, against the columns the grid is built with; a refusal named
 * under the grid's path. Only the conversion is in the try: a refusal of the source's own is not the spec's. */
export function engineFilterAt(filter: GridFilter | undefined, shown: ReadonlyMap<string, GridColumn<ShapedRow>>, path: string): GridFilter | undefined {
    try {
        return engineFilterOf(filter, name => shown.get(name));
    } catch (error) {
        if (!(error instanceof GridViewStateJsonError)) throw error;
        throw refusedAt(error, path);
    }
}

/** A column `column` read with a width, as a bound grid view sizes one again when the user resizes it. */
export function resized(column: GridColumn<ShapedRow>, width: GridColumnWidth): GridColumn<ShapedRow> {
    return Object.freeze({ ...column, width });
}

function shapedColumn(shown: ShapedColumn, withTime: boolean, width: GridColumnWidth, locale: string): GridColumn<ShapedRow> {
    const named = { name: shown.column, header: shown.header, width };
    switch (shown.kind) {
        case 'text': {
            const at = reader(shown.column, 'text');
            return Object.freeze({ ...named, type: 'text', value: (row: ShapedRow) => at(row).valueAt(row.at) });
        }
        case 'boolean': {
            const at = reader(shown.column, 'boolean');
            return Object.freeze({ ...named, type: 'boolean', value: (row: ShapedRow) => at(row).valueAt(row.at) });
        }
        case 'date': {
            const at = reader(shown.column, 'date');
            const code = parsed(shown, shown.format ?? (withTime ? dateTimeFormat : dateFormat));
            return Object.freeze({
                ...named, type: 'date', dateType: 'dateTime',
                value: (row: ShapedRow) => {
                    const ticks = at(row).valueAt(row.at);
                    return ticks === null ? null : plainDateTimeOf(ticks);
                },
                format: (value: unknown) => code.format(ticksOf(value), locale),
            });
        }
        case 'decimal': {
            const at = reader(shown.column, 'decimal');
            const code = parsed(shown, required(shown));
            return Object.freeze({
                ...named, type: 'number',
                value: (row: ShapedRow) => {
                    const value = at(row).valueAt(row.at);
                    return value === null ? null : gridNumberOf(value);
                },
                format: (value: unknown) => code.format(numberOf(value), locale),
            });
        }
        case 'double': {
            const at = reader(shown.column, 'double');
            const code = parsed(shown, required(shown));
            return Object.freeze({
                ...named, type: 'number', value: (row: ShapedRow) => at(row).valueAt(row.at), format: (value: unknown) => code.format(numberOf(value), locale),
            });
        }
        case 'integer': {
            const at = reader(shown.column, 'integer');
            const code = parsed(shown, shown.format ?? integerFormat);
            return Object.freeze({
                ...named, type: 'number', value: (row: ShapedRow) => at(row).valueAt(row.at), format: (value: unknown) => code.format(integerOf(value), locale),
            });
        }
        default: throw new Error(`Internal: ${String(shown.kind satisfies never)} is not a kind.`);
    }
}

type ColumnOf<K extends SnapshotKind> = Extract<SnapshotColumn, { readonly kind: K }>;

// The column `name` of a row's Snapshot, found once per Snapshot rather than once per cell: the rows of one Snapshot,
// or of one page, share it. Its kind was checked when the grid was built, or when the page was.
function reader<K extends SnapshotKind>(name: string, kind: K): (row: ShapedRow) => ColumnOf<K> {
    const is = (column: SnapshotColumn): column is ColumnOf<K> => column.kind === kind;
    let page: Snapshot | undefined;
    let held: ColumnOf<K> | undefined;
    return row => {
        if (held === undefined || row.snapshot !== page) {
            const column = row.snapshot.column(name);
            if (!is(column)) throw new Error(`Internal: column '${name}' is ${column.kind}, and the grid reads it as ${kind}.`);
            page = row.snapshot;
            held = column;
        }
        return held;
    };
}

function required(shown: ShapedColumn): string {
    if (shown.format === undefined) throw new Error(`Internal: the spec's reader lets column '${shown.column}' through without a format.`);
    return shown.format;
}

function parsed(shown: ShapedColumn, code: string): FormatCode {
    const reading = FormatCode.tryParse(code);
    if ('reason' in reading) throw new BoardRefusedError(shown.column, `Column '${shown.column}': the format code "${code}" ${reading.reason}.`);
    return reading.format;
}

function hasTime(column: SnapshotColumn & { readonly kind: 'date' }, rowCount: number): boolean {
    for (let row = 0; row < rowCount; row++) {
        const ticks = column.valueAt(row);
        if (ticks !== null && ticks % 864_000_000_000n !== 0n) return true;
    }
    return false;
}

function ticksOf(value: unknown): bigint {
    const ticks = typeof value === 'object' && value !== null ? ticksOfTemporal(value) : undefined;
    if (ticks === undefined) throw new TypeError('A Date column writes the dates it reads, and was handed another value.');
    return ticks;
}

function numberOf(value: unknown): number {
    if (typeof value !== 'number') throw new TypeError('A number column writes the numbers it reads, and was handed another value.');
    return value;
}

function integerOf(value: unknown): bigint {
    if (typeof value !== 'bigint') throw new TypeError('An Integer column writes the integers it reads, and was handed another value.');
    return value;
}
