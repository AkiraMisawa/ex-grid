// A table a Chart Spec reads by column name (ADR-0320, ADR-0322): the chart's own seam, as C#'s `IChartData`
// is the Engine's, so a host adapts its data to it, such as a Board's bound Snapshot.

/**
 * A table a chart reads by column name (ADR-0320, ADR-0322), as C#'s `IChartData` is read. A value is
 * `null` where it is missing, and otherwise one of: a `number` (C#'s `double`), a `bigint` (a whole number,
 * held exactly as C#'s `int` and `long` are), a Snapshot's decimal `{ mantissa, scale }` (C#'s `decimal`),
 * a `string`, a `boolean`, a `Temporal.PlainDate` (C#'s `DateOnly`) or a `Temporal.PlainDateTime` (C#'s
 * `DateTime`). A date is recognised by its own tag and read through its own members, never through the
 * global `Temporal`, as `@ex-grid/data` reads one.
 */
export interface ChartData {
    /** The columns' names, in the table's order. */
    readonly columns: readonly string[];
    /** How many rows it has. */
    readonly rowCount: number;
    /** The value in `column` at `row`, as the table holds it; `null` where it is missing. */
    value(column: string, row: number): unknown;
}

/**
 * A table of columns held in memory, each `[name, values]`, all of one length: the reference implementation
 * of {@link ChartData}, as C#'s `ChartData.Of` is. Two columns of one name, or columns of different lengths,
 * are refused with a `TypeError`; reading a column it does not have, or a row past its last, with a
 * `RangeError`.
 */
export function chartDataOf(columns: readonly (readonly [name: string, values: readonly unknown[]])[]): ChartData {
    const byName = new Map<string, readonly unknown[]>();
    const [first] = columns;
    const rows = first === undefined ? 0 : first[1].length;
    for (const [name, values] of columns) {
        if (byName.has(name)) {
            throw new TypeError(`Two columns are named '${name}'.`);
        }
        if (values.length !== rows) {
            throw new TypeError(`Column '${name}' has ${String(values.length)} rows and '${first === undefined ? '' : first[0]}' has ${String(rows)}; a table's columns are one length.`);
        }
        byName.set(name, Object.freeze([...values]));
    }
    const names = Object.freeze(columns.map(c => c[0]));
    return Object.freeze({
        columns: names,
        rowCount: rows,
        value(column: string, row: number): unknown {
            const values = byName.get(column);
            if (values === undefined) {
                throw new RangeError(`The table has no column '${column}'; its columns are ${names.join(', ')}.`);
            }
            if (!Number.isInteger(row) || row < 0 || row >= values.length) {
                throw new RangeError(`Row ${String(row)} is outside a table of ${String(values.length)} rows.`);
            }
            return values[row];
        },
    });
}
