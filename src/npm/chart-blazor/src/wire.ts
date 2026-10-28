// The Chart Definition and the Surface Definition as Blazor's ExChart writes them (ADR-0370 section 1): the C#
// records, member for member, in the Script Core's spelling, read back into the Script Core's own definitions, which
// check them again. Nothing here decides how a chart is drawn; it only reads what .NET wrote.
//
// Three things in a C# definition are not data. A value keeps the type it was handed in, so a decimal 3.30 is written
// "3.30" where a number would be "3.3"; a series, an axis or a surface may carry a Consumer's Format, a .NET function;
// and a point may carry a Row Key, any .NET object. A function cannot cross, so .NET writes every value the chart
// holds through it once and sends the texts that differ from the Script Core's own writing (`texts`), and the chart
// writes each value by them. .NET refuses, by name, a function the chart would ask of a value it does not hold, such
// as a tick between the data's positions, rather than send a writer that would answer some values its own way
// (spine rule 1); an Excel format code crosses as itself in its place, read by the Script Core's own reader (ADR-0420). A series
// sampled from one of the family's interpolations or fits crosses as what it was made from, and is made again here by the
// Script Core's twin of its method (ADR-0420 section 4). A Row Key crosses as the JSON value .NET chose for it, which the click hands back.

import { ChartDefinition, FittedCurve, interpolateLinear, interpolateNaturalCubicSpline, SurfaceAxis, SurfaceDefinition } from '@ex-grid/chart';
import type {
    AppearanceKeys, AxisScale, AxisValues, CandlePeriod, ChartEdge, ChartPoint, ChartSeriesOptions, ChartValue, IntervalBound, IntervalStyle,
    NamedPosition, PixelSummary, PointColour, PointDetail, PointSize, PriceBar, Segment, SeriesInterval, SeriesKind, StepSide, AxisSide,
    SeriesSampler, SurfaceNode, SurfaceTriangle, SurfaceValue, FitSpace,
} from '@ex-grid/chart';

/** A number as .NET writes one JSON cannot hold: its name. */
export type WireNumber = number | 'NaN' | 'Infinity' | '-Infinity';

/** A value that may be missing. */
export type WireValue = WireNumber | null;

/** A Row Key as .NET sends it: text, a number or a Boolean, which a click hands back as it came. */
export type WireRow = string | number | boolean;

/** Texts .NET wrote a value with, where they differ from the Script Core's own: each value and its text. */
export type WireTexts = readonly (readonly [value: WireNumber, text: string])[];

/** The horizontal axis. */
export interface WireAxis {
    readonly values: AxisValues;
    readonly title: string;
    readonly scale: AxisScale | null;
    readonly labels: readonly string[];
    readonly named: readonly (readonly [position: WireNumber, label: string])[];
    readonly daysPerYear: number;
    readonly packed: boolean;
    /** The Excel format code its positions are written by, which the Script Core reads as C# does (ADR-0420). */
    readonly formatCode: string | null;
}

/** An Interval's bounds, each `[x, lower, upper]`. */
export interface WireInterval {
    readonly meaning: string;
    readonly style: IntervalStyle;
    readonly bounds: readonly (readonly [x: WireNumber, lower: WireValue, upper: WireValue])[];
}

/**
 * What a series sampled from one of the family's own curves was made from (ADR-0420 section 4): an interpolation's
 * points, or a fit's members, from which the Script Core makes the same curve again.
 */
export type WireMadeFrom =
    | { readonly kind: 'linear' | 'naturalCubicSpline'; readonly xs: readonly WireNumber[]; readonly ys: readonly WireNumber[] }
    | {
        readonly kind: 'fit'; readonly method: string; readonly coefficients: readonly WireNumber[]; readonly rSquared: WireNumber;
        readonly count: number; readonly from: WireNumber; readonly to: WireNumber; readonly space: FitSpace;
    };

/** A series' sampler: the range it is asked over, and the curve it asks. */
export interface WireSampler {
    readonly from: WireNumber;
    readonly to: WireNumber;
    readonly madeFrom: WireMadeFrom;
}

/** One series. Its points are columns: `x` and `y` one entry a point, `names` and `rows` too where any point has one. */
export interface WireSeries {
    readonly id: string;
    readonly name: string;
    readonly kind: SeriesKind;
    readonly step: StepSide;
    readonly axis: AxisSide;
    readonly panel: string;
    /** The Excel format code its values are written by (ADR-0420); it then carries no `texts`. */
    readonly formatCode: string | null;
    readonly keys: { readonly colour: string; readonly lineStyle: string | null; readonly marker: string | null } | null;
    readonly fromZero: boolean | null;
    readonly interpolated: boolean;
    readonly segments: readonly { readonly from: WireNumber; readonly to: WireNumber; readonly method: string; readonly extrapolated: boolean }[];
    /** The curve it is sampled from, `null` for a series of points. */
    readonly sampler: WireSampler | null;
    readonly x: readonly WireNumber[];
    readonly y: readonly WireValue[];
    readonly names: readonly (string | null)[] | null;
    readonly rows: readonly (WireRow | null)[] | null;
    readonly colourBy: { readonly title: string; readonly values: readonly WireValue[]; readonly centre: number | null } | null;
    readonly sizeBy: { readonly title: string; readonly values: readonly WireValue[] } | null;
    readonly details: readonly { readonly title: string; readonly values: readonly (string | null)[] }[];
    readonly edges: readonly (readonly [from: WireRow, to: WireRow])[];
    /** A Price Range's rows, each `[x, open, high, low, close, volume]`; `null` for any other series. */
    readonly bars: readonly (readonly [x: WireNumber, open: WireValue, high: WireValue, low: WireValue, close: WireValue, volume: WireValue])[] | null;
    readonly mergeTo: CandlePeriod | null;
    readonly interval: WireInterval | null;
    readonly xInterval: WireInterval | null;
    /** Each thinned pixel, `[from, to, min, max, count]`. */
    readonly thinned: readonly (readonly [from: WireNumber, to: WireNumber, min: WireNumber, max: WireNumber, count: number])[];
    /** How its values are written where that is not the Script Core's own writing, or `null` for none. */
    readonly texts: WireTexts | null;
}

/** A Chart Definition. */
export interface WireDefinition {
    readonly axis: WireAxis;
    readonly series: readonly WireSeries[];
    readonly notes: readonly string[];
}

/** One axis of a surface's grid. */
export interface WireSurfaceAxis {
    readonly title: string;
    readonly values: readonly SurfaceValue[];
    readonly scale: AxisScale;
    /** How its values are written. */
    readonly writing: WireSurfaceWriting;
    /** The number code its values and ticks are written by (ADR-0420); it then carries no texts. */
    readonly formatCode: string | null;
}

/** How a surface axis writes its values: `null` as the Script Core does, `plain` as a sampled surface's axis does, or
 * by the text .NET wrote each of its values with, where any differs from the Script Core's own, a tick between them
 * written plainly, as C# writes a tick on an axis with no Format of the Consumer's. */
export type WireSurfaceWriting = null | 'plain' | readonly (readonly [value: SurfaceValue, text: string])[];

/** What every form of surface says of its values. */
interface WireSurfaceScale {
    readonly zTitle: string;
    readonly zScale: AxisScale;
    readonly centre: number | null;
    /** How its values are written where that is not the Script Core's own writing, or `null` for none. */
    readonly zTexts: WireTexts | null;
    /** The code its values are written by (ADR-0420); it then carries no texts. */
    readonly zFormatCode: string | null;
    readonly notes: readonly string[];
}

/** A surface of a grid, a Consumer's function sampled into one included. */
export interface WireSurfaceGrid extends WireSurfaceScale {
    readonly form: 'grid';
    readonly x: WireSurfaceAxis;
    readonly y: WireSurfaceAxis;
    readonly z: readonly (readonly WireValue[])[];
    readonly method: string | null;
    readonly interpolated: readonly (readonly boolean[])[] | null;
}

/** A surface on the Consumer's own triangles. */
export interface WireSurfaceTriangles extends WireSurfaceScale {
    readonly form: 'triangles';
    readonly xTitle: string;
    readonly yTitle: string;
    readonly xScale: AxisScale;
    readonly yScale: AxisScale;
    /** How each axis writes its values: as the Script Core does, or plainly; its ticks lie between the nodes, so .NET
     * sends no texts for them. */
    readonly xWriting: null | 'plain';
    readonly yWriting: null | 'plain';
    /** Each axis' number code (ADR-0420). */
    readonly xFormatCode: string | null;
    readonly yFormatCode: string | null;
    /** Each node, `[x, y, z]`. */
    readonly nodes: readonly (readonly [x: WireNumber, y: WireNumber, z: WireValue])[];
    /** Each triangle, `[a, b, c]`. */
    readonly triangles: readonly (readonly [a: number, b: number, c: number])[];
}

/** A Surface Definition. */
export type WireSurface = WireSurfaceGrid | WireSurfaceTriangles;

/** A number .NET wrote, its name read back for one JSON cannot hold. */
export function wireNumber(value: WireNumber): number {
    switch (value) {
        case 'NaN':
            return NaN;
        case 'Infinity':
            return Infinity;
        case '-Infinity':
            return -Infinity;
        default:
            if (typeof value !== 'number') throw new TypeError(`Internal: .NET sent ${JSON.stringify(value)} for a number.`);
            return value;
    }
}

function wireValue(value: WireValue): ChartValue | null {
    return value === null ? null : wireNumber(value);
}

// The Script Core's own writing of a number, as an axis with no format of its own writes a value and a tick: what
// .NET writes too where the Consumer gave no Format, and the values it computes. Read through the public surface.
const own = new SurfaceAxis({ title: '', values: [] });

/** A writer of values by the texts .NET sent, or none where it sent none. A value it sent no text for is written as
 * the Script Core writes it, as .NET wrote it: .NET sends texts that differ, and refuses a Format the chart would ask
 * of a value it does not hold. */
function writerOf(texts: WireTexts | null): ((value: ChartValue) => string) | undefined {
    if (texts === null) return undefined;
    const held = new Map<number, string>(texts.map(([value, text]) => [wireNumber(value), text]));
    return value => held.get(Number(value)) ?? own.writeValue(Number(value));
}

/** A surface axis' writer: none, the plain writing a sampled surface's axis takes (C#'s `SurfaceMesh.Plain`, which
 * writes a tick as it writes a value), or the texts of its values, with a tick between them written plainly. */
function surfaceValueWriter(writing: WireSurfaceWriting): ((value: SurfaceValue) => string) | undefined {
    if (writing === null) return undefined;
    if (writing === 'plain') return plain;
    const held = new Map<SurfaceValue, string>(writing.map(([value, text]) => [value, text]));
    return value => held.get(value) ?? plain(value);
}

function plain(value: SurfaceValue): string {
    return typeof value === 'number' ? own.writeTick(value) : value;
}

/** The entry of a column at a point, its length checked against the points'. */
function cell<T>(column: readonly T[], i: number): T {
    const entry = column[i];
    if (entry === undefined) throw new Error(`Internal: .NET sent a column without an entry at ${String(i)}.`);
    return entry;
}

function points(s: WireSeries): ChartPoint[] {
    if (s.y.length !== s.x.length || (s.names !== null && s.names.length !== s.x.length) || (s.rows !== null && s.rows.length !== s.x.length)) {
        throw new Error(`Internal: .NET sent '${s.name}' with columns of different lengths.`);
    }
    return s.x.map((x, i) => {
        const name = s.names === null ? null : cell(s.names, i);
        const row = s.rows === null ? null : cell(s.rows, i);
        return {
            x: wireNumber(x),
            y: wireValue(cell(s.y, i)),
            ...(name === null ? {} : { name }),
            ...(row === null ? {} : { row }),
        };
    });
}

function interval(wire: WireInterval | null): SeriesInterval | undefined {
    if (wire === null) return undefined;
    return {
        meaning: wire.meaning,
        style: wire.style,
        bounds: wire.bounds.map(([x, lower, upper]): IntervalBound => ({ x: wireNumber(x), lower: wireValue(lower), upper: wireValue(upper) })),
    };
}

function keys(wire: WireSeries['keys']): AppearanceKeys | undefined {
    if (wire === null) return undefined;
    return {
        colour: wire.colour,
        ...(wire.lineStyle === null ? {} : { lineStyle: wire.lineStyle }),
        ...(wire.marker === null ? {} : { marker: wire.marker }),
    };
}

const twins: ReadonlyMap<string, (xs: readonly number[], ys: readonly number[]) => { valueAt(x: number): number | null }> = new Map([
    ['linear', interpolateLinear],
    ['naturalCubicSpline', interpolateNaturalCubicSpline],
]);

// The curve made again from what .NET's was made from, by the Script Core's twin of its method.
function curve(made: WireMadeFrom): (x: number) => number | null {
    if (made.kind === 'fit') {
        const fitted = new FittedCurve({
            method: made.method, coefficients: made.coefficients.map(wireNumber), rSquared: wireNumber(made.rSquared), count: made.count,
            from: wireNumber(made.from), to: wireNumber(made.to), space: made.space,
        });
        return x => fitted.valueAt(x);
    }
    // A kind this side does not know is a writer's defect, said by name, never drawn by another method.
    const make = twins.get(made.kind);
    if (make === undefined) throw new Error(`Internal: .NET sent a curve made by "${made.kind}", which has no twin here.`);
    const interpolant = make(made.xs.map(wireNumber), made.ys.map(wireNumber));
    return x => interpolant.valueAt(x);
}

function sampler(wire: WireSampler | null): SeriesSampler | undefined {
    if (wire === null) return undefined;
    return { from: wireNumber(wire.from), to: wireNumber(wire.to), valueAt: curve(wire.madeFrom) };
}

function series(s: WireSeries): ChartSeriesOptions {
    const format = writerOf(s.texts);
    const colourBy: PointColour | undefined = s.colourBy === null ? undefined : {
        title: s.colourBy.title,
        values: s.colourBy.values.map(wireValue),
        ...(s.colourBy.centre === null ? {} : { centre: s.colourBy.centre }),
    };
    const sizeBy: PointSize | undefined = s.sizeBy === null ? undefined : { title: s.sizeBy.title, values: s.sizeBy.values.map(wireValue) };
    const appearance = keys(s.keys);
    const xInterval = interval(s.xInterval);
    const yInterval = interval(s.interval);
    const sampled = sampler(s.sampler);
    return {
        id: s.id,
        name: s.name,
        kind: s.kind,
        step: s.step,
        axis: s.axis,
        panel: s.panel,
        ...(s.formatCode === null ? {} : { formatCode: s.formatCode }),
        ...(appearance === undefined ? {} : { keys: appearance }),
        ...(s.fromZero === null ? {} : { fromZero: s.fromZero }),
        interpolated: s.interpolated,
        segments: s.segments.map((g): Segment => ({ from: wireNumber(g.from), to: wireNumber(g.to), method: g.method, extrapolated: g.extrapolated })),
        ...(sampled === undefined ? {} : { sampler: sampled }),
        // A Price Range's points are its closes, which the definition makes from its rows, as C#'s does.
        ...(s.bars === null
            ? { points: points(s) }
            : {
                bars: s.bars.map(([x, open, high, low, close, volume]): PriceBar => ({
                    x: wireNumber(x), open: wireValue(open), high: wireValue(high), low: wireValue(low), close: wireValue(close), volume: wireValue(volume),
                })),
            }),
        ...(colourBy === undefined ? {} : { colourBy }),
        ...(sizeBy === undefined ? {} : { sizeBy }),
        details: s.details.map((d): PointDetail => ({ title: d.title, values: d.values })),
        edges: s.edges.map(([from, to]): ChartEdge => ({ from, to })),
        ...(s.mergeTo === null ? {} : { mergeTo: s.mergeTo }),
        ...(yInterval === undefined ? {} : { interval: yInterval }),
        ...(xInterval === undefined ? {} : { xInterval }),
        thinned: s.thinned.map(([from, to, min, max, count]): PixelSummary => ({
            from: wireNumber(from), to: wireNumber(to), min: wireNumber(min), max: wireNumber(max), count,
        })),
        ...(format === undefined ? {} : { format }),
    };
}

/** The Chart Definition .NET wrote, made again in the Script Core, which checks it as C# did. */
export function readDefinition(wire: WireDefinition): ChartDefinition {
    const named = wire.axis.named.map(([position, label]): NamedPosition => [wireNumber(position), label]);
    return new ChartDefinition({
        axis: {
            values: wire.axis.values,
            title: wire.axis.title,
            ...(wire.axis.scale === null ? {} : { scale: wire.axis.scale }),
            labels: wire.axis.labels,
            named,
            daysPerYear: wire.axis.daysPerYear,
            packed: wire.axis.packed,
            ...(wire.axis.formatCode === null ? {} : { formatCode: wire.axis.formatCode }),
        },
        series: wire.series.map(series),
        notes: wire.notes,
    });
}

/** The Surface Definition .NET wrote, made again in the Script Core, which checks it as C# did. */
export function readSurface(wire: WireSurface): SurfaceDefinition {
    const zFormat = writerOf(wire.zTexts);
    const zFormatCode = wire.zFormatCode === null ? {} : { zFormatCode: wire.zFormatCode };
    const scale = {
        ...(wire.zScale === 'proportional' ? {} : { zScale: wire.zScale }),
        ...(wire.centre === null ? {} : { centre: wire.centre }),
    };
    switch (wire.form) {
        case 'grid': {
            const axis = (a: WireSurfaceAxis) => {
                const format = surfaceValueWriter(a.writing);
                return {
                    title: a.title, values: a.values, scale: a.scale, ...(format === undefined ? {} : { format }),
                    ...(a.formatCode === null ? {} : { formatCode: a.formatCode }),
                };
            };
            return SurfaceDefinition.grid({
                x: axis(wire.x),
                y: axis(wire.y),
                z: wire.z.map(row => row.map(wireValue)),
                zTitle: wire.zTitle,
                ...(zFormat === undefined ? {} : { zFormat }),
                ...zFormatCode,
                ...(wire.method === null ? {} : { method: wire.method }),
                ...(wire.interpolated === null ? {} : { interpolated: wire.interpolated }),
                notes: wire.notes,
                ...scale,
            });
        }
        case 'triangles': {
            const xFormat = surfaceValueWriter(wire.xWriting);
            const yFormat = surfaceValueWriter(wire.yWriting);
            return SurfaceDefinition.fromTriangles({
                nodes: wire.nodes.map(([x, y, z]): SurfaceNode => ({ x: wireNumber(x), y: wireNumber(y), z: wireValue(z) })),
                triangles: wire.triangles.map(([a, b, c]): SurfaceTriangle => ({ a, b, c })),
                xTitle: wire.xTitle,
                yTitle: wire.yTitle,
                zTitle: wire.zTitle,
                ...(zFormat === undefined ? {} : { zFormat }),
                xScale: wire.xScale,
                yScale: wire.yScale,
                ...(xFormat === undefined ? {} : { xFormat }),
                ...(yFormat === undefined ? {} : { yFormat }),
                ...(wire.xFormatCode === null ? {} : { xFormatCode: wire.xFormatCode }),
                ...(wire.yFormatCode === null ? {} : { yFormatCode: wire.yFormatCode }),
                ...zFormatCode,
                notes: wire.notes,
                ...scale,
            });
        }
        default:
            throw new TypeError(`Internal: .NET sent a surface of the form ${JSON.stringify((wire as { readonly form: unknown }).form)}.`);
    }
}
