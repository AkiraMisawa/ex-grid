// A Snapshot's values handed to the views a Board draws, each in the form that view reads: a Date's ticks as a
// Temporal PlainDateTime, which the core and ExChart recognise by its tag (C#'s DateTime, which the C# views are
// handed), and a table a chart reads, of the columns an input declares.

import type { ChartData } from '@ex-grid/chart';
import { plainDateTimeOf } from '@ex-grid/core';
import type { BoardInput, Snapshot, SnapshotColumn } from '@ex-grid/data';

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
