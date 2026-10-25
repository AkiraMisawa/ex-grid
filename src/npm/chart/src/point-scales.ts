// An Observation's colour and size read from a column (ADR-0320), and the keys beside the plot they are
// read on.
import { upToPlaces } from './invariant-text';
import type { ChartSeries, ChartValue, PointColour } from './model';
import { invariantText, toNumber } from './values';
import { nice, steps } from './ticks';
import type { Box, ChartMetricsResolved } from './metrics';
import { greatest, least } from './numbers';

/** A tick of a key: its value, its height in CSS pixels, and its label. */
export interface KeyTick {
    /** The value. */
    readonly value: number;
    /** Its height in CSS pixels. */
    readonly y: number;
    /** Its label. */
    readonly label: string;
}

/** The colour bar a series' colour is read on (ADR-0320). */
export interface ColourKey {
    /** The series it belongs to. */
    readonly seriesId: string;
    /** What the colours are. */
    readonly title: string;
    /** The value at the foot of the lowest band. */
    readonly from: number;
    /** The value at the head of the highest band. */
    readonly to: number;
    /** Its ticks, the centre always among them when it diverges. */
    readonly ticks: readonly KeyTick[];
    /** Whether the bands diverge from a centre, which the middle band holds. */
    readonly diverging: boolean;
    /** The bar's box in CSS pixels; its title stands above it. */
    readonly box: Box;
    /** How many bands it is drawn in, low to high. */
    readonly bands: number;
}

/** A size key's entry: a value, its label, and the radius a marker of that value is drawn at. */
export interface SizeEntry {
    /** The value. */
    readonly value: number;
    /** Its label. */
    readonly label: string;
    /** The radius in CSS pixels. */
    readonly radius: number;
}

/** The size key a series' size is read on (ADR-0320). */
export interface SizeKey {
    /** The series it belongs to. */
    readonly seriesId: string;
    /** What the sizes are. */
    readonly title: string;
    /** Its entries, smallest first. */
    readonly entries: readonly SizeEntry[];
    /** Its box in CSS pixels, under its title. */
    readonly box: Box;
}

/** How many bands a colour is drawn in; the stylesheet's heat and diverge tokens, as the surface's (ADR-0310). */
export const bandCount = 9;

const barWidth = 12;

/** The largest marker's radius, against a line's height. */
export const maxRadius = (lineHeight: number): number => lineHeight * 0.6;

/** The gap between the plot and the keys: a marker at the plot's edge reaches past it by its radius. */
export const keyGap = (m: ChartMetricsResolved): number => m.padding + maxRadius(m.lineHeight);

const keyed = (s: ChartSeries): boolean => s.colourBy !== undefined || s.sizeBy !== undefined;

/** A number on a key, as an axis writes its ticks. */
export const keyNumber = (v: number): string => upToPlaces(v, 4);

/** The lowest and highest of a scale's values, or `undefined` when it has none. */
export function valueRange(values: readonly (ChartValue | null)[]): readonly [lo: number, hi: number] | undefined {
    const present = values.flatMap(v => (v === null ? [] : [toNumber(v)]));
    return present.length === 0 ? undefined : [least(present), greatest(present)];
}

/** The width of the column the keys take at the plot's right, or 0 when no visible series has one. */
export function keyColumnWidth(visible: readonly ChartSeries[], m: ChartMetricsResolved): number {
    const withKeys = visible.filter(keyed);
    if (withKeys.length === 0) {
        return 0;
    }
    const ends = withKeys.flatMap(s => {
        const colour = s.colourBy === undefined ? [0, 0] : colourRun(s.colourBy);
        const size = s.sizeBy === undefined ? undefined : valueRange(s.sizeBy.values);
        return [colour[0], colour[1], 0, size === undefined ? 0 : size[1]];
    });
    const label = greatest(ends.map(v => m.textWidth(keyNumber(v))));
    const titles = greatest(withKeys.flatMap(s => [s.colourBy?.title, s.sizeBy?.title]).flatMap(t => (t === undefined ? [] : [m.textWidth(t)])));
    return keyGap(m) + Math.max(titles, Math.max(barWidth, 2 * maxRadius(m.lineHeight)) + m.padding / 2 + label) + m.padding;
}

/** The run a colour's bands cover: the values' own, or alike on both sides of its centre (ADR-0310). */
export function colourRun(c: PointColour): readonly [from: number, to: number] {
    const range = valueRange(c.values);
    const [lo, hi] = range === undefined ? [c.centre === undefined ? 0 : c.centre, c.centre === undefined ? 0 : c.centre] : range;
    if (c.centre !== undefined) {
        const mid = c.centre;
        let reach = Math.max(Math.abs(hi - mid), Math.abs(lo - mid));
        if (reach === 0) {
            reach = mid === 0 ? 1 : Math.abs(mid) / 10;
        }
        return [mid - reach, mid + reach];
    }
    return hi > lo ? [lo, hi] : [lo - (lo === 0 ? 1 : Math.abs(lo) / 10), hi + (hi === 0 ? 1 : Math.abs(hi) / 10)];
}

/** A value's band: by its place in the run, or by its distance from the centre, the middle band neutral. */
export function colourBand(c: PointColour, run: readonly [number, number], value: number): number {
    const [from, to] = run;
    if (c.centre !== undefined) {
        const t = (value - c.centre) / ((to - from) / 2);
        const depth = Math.min(Math.trunc(bandCount / 2), Math.trunc(Math.floor(Math.abs(t) * bandCount / 2 + 0.5)));
        return Math.trunc(bandCount / 2) + Math.sign(t) * depth;
    }
    return Math.min(Math.max(Math.trunc(Math.floor((value - from) / (to - from) * bandCount)), 0), bandCount - 1);
}

/** A value's radius as a share of the largest: its area in proportion to the value. */
export const sizeShare = (max: number, value: number): number => (max > 0 ? Math.sqrt(value / max) : 1);

/** The keys of the visible series, stacked down the column at `x`. */
export function pointKeys(visible: readonly ChartSeries[], x: number, top: number, height: number, m: ChartMetricsResolved):
    readonly [colours: ColourKey[], sizes: SizeKey[]] {
    const colours: ColourKey[] = [];
    const sizes: SizeKey[] = [];
    const lh = m.lineHeight;
    let y = top;
    for (const s of visible.filter(keyed)) {
        if (s.colourBy !== undefined) {
            const c = s.colourBy;
            const [from, to] = colourRun(c);
            const barTop = y + lh;
            const barHeight = Math.max(4 * lh, Math.min(10 * lh, height - (barTop - top) - lh));
            const box: Box = Object.freeze({ x, y: barTop, width: barWidth, height: barHeight });
            const n = nice(from, to, 4);
            const values = steps(n.min, n.max, n.step).filter(v => v >= from - 1e-9 * (to - from) && v <= to + 1e-9 * (to - from));
            if (c.centre !== undefined && !values.includes(c.centre)) {
                values.push(c.centre);
            }
            const ticks = values.toSorted((a, b) => a - b)
                .map(v => Object.freeze({ value: v, y: box.y + box.height * (to - v) / (to - from), label: keyNumber(v) }));
            colours.push(Object.freeze({
                seriesId: s.id, title: c.title, from, to, ticks: Object.freeze(ticks), diverging: c.centre !== undefined, box, bands: bandCount,
            }));
            y = barTop + barHeight + lh;
        }
        if (s.sizeBy !== undefined) {
            // Always drawn (ADR-0320): with no value above zero, every marker is drawn whole, and the key says so.
            const z = s.sizeBy;
            const rMax = maxRadius(lh);
            const range = valueRange(z.values);
            const hi = range === undefined ? 0 : range[1];
            const values: number[] = [];
            if (hi > 0) {
                const n = nice(0, hi, 3);
                values.push(...steps(n.step, n.max, n.step).filter(v => v <= hi).slice(-3));
            }
            if (values.length === 0) {
                values.push(hi);
            }
            const entries = values.map(v => Object.freeze({ value: v, label: keyNumber(v), radius: Math.max(0.75, rMax * sizeShare(hi, v)) }));
            const boxHeight = entries.reduce((sum, e) => sum + Math.max(2 * e.radius, lh) + lh / 4, 0);
            sizes.push(Object.freeze({
                seriesId: s.id, title: z.title, entries: Object.freeze(entries), box: Object.freeze({ x, y: y + lh, width: 2 * rMax, height: boxHeight }),
            }));
            y += lh + boxHeight + lh;
        }
    }
    return [colours, sizes];
}

/** What the Notes say of points a scale has no value for. */
export function pointScaleNotes(s: ChartSeries): string[] {
    const notes: string[] = [];
    for (const scale of [s.colourBy, s.sizeBy]) {
        if (scale === undefined) {
            continue;
        }
        const missing = s.points.filter((p, i) => p.y !== null && scale.values[i] === null).length;
        if (missing > 0) {
            notes.push(`${s.name}: ${String(missing)} ${missing === 1 ? 'point has' : 'points have'} no ${scale.title}, drawn as ${missing === 1 ? 'an outline' : 'outlines'}.`);
        }
    }
    return notes;
}

/** What the definition refuses of a series' scales, or `undefined`. */
export function pointScaleRefusal(s: ChartSeries): string | undefined {
    if (!keyed(s)) {
        return undefined;
    }
    if (s.kind !== 'observation') {
        return `'${s.name}' takes a colour or a size from a column, which belongs to marks drawn one a row: an observation.`;
    }
    if (s.colourBy !== undefined) {
        const c = s.colourBy;
        if (c.values.length !== s.points.length) {
            return `'${s.name}' has ${String(s.points.length)} points and ${String(c.values.length)} values of ${c.title}: its colour takes one a point.`;
        }
        if (c.centre !== undefined && !Number.isFinite(c.centre)) {
            return `'${s.name}': the value its colours diverge from is a finite number.`;
        }
    }
    if (s.sizeBy !== undefined) {
        const z = s.sizeBy;
        if (z.values.length !== s.points.length) {
            return `'${s.name}' has ${String(s.points.length)} points and ${String(z.values.length)} values of ${z.title}: its size takes one a point.`;
        }
        for (let i = 0; i < z.values.length; i++) {
            const v = z.values[i];
            if (v !== undefined && v !== null && toNumber(v) < 0) {
                const point = s.points[i];
                const at = point?.name === undefined ? `row ${String(i + 1)}` : point.name;
                return `'${s.name}' at ${at}: ${z.title} is ${invariantText(v)}, a negative size, which no marker can be drawn at.`;
            }
        }
    }
    return undefined;
}
