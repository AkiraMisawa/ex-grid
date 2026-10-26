// What the tooltip says (ADR-0270): every visible series of the pointer's panel read at the position under
// the pointer, each by its own Series Kind, an Observation's mark (ADR-0290) and an edge (ADR-0321). A value
// is only ever one the Consumer handed in or its sampler answered; nothing is read between two points. The
// port of the C# engine's `ChartReadings`, held to it by the shared cases under tests/cases/chart/.
import type { ChartDefinition } from './definition';
import { resolveMetrics, type ChartMetrics, type ChartView } from './metrics';
import type { ChartAxis, ChartPoint, ChartSeries, ChartValue, SeriesInterval, SeriesLook, StepSide } from './model';
import { defined, elementAt } from './must';
import { candlesOf, hasOpen, percentChange, periodStart, wholeBar } from './price-ranges';
import { yOf, type ChartScene, type PanelScene, type SeriesPiece } from './scene';
import { invariantText, toNumber, writeObject, writePosition, writeValue, yearsBetween } from './values';

/** One series' line in the tooltip (ADR-0270). */
export interface ReadingLine {
    /** The series' identity. */
    readonly id: string;
    /** Its name. */
    readonly name: string;
    /** Its look, for the swatch. */
    readonly look: SeriesLook;
    /** Its value at the position, written through the series' format; `null` when it has none there. */
    readonly value: string | null;
    /** What the value is: a step's interval, a thinned pixel's count, interpolated or extrapolated, or why there is no value. */
    readonly note: string | null;
    /** The value's height in pixels, which the nearest lines are chosen by; `null` when it has none there. */
    readonly y: number | null;
}

/** What the tooltip says at one position in one panel, over a mark or over an edge (ADR-0270). */
export interface ChartReading {
    /** The position, written as the axis writes it, or what the mark or the edge is named by. */
    readonly position: string;
    /** The lines shown, top to bottom: the nearest to the pointer's height, as many as fit the plot. */
    readonly lines: readonly ReadingLine[];
    /** How many series are not listed, which the tooltip says as "and M more series"; never a silent cut. */
    readonly more: number;
}

/**
 * What the tooltip says at `position` in panel `panel` of `scene`, built from `definition` at `metrics`.
 * Past what fits the plot's height, the series nearest `pointerY` are listed and the rest counted. The
 * view is the one the scene was built with, which the scene already holds.
 */
export function readingAt(definition: ChartDefinition, scene: ChartScene, metrics: ChartMetrics, _view: ChartView, position: number, panel: number,
    pointerY: number): ChartReading {
    const p = scene.panels[panel];
    if (p === undefined) {
        throw new RangeError(`The scene has no panel ${String(panel)}; it has ${String(scene.panels.length)}.`);
    }
    const axis = definition.axis;
    const byId = new Map(definition.series.map(s => [s.id, s] as const));
    const lines = p.series.map(piece => {
        const s = defined(byId.get(piece.id), `the series '${piece.id}'`);
        return s.kind === 'priceRange' ? candle(s, piece, p, scene, axis, position) : line(s, piece, p, axis, position);
    });

    // A header line, the lines, and the "more" line, all inside the plot's height.
    const room = Math.max(1, Math.floor(p.plot.height / resolveMetrics(metrics).lineHeight) - 2);
    const shown = lines.length <= room ? lines
        : lines.map((l, i) => ({ l, i, d: l.y === null ? Infinity : Math.abs(l.y - pointerY) }))
            .toSorted((a, b) => (a.d === b.d ? a.i - b.i : a.d < b.d ? -1 : 1)).slice(0, room).map(t => t.l);
    const ordered = shown.map(l => ({ l, i: lines.indexOf(l) }))
        .toSorted((a, b) => {
            const byHeight = heightOrder(a.l.y, b.l.y);
            return byHeight === 0 ? a.i - b.i : byHeight;
        }).map(t => t.l);
    return reading(writePosition(axis, position), ordered, lines.length - shown.length);
}

/**
 * What the tooltip says over an Observation's mark (ADR-0290): the point's name, or its position across,
 * then the series with the point's x and y, and how many points the mark stands for when markers on one
 * device pixel were merged — never one of them as if it were the only one.
 */
export function readingOfMark(definition: ChartDefinition, scene: ChartScene, mark: number): ChartReading {
    const m = scene.marks[mark];
    if (m === undefined) {
        throw new RangeError(`The scene has no mark ${String(mark)}; it has ${String(scene.marks.length)}.`);
    }
    const s = seriesOf(definition, m.seriesId);
    const look = lookOf(scene, m.panel, s.id);
    const p = elementAt(s.points, m.point);
    const axis = definition.axis;
    const value = `${writePosition(axis, p.x)}, ${p.y === null ? '' : writeValue(s, p.y)}`;
    let note = join(join(observedBounds(s.interval, m.point, v => writeValue(s, v), s.xInterval === undefined ? '' : 'y '),
        observedBounds(s.xInterval, m.point, v => writePosition(axis, toNumber(v)), 'x ')),
    m.count > 1 ? `${String(m.count)} points here: ${p.name === undefined ? 'this one' : p.name} and ${String(m.count - 1)} more` : null);
    // A colour or a size read from a column is said with its value (ADR-0320).
    if (s.colourBy !== undefined) {
        note = join(note, `${s.colourBy.title} ${said(elementAt(s.colourBy.values, m.point))}`);
    }
    if (s.sizeBy !== undefined) {
        note = join(note, `${s.sizeBy.title} ${said(elementAt(s.sizeBy.values, m.point))}`);
    }
    // The columns the Consumer named for the tooltip, and no other (ADR-0321).
    for (const d of s.details) {
        const v = elementAt(d.values, m.point);
        note = join(note, `${d.title} ${v === null ? 'has no value' : v}`);
    }
    // A node with no label is named by its key (ADR-0321).
    const named = p.name !== undefined ? p.name : s.edges.length > 0 ? writeObject(p.row) : writePosition(axis, p.x);
    return reading(named, [readingLine(s.id, s.name, look, value, note, m.y)], 0);
}

/**
 * What the tooltip says over an edge (ADR-0321): its two keys. An edge has no value of its own, and nothing
 * about it is read from its nodes.
 */
export function readingOfEdge(definition: ChartDefinition, scene: ChartScene, edge: number): ChartReading {
    const m = scene.edgeMarks[edge];
    if (m === undefined) {
        throw new RangeError(`The scene has no edge ${String(edge)}; it has ${String(scene.edgeMarks.length)}.`);
    }
    const s = seriesOf(definition, m.seriesId);
    const look = lookOf(scene, m.panel, s.id);
    const e = elementAt(s.edges, m.edge.index);
    const from = writeObject(e.from);
    const to = writeObject(e.to);
    return reading(`${from} → ${to}`, [readingLine(s.id, s.name, look, `edge from ${from} to ${to}`, null, (m.edge.y1 + m.edge.y2) / 2)], 0);
}

function reading(position: string, lines: readonly ReadingLine[], more: number): ChartReading {
    return Object.freeze({ position, lines: Object.freeze([...lines]), more });
}

function readingLine(id: string, name: string, look: SeriesLook, value: string | null, note: string | null, y: number | null): ReadingLine {
    return Object.freeze({ id, name, look, value, note, y });
}

function seriesOf(definition: ChartDefinition, id: string): ChartSeries {
    const s = definition.series.find(x => x.id === id);
    if (s === undefined) {
        throw new Error(`The scene names a series '${id}' the definition does not have.`);
    }
    return s;
}

function lookOf(scene: ChartScene, panel: number, id: string): SeriesLook {
    const piece = scene.panels[panel]?.series.find(p => p.id === id);
    if (piece === undefined) {
        throw new Error(`The scene's panel ${String(panel)} does not draw the series '${id}'.`);
    }
    return piece.look;
}

// A height a line has before one it has not, as C# orders a missing height last.
function heightOrder(a: number | null, b: number | null): number {
    if (a === null || b === null) {
        return a === null ? (b === null ? 0 : 1) : -1;
    }
    return a - b;
}

// A value read from a column, or that it has none.
function said(v: ChartValue | null): string {
    return v === null ? 'has no value' : invariantText(v);
}

function line(s: ChartSeries, piece: SeriesPiece, panel: PanelScene, axis: ChartAxis, x: number): ReadingLine {
    const vertical = s.axis === 'right' ? panel.right : panel.left;
    const y = (height: number): number | null => (vertical === null ? null : yOf(vertical, height, panel.plot));
    const make = (value: string | null, note: string | null, at: number | null): ReadingLine => readingLine(s.id, s.name, piece.look, value, note, at);

    // A thinned pixel names its range, never one point as if it were the only one (ADR-0254).
    const thinned = piece.thinned.find(t => t.from <= x && x <= t.to);
    if (thinned !== undefined && s.sampler === undefined) {
        const held = s.points.flatMap(q => (q.x >= thinned.from && q.x <= thinned.to && q.y !== null ? [q.y] : []));
        if (held.length > 1) {
            const lo = extreme(held, (a, b) => a < b);
            const hi = extreme(held, (a, b) => a > b);
            const at = s.kind === 'amountPerPeriod' ? null : y((toNumber(lo) + toNumber(hi)) / 2);
            return make(`${writeValue(s, lo)} – ${writeValue(s, hi)}`,
                `${String(held.length)} points, ${writePosition(axis, thinned.from)} – ${writePosition(axis, thinned.to)}`, at);
        }
    }

    const f = s.sampler;
    if (f !== undefined) {
        if (x < f.from || x > f.to) {
            return make(null, 'outside its range', null);
        }
        const v = f.valueAt(x);
        return v === null ? make(null, 'missing', null) : make(writeValue(s, v), method(s, x), y(toNumber(v)));
    }

    const points = s.points;
    if (s.kind === 'level') {
        const point = points.find(q => q.x === x);
        if (point === undefined) {
            return make(null, 'no point here', null);
        }
        return point.y === null ? make(null, 'missing', null) : make(writeValue(s, point.y), join(method(s, x), bounds(s, x)), y(toNumber(point.y)));
    }

    // A step names its interval (ADR-0253): the point whose value holds at x, and the interval it holds over.
    const found = intervalAt(points, x, s.step);
    if (found === undefined) {
        return make(null, 'no interval here', null);
    }
    const [index, from, to] = found;
    const held = elementAt(points, index).y;
    if (held === null) {
        return make(null, 'missing', null);
    }
    const interval = from === to ? `at ${writePosition(axis, from)}` : `${writePosition(axis, from)} – ${writePosition(axis, to)}`;
    const m = method(s, x);
    const note = m === null ? interval : `${interval}, ${m}`;
    const drawn = s.kind === 'amountPerPeriod' && from !== to ? toNumber(held) / yearsBetween(axis, from, to) : toNumber(held);
    return make(writeValue(s, held), note, y(drawn));
}

// The first of the values that `before` puts ahead of every other, as C#'s MinBy and MaxBy take it.
function extreme(values: readonly ChartValue[], before: (a: number, b: number) => boolean): ChartValue {
    let best = elementAt(values, 0);
    for (const v of values) {
        if (before(toNumber(v), toNumber(best))) {
            best = v;
        }
    }
    return best;
}

// A candle's four values, its change from the previous candle's close and its volume; a merged candle names
// its period and how many rows it holds (ADR-0300, ADR-0302). In a volume panel, the volume alone.
function candle(s: ChartSeries, piece: SeriesPiece, panel: PanelScene, scene: ChartScene, axis: ChartAxis, x: number): ReadingLine {
    const vertical = panel.left;
    const plot = panel.plot;
    const y = (height: number): number | null => (vertical === null ? null
        : plot.y + plot.height - (vertical.max === vertical.min ? 0.5 : (height - vertical.min) / (vertical.max - vertical.min)) * plot.height);
    const make = (value: string | null, note: string | null, at: number | null): ReadingLine => readingLine(s.id, piece.name, piece.look, value, note, at);
    const period = piece.period;
    // The candles as drawn, over the range shown, so the tooltip reads what the plot draws.
    const candles = candlesOf(s, scene.xFrom, scene.xTo, period);
    const c = candles.find(k => (period === 'row' ? k.from === x : periodStart(k.from, period) === periodStart(x, period)));
    if (c === undefined) {
        return s.bars.some(b => b.x === x) ? make(null, '(no value)', null) : make(null, 'no candle here', null);
    }
    const w = (v: number): string => writeValue(s, v);
    const volume = c.volume === null ? null : `Vol ${w(c.volume)}`;
    const span = c.rows > 1 || period !== 'row' ? `${writePosition(axis, c.from)} – ${writePosition(axis, c.to)}, ${String(c.rows)} rows` : null;
    if (panel.volumeOf !== null) {
        return make(volume === null ? '(no value)' : volume, span, c.volume === null ? null : y(c.volume));
    }
    const values = c.open === null ? `H ${w(c.high)} L ${w(c.low)} C ${w(c.close)}` : `O ${w(c.open)} H ${w(c.high)} L ${w(c.low)} C ${w(c.close)}`;
    // The change from the last close before this candle, shown or not.
    const withOpen = hasOpen(s.bars);
    const before = s.bars.filter(b => b.x < c.from && wholeBar(b, withOpen));
    const last = before[before.length - 1];
    const change = last === undefined || last.close === null ? 'Chg —' : changeFrom(c.close, toNumber(last.close), w);
    return make(values, join(join(span, c.unchanged ? 'unchanged' : null), join(change, volume)), y(c.close));
}

function changeFrom(close: number, previous: number, w: (v: number) => string): string {
    const delta = close - previous;
    return `Chg ${delta >= 0 ? '+' : ''}${w(delta)}${percentChange(delta, previous)}`;
}

function bounds(s: ChartSeries, x: number): string | null {
    const iv = s.interval;
    if (iv === undefined) {
        return null;
    }
    const found = iv.bounds.filter(b => b.x === x);
    const [b] = found;
    return found.length === 1 && b !== undefined && b.lower !== null && b.upper !== null
        ? `[${writeValue(s, b.lower)}, ${writeValue(s, b.upper)}] ${iv.meaning}`
        : '(no interval)';
}

// An Observation's bounds at its row, with their meaning (ADR-0304).
function observedBounds(iv: SeriesInterval | undefined, point: number, write: (v: ChartValue) => string, which: string): string | null {
    if (iv === undefined) {
        return null;
    }
    const b = elementAt(iv.bounds, point);
    return b.lower !== null && b.upper !== null ? `${which}[${write(b.lower)}, ${write(b.upper)}] ${iv.meaning}` : `${which}(no interval)`;
}

function join(a: string | null, b: string | null): string | null {
    return a === null ? b : b === null ? a : `${a}, ${b}`;
}

// Preceding: a point's value holds over (previous, point]; Following: over [point, next). At an edge with no
// interval, the point's own value at its own position.
function intervalAt(points: readonly ChartPoint[], x: number, side: StepSide): readonly [index: number, from: number, to: number] | undefined {
    for (let i = 0; i < points.length; i++) {
        const here = elementAt(points, i).x;
        if (side === 'preceding' && i > 0 && elementAt(points, i - 1).x < x && x <= here) {
            return [i, elementAt(points, i - 1).x, here];
        }
        if (side === 'following' && i < points.length - 1 && here <= x && x < elementAt(points, i + 1).x) {
            return [i, here, elementAt(points, i + 1).x];
        }
    }
    const first = points[0];
    const last = points[points.length - 1];
    if (first !== undefined && side === 'preceding' && first.x === x) {
        return [0, x, x];
    }
    if (last !== undefined && side === 'following' && last.x === x) {
        return [points.length - 1, x, x];
    }
    return undefined;
}

// An interpolated value says so (ADR-0257), with its Segment's method where one covers it.
function method(s: ChartSeries, x: number): string | null {
    const g = s.segments.find(k => k.from <= x && x < k.to) ?? s.segments.findLast(k => k.to === x);
    if (g !== undefined) {
        return g.extrapolated === true ? `${g.method}, extrapolated` : s.interpolated ? `interpolated, ${g.method}` : g.method;
    }
    return s.interpolated ? 'interpolated' : null;
}
