// A 3D surface in the data's terms (ADR-0274): a grid of values over two axes, a Consumer's sampled
// function, or a Consumer's own triangle mesh (ADR-0311), checked when it is made, and its slices as 2D
// charts. The port of the C# engine's `SurfaceDefinition`, held to it by the shared cases under
// tests/cases/chart/ (ADR-0370).
import { ChartDefinition } from './definition';
import { roundTrip, tickText } from './invariant-text';
import type { AxisScale, ChartAxisOptions, ChartPoint, ChartSeriesOptions, ChartValue } from './model';
import { elementAt } from './must';
import { greatest, least } from './numbers';
import { ChartRefusedError, invariantText, toNumber } from './values';

/** A value of a surface's axis: a strike, a delta or a tenor, as the column holds it. */
export type SurfaceValue = string | number;

/** One axis of a surface as a Consumer declares it (ADR-0274, ADR-0312). */
export interface SurfaceAxisOptions {
    /** The axis' title. */
    readonly title: string;
    /** The values, one per grid line, in order. */
    readonly values: readonly SurfaceValue[];
    /** Writes a value as the column it came from writes it; absent writes it invariantly. */
    readonly format?: (value: SurfaceValue) => string;
    /** How the axis places its values; `categories`, evenly spaced, by default (ADR-0312). */
    readonly scale?: AxisScale;
}

/**
 * One axis of a 3D surface (ADR-0274): its title and its values in order, each written through its
 * format. A grid's lines are evenly spaced, one per value, unless its scale places them by value (ADR-0312).
 */
export class SurfaceAxis {
    readonly #title: string;
    readonly #values: readonly SurfaceValue[];
    readonly #format: ((value: SurfaceValue) => string) | undefined;
    readonly #scale: AxisScale;

    /** An axis of the given values, in the order the grid holds them. */
    constructor(options: SurfaceAxisOptions) {
        const { scale = 'categories' } = options;
        this.#title = options.title;
        this.#values = Object.freeze([...options.values]);
        this.#format = options.format;
        this.#scale = scale;
    }

    /** The axis' title. */
    get title(): string {
        return this.#title;
    }

    /** The values, one per grid line, in order. */
    get values(): readonly SurfaceValue[] {
        return this.#values;
    }

    /** The Consumer's writer of a value, if any. */
    get format(): ((value: SurfaceValue) => string) | undefined {
        return this.#format;
    }

    /** How the axis places its values (ADR-0312). */
    get scale(): AxisScale {
        return this.#scale;
    }

    /** Writes the value at `index` through the format. */
    write(index: number): string {
        return this.writeValue(elementAt(this.#values, index));
    }

    /** Writes `value` through the format, or invariantly. */
    writeValue(value: SurfaceValue): string {
        if (this.#format !== undefined) {
            return this.#format(value);
        }
        return typeof value === 'number' ? roundTrip(value) : value;
    }

    /**
     * Writes a position that need not be one of the values, such as a tick: through the format when the
     * values are numbers, invariantly otherwise.
     */
    writeTick(position: number): string {
        const [first] = this.#values;
        if (this.#format !== undefined && typeof first === 'number') {
            return this.#format(position);
        }
        return tickText(position);
    }

    /** The index of `value` among the values, or -1. */
    indexOf(value: SurfaceValue): number {
        return this.#values.indexOf(value);
    }

    /**
     * The value at `index` as a number; a value that is not one is a defect of the caller, which checked it.
     *
     * @internal
     */
    number(index: number): number {
        const value = elementAt(this.#values, index);
        if (typeof value !== 'number') {
            throw new Error(`Internal: ${value} is not a number.`);
        }
        return value;
    }
}

/** Which of a surface's two axes a slice holds fixed: `x` runs along y, `y` along x. */
export type SurfaceSliceAxis = 'x' | 'y';

/** A slice of a surface, named in the data's terms (ADR-0272, ADR-0274): the axis held fixed and its value there. */
export interface SurfaceSlice {
    /** The axis held fixed. */
    readonly axis: SurfaceSliceAxis;
    /** Its value, as the axis holds it. */
    readonly value: SurfaceValue;
}

/** How a surface is seen, in its own terms (ADR-0272): angles in degrees, the zoom, and the centre's offset as fractions. Never pixels. */
export interface SurfaceView {
    /** The turn about the vertical, in degrees. */
    readonly yaw: number;
    /** The tilt from level, in degrees, -90 to 90. */
    readonly pitch: number;
    /** The zoom, 1 fitting the surface to its box. */
    readonly zoom: number;
    /** The centre's horizontal offset, as a fraction of the width. */
    readonly panX: number;
    /** The centre's vertical offset, as a fraction of the height. */
    readonly panY: number;
}

/** The view a surface is first drawn at. */
export const defaultSurfaceView: SurfaceView = Object.freeze({ yaw: -35, pitch: 28, zoom: 1, panX: 0, panY: 0 });

/** A node of a Consumer's triangle mesh (ADR-0311): its place in the plane and its value, or `null`, a hole. */
export interface SurfaceNode {
    /** Its x position. */
    readonly x: number;
    /** Its y position. */
    readonly y: number;
    /** Its value, or `null`: no triangle at it is drawn. */
    readonly z: ChartValue | null;
}

/** A triangle of a Consumer's mesh (ADR-0311): the indices of its three nodes. */
export interface SurfaceTriangle {
    /** The first corner's index. */
    readonly a: number;
    /** The second corner's index. */
    readonly b: number;
    /** The third corner's index. */
    readonly c: number;
}

/** How the values are scaled and coloured, which every form of surface may say (ADR-0310, ADR-0312). */
export interface SurfaceScaleOptions {
    /** The vertical scale: `proportional`, the default, or `logarithmic`; never `categories`. */
    readonly zScale?: AxisScale;
    /** The value the colours diverge from, such as 0 for a P&L (ADR-0310); absent colours low to high. */
    readonly centre?: number;
}

/** A surface of a grid (ADR-0274): `z` holds one row per y value, each with one value per x value. */
export interface SurfaceGridOptions extends SurfaceScaleOptions {
    /** The x axis: the table's columns. */
    readonly x: SurfaceAxisOptions;
    /** The y axis: the table's rows. */
    readonly y: SurfaceAxisOptions;
    /** The values, one row per y value, `null` at a hole. */
    readonly z: readonly (readonly (ChartValue | null)[])[];
    /** What the values are; empty by default. */
    readonly zTitle?: string;
    /** Writes a value as its cell writes it. */
    readonly zFormat?: (value: ChartValue) => string;
    /** The method the interpolated values were made with, as the tooltip and the Notes say it (ADR-0257). */
    readonly method?: string;
    /** Which values are interpolated, in the table's shape; absent with a method marks every value. */
    readonly interpolated?: readonly (readonly boolean[])[];
    /** The Consumer's Notes, shown under the surface (ADR-0257). */
    readonly notes?: readonly string[];
}

/** A point of a surface handed in as points, at an x value and a y value. */
export interface SurfacePoint {
    /** Its x value. */
    readonly x: SurfaceValue;
    /** Its y value. */
    readonly y: SurfaceValue;
    /** Its value. */
    readonly z: ChartValue;
}

/** A surface from points on a grid (ADR-0274). */
export interface SurfacePointsOptions extends SurfaceScaleOptions {
    /** The points. */
    readonly points: readonly SurfacePoint[];
    /** The x axis' title. */
    readonly xTitle: string;
    /** The y axis' title. */
    readonly yTitle: string;
    /** What the values are. */
    readonly zTitle?: string;
    /** Writes an x value. */
    readonly xFormat?: (value: SurfaceValue) => string;
    /** Writes a y value. */
    readonly yFormat?: (value: SurfaceValue) => string;
    /** Writes a value. */
    readonly zFormat?: (value: ChartValue) => string;
}

/** A surface on the Consumer's own triangle mesh (ADR-0311). */
export interface SurfaceTrianglesOptions extends SurfaceScaleOptions {
    /** The nodes. */
    readonly nodes: readonly SurfaceNode[];
    /** The triangles. */
    readonly triangles: readonly SurfaceTriangle[];
    /** The x axis' title. */
    readonly xTitle: string;
    /** The y axis' title. */
    readonly yTitle: string;
    /** What the values are. */
    readonly zTitle?: string;
    /** Writes an x position. */
    readonly xFormat?: (value: SurfaceValue) => string;
    /** Writes a y position. */
    readonly yFormat?: (value: SurfaceValue) => string;
    /** Writes a value. */
    readonly zFormat?: (value: ChartValue) => string;
    /** How x positions are placed: `proportional`, the default, or `logarithmic`. */
    readonly xScale?: AxisScale;
    /** How y positions are placed. */
    readonly yScale?: AxisScale;
    /** The Consumer's Notes. */
    readonly notes?: readonly string[];
}

/** A surface handed in as the Consumer's function z(x, y), which names its method (ADR-0257). */
export interface SurfaceSampledOptions extends SurfaceScaleOptions {
    /** The method the function interpolates with, as the tooltip and the Notes say it. */
    readonly method: string;
    /** The first x sampled. */
    readonly xFrom: number;
    /** The last. */
    readonly xTo: number;
    /** The first y sampled. */
    readonly yFrom: number;
    /** The last. */
    readonly yTo: number;
    /** The Consumer's surface: the value at (x, y), or `null` where it has none, outside its points. */
    readonly z: (x: number, y: number) => ChartValue | null;
    /** The x axis' title. */
    readonly xTitle: string;
    /** The y axis' title. */
    readonly yTitle: string;
    /** What the values are. */
    readonly zTitle?: string;
    /** How many values an axis is sampled at; 25 by default. */
    readonly samples?: number;
    /** Writes an x value. */
    readonly xFormat?: (value: SurfaceValue) => string;
    /** Writes a y value. */
    readonly yFormat?: (value: SurfaceValue) => string;
    /** Writes a value. */
    readonly zFormat?: (value: ChartValue) => string;
    /** How x values are sampled and placed; `categories` by default. */
    readonly xScale?: AxisScale;
    /** How y values are sampled and placed. */
    readonly yScale?: AxisScale;
}

/** One point of a cut through a mesh: its position along the other axis and its value there. */
interface CutPoint {
    readonly along: number;
    readonly z: ChartValue;
}

/** The method a slice through a Consumer's triangles reads between its nodes with (ADR-0311). */
export const trianglesMethod = 'linear on the Consumer\'s triangles (P1)';

const scatteredReason = 'scattered points form no grid, and a surface through them is an interpolation, which ExChart does not choose. Interpolate them, with a method of ExChart.Engine.Interpolation or your own, and hand the surface in as a function that names its method, or hand in your own triangles (ADR-0257, ADR-0274, ADR-0311).';

const nameOr = (title: string, fallback: string): string => (title.trim() === '' ? fallback : title);

interface SurfaceParts {
    readonly x: SurfaceAxis;
    readonly y: SurfaceAxis;
    readonly z: readonly (readonly (ChartValue | null)[])[];
    readonly zTitle: string;
    readonly zFormat: ((value: ChartValue) => string) | undefined;
    readonly method: string | undefined;
    readonly interpolated: readonly (readonly boolean[])[] | undefined;
    readonly notes: readonly string[];
    readonly nodes: readonly SurfaceNode[] | undefined;
    readonly triangles: readonly SurfaceTriangle[] | undefined;
    readonly zScale: AxisScale;
    readonly centre: number | undefined;
}

/**
 * A 3D surface in the data's terms (ADR-0274): x values × y values × a table of z, one row per y value
 * and one column per x value. A missing value is a hole, drawn as one: never 0 and never filled from its
 * neighbours. Scattered points are refused by name (`fromPoints`). Interpolation is the Consumer's
 * (ADR-0257): a surface may be handed in as a function, which ExChart samples (`sampled`), or as a grid
 * whose interpolated values are marked. A Consumer's own triangle mesh is drawn as its triangles
 * (`fromTriangles`, ADR-0311).
 */
export class SurfaceDefinition {
    readonly #parts: SurfaceParts;

    private constructor(parts: SurfaceParts) {
        this.#parts = parts;
    }

    /** A surface of the given grid; refused with a `ChartRefusedError` when it cannot be drawn as asked. */
    static grid(options: SurfaceGridOptions): SurfaceDefinition {
        const { zTitle = '', notes = [] } = options;
        const method = options.method === undefined || options.method.trim() === '' ? undefined : options.method;
        const x = new SurfaceAxis(options.x);
        const y = new SurfaceAxis(options.y);
        const z = Object.freeze(options.z.map(row => Object.freeze([...row])));
        const interpolated = options.interpolated === undefined ? undefined : Object.freeze(options.interpolated.map(row => Object.freeze([...row])));
        validateGrid(x, y, z, method, interpolated);
        return new SurfaceDefinition(Object.freeze({
            x, y, z, zTitle, zFormat: options.zFormat, method, interpolated, notes: Object.freeze([...notes]),
            nodes: undefined, triangles: undefined, ...scaled(options),
        }));
    }

    /**
     * A surface from points, each at an x value and a y value. The points must lie on a grid: every x value
     * they name must hold a value at two y values at least, and every y value at two x values, or the point
     * stands alone and could join a surface only by interpolation (ADR-0257, ADR-0274). Where the grid has no
     * point, it has a hole.
     */
    static fromPoints(options: SurfacePointsOptions): SurfaceDefinition {
        const { points, xTitle, yTitle, zTitle = '' } = options;
        const xs = ordered(points.map(p => p.x));
        const ys = ordered(points.map(p => p.y));
        const xAxis = new SurfaceAxis({ title: xTitle, values: xs, ...(options.xFormat === undefined ? {} : { format: options.xFormat }) });
        const yAxis = new SurfaceAxis({ title: yTitle, values: ys, ...(options.yFormat === undefined ? {} : { format: options.yFormat }) });
        const count = (key: (p: SurfacePoint) => SurfaceValue): Map<SurfaceValue, number> => {
            const counts = new Map<SurfaceValue, number>();
            for (const p of points) {
                const k = key(p);
                const seen = counts.get(k);
                counts.set(k, seen === undefined ? 1 : seen + 1);
            }
            return counts;
        };
        const xCounts = count(p => p.x);
        const yCounts = count(p => p.y);
        for (const x of xs) {
            if (elementOf(xCounts, x) < 2) {
                throw new ChartRefusedError(`${nameOr(xTitle, 'x')} ${xAxis.writeValue(x)} has a value at only one ${nameOr(yTitle, 'y')}: ${scatteredReason}`);
            }
        }
        for (const y of ys) {
            if (elementOf(yCounts, y) < 2) {
                throw new ChartRefusedError(`${nameOr(yTitle, 'y')} ${yAxis.writeValue(y)} has a value at only one ${nameOr(xTitle, 'x')}: ${scatteredReason}`);
            }
        }
        const xIndex = new Map(xs.map((v, i) => [v, i]));
        const yIndex = new Map(ys.map((v, j) => [v, j]));
        const z: (ChartValue | null)[][] = ys.map(() => xs.map(() => null));
        for (const p of points) {
            const row = elementAt(z, elementOf(yIndex, p.y));
            const i = elementOf(xIndex, p.x);
            if (elementAt(row, i) !== null) {
                throw new ChartRefusedError(`Two points lie at ${nameOr(xTitle, 'x')} ${xAxis.writeValue(p.x)} and ${nameOr(yTitle, 'y')} ${yAxis.writeValue(p.y)}; a grid point holds one value.`);
            }
            row[i] = p.z;
        }
        return SurfaceDefinition.grid({
            x: axisOptions(xAxis), y: axisOptions(yAxis), z, zTitle,
            ...(options.zFormat === undefined ? {} : { zFormat: options.zFormat }),
            ...scaleOptions(options),
        });
    }

    /**
     * A surface drawn on the Consumer's own triangle mesh (ADR-0311), each triangle flat through its three
     * corners, never resampled onto a grid. A slice cuts the triangles exactly, reading between nodes
     * linearly on them (`trianglesMethod`), which it says.
     */
    static fromTriangles(options: SurfaceTrianglesOptions): SurfaceDefinition {
        const { nodes, triangles, xTitle, yTitle, zTitle = '', xScale = 'proportional', yScale = 'proportional', notes = [] } = options;
        if (xScale === 'categories' || yScale === 'categories') {
            throw new ChartRefusedError('A mesh\'s nodes are placed by their position: its axes are proportional or logarithmic, never categories (ADR-0311).');
        }
        for (const n of nodes) {
            if (!Number.isFinite(n.x) || !Number.isFinite(n.y)) {
                throw new ChartRefusedError(`A node lies at (${roundTrip(n.x)}, ${roundTrip(n.y)}), which is no place in the plane.`);
            }
        }
        const x = new SurfaceAxis({
            title: xTitle, values: ordered(nodes.map(n => n.x)), scale: xScale, ...(options.xFormat === undefined ? {} : { format: options.xFormat }),
        });
        const y = new SurfaceAxis({
            title: yTitle, values: ordered(nodes.map(n => n.y)), scale: yScale, ...(options.yFormat === undefined ? {} : { format: options.yFormat }),
        });
        const frozenNodes = Object.freeze(nodes.map(n => Object.freeze({ x: n.x, y: n.y, z: n.z })));
        const frozenTriangles = Object.freeze(triangles.map(t => Object.freeze({ a: t.a, b: t.b, c: t.c })));
        validateMesh(x, y, frozenNodes, frozenTriangles);
        return new SurfaceDefinition(Object.freeze({
            x, y, z: Object.freeze([]), zTitle, zFormat: options.zFormat, method: undefined, interpolated: undefined, notes: Object.freeze([...notes]),
            nodes: frozenNodes, triangles: frozenTriangles, ...scaled(options),
        }));
    }

    /**
     * A surface handed in as a function z(x, y), the Consumer's interpolation, which names its method
     * (ADR-0257). ExChart samples it on a grid of `samples` values an axis across the rectangle — evenly in
     * the axis' own scale, so geometrically on a logarithmic one (ADR-0312) — and draws a hole where it
     * returns `null`. Every value is marked interpolated.
     */
    static sampled(options: SurfaceSampledOptions): SurfaceDefinition {
        const { method, xFrom, xTo, yFrom, yTo, xTitle, yTitle, zTitle = '', samples = 25, xScale = 'categories', yScale = 'categories' } = options;
        if (method.trim() === '') {
            throw new ChartRefusedError('A surface handed in as a function names its method, so the chart can say how its values were made (ADR-0257).');
        }
        if (!(xFrom < xTo) || !(yFrom < yTo)) {
            throw new ChartRefusedError('A surface handed in as a function is sampled over a rectangle that is not empty.');
        }
        if (!Number.isInteger(samples) || samples < 2) {
            throw new RangeError(`${String(samples)} samples: a surface is sampled at two values an axis at least.`);
        }
        if ((xScale === 'logarithmic' && !(xFrom > 0)) || (yScale === 'logarithmic' && !(yFrom > 0))) {
            throw new ChartRefusedError('A surface sampled on a logarithmic axis is sampled from above zero, which is where a logarithmic axis begins.');
        }
        const at = (from: number, to: number, scale: AxisScale, k: number): number => (k === samples - 1 ? to
            : scale === 'logarithmic' ? from * Math.pow(to / from, k / (samples - 1))
                : from + (to - from) * k / (samples - 1));
        const xs = Array.from({ length: samples }, (_, k) => at(xFrom, xTo, xScale, k));
        const ys = Array.from({ length: samples }, (_, k) => at(yFrom, yTo, yScale, k));
        const z = ys.map(v => xs.map(u => options.z(u, v)));
        return SurfaceDefinition.grid({
            x: { title: xTitle, values: xs, format: options.xFormat === undefined ? writeNumber : options.xFormat, scale: xScale },
            y: { title: yTitle, values: ys, format: options.yFormat === undefined ? writeNumber : options.yFormat, scale: yScale },
            z, zTitle, method,
            ...(options.zFormat === undefined ? {} : { zFormat: options.zFormat }),
            ...scaleOptions(options),
        });
    }

    /** The x axis: the table's columns, or a mesh's x positions in order. */
    get x(): SurfaceAxis {
        return this.#parts.x;
    }

    /** The y axis: the table's rows, or a mesh's y positions in order. */
    get y(): SurfaceAxis {
        return this.#parts.y;
    }

    /** A grid's values, one row per y value, `null` at a hole; empty for a mesh. */
    get z(): readonly (readonly (ChartValue | null)[])[] {
        return this.#parts.z;
    }

    /** What the values are, such as "Volatility". */
    get zTitle(): string {
        return this.#parts.zTitle;
    }

    /** The Consumer's writer of a value, if any. */
    get zFormat(): ((value: ChartValue) => string) | undefined {
        return this.#parts.zFormat;
    }

    /** The method the interpolated values were made with, if the surface claims one. */
    get method(): string | undefined {
        return this.#parts.method;
    }

    /** Which values are interpolated, in the table's shape; absent with a method marks every value. */
    get interpolated(): readonly (readonly boolean[])[] | undefined {
        return this.#parts.interpolated;
    }

    /** The Consumer's Notes (ADR-0257). */
    get notes(): readonly string[] {
        return this.#parts.notes;
    }

    /** A mesh's nodes; absent for a grid (ADR-0311). */
    get nodes(): readonly SurfaceNode[] | undefined {
        return this.#parts.nodes;
    }

    /** A mesh's triangles; absent for a grid (ADR-0311). */
    get triangles(): readonly SurfaceTriangle[] | undefined {
        return this.#parts.triangles;
    }

    /** Whether the surface is a Consumer's triangle mesh rather than a grid (ADR-0311). */
    get isMesh(): boolean {
        return this.#parts.triangles !== undefined;
    }

    /** The vertical scale asked for (ADR-0312). */
    get zScale(): AxisScale {
        return this.#parts.zScale;
    }

    /** The value the colours diverge from, if any (ADR-0310). */
    get centre(): number | undefined {
        return this.#parts.centre;
    }

    /** The vertical scale as drawn: the one asked for, unless a value or the centre cannot lie on it. */
    get effectiveZScale(): AxisScale {
        return this.#zScaleNote() === undefined ? this.#parts.zScale : 'proportional';
    }

    /** The lines shown under the surface: the Consumer's Notes, the method (ADR-0257), and why a logarithmic scale stays proportional (ADR-0312). */
    get allNotes(): readonly string[] {
        const lines = [...this.#parts.notes];
        if (this.#parts.method !== undefined) {
            lines.push(`Interpolated by the Consumer: ${this.#parts.method}.`);
        }
        const note = this.#zScaleNote();
        if (note !== undefined) {
            lines.push(note);
        }
        return Object.freeze(lines);
    }

    /** How many points the surface holds: a grid's points, row by row, or a mesh's nodes. */
    get nodeCount(): number {
        const nodes = this.#parts.nodes;
        return nodes === undefined ? this.#parts.x.values.length * this.#parts.y.values.length : nodes.length;
    }

    /** Whether a grid's value at x index `x` and y index `y` is interpolated; a mesh has no grid indices, and is refused. */
    isInterpolated(x: number, y: number): boolean {
        this.#gridOnly();
        const marks = this.#parts.interpolated;
        return this.#parts.method !== undefined && (marks === undefined || elementAt(elementAt(marks, y), x));
    }

    /** A grid's value at x index `x` and y index `y`, or `null` at a hole; a mesh has no grid indices, and is refused. */
    at(x: number, y: number): ChartValue | null {
        this.#gridOnly();
        return elementAt(elementAt(this.#parts.z, y), x);
    }

    /** The point at `index`, counted as `nodeCount` counts: its x and y as the axes hold them, and its value, or `null`. */
    node(index: number): readonly [x: SurfaceValue, y: SurfaceValue, z: ChartValue | null] {
        const nodes = this.#parts.nodes;
        if (nodes !== undefined) {
            const n = elementAt(nodes, index);
            return [n.x, n.y, n.z];
        }
        const count = this.#parts.x.values.length;
        const i = index % count;
        const j = Math.trunc(index / count);
        return [elementAt(this.#parts.x.values, i), elementAt(this.#parts.y.values, j), elementAt(elementAt(this.#parts.z, j), i)];
    }

    /** Whether the point at `index` is interpolated; a mesh's nodes are its values. */
    isInterpolatedNode(index: number): boolean {
        const count = this.#parts.x.values.length;
        return this.#parts.nodes === undefined && this.isInterpolated(index % count, Math.trunc(index / count));
    }

    /** Writes a value through the format, or invariantly. */
    writeZ(value: ChartValue): string {
        const format = this.#parts.zFormat;
        return format === undefined ? invariantText(value) : format(value);
    }

    /**
     * The values the surface holds, holes left out.
     *
     * @internal
     */
    present(): ChartValue[] {
        const nodes = this.#parts.nodes;
        const all = nodes === undefined ? this.#parts.z.flat() : nodes.map(n => n.z);
        return all.filter(v => v !== null);
    }

    /**
     * The slice as a 2D chart (ADR-0274): the values along the other axis, read exactly — as categories on
     * an evenly spaced axis, as numbers on the axis' own scale otherwise (ADR-0312). A hole is a gap. A
     * mesh's slice cuts its triangles and reads between nodes linearly on them, which it says (ADR-0311).
     * `undefined` when the slice's value is not on this surface.
     */
    sliceChart(slice: SurfaceSlice): ChartDefinition | undefined {
        const onX = slice.axis === 'x';
        const fixedAxis = onX ? this.x : this.y;
        const along = onX ? this.y : this.x;
        const zName = nameOr(this.zTitle, 'Value');
        const format = this.zFormat;
        if (this.isMesh) {
            const runs = this.cut(slice);
            if (runs === undefined || runs.length === 0) {
                return undefined;
            }
            const cut: ChartPoint[] = [];
            for (const run of runs) {
                // A gap between runs is a point without a value, where no triangle is.
                const last = cut.at(-1);
                if (last !== undefined) {
                    cut.push({ x: (last.x + elementAt(run, 0).along) / 2, y: null });
                }
                cut.push(...run.map(p => ({ x: p.along, y: p.z })));
            }
            const series: ChartSeriesOptions = {
                id: 'slice', name: `${zName} at ${fixedAxis.writeValue(slice.value)}`, kind: 'level', points: cut, fromZero: false, interpolated: true,
                segments: [{ from: elementAt(cut, 0).x, to: elementAt(cut, cut.length - 1).x, method: trianglesMethod, extrapolated: false }],
                ...(format === undefined ? {} : { format }),
            };
            return new ChartDefinition({ axis: numberAxis(along), series: [series] });
        }

        const at = fixedAxis.indexOf(slice.value);
        if (at < 0) {
            return undefined;
        }
        const byValue = along.scale !== 'categories';
        const count = along.values.length;
        const points = Array.from({ length: count }, (_, k) => ({ x: byValue ? along.number(k) : k, y: onX ? this.at(at, k) : this.at(k, at) }));
        const interpolated = Array.from({ length: count }, (_, k) => (onX ? this.isInterpolated(at, k) : this.isInterpolated(k, at))).some(v => v);
        const method = this.method;
        const series: ChartSeriesOptions = {
            id: 'slice', name: `${zName} at ${fixedAxis.write(at)}`, kind: 'level', points, fromZero: false,
            ...(format === undefined ? {} : { format }),
            ...(interpolated && method !== undefined ? {
                interpolated: true,
                segments: [{ from: byValue ? along.number(0) : 0, to: byValue ? along.number(count - 1) : count - 1, method, extrapolated: false }],
            } : {}),
        };
        const axis: ChartAxisOptions = byValue ? numberAxis(along)
            : { values: 'labels', title: along.title, labels: Array.from({ length: count }, (_, k) => along.write(k)) };
        return new ChartDefinition({ axis, series: [series] });
    }

    /**
     * A mesh cut at the slice's value (ADR-0311): the runs of positions along the other axis and the values
     * there, linear on each triangle, broken where no triangle is. `undefined` when the value is not a number
     * within the mesh's positions.
     *
     * @internal
     */
    cut(slice: SurfaceSlice): readonly (readonly { readonly along: number; readonly z: ChartValue }[])[] | undefined {
        const nodes = this.#parts.nodes;
        const triangles = this.#parts.triangles;
        if (nodes === undefined || triangles === undefined || typeof slice.value !== 'number') {
            return undefined;
        }
        const c = slice.value;
        const onX = slice.axis === 'x';
        const fixedOf = (n: SurfaceNode): number => (onX ? n.x : n.y);
        const alongOf = (n: SurfaceNode): number => (onX ? n.y : n.x);
        const fixedAxis = onX ? this.x : this.y;
        if (c < fixedAxis.number(0) || c > fixedAxis.number(fixedAxis.values.length - 1)) {
            return undefined;
        }
        const alongAxis = onX ? this.y : this.x;
        const span = Math.max(alongAxis.number(alongAxis.values.length - 1) - alongAxis.number(0), Number.MIN_VALUE);
        const segments: (readonly [from: CutPoint, to: CutPoint])[] = [];
        for (const t of triangles) {
            const corners = [elementAt(nodes, t.a), elementAt(nodes, t.b), elementAt(nodes, t.c)];
            if (corners.some(n => n.z === null)) {
                continue;
            }
            const hits: CutPoint[] = [];
            for (let e = 0; e < 3; e++) {
                const p = elementAt(corners, e);
                const q = elementAt(corners, (e + 1) % 3);
                const pz = p.z;
                const qz = q.z;
                if (pz === null || qz === null) {
                    throw new Error('Internal: a corner without a value was passed over above.');
                }
                const dp = fixedOf(p) - c;
                const dq = fixedOf(q) - c;
                if (dp === 0) {
                    hits.push({ along: alongOf(p), z: pz });
                }
                if (dp * dq < 0) {
                    const w = dp / (dp - dq);
                    hits.push({ along: alongOf(p) + w * (alongOf(q) - alongOf(p)), z: toNumber(pz) + w * (toNumber(qz) - toNumber(pz)) });
                }
            }
            if (hits.length < 2) {
                continue;
            }
            const lo = hits.reduce((m, h) => (h.along < m.along ? h : m));
            const hi = hits.reduce((m, h) => (h.along > m.along ? h : m));
            if (hi.along > lo.along) {
                segments.push([lo, hi]);
            }
        }

        // Neighbouring triangles meet on an edge, so their segments meet end to start; one that starts past the
        // run's end begins another run, the gap between them a hole.
        const runs: CutPoint[][] = [];
        const tolerance = 1e-9 * span;
        for (const [from, to] of segments.toSorted(bySegment)) {
            const run = runs.at(-1);
            const end = run?.at(-1);
            if (run !== undefined && end !== undefined && from.along <= end.along + tolerance) {
                if (to.along > end.along + tolerance) {
                    run.push(to);
                }
                continue;
            }
            runs.push([from, to]);
        }
        return runs;
    }

    #gridOnly(): void {
        if (this.isMesh) {
            throw new Error('A mesh has no grid to index: read its points through node, counted as nodeCount counts them (ADR-0311).');
        }
    }

    // Why a logarithmic vertical scale stays proportional, or `undefined` when it does not.
    #zScaleNote(): string | undefined {
        if (this.#parts.zScale !== 'logarithmic') {
            return undefined;
        }
        const low = this.present().reduce((m, v) => (toNumber(v) < toNumber(m) ? v : m));
        if (!(toNumber(low) > 0)) {
            return `The vertical scale stays proportional: ${nameOr(this.zTitle, 'the values')} reach ${this.writeZ(low)}, which a logarithmic scale cannot place.`;
        }
        const centre = this.#parts.centre;
        if (centre !== undefined && !(centre > 0)) {
            return `The vertical scale stays proportional: the colours diverge from ${tickText(centre)}, which a logarithmic scale cannot place.`;
        }
        return undefined;
    }
}

// A sampled axis' value written as a tick is, where the Consumer gives no format.
function writeNumber(value: SurfaceValue): string {
    return typeof value === 'number' ? tickText(value) : value;
}

// Segments in order of their start, then of their end.
function bySegment(s: readonly [CutPoint, CutPoint], u: readonly [CutPoint, CutPoint]): number {
    const start = s[0].along - u[0].along;
    return start !== 0 ? start : s[1].along - u[1].along;
}

// The scale and centre a surface is asked for, checked (ADR-0310, ADR-0312).
function scaled(options: SurfaceScaleOptions): Pick<SurfaceParts, 'zScale' | 'centre'> {
    const { zScale = 'proportional', centre } = options;
    if (zScale === 'categories') {
        throw new ChartRefusedError('A surface\'s values are placed proportionally or logarithmically, never as categories.');
    }
    if (centre !== undefined && !Number.isFinite(centre)) {
        throw new ChartRefusedError('The value the colours diverge from is a finite number.');
    }
    return { zScale, centre };
}

function scaleOptions(options: SurfaceScaleOptions): SurfaceScaleOptions {
    return {
        ...(options.zScale === undefined ? {} : { zScale: options.zScale }),
        ...(options.centre === undefined ? {} : { centre: options.centre }),
    };
}

function axisOptions(axis: SurfaceAxis): SurfaceAxisOptions {
    const format = axis.format;
    return { title: axis.title, values: axis.values, scale: axis.scale, ...(format === undefined ? {} : { format }) };
}

function numberAxis(along: SurfaceAxis): ChartAxisOptions {
    return { values: 'numbers', title: along.title, scale: along.scale, format: position => along.writeTick(position) };
}

// The distinct values in order: numbers by value, texts by their code units. A list of both cannot be ordered.
function ordered(values: readonly SurfaceValue[]): SurfaceValue[] {
    const unique = [...new Set(values)];
    if (unique.every(v => typeof v === 'number')) {
        return unique.toSorted((a, b) => a - b);
    }
    if (unique.every(v => typeof v === 'string')) {
        return unique.toSorted((a, b) => (a < b ? -1 : a > b ? 1 : 0));
    }
    throw new TypeError('An axis\' values are all numbers or all texts; one of each cannot be put in order.');
}

function validateGrid(x: SurfaceAxis, y: SurfaceAxis, z: readonly (readonly (ChartValue | null)[])[], method: string | undefined,
    interpolated: readonly (readonly boolean[])[] | undefined): void {
    const nx = x.values.length;
    const ny = y.values.length;
    if (nx < 2 || ny < 2) {
        throw new ChartRefusedError(`A surface needs two ${nameOr(x.title, 'x')} values and two ${nameOr(y.title, 'y')} values at least; this one has ${String(nx)} and ${String(ny)}.`);
    }
    if (z.length !== ny) {
        throw new ChartRefusedError(`The surface's table has ${String(z.length)} rows for ${String(ny)} ${nameOr(y.title, 'y')} values; it needs one row per value.`);
    }
    z.forEach((row, j) => {
        if (row.length !== nx) {
            throw new ChartRefusedError(`The surface's row at ${nameOr(y.title, 'y')} ${y.write(j)} has ${String(row.length)} values for ${String(nx)} ${nameOr(x.title, 'x')} values; a hole is handed in as null, never left out.`);
        }
    });
    distinct(x, 'x');
    distinct(y, 'y');
    placed(x, 'x');
    placed(y, 'y');
    if (z.every(row => row.every(v => v === null))) {
        throw new ChartRefusedError('The surface holds no value.');
    }
    if (interpolated !== undefined) {
        if (method === undefined) {
            throw new ChartRefusedError('The surface marks values interpolated but names no method; the chart says how its values were made (ADR-0257).');
        }
        if (interpolated.length !== ny || interpolated.some(r => r.length !== nx)) {
            throw new ChartRefusedError('The surface\'s marks of interpolated values need the table\'s shape: one row per y value, one mark per x value.');
        }
    }
}

function validateMesh(x: SurfaceAxis, y: SurfaceAxis, nodes: readonly SurfaceNode[], triangles: readonly SurfaceTriangle[]): void {
    if (triangles.length === 0) {
        throw new ChartRefusedError('A mesh needs a triangle (ADR-0311).');
    }
    // A place is keyed as C# compares a pair of doubles: 0 and -0 alike.
    const places = new Map<string, number>();
    nodes.forEach((n, k) => {
        const key = `${String(n.x)},${String(n.y)}`;
        const first = places.get(key);
        if (first !== undefined) {
            throw new ChartRefusedError(`Nodes ${String(first)} and ${String(k)} both lie at ${nameOr(x.title, 'x')} ${x.writeTick(n.x)}, ${nameOr(y.title, 'y')} ${y.writeTick(n.y)}; a place in the plane holds one value.`);
        }
        places.set(key, k);
    });
    const xs = nodes.map(n => n.x);
    const ys = nodes.map(n => n.y);
    // Folded, not spread: a Consumer's mesh may hold more nodes than a call takes arguments.
    const span = Math.max(greatest(xs) - least(xs), greatest(ys) - least(ys));
    triangles.forEach((t, i) => {
        const { a, b, c } = t;
        const holds = (k: number): boolean => Number.isInteger(k) && k >= 0 && k < nodes.length;
        if (!holds(a) || !holds(b) || !holds(c)) {
            throw new ChartRefusedError(`Triangle ${String(i)} names a node the mesh does not hold; it has ${String(nodes.length)} nodes.`);
        }
        const p = elementAt(nodes, a);
        const q = elementAt(nodes, b);
        const r = elementAt(nodes, c);
        const area = (q.x - p.x) * (r.y - p.y) - (r.x - p.x) * (q.y - p.y);
        if (!(Math.abs(area) > 1e-12 * span * span)) {
            throw new ChartRefusedError(`Triangle ${String(i)} has no area: its corners ${String(a)}, ${String(b)} and ${String(c)} lie on one line, so it holds no plane to draw.`);
        }
    });
    placed(x, 'x');
    placed(y, 'y');
    if (nodes.every(n => n.z === null)) {
        throw new ChartRefusedError('The surface holds no value.');
    }
}

// An axis that places its values by value holds numbers, rising, above zero when logarithmic (ADR-0312).
function placed(axis: SurfaceAxis, fallback: string): void {
    if (axis.scale === 'categories') {
        return;
    }
    const kind = axis.scale === 'logarithmic' ? 'logarithmic' : 'proportional';
    let previous: number | undefined;
    axis.values.forEach((v, i) => {
        if (typeof v !== 'number') {
            throw new ChartRefusedError(`${nameOr(axis.title, fallback)} ${axis.write(i)} is not a number, which a ${kind} axis cannot place; leave the axis evenly spaced (ADR-0312).`);
        }
        if (axis.scale === 'logarithmic' && !(v > 0)) {
            throw new ChartRefusedError(`${nameOr(axis.title, fallback)} ${axis.write(i)} is at or below zero, which a logarithmic axis cannot place: every position must be above zero.`);
        }
        if (previous !== undefined && !(v > previous)) {
            throw new ChartRefusedError(`${nameOr(axis.title, fallback)} ${axis.write(i)} comes after ${axis.write(i - 1)}; a ${kind} axis' values rise along it.`);
        }
        previous = v;
    });
}

// A Set compares as C#'s Equals does: NaN alike with NaN, 0 with -0.
function distinct(axis: SurfaceAxis, fallback: string): void {
    const seen = new Set<SurfaceValue>();
    axis.values.forEach((v, i) => {
        if (seen.has(v)) {
            throw new ChartRefusedError(`${nameOr(axis.title, fallback)} ${axis.write(i)} appears twice; each grid line needs a value of its own.`);
        }
        seen.add(v);
    });
}

// The entry a map holds for a key it was built from.
function elementOf<K, V>(map: ReadonlyMap<K, V>, key: K): V {
    const value = map.get(key);
    if (value === undefined) {
        throw new Error(`Internal: ${String(key)} is not among the keys the map was built from.`);
    }
    return value;
}
