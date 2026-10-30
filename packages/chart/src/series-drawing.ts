// What one series draws, in the axes' units, before any pixel (ADR-0253): a Level's points joined only to
// their neighbours, a step's levels with filled and open ends, a gap where a value is missing.
import type { ChartAxis, ChartPoint, ChartSeries, IntervalBound, Segment, SeriesInterval, SeriesSampler } from './model';
import { elementAt, unreachable } from './must';
import { bitDecrement } from './numbers';
import { colourBand, colourRun, sizeShare, valueRange } from './point-scales';
import { candlesOf, type DrawnCandle } from './price-ranges';
import { toNumber, yearsBetween } from './values';

/** A point of a drawn line, in the axes' own units: the horizontal position and the height. */
export interface DrawnPoint {
    /** The horizontal position. */
    readonly x: number;
    /** The height, in the vertical axis' units. */
    readonly y: number;
}

/** A lower and an upper bound. */
export type Bounds = readonly [lower: number, upper: number];

/** A marker: where it stands, and whether it is filled — the function takes the value there — or an outline (ADR-0253). */
export interface DrawnMarker {
    /** The horizontal position. */
    readonly x: number;
    /** The height. */
    readonly y: number;
    /** Filled where the function takes the value; an outline where it does not. */
    readonly filled: boolean;
    /** The index of the series' point the marker stands for, or -1 when it stands for none. */
    readonly point: number;
    /** How many of the series' points it stands for: more than one where markers on one device pixel were merged (ADR-0290). */
    readonly count: number;
    /** An Observation's vertical interval at the marker; where marks merged, the union of theirs (ADR-0304). */
    readonly yBounds?: Bounds;
    /** An Observation's horizontal interval at the marker (ADR-0304). */
    readonly xBounds?: Bounds;
    /** Its colour band from the series' colour scale; absent for the series' own colour (ADR-0320). */
    readonly band?: number;
    /** Its radius from the series' size scale: a share of the largest in a drawing, CSS pixels in a scene (ADR-0320). */
    readonly radius?: number;
}

/** A whisker as drawn: a point's interval. */
export interface DrawnWhisker {
    /** The point's position. */
    readonly x: number;
    /** Its lower bound. */
    readonly lower: number;
    /** Its upper bound. */
    readonly upper: number;
}

/** What one series draws, in the axes' units: its lines, each joining only neighbours, and its markers. */
export interface SeriesDrawing {
    /** Each an unbroken line; a gap, a step's level or a Segment's boundary starts another. */
    readonly lines: readonly (readonly DrawnPoint[])[];
    /** The markers, filled or an outline. */
    readonly markers: readonly DrawnMarker[];
    /** A Price Range's candles (ADR-0300, ADR-0302). */
    readonly candles: readonly DrawnCandle[];
    /** A Level's Interval drawn as whiskers (ADR-0303). */
    readonly whiskers: readonly DrawnWhisker[];
    /** A Level's Interval drawn as a band: each run of neighbours that all have one (ADR-0303). */
    readonly bands: readonly (readonly DrawnWhisker[])[];
}

/** A drawing of these lines and markers, and nothing else. */
export function seriesDrawing(lines: readonly (readonly DrawnPoint[])[], markers: readonly DrawnMarker[],
    more: Partial<Pick<SeriesDrawing, 'candles' | 'whiskers' | 'bands'>> = {}): SeriesDrawing {
    const { candles = [], whiskers = [], bands = [] } = more;
    return Object.freeze({
        lines: Object.freeze(lines), markers: Object.freeze(markers),
        candles: Object.freeze(candles), whiskers: Object.freeze(whiskers), bands: Object.freeze(bands),
    });
}

/** A marker, its point and count those of a marker that stands for none. */
export function marker(x: number, y: number, filled: boolean, more: Partial<Omit<DrawnMarker, 'x' | 'y' | 'filled'>> = {}): DrawnMarker {
    return Object.freeze({ x, y, filled, point: -1, count: 1, ...more });
}

const point = (x: number, y: number): DrawnPoint => Object.freeze({ x, y });

/** Where a sampled series is drawn, and the quotes it is asked at. */
export interface DrawingRange {
    /** The first position drawn; −∞ by default. */
    readonly from?: number;
    /** The last; +∞ by default. */
    readonly to?: number;
    /** About how many positions a sampled series is asked at; 0 by default. */
    readonly pixels?: number;
    /** Positions a sampled series is always asked at: the quotes it is drawn through (ADR-0257). */
    readonly quotes?: Iterable<number>;
}

/**
 * What a series draws. A sampled series is asked at about `pixels` positions over the part of its range
 * inside `from` to `to`, and always at each Segment's boundary and at each of `quotes` (ADR-0257).
 */
export function drawSeries(series: ChartSeries, axis: ChartAxis, range: DrawingRange = {}): SeriesDrawing {
    const { from = -Infinity, to = Infinity, pixels = 0, quotes = [] } = range;
    switch (series.kind) {
        case 'level':
            return series.sampler === undefined
                ? withInterval(level(series.points, series.segments, true), series)
                : level(sample(series.sampler, series.segments, from, to, pixels, quotes, axis.effectiveScale === 'logarithmic'), series.segments, false);
        case 'priceRange':
            return seriesDrawing([], [], { candles: candlesOf(series, from, to, 'row') });
        case 'rateOverInterval':
            return steps(series, axis);
        case 'amountPerPeriod':
            return steps(series, axis);
        case 'observation':
            return observations(series);
        default:
            return unreachable(series.kind);
    }
}

// A point's interval where both its bounds are given; a band joins only neighbours that each have one, and
// breaks at a point that has none (ADR-0303).
function withInterval(drawing: SeriesDrawing, series: ChartSeries): SeriesDrawing {
    const iv = series.interval;
    if (iv === undefined) {
        return drawing;
    }
    const bounds = new Map<number, DrawnWhisker>();
    for (const b of iv.bounds) {
        if (b.lower !== null && b.upper !== null) {
            if (bounds.has(b.x)) {
                throw new Error('Internal: two bounds at one position passed the definition.');
            }
            bounds.set(b.x, Object.freeze({ x: b.x, lower: toNumber(b.lower), upper: toNumber(b.upper) }));
        }
    }
    if (iv.style !== 'band') {
        return seriesDrawing(drawing.lines, drawing.markers, {
            whiskers: series.points.flatMap(p => {
                const w = bounds.get(p.x);
                return w === undefined ? [] : [w];
            }),
        });
    }
    const bands: (readonly DrawnWhisker[])[] = [];
    let run: DrawnWhisker[] = [];
    for (const p of series.points) {
        const w = bounds.get(p.x);
        if (w !== undefined) {
            run.push(w);
            continue;
        }
        if (run.length > 0) {
            bands.push(Object.freeze(run));
        }
        run = [];
    }
    if (run.length > 0) {
        bands.push(Object.freeze(run));
    }
    return seriesDrawing(drawing.lines, drawing.markers, { bands });
}

/** The height a step at point `i` is drawn at: its value, or for an amount its value per year. */
export function stepHeight(series: ChartSeries, axis: ChartAxis, i: number): number {
    const v = toNumber(valueAt(series.points, i));
    if (series.kind !== 'amountPerPeriod') {
        return v;
    }
    const [a, b] = stepInterval(series, i);
    return v / yearsBetween(axis, a, b);
}

/** The interval a step's point `i` stands for. */
export function stepInterval(series: ChartSeries, i: number): readonly [from: number, to: number] {
    return series.step === 'preceding'
        ? [elementAt(series.points, i - 1).x, elementAt(series.points, i).x]
        : [elementAt(series.points, i).x, elementAt(series.points, i + 1).x];
}

function valueAt(points: readonly ChartPoint[], i: number): number | bigint {
    const y = elementAt(points, i).y;
    if (y === null) {
        throw new Error('Internal: a step was asked its height where it has no value.');
    }
    return y;
}

function level(points: readonly ChartPoint[], segments: readonly Segment[], withMarkers: boolean): SeriesDrawing {
    const lines: (readonly DrawnPoint[])[] = [];
    const markers: DrawnMarker[] = [];
    let line: DrawnPoint[] = [];
    const end = (): void => {
        if (line.length > 1) {
            lines.push(Object.freeze(line));
        }
        line = [];
    };
    for (const p of points) {
        if (p.y === null) {
            end();
            continue;
        }
        const d = point(p.x, toNumber(p.y));
        const last = line.at(-1);
        if (last !== undefined && segments.some(g => crosses(g, last.x, p.x))) {
            end();
        }
        const atBoundary = segments.some(g => g.from === p.x || g.to === p.x);
        // Sampled, the curve was asked just short of the boundary from its left: the left line ends there
        // and the right one starts at the boundary itself, never joined.
        const before = line.at(-1);
        if (atBoundary && before !== undefined && before.x === bitDecrement(p.x)) {
            end();
        }
        line.push(d);
        if (withMarkers) {
            markers.push(marker(d.x, d.y, true));
        }
        // Handed in as points, a point on the boundary ends the left line and starts the right.
        if (atBoundary && line.length > 1) {
            end();
            line.push(d);
        }
    }
    end();
    return seriesDrawing(lines, markers);
}

// A boundary strictly between two neighbours: the line is not joined across it, where the Consumer's
// method changes and a forward may jump (ADR-0257).
const crosses = (g: Segment, a: number, b: number): boolean => (a < g.from && g.from < b) || (a < g.to && g.to < b);

function steps(series: ChartSeries, axis: ChartAxis): SeriesDrawing {
    const p = series.points;
    const lines: (readonly DrawnPoint[])[] = [];
    const markers: DrawnMarker[] = [];
    const preceding = series.step === 'preceding';
    for (let i = 0; i < p.length; i++) {
        const y = elementAt(p, i).y;
        if (y === null) {
            continue;
        }
        const hasInterval = preceding ? i > 0 : i < p.length - 1;
        if (!hasInterval) {
            // A rate at the edge holds at its own instant only; an amount there is refused when the
            // definition is made.
            markers.push(marker(elementAt(p, i).x, toNumber(y), true));
            continue;
        }
        const h = stepHeight(series, axis, i);
        const [a, b] = stepInterval(series, i);
        lines.push(Object.freeze([point(a, h), point(b, h)]));
        const [own, other] = preceding ? [b, a] : [a, b];
        markers.push(marker(own, h, true));
        const neighbour = preceding ? i - 1 : i + 1;
        const continuous = elementAt(p, neighbour).y !== null && neighbourHeight(series, axis, neighbour, preceding) === h;
        if (!continuous) {
            markers.push(marker(other, h, false));
        }
    }
    return seriesDrawing(lines, markers);
}

// The height the neighbour's own level takes at the shared end; at an edge with no interval the value
// itself, which an amount never has there.
function neighbourHeight(series: ChartSeries, axis: ChartAxis, i: number, preceding: boolean): number {
    const hasInterval = preceding ? i > 0 : i < series.points.length - 1;
    return hasInterval ? stepHeight(series, axis, i) : toNumber(valueAt(series.points, i));
}

// An Observation's points, each a marker of its own and none joined (ADR-0290); a point missing its value
// is no marker. Its interval's bounds are one a row (ADR-0304).
function observations(series: ChartSeries): SeriesDrawing {
    const markers: DrawnMarker[] = [];
    const colourBy = series.colourBy;
    const sizeBy = series.sizeBy;
    // Each scale's run is read once, not once a point.
    const run = colourBy === undefined ? undefined : colourRun(colourBy);
    const sizeRange = sizeBy === undefined ? undefined : valueRange(sizeBy.values);
    const largest = sizeRange === undefined ? 0 : sizeRange[1];
    series.points.forEach((p, i) => {
        if (p.y === null || !Number.isFinite(p.x)) {
            return;
        }
        // A point a scale has no value for is an outline, and the Notes say so (ADR-0320).
        const colour = colourBy === undefined ? undefined : colourBy.values[i];
        const size = sizeBy === undefined ? undefined : sizeBy.values[i];
        const unscaled = (colourBy !== undefined && (colour === undefined || colour === null)) || (sizeBy !== undefined && (size === undefined || size === null));
        const yBounds = boundsAt(series.interval, i);
        const xBounds = boundsAt(series.xInterval, i);
        markers.push(marker(p.x, toNumber(p.y), !unscaled, {
            point: i,
            ...(yBounds === undefined ? {} : { yBounds }),
            ...(xBounds === undefined ? {} : { xBounds }),
            ...(colourBy === undefined || run === undefined || colour === undefined || colour === null ? {} : { band: colourBand(colourBy, run, toNumber(colour)) }),
            ...(size === undefined || size === null ? {} : { radius: sizeShare(largest, toNumber(size)) }),
        }));
    });
    return seriesDrawing([], markers);
}

/** An Observation's bounds at row `i`, where both are given. */
export function boundsAt(interval: SeriesInterval | undefined, i: number): Bounds | undefined {
    if (interval === undefined) {
        return undefined;
    }
    const b: IntervalBound = elementAt(interval.bounds, i);
    return b.lower === null || b.upper === null ? undefined : Object.freeze([toNumber(b.lower), toNumber(b.upper)] as const);
}

/** The positions a sampled series is asked at, and its answers. */
export function sample(f: SeriesSampler, segments: readonly Segment[], from: number, to: number, pixels: number, quotes: Iterable<number>, logarithmic = false): ChartPoint[] {
    const lo = Math.max(f.from, from);
    const hi = Math.min(f.to, to);
    if (!(lo <= hi)) {
        return [];
    }
    const xs = new Set<number>([lo, hi]);
    const n = Math.max(pixels, 2);
    // On a logarithmic axis the samples are even in the axis' own scale (ADR-0290).
    for (let k = 1; k < n; k++) {
        xs.add(logarithmic && lo > 0 ? lo * (hi / lo) ** (k / n) : lo + (hi - lo) * k / n);
    }
    for (const g of segments) {
        for (const b of [g.from, g.to]) {
            if (lo <= b && b <= hi) {
                xs.add(b);
            }
        }
    }
    for (const q of quotes) {
        if (lo <= q && q <= hi) {
            xs.add(q);
        }
    }
    const points: ChartPoint[] = [];
    for (const x of [...xs].toSorted((a, b) => a - b)) {
        // Just short of a boundary the curve is asked from its left, so a jump there is drawn as the
        // Consumer's method has it on each side.
        if (segments.some(g => (g.from === x || g.to === x) && x > lo)) {
            const left = bitDecrement(x);
            points.push(Object.freeze({ x: left, y: f.valueAt(left) }));
        }
        points.push(Object.freeze({ x, y: f.valueAt(x) }));
    }
    return points;
}

/** The Segment that covers `x`: the one it lies in, or the one it ends, since a Segment's end is its own (ADR-0257). */
export function segmentAt(s: ChartSeries, x: number): Segment | undefined {
    const inside = s.segments.find(k => k.from <= x && x < k.to);
    return inside !== undefined ? inside : s.segments.findLast(k => k.to === x);
}
