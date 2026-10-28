// The Chart Source (ADR-0322, ADR-0344): where a chart reads a series' points, thinned where the data lives. A host
// whose data is on a server thins it there and sends only the points kept; `chartSourceOver` thins a table held in
// the page. `thinChartData` is the reference implementation, the specification a server's source answers as, and
// the port of C#'s `ChartSource.Thin`, held to it by the shared cases under tests/cases/chart/ (ADR-0370, section 2).
import { decimalText } from '@ex-grid/data';
import { chartDataOf, type ChartData } from './chart-data';
import type { AxisValues, PixelSummary } from './model';
import { asDecimal, cellText, ChartSpecRefusedError, temporalDate } from './spec';

/**
 * What a chart asks a {@link ChartSource} (ADR-0322, ADR-0344): one column of values, read across another, from
 * `from` to `to`, at `pixels` columns. The counterpart of C#'s `ChartSourceQuery`.
 */
export interface ChartSourceQuery {
    /** The Data Handle the series reads. */
    readonly data: string;
    /** The column across. */
    readonly x: string;
    /** The column of values. */
    readonly y: string;
    /** What the axis places: dates, as their day numbers, or numbers. A labels axis is never asked of a source. */
    readonly values: AxisValues;
    /** The first position asked for, in the axis' own values; `null` for the data's first. */
    readonly from: number | null;
    /** The last, or `null` for the data's last. */
    readonly to: number | null;
    /** How many columns the range is cut into, each keeping its first, last, lowest and highest point. */
    readonly pixels: number;
}

/**
 * A {@link ChartSource}'s answer (ADR-0344): the rows kept, in order across, as a table with the query's `x` and
 * `y` columns, what each thinned pixel held, and the version of the data it was read from. The counterpart of C#'s
 * `ChartSourceAnswer`.
 */
export interface ChartSourceAnswer {
    /** The version of the data the answer was read from, as a Snapshot names its own; an answer for an older one is never
     * drawn over a newer one. */
    readonly version: bigint;
    /** The rows kept, in order across. */
    readonly points: ChartData;
    /** What each pixel that held more than one row held, in the data's positions; empty when nothing was thinned. */
    readonly thinned: readonly PixelSummary[];
}

/**
 * Where a chart reads a series' points (ADR-0322): thinned where the data lives. The counterpart of C#'s
 * `IChartSource`.
 */
export interface ChartSource {
    /** Answers `query` with the points the chart draws; `signal` is aborted when the chart no longer waits for it. */
    thin(query: ChartSourceQuery, signal: AbortSignal): Promise<ChartSourceAnswer>;
}

/** A Chart Source over a table held in the page, thinning it as it is asked, its answers naming `version`; a refusal is
 * its promise's. */
export function chartSourceOver(data: ChartData, version = 0n): ChartSource {
    return Object.freeze({
        thin: async (query: ChartSourceQuery) => thinChartData(data, query, version),
    });
}

interface Row {
    readonly x: number;
    readonly y: number | null;
    readonly row: number;
}

/**
 * Thins `data` as `query` asks (ADR-0344): the range cut into `pixels` equal columns, each keeping its first, last,
 * lowest and highest row (ADR-0254), never a row picked by LTTB; a row with no value kept where it is, so a gap stays a
 * gap. Rows are answered in order across, a row's place in the data breaking a tie. A table without the query's
 * columns, or a query of no pixels, of a range that is not finite or of a labels axis, is refused with a `TypeError`; a
 * position that is not the axis' kind, or a value that is no number, with a {@link ChartSpecRefusedError}.
 */
export function thinChartData(data: ChartData, query: ChartSourceQuery, version = 0n): ChartSourceAnswer {
    if (!Number.isInteger(query.pixels) || query.pixels < 1) throw new TypeError('A Chart Source is asked for one pixel or more.');
    if ((query.from !== null && !Number.isFinite(query.from)) || (query.to !== null && !Number.isFinite(query.to))) {
        throw new TypeError('A Chart Source is asked for a range of finite positions.');
    }
    if (query.values === 'labels') throw new TypeError('A labels axis needs every category, which a Chart Source does not thin.');
    for (const column of [query.x, query.y]) {
        if (!data.columns.includes(column)) {
            throw new TypeError(`"${query.data}" has no column "${column}"; its columns are ${data.columns.join(', ')}.`);
        }
    }
    const rows: Row[] = [];
    for (let r = 0; r < data.rowCount; r++) {
        const x = positionOf(data.value(query.x, r), query.values, query.data, r);
        if ((query.from !== null && x < query.from) || (query.to !== null && x > query.to)) continue;
        rows.push({ x, y: numberOf(data.value(query.y, r), query.data, r), row: r });
    }
    const ordered = rows.toSorted((a, b) => (a.x === b.x ? a.row - b.row : a.x - b.x));
    const xs: unknown[] = [];
    const ys: unknown[] = [];
    const thinned: PixelSummary[] = [];
    const keep = (at: number) => {
        const { row } = atRun(ordered, at);
        xs.push(data.value(query.x, row));
        ys.push(data.value(query.y, row));
    };
    // The rows from `first` to `last`, each with a value: kept whole when one, else the first, the lowest, the highest
    // and the last, in order.
    const keepRun = (first: number, last: number) => {
        if (first === last) {
            keep(first);
            return;
        }
        let lo = first;
        let hi = first;
        let low = heightOf(atRun(ordered, first));
        let high = low;
        for (let k = first + 1; k <= last; k++) {
            const y = heightOf(atRun(ordered, k));
            if (y < low) [lo, low] = [k, y];
            if (y > high) [hi, high] = [k, y];
        }
        const kept = [...new Set([first, lo, hi, last])].toSorted((a, b) => a - b);
        for (const k of kept) keep(k);
        // Said only where rows were left out: a pixel whose rows were all kept is drawn as they are.
        if (kept.length < last - first + 1) {
            thinned.push(Object.freeze({ from: atRun(ordered, first).x, to: atRun(ordered, last).x, min: low, max: high, count: last - first + 1 }));
        }
    };
    const [firstRow] = ordered;
    const lastRow = ordered.at(-1);
    if (firstRow !== undefined && lastRow !== undefined) {
        const low = query.from ?? firstRow.x;
        const width = (query.to ?? lastRow.x) - low;
        const columnOf = (x: number) => (width <= 0 ? 0 : Math.min(Math.max(Math.floor((x - low) / width * query.pixels), 0), query.pixels - 1));
        let i = 0;
        while (i < ordered.length) {
            const column = columnOf(atRun(ordered, i).x);
            let j = i;
            while (j + 1 < ordered.length && columnOf(atRun(ordered, j + 1).x) === column) j++;
            // Runs of rows with a value, each thinned on its own: a row with none is a gap, kept where it is.
            let start = i;
            for (let k = i; k <= j + 1; k++) {
                if (k <= j && atRun(ordered, k).y !== null) continue;
                if (k > start) keepRun(start, k - 1);
                if (k <= j) keep(k);
                start = k + 1;
            }
            i = j + 1;
        }
    }
    return Object.freeze({ version, points: chartDataOf([[query.x, xs], [query.y, ys]]), thinned: Object.freeze(thinned) });
}

function atRun(rows: readonly Row[], at: number): Row {
    const row = rows[at];
    if (row === undefined) throw new Error(`Internal: no row at ${String(at)} of the run.`);
    return row;
}

function heightOf(row: Row): number {
    if (row.y === null) throw new Error('Internal: a row with no value is thinned.');
    return row.y;
}

const ticksPerDay = 864_000_000_000n;

// A position as a Chart Spec reads one: a date's day number, or a number.
function positionOf(value: unknown, values: AxisValues, data: string, row: number): number {
    const date = values === 'dates' ? temporalDate(value) : undefined;
    if (date !== undefined && date.ticks % ticksPerDay === 0n) return Number(date.ticks / ticksPerDay);
    if (values === 'numbers') {
        const n = numberOf(value, data, row);
        if (n !== null) return n;
    }
    const expected = values === 'dates' ? 'a date' : 'a number';
    throw new ChartSpecRefusedError('', `Row ${String(row + 1)} of "${data}" holds "${cellText(value)}" across, and the axis needs ${expected}.`);
}

// A value as a height, as C#'s `ChartSource` casts one to a double: a double, a whole number, a Snapshot's decimal as
// the double nearest it; null for none.
function numberOf(value: unknown, data: string, row: number): number | null {
    if (value === null) return null;
    if (typeof value === 'number' && Number.isFinite(value)) return value;
    if (typeof value === 'bigint') return Number(value);
    const decimal = asDecimal(value);
    if (decimal !== undefined) return Number(decimalText(decimal));
    throw new ChartSpecRefusedError('', `Row ${String(row + 1)} of "${data}" holds "${cellText(value)}", which is not a number.`);
}
