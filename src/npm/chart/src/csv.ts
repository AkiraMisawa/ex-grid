// What a chart shows, as CSV (ticket 07): the series and range left after the Chart Filters, every point
// before Thinning (ADR-0254), each value with the digits it was handed in with. Invariant, unformatted, a
// byte-order mark first so Excel reads it as UTF-8; a missing value is an empty field, never a zero. The
// port of the C# engine's `ChartCsv`, held to it by the shared cases under tests/cases/chart/.
import { dateOf, isoDate, wholeDay } from './day-numbers';
import type { ChartDefinition } from './definition';
import { roundTrip } from './invariant-text';
import type { ChartMetrics, ChartView } from './metrics';
import type { ChartAxis, ChartPoint, ChartSeries, ChartValue, PriceBar, SeriesInterval } from './model';
import { defined, elementAt, unreachable } from './must';
import { hasOpen } from './price-ranges';
import { frameOf, quotesOf } from './scene';
import { sample } from './series-drawing';
import { invariantText, writeObject } from './values';

/** A table of what the chart shows: a header and rows of invariant text. */
export interface ChartTable {
    /** The column headers: the axis' title, then a column per series. */
    readonly header: readonly string[];
    /** A row per position. */
    readonly rows: readonly (readonly string[])[];
}

const bom = '﻿';

/**
 * A row per point, after a byte-order mark: Series, the axis' title, Value, Kind (quoted, interpolated,
 * extrapolated; a Price Range's columns are "price range", an Interval's bounds "interval") and Method (the
 * Segment's). Its empty Value is only ever a missing one.
 */
export function chartCsvLong(definition: ChartDefinition, metrics: ChartMetrics, view: ChartView = {}): string {
    const axis = definition.axis;
    const named = definition.series.some(s => s.kind === 'observation');
    let csv = bom + row(named ? ['Series', 'Point', axisTitle(axis), 'Value', 'Kind', 'Method'] : ['Series', axisTitle(axis), 'Value', 'Kind', 'Method']);
    for (const [s, points] of shown(definition, metrics, view)) {
        // An Observation's bounds follow each of its points, one a row (ADR-0304).
        if (s.kind === 'observation') {
            for (const [p, i] of observed(s, points)) {
                csv += row([s.name, nameOf(p), position(axis, p.x), text(p.y), 'observed', '']);
                for (const [iv, which] of [[s.interval, s.xInterval === undefined ? '' : 'y '], [s.xInterval, 'x ']] as const) {
                    if (iv !== undefined) {
                        const b = elementAt(iv.bounds, i);
                        csv += row([`${s.name} ${which}lower (${iv.meaning})`, nameOf(p), position(axis, p.x), text(b.lower), 'interval', '']);
                        csv += row([`${s.name} ${which}upper (${iv.meaning})`, nameOf(p), position(axis, p.x), text(b.upper), 'interval', '']);
                    }
                }
            }
            continue;
        }
        const more = extra(s, points);
        if (more.length > 0) {
            // A Price Range's every row, and an Interval's bounds named by their meaning (ADR-0300, ADR-0303).
            const kind = s.kind === 'priceRange' ? 'price range' : 'interval';
            if (s.kind !== 'priceRange') {
                for (const p of points) {
                    const [pk, pm] = kindOf(s, p.x);
                    csv += row([s.name, position(axis, p.x), text(p.y), pk, pm]);
                }
            }
            for (const [header, values] of more) {
                for (const [x, v] of [...values].toSorted((a, b) => a[0] - b[0])) {
                    csv += row([header, position(axis, x), v, kind, '']);
                }
            }
            continue;
        }
        for (const p of points) {
            const [kind, method] = kindOf(s, p.x);
            const tail = [position(axis, p.x), text(p.y), kind, method];
            csv += row(named ? [s.name, nameOf(p), ...tail] : [s.name, ...tail]);
        }
    }
    return csv;
}

/**
 * A row per position, a column per series, after a byte-order mark — an Interpolated series' column says
 * so, and a series with extrapolated points has a column of its own for them. An empty cell cannot tell a
 * missing value from a position the series has no point at; the long form can.
 */
export function chartCsvWide(definition: ChartDefinition, metrics: ChartMetrics, view: ChartView = {}): string {
    const table = chartTable(definition, metrics, view);
    return bom + row(table.header) + table.rows.map(row).join('');
}

/** The wide form's header and rows, unquoted: what "View as table" shows (ADR-0275), the same content as {@link chartCsvWide}. */
export function chartTable(definition: ChartDefinition, metrics: ChartMetrics, view: ChartView = {}): ChartTable {
    const axis = definition.axis;
    const all = shown(definition, metrics, view);
    // A scatter's points share positions across, so its table is a row per point, never a column per series
    // keyed by position, which would keep one point of each position (ADR-0290).
    if (definition.series.some(s => s.kind === 'observation')) {
        return observations(definition, all);
    }
    const columns: Column[] = [];
    for (const [s, points] of all) {
        if (s.kind === 'priceRange') {
            columns.push(...extra(s, points));
            continue;
        }
        const main = new Map<number, string>();
        const extrapolated = new Map<number, string>();
        for (const p of points) {
            (kindOf(s, p.x)[0] === 'extrapolated' ? extrapolated : main).set(p.x, text(p.y));
        }
        columns.push([s.interpolated ? `${s.name} (interpolated)` : s.name, main]);
        if (extrapolated.size > 0) {
            columns.push([`${s.name} (extrapolated)`, extrapolated]);
        }
        columns.push(...extra(s, points));
    }
    const xs = [...new Set(columns.flatMap(([, values]) => [...values.keys()]))].toSorted((a, b) => a - b);
    // A position a column has no value at is an empty cell (ticket 07), which only the long form tells from a missing value.
    // oxlint-disable-next-line ex-grid/no-stand-in-default -- ticket 07: the wide form leaves a position a series has no point at empty
    const rows = xs.map(x => [position(axis, x), ...columns.map(([, values]) => values.get(x) ?? '')]);
    return frozenTable([axisTitle(axis), ...columns.map(([header]) => header)], rows);
}

/** The horizontal axis' title in the files and the summary: its own, or what its positions are. */
export function axisTitle(axis: ChartAxis): string {
    return axis.title.length > 0 ? axis.title : axis.values === 'dates' ? 'Date' : 'X';
}

type Column = readonly [header: string, values: ReadonlyMap<number, string>];

type Shown = readonly [series: ChartSeries, points: readonly ChartPoint[]];

function frozenTable(header: readonly string[], rows: readonly (readonly string[])[]): ChartTable {
    return Object.freeze({ header: Object.freeze([...header]), rows: Object.freeze(rows.map(r => Object.freeze([...r]))) });
}

// The visible series and the points each shows: those in range, or a sampled series' samples.
function shown(d: ChartDefinition, metrics: ChartMetrics, view: ChartView): Shown[] {
    const { axis, visible, from, to, devicePixels } = frameOf(d, metrics, view);
    return visible.map(s => [s, s.sampler === undefined
        ? s.points.filter(p => p.x >= from && p.x <= to)
        : sample(s.sampler, s.segments, from, to, devicePixels, quotesOf(s, visible), axis.effectiveScale === 'logarithmic')]);
}

// A scatter's table, a row per point, with a lower and an upper column for each quantity that has an
// interval: named by its meaning when every series means the same, else with the meaning in a column of its
// own (ADR-0304).
function observations(d: ChartDefinition, all: readonly Shown[]): ChartTable {
    const title = axisTitle(d.axis);
    const header = ['Series', 'Point', title, 'Value'];
    const observing = all.filter(([s]) => s.kind === 'observation').map(([s]) => s);
    const quantities: { readonly of: (s: ChartSeries) => SeriesInterval | undefined; readonly plain: boolean }[] = [];
    for (const [of, name] of [[(s: ChartSeries) => s.interval, 'Value'], [(s: ChartSeries) => s.xInterval, title]] as const) {
        const meanings = distinct(observing.flatMap(s => {
            const iv = of(s);
            return iv === undefined ? [] : [iv.meaning];
        }));
        const [meaning] = meanings;
        if (meaning === undefined) {
            continue;
        }
        const plain = meanings.length > 1 || observing.some(s => of(s) === undefined);
        header.push(...(plain ? [`${name} lower`, `${name} upper`, `${name} interval`] : [`${name} lower (${meaning})`, `${name} upper (${meaning})`]));
        quantities.push({ of, plain });
    }
    // A colour or a size read from a column is a column of the file: files carry every value (ADR-0320).
    const scales = distinct(observing.flatMap(s => [s.colourBy?.title, s.sizeBy?.title].filter(t => t !== undefined)));
    header.push(...scales);
    // So is a column the tooltip says (ADR-0321).
    const details = distinct(observing.flatMap(s => s.details.map(x => x.title)).filter(t => !scales.includes(t)));
    header.push(...details);
    const rows: string[][] = [];
    for (const [s, points] of all) {
        for (const [p, i] of observed(s, points)) {
            const r = [s.name, nameOf(p), position(d.axis, p.x), text(p.y)];
            for (const { of, plain } of quantities) {
                const iv = s.kind === 'observation' ? of(s) : undefined;
                const b = iv === undefined ? undefined : elementAt(iv.bounds, i);
                r.push(b === undefined ? '' : text(b.lower), b === undefined ? '' : text(b.upper));
                if (plain) {
                    r.push(iv === undefined ? '' : iv.meaning);
                }
            }
            for (const t of scales) {
                const values = s.colourBy?.title === t ? s.colourBy.values : s.sizeBy?.title === t ? s.sizeBy.values : undefined;
                r.push(values === undefined ? '' : text(elementAt(values, i)));
            }
            for (const t of details) {
                const detail = s.details.find(x => x.title === t);
                const v = detail === undefined ? null : elementAt(detail.values, i);
                r.push(v === null ? '' : v);
            }
            rows.push(r);
        }
    }
    // A node series lists its edges after its nodes (ADR-0321).
    for (const [s] of all.filter(([series]) => series.edges.length > 0)) {
        for (const e of s.edges) {
            const r = [s.name, `edge ${writeObject(e.from)} → ${writeObject(e.to)}`];
            while (r.length < header.length) {
                r.push('');
            }
            rows.push(r);
        }
    }
    return frozenTable(header, rows);
}

// The points shown with their rows: an Observation's every point in range, a sampled series' samples (row -1).
function observed(s: ChartSeries, points: readonly ChartPoint[]): (readonly [point: ChartPoint, row: number])[] {
    if (s.kind !== 'observation' || s.sampler !== undefined) {
        return points.map(p => [p, -1] as const);
    }
    const kept = new Set(points);
    return s.points.flatMap((p, i) => (kept.has(p) ? [[p, i] as const] : []));
}

// The columns a series carries beyond its points: a Price Range's open, high, low, close and volume, and an
// Interval's lower and upper bounds, each named by its meaning.
function extra(s: ChartSeries, points: readonly ChartPoint[]): Column[] {
    const xs = new Set(points.map(p => p.x));
    const columns = (cols: readonly (readonly [name: string, value: (x: number) => string])[]): Column[] =>
        cols.map(([name, value]) => [`${s.name} ${name}`, new Map([...xs].map(x => [x, value(x)] as const))]);
    if (s.kind === 'priceRange') {
        const bars = new Map(s.bars.filter(b => xs.has(b.x)).map(b => [b.x, b] as const));
        const bar = (x: number): PriceBar => defined(bars.get(x), `the row of ${roundTrip(x)}`);
        const cols: (readonly [string, (x: number) => string])[] = [];
        if (hasOpen(s.bars)) {
            cols.push(['open', x => text(bar(x).open)]);
        }
        cols.push(['high', x => text(bar(x).high)], ['low', x => text(bar(x).low)], ['close', x => text(bar(x).close)]);
        if (s.bars.some(b => b.volume !== undefined && b.volume !== null)) {
            cols.push(['volume', x => text(bar(x).volume)]);
        }
        return columns(cols);
    }
    const iv = s.interval;
    if (iv !== undefined) {
        const bounds = new Map(iv.bounds.filter(b => xs.has(b.x)).map(b => [b.x, b] as const));
        return columns([
            [`lower (${iv.meaning})`, x => {
                const b = bounds.get(x);
                return b === undefined ? '' : text(b.lower);
            }],
            [`upper (${iv.meaning})`, x => {
                const b = bounds.get(x);
                return b === undefined ? '' : text(b.upper);
            }],
        ]);
    }
    return [];
}

function kindOf(s: ChartSeries, x: number): readonly [kind: string, method: string] {
    const g = s.segments.find(k => k.from <= x && x < k.to) ?? s.segments.findLast(k => k.to === x);
    if (s.kind === 'observation') {
        return ['observed', ''];
    }
    if (g !== undefined && g.extrapolated === true) {
        return ['extrapolated', g.method];
    }
    return [s.interpolated ? 'interpolated' : 'quoted', g === undefined ? '' : g.method];
}

// A value as the files write it: invariantly with every digit, a missing one as an empty field (ticket 07).
function text(v: ChartValue | null | undefined): string {
    return v === null || v === undefined ? '' : invariantText(v);
}

// A point's name, a point with none an empty field.
function nameOf(p: ChartPoint): string {
    return p.name === undefined ? '' : p.name;
}

// A position as the files write it: a date as yyyy-MM-dd, a category by its label, a number with every digit.
function position(axis: ChartAxis, x: number): string {
    switch (axis.values) {
        case 'dates':
            return isoDate(dateOf(wholeDay(x)));
        case 'labels': {
            const label = axis.labels[wholeDay(x)];
            if (label === undefined) {
                throw new RangeError(`${roundTrip(x)} is not one of the axis' ${String(axis.labels.length)} labels.`);
            }
            return label;
        }
        case 'numbers':
            return roundTrip(x);
        default:
            return unreachable(axis.values);
    }
}

function row(fields: readonly string[]): string {
    return `${fields.map(field).join(',')}\r\n`;
}

function field(f: string): string {
    return /[,"\r\n]/.test(f) ? `"${f.replaceAll('"', '""')}"` : f;
}

function distinct(values: readonly string[]): string[] {
    return [...new Set(values)];
}
