// The Chart Source (ADR-0322, ADR-0344): where a chart reads a series' points, thinned where the data lives. A host
// whose data is on a server thins it there and sends only the points kept; `chartSourceOver` thins a table held in
// the page. `thinChartData` is the reference implementation, the specification a server's source answers as, and
// the port of C#'s `ChartSource.Thin`, held to it by the shared cases under tests/cases/chart/ (ADR-0370, section 2).
import { ticksOfTemporal } from '@ex-grid/data';
import { chartDataOf, type ChartData } from './chart-data';
import type { AxisValues, PixelSummary } from './model';
import { ChartSpecRefusedError } from './spec';

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
    /** The version of the data the answer was read from; an answer for an older one is never drawn over a newer one. */
    readonly version: number;
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

/** A Chart Source over a table held in the page, thinning it as it is asked, its answers naming `version`. */
export function chartSourceOver(data: ChartData, version = 0): ChartSource {
    return Object.freeze({
        thin: (query: ChartSourceQuery) => Promise.resolve(thinChartData(data, query, version)),
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
 * columns, or a query of no pixels or of a labels axis, is refused with a `TypeError`; a position that is not the
 * axis' kind, or a value that is no number, with a {@link ChartSpecRefusedError}.
 */
export function thinChartData(data: ChartData, query: ChartSourceQuery, version = 0): ChartSourceAnswer {
    if (!Number.isInteger(query.pixels) || query.pixels < 1) throw new TypeError('A Chart Source is asked for one pixel or more.');
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
    const keep = (row: Row) => {
        xs.push(data.value(query.x, row.row));
        ys.push(data.value(query.y, row.row));
    };
    const keepRun = (run: readonly Row[]) => {
        const [first] = run;
        const last = run.at(-1);
        if (first === undefined || last === undefined) throw new Error('Internal: a run of rows is empty.');
        if (run.length === 1) {
            keep(first);
            return;
        }
        let lo = 0;
        let hi = 0;
        run.forEach((row, k) => {
            if (heightOf(row) < heightOf(atRun(run, lo))) lo = k;
            if (heightOf(row) > heightOf(atRun(run, hi))) hi = k;
        });
        const kept = [...new Set([0, lo, hi, run.length - 1])].toSorted((a, b) => a - b);
        for (const k of kept) keep(atRun(run, k));
        // Said only where rows were left out: a pixel whose rows were all kept is drawn as they are.
        if (kept.length < run.length) {
            thinned.push(Object.freeze({ from: first.x, to: last.x, min: heightOf(atRun(run, lo)), max: heightOf(atRun(run, hi)), count: run.length }));
        }
    };
    const [firstRow] = ordered;
    const lastRow = ordered.at(-1);
    if (firstRow !== undefined && lastRow !== undefined) {
        const low = query.from ?? firstRow.x;
        const high = query.to ?? lastRow.x;
        const width = high - low;
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
                if (k > start) keepRun(ordered.slice(start, k));
                if (k <= j) keep(atRun(ordered, k));
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
    if (values === 'dates' && typeof value === 'object' && value !== null) {
        const ticks = ticksOfTemporal(value);
        if (ticks !== undefined && ticks % ticksPerDay === 0n) return Number(ticks / ticksPerDay);
    }
    if (values === 'numbers') {
        const n = numberOf(value, data, row);
        if (n !== null) return n;
    }
    const expected = values === 'dates' ? 'a date' : 'a number';
    throw new ChartSpecRefusedError('', `Row ${String(row + 1)} of "${data}" holds "${written(value)}" across, and the axis needs ${expected}.`);
}

// A value as a height: a double, a whole number, a Snapshot's decimal as the double nearest it, as C#'s `(double)`
// casts one; null for none.
function numberOf(value: unknown, data: string, row: number): number | null {
    if (value === null) return null;
    if (typeof value === 'number' && Number.isFinite(value)) return value;
    if (typeof value === 'bigint') return Number(value);
    if (typeof value === 'object') {
        const mantissa: unknown = Reflect.get(value, 'mantissa');
        const scale: unknown = Reflect.get(value, 'scale');
        if (typeof mantissa === 'bigint' && typeof scale === 'number' && Number.isInteger(scale) && scale >= 0 && scale <= 28) {
            return Number(`${mantissa.toString()}e-${String(scale)}`);
        }
    }
    throw new ChartSpecRefusedError('', `Row ${String(row + 1)} of "${data}" holds "${written(value)}", which is not a number.`);
}

function written(value: unknown): string {
    if (typeof value === 'string') return value;
    if (typeof value === 'number' || typeof value === 'bigint' || typeof value === 'boolean') return String(value);
    return Object.prototype.toString.call(value);
}
