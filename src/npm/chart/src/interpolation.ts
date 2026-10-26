// Interpolation helpers a Consumer may call (ADR-0257), as ExChart.Engine's `Interpolate` does. The chart never
// calls them: a Consumer calls one, explicitly, and hands the result in as a function, so no interpolation sits
// on the drawing's path. Each result carries its method's name, which the chart shows as it is, so an
// interpolated curve or surface is never drawn silently as data. Only general methods live here; a
// finance-specific choice stays the Consumer's. None extrapolates: outside the points it was made from, a
// result has no value, and the chart draws a gap or a hole there (ADR-0274). The arithmetic is the C#
// engine's, step for step (tests/cases/chart/interpolation.json).
import type { ChartSeriesOptions } from './model';
import { elementAt } from './must';
import { least, greatest } from './numbers';
import { SurfaceDefinition, type SurfaceSampledOptions, type SurfaceTriangle } from './surface';

/** What a curve interpolated by one of the methods is made of: the C# record's members. */
export interface Interpolant1DOptions {
    /** The method's name, shown as it is, such as `natural cubic spline`. */
    readonly method: string;
    /** The first position it has a value at. */
    readonly from: number;
    /** The last position it has a value at. */
    readonly to: number;
    /** Its value at a position, `null` outside `from`–`to`. */
    readonly evaluate: (x: number) => number | null;
}

/**
 * A curve interpolated by one of the methods (`interpolateLinear`, `interpolateNaturalCubicSpline`): its
 * method's name, the range it has values over, and its value at a position, `null` outside that range
 * (ADR-0257).
 */
export class Interpolant1D {
    readonly #method: string;
    readonly #from: number;
    readonly #to: number;
    readonly #evaluate: (x: number) => number | null;

    /** A curve of the given method and function. */
    constructor(options: Interpolant1DOptions) {
        this.#method = options.method;
        this.#from = options.from;
        this.#to = options.to;
        this.#evaluate = options.evaluate;
    }

    /** The method's name, shown as it is, such as `natural cubic spline`. */
    get method(): string {
        return this.#method;
    }

    /** The first position it has a value at. */
    get from(): number {
        return this.#from;
    }

    /** The last position it has a value at. */
    get to(): number {
        return this.#to;
    }

    /** Its value at a position, `null` outside `from`–`to`, as the function it was made with answers. */
    get evaluate(): (x: number) => number | null {
        return this.#evaluate;
    }

    /** The value at `x`, or `null` outside the range it was made over or where it is not a finite number. */
    valueAt(x: number): number | null {
        const v = this.#evaluate(x);
        return v !== null && Number.isFinite(v) ? v : null;
    }

    /**
     * The curve as a series declared Interpolated, sampled by the chart as it draws, with one Segment named by
     * the method, so the tooltip and the Notes say it (ADR-0257).
     */
    series(id: string, name: string): ChartSeriesOptions {
        const from = this.#from;
        const to = this.#to;
        return Object.freeze({
            id, name, kind: 'level', interpolated: true,
            segments: Object.freeze([Object.freeze({ from, to, method: this.#method, extrapolated: false })]),
            sampler: Object.freeze({ from, to, valueAt: (x: number) => this.valueAt(x) }),
        });
    }
}

/** What a surface interpolated by one of the methods is made of: the C# record's members. */
export interface Interpolant2DOptions {
    /** The method's name, shown as it is, such as `bilinear`. */
    readonly method: string;
    /** The rectangle's left edge. */
    readonly xFrom: number;
    /** Its right edge. */
    readonly xTo: number;
    /** Its near edge. */
    readonly yFrom: number;
    /** Its far edge. */
    readonly yTo: number;
    /** Its value at a point, or `null` where it has none. */
    readonly evaluate: (x: number, y: number) => number | null;
}

/**
 * What a surface from an interpolant is sampled with besides: everything `SurfaceDefinition.sampled` takes but
 * the method, the rectangle and the function, which the interpolant gives.
 */
export type InterpolatedSurfaceOptions = Omit<SurfaceSampledOptions, 'method' | 'xFrom' | 'xTo' | 'yFrom' | 'yTo' | 'z'>;

/**
 * A surface interpolated by one of the methods (`interpolateBilinear`, `interpolateTriangles`): its method's
 * name, the rectangle it is evaluated over, and its value at a point, `null` where it has none — outside the
 * points it was made from, or where a value it would need is missing (ADR-0257, ADR-0274).
 */
export class Interpolant2D {
    readonly #method: string;
    readonly #xFrom: number;
    readonly #xTo: number;
    readonly #yFrom: number;
    readonly #yTo: number;
    readonly #evaluate: (x: number, y: number) => number | null;

    /** A surface of the given method and function. */
    constructor(options: Interpolant2DOptions) {
        this.#method = options.method;
        this.#xFrom = options.xFrom;
        this.#xTo = options.xTo;
        this.#yFrom = options.yFrom;
        this.#yTo = options.yTo;
        this.#evaluate = options.evaluate;
    }

    /** The method's name, shown as it is, such as `bilinear`. */
    get method(): string {
        return this.#method;
    }

    /** The rectangle's left edge. */
    get xFrom(): number {
        return this.#xFrom;
    }

    /** Its right edge. */
    get xTo(): number {
        return this.#xTo;
    }

    /** Its near edge. */
    get yFrom(): number {
        return this.#yFrom;
    }

    /** Its far edge. */
    get yTo(): number {
        return this.#yTo;
    }

    /** Its value at a point, or `null`, as the function it was made with answers. */
    get evaluate(): (x: number, y: number) => number | null {
        return this.#evaluate;
    }

    /** The value at (`x`, `y`), or `null` where it has none or where it is not a finite number. */
    valueAt(x: number, y: number): number | null {
        const v = this.#evaluate(x, y);
        return v !== null && Number.isFinite(v) ? v : null;
    }

    /**
     * The surface sampled across its rectangle under its method's name, as `SurfaceDefinition.sampled` samples a
     * function (ADR-0257): every value marked interpolated, a hole where it has none.
     */
    surface(options: InterpolatedSurfaceOptions): SurfaceDefinition {
        return SurfaceDefinition.sampled({
            ...options,
            method: this.#method, xFrom: this.#xFrom, xTo: this.#xTo, yFrom: this.#yFrom, yTo: this.#yTo,
            z: (x, y) => this.valueAt(x, y),
        });
    }
}

/** A point of a Consumer's mesh: its position and its value. */
export interface MeshPoint {
    /** Its x position. */
    readonly x: number;
    /** Its y position. */
    readonly y: number;
    /** Its value. */
    readonly z: number;
}

/** Straight lines between neighbouring points (ADR-0257); `xs` strictly increasing. */
export function interpolateLinear(xs: readonly number[], ys: readonly number[]): Interpolant1D {
    check(xs, ys.length, 2);
    finite(ys);
    const x = [...xs];
    const y = [...ys];
    const first = elementAt(x, 0);
    const last = elementAt(x, x.length - 1);
    return new Interpolant1D({
        method: 'linear', from: first, to: last,
        evaluate: t => {
            if (!(t >= first && t <= last)) {
                return null;
            }
            const k = interval(x, t);
            const xk = elementAt(x, k);
            const yk = elementAt(y, k);
            const f = (t - xk) / (elementAt(x, k + 1) - xk);
            return yk + f * (elementAt(y, k + 1) - yk);
        },
    });
}

/** A natural cubic spline through the points (ADR-0257): zero second derivative at both ends; `xs` strictly increasing. */
export function interpolateNaturalCubicSpline(xs: readonly number[], ys: readonly number[]): Interpolant1D {
    check(xs, ys.length, 3);
    finite(ys);
    const x = [...xs];
    const y = [...ys];
    const m = secondDerivatives(x, y);
    const first = elementAt(x, 0);
    const last = elementAt(x, x.length - 1);
    return new Interpolant1D({
        method: 'natural cubic spline', from: first, to: last,
        evaluate: t => {
            if (!(t >= first && t <= last)) {
                return null;
            }
            const k = interval(x, t);
            const xk = elementAt(x, k);
            const xk1 = elementAt(x, k + 1);
            const h = xk1 - xk;
            const a = (xk1 - t) / h;
            const b = (t - xk) / h;
            return a * elementAt(y, k) + b * elementAt(y, k + 1) + ((a * a * a - a) * elementAt(m, k) + (b * b * b - b) * elementAt(m, k + 1)) * h * h / 6;
        },
    });
}

/**
 * Bilinear interpolation on a grid (ADR-0257): `z` holds one row per y value and one value per x value, both
 * axes strictly increasing. A cell with a missing corner, `null`, has no value inside (ADR-0274).
 */
export function interpolateBilinear(xs: readonly number[], ys: readonly number[], z: readonly (readonly (number | null)[])[]): Interpolant2D {
    check(xs, xs.length, 2, 'X position');
    check(ys, ys.length, 2, 'Y position');
    if (z.length !== ys.length || z.some(r => r.length !== xs.length)) {
        throw new TypeError('The table needs one row per y value, each with one value per x value.');
    }
    for (let j = 0; j < z.length; j++) {
        const row = elementAt(z, j);
        for (let i = 0; i < row.length; i++) {
            // Read as what an untyped caller may have handed in: a hole in the list is no number either.
            const value: unknown = row[i];
            if (value !== null && (typeof value !== 'number' || !Number.isFinite(value))) {
                throw new TypeError(`The table's value in row ${String(j)}, column ${String(i)} is not a finite number; a missing value is null.`);
            }
        }
    }
    const x = [...xs];
    const y = [...ys];
    const table = z.map(r => [...r]);
    const xFrom = elementAt(x, 0);
    const xTo = elementAt(x, x.length - 1);
    const yFrom = elementAt(y, 0);
    const yTo = elementAt(y, y.length - 1);
    return new Interpolant2D({
        method: 'bilinear', xFrom, xTo, yFrom, yTo,
        evaluate: (u, v) => {
            if (!(u >= xFrom && u <= xTo && v >= yFrom && v <= yTo)) {
                return null;
            }
            const i = interval(x, u);
            const j = interval(y, v);
            const near = elementAt(table, j);
            const far = elementAt(table, j + 1);
            const z00 = elementAt(near, i);
            const z10 = elementAt(near, i + 1);
            const z01 = elementAt(far, i);
            const z11 = elementAt(far, i + 1);
            if (z00 === null || z10 === null || z01 === null || z11 === null) {
                return null;
            }
            const xi = elementAt(x, i);
            const yj = elementAt(y, j);
            const fx = (u - xi) / (elementAt(x, i + 1) - xi);
            const fy = (v - yj) / (elementAt(y, j + 1) - yj);
            return (1 - fy) * ((1 - fx) * z00 + fx * z10) + fy * ((1 - fx) * z01 + fx * z11);
        },
    });
}

/**
 * Linear interpolation on a triangle mesh (P1, ADR-0257): within each triangle the plane through its three
 * corners. `triangles` index into `points`; outside every triangle there is no value. The mesh is the
 * Consumer's, from its own solver or triangulation.
 */
export function interpolateTriangles(points: readonly MeshPoint[], triangles: readonly SurfaceTriangle[]): Interpolant2D {
    if (triangles.length === 0) {
        throw new TypeError('A mesh needs a triangle.');
    }
    for (const { a, b, c } of triangles) {
        if ([a, b, c].some(k => !Number.isInteger(k) || k < 0 || k >= points.length)) {
            throw new TypeError('A triangle names a point the mesh does not hold.');
        }
    }
    for (let k = 0; k < points.length; k++) {
        const q: MeshPoint | undefined = points[k];
        if (q === undefined || !Number.isFinite(q.x) || !Number.isFinite(q.y) || !Number.isFinite(q.z)) {
            throw new TypeError(`Point ${String(k)} is not a finite number.`);
        }
    }
    const p = points.map(q => Object.freeze({ x: q.x, y: q.y, z: q.z }));
    const t = triangles.map(q => Object.freeze({ a: q.a, b: q.b, c: q.c }));
    return new Interpolant2D({
        method: 'linear on triangles (P1)',
        xFrom: least(p.map(q => q.x)), xTo: greatest(p.map(q => q.x)), yFrom: least(p.map(q => q.y)), yTo: greatest(p.map(q => q.y)),
        evaluate: (u, v) => {
            for (const { a, b, c } of t) {
                const pa = elementAt(p, a);
                const pb = elementAt(p, b);
                const pc = elementAt(p, c);
                const det = (pb.y - pc.y) * (pa.x - pc.x) + (pc.x - pb.x) * (pa.y - pc.y);
                if (det === 0) {
                    continue;
                }
                const l1 = ((pb.y - pc.y) * (u - pc.x) + (pc.x - pb.x) * (v - pc.y)) / det;
                const l2 = ((pc.y - pa.y) * (u - pc.x) + (pa.x - pc.x) * (v - pc.y)) / det;
                const l3 = 1 - l1 - l2;
                if (l1 >= edge && l2 >= edge && l3 >= edge) {
                    return l1 * pa.z + l2 * pb.z + l3 * pc.z;
                }
            }
            return null;
        },
    });
}

// How far outside a triangle a point may lie, in its barycentric coordinates, and still be read on it, so a
// point on an edge shared by two triangles is never a hole.
const edge = -1e-12;

function check(xs: readonly number[], count: number, fewest: number, position = 'Position'): void {
    if (xs.length !== count) {
        throw new TypeError('Each position needs one value.');
    }
    if (xs.length < fewest) {
        throw new TypeError(`The method needs ${String(fewest)} points at least.`);
    }
    for (let k = 0; k < xs.length; k++) {
        if (!Number.isFinite(xs[k])) {
            throw new TypeError(`${position} ${String(k)} is not a finite number.`);
        }
    }
    for (let k = 1; k < xs.length; k++) {
        if (!(elementAt(xs, k - 1) < elementAt(xs, k))) {
            throw new TypeError('The positions must be strictly increasing.');
        }
    }
}

// A value that is no number would spread to every interval a spline or a line reads it in, so it is refused by
// its place rather than drawn (spine rule 1).
function finite(ys: readonly number[]): void {
    for (let k = 0; k < ys.length; k++) {
        if (!Number.isFinite(ys[k])) {
            throw new TypeError(`Value ${String(k)} is not a finite number.`);
        }
    }
}

// The interval [x[k], x[k+1]] that holds t, which lies within x[0]..x[^1]: the last x at or below t, as C#'s
// Array.BinarySearch finds it in strictly increasing positions, kept off the last position.
function interval(x: readonly number[], t: number): number {
    let lo = 0;
    let hi = x.length - 1;
    while (lo < hi) {
        const mid = Math.ceil((lo + hi) / 2);
        if (elementAt(x, mid) <= t) {
            lo = mid;
        } else {
            hi = mid - 1;
        }
    }
    return Math.min(Math.max(lo, 0), x.length - 2);
}

function secondDerivatives(x: readonly number[], y: readonly number[]): number[] {
    const n = x.length;
    const m = Array.from({ length: n }, () => 0);
    const c = Array.from({ length: n }, () => 0);
    const d = Array.from({ length: n }, () => 0);
    for (let k = 1; k < n - 1; k++) {
        const h0 = elementAt(x, k) - elementAt(x, k - 1);
        const h1 = elementAt(x, k + 1) - elementAt(x, k);
        const rhs = 6 * ((elementAt(y, k + 1) - elementAt(y, k)) / h1 - (elementAt(y, k) - elementAt(y, k - 1)) / h0);
        const diag = 2 * (h0 + h1) - h0 * elementAt(c, k - 1);
        c[k] = h1 / diag;
        d[k] = (rhs - h0 * elementAt(d, k - 1)) / diag;
    }
    for (let k = n - 2; k >= 1; k--) {
        m[k] = elementAt(d, k) - elementAt(c, k) * elementAt(m, k + 1);
    }
    return m;
}
