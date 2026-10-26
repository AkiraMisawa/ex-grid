// A grid built from a source's shape (ADR-0342): C#'s ShapedGrid over a Snapshot. Each column the spec shows reads
// its value from the Snapshot's row and writes it through its format code, the kind's default where it writes none.

import { exactDigits, FormatCode, gridNumberOf, plainDateTimeOf, significantDigits, type GridColumn, type GridColumnWidth } from '@ex-grid/core';
import { BoardRefusedError, ticksOfTemporal, type Snapshot, type SnapshotColumn } from '@ex-grid/data';
import type { GridViewSpecReading, ShapedColumn } from './grid-view-spec';

/** The format of an Integer column that writes none. */
export const integerFormat = '#,##0';
/** The format of a Date column that writes none, whose values are all midnights. */
export const dateFormat = 'yyyy-mm-dd';
/** The format of a Date column that writes none, a value of which has a time. */
export const dateTimeFormat = 'yyyy-mm-dd hh:mm:ss';

/** A row of a grid built from a source's shape: where it stands in its Snapshot. */
export interface ShapedRow {
    /** The Snapshot it is a row of. */
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
        const state = spec.viewState.columns.find(column => column.column === shown.column);
        if (state === undefined) throw new Error(`Internal: the View State does not size column '${shown.column}'.`);
        return shapedColumn(shown, held, snapshot.rowCount, state.width, locale);
    }));
}

/** A column `column` read with a width, as a bound grid view sizes one again when the user resizes it. */
export function resized(column: GridColumn<ShapedRow>, width: GridColumnWidth): GridColumn<ShapedRow> {
    return Object.freeze({ ...column, width });
}

function shapedColumn(shown: ShapedColumn, held: SnapshotColumn, rowCount: number, width: GridColumnWidth, locale: string): GridColumn<ShapedRow> {
    const named = { name: shown.column, header: shown.header, width };
    switch (held.kind) {
        case 'text': return Object.freeze({ ...named, type: 'text', value: (row: ShapedRow) => held.valueAt(row.at) });
        case 'boolean': return Object.freeze({ ...named, type: 'boolean', value: (row: ShapedRow) => held.valueAt(row.at) });
        case 'date': {
            const code = parsed(shown, shown.format ?? (hasTime(held, rowCount) ? dateTimeFormat : dateFormat));
            return Object.freeze({
                ...named, type: 'date', dateType: 'dateTime',
                value: (row: ShapedRow) => {
                    const ticks = held.valueAt(row.at);
                    return ticks === null ? null : plainDateTimeOf(ticks);
                },
                format: (value: unknown) => code.format(ticksOf(value), locale),
            });
        }
        case 'decimal': {
            for (let row = 0; row < rowCount; row++) {
                const value = held.valueAt(row);
                if (value !== null && significantDigits(value) > exactDigits) {
                    throw new BoardRefusedError(shown.column, `Column '${shown.column}' holds a value of ${significantDigits(value)} significant digits, `
                        + `and a grid shows a number to ${exactDigits}; it would be shown rounded.`);
                }
            }
            const code = parsed(shown, required(shown));
            return Object.freeze({
                ...named, type: 'number',
                value: (row: ShapedRow) => {
                    const value = held.valueAt(row.at);
                    return value === null ? null : gridNumberOf(value);
                },
                format: (value: unknown) => code.format(numberOf(value), locale),
            });
        }
        case 'double': {
            const code = parsed(shown, required(shown));
            return Object.freeze({
                ...named, type: 'number', value: (row: ShapedRow) => held.valueAt(row.at), format: (value: unknown) => code.format(numberOf(value), locale),
            });
        }
        case 'integer': {
            const code = parsed(shown, shown.format ?? integerFormat);
            return Object.freeze({
                ...named, type: 'number', value: (row: ShapedRow) => held.valueAt(row.at), format: (value: unknown) => code.format(integerOf(value), locale),
            });
        }
        default: throw new Error(`Internal: ${String(held satisfies never)} is not a column.`);
    }
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
