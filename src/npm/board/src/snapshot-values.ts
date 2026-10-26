// A Snapshot's values handed to the views a Board draws, each in the form that view reads: a Date's ticks as a
// Temporal PlainDateTime, which the core and ExChart recognise by its tag (C#'s DateTime, which the C# views are
// handed), and a table a chart reads, of the columns an input declares.

import type { ChartData } from '@ex-grid/chart';
import { ticksText, type BoardInput, type Snapshot, type SnapshotColumn, type SnapshotDecimal } from '@ex-grid/data';

/** A Date's ticks as a Temporal `PlainDateTime` in ISO's calendar, read through its members as the core and ExChart
 * read the browser's or a polyfill's; the global `Temporal` is not needed. */
export function plainDateTimeOf(ticks: bigint): object {
    const text = ticksText(ticks);
    const fraction = Number(ticks % 10_000_000n);
    return Object.freeze({
        [Symbol.toStringTag]: 'Temporal.PlainDateTime',
        calendarId: 'iso8601',
        year: Number(text.slice(0, 4)),
        month: Number(text.slice(5, 7)),
        day: Number(text.slice(8, 10)),
        hour: Number(text.slice(11, 13)),
        minute: Number(text.slice(14, 16)),
        second: Number(text.slice(17, 19)),
        millisecond: Math.floor(fraction / 10_000),
        microsecond: Math.floor(fraction / 10) % 1000,
        nanosecond: (fraction % 10) * 100,
        toString: () => text,
    });
}

/** The value of `column` at `row` in the form a chart reads: a Date as a `PlainDateTime`, every other value as the
 * Snapshot hands it out. */
export function chartValueAt(column: SnapshotColumn, row: number): unknown {
    if (column.kind !== 'date') return column.valueAt(row);
    const ticks = column.valueAt(row);
    return ticks === null ? null : plainDateTimeOf(ticks);
}

/** A chart's table over `snapshot`, showing only the columns `input` declares, and, given `rows`, only the rows at
 * those positions: C#'s DeclaredChartData over SnapshotChartData. */
export function declaredChartData(snapshot: Snapshot, input: BoardInput, rows: readonly number[] | undefined): ChartData {
    const columns = Object.freeze(input.columns.map(column => column.name));
    const held = new Map(columns.map(name => [name, snapshot.column(name)]));
    return Object.freeze({
        columns,
        rowCount: rows === undefined ? snapshot.rowCount : rows.length,
        value(name: string, row: number): unknown {
            const column = held.get(name);
            if (column === undefined) throw new RangeError(`Input '${input.name}' does not declare column '${name}'.`);
            const at = rows === undefined ? row : rows[row];
            if (at === undefined) throw new RangeError(`Row ${row} is not one of the ${rows?.length} the chart reads.`);
            return chartValueAt(column, at);
        },
    });
}

/** The most significant digits a Decimal is shown and compared at exactly in a grid, whose numbers are C#'s decimal
 * read from a double, fifteen digits at most (ADR-0023). */
export const exactDigits = 15;

/** A Decimal as the number a grid reads it as, which holds it exactly while it has no more than
 * {@link exactDigits} significant digits. */
export function gridNumberOf(value: SnapshotDecimal): number {
    return Number(`${value.mantissa}e-${value.scale}`);
}

/** How many significant digits a Decimal holds, trailing zeros after the point left out. */
export function significantDigits(value: SnapshotDecimal): number {
    let mantissa = value.mantissa < 0n ? -value.mantissa : value.mantissa;
    if (mantissa === 0n) return 0;
    while (mantissa % 10n === 0n) mantissa /= 10n;
    return mantissa.toString().length;
}
