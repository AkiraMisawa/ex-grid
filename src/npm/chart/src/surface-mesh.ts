// What a surface's 3D entry is handed to draw (ADR-0255): the points placed on the floor and the vertical
// scale, the faces with their colour bands, the floor's lines and the vertical scale's ticks, and the colour
// bar. The core decides all of it — where each value lies on each scale (ADR-0312) and how each face is
// coloured (ADR-0310) — and the entry only projects, orders and draws it. The port of the C# engine's
// `SurfaceMesh`, held to it by the shared cases under tests/cases/chart/ (ADR-0370).
import { tickText } from './invariant-text';
import { elementAt } from './must';
import { greatest, least, roundToDigits } from './numbers';
import type { SurfaceAxis, SurfaceDefinition, SurfaceSlice, SurfaceValue } from './surface';
import { decades, logSteps, nice, steps } from './ticks';
import { toNumber } from './values';

/** A labelled position on one of a surface's scales: its value there, and its label or `null` where labels would crowd. */
export interface SurfaceTick {
    /** A fraction of the floor for x and y; a value on the vertical scale for z and the colour bar. */
    readonly value: number;
    /** The label, or `null` for a line drawn unlabelled. */
    readonly label: string | null;
}

/** A point of a surface as the 3D entry places it. */
export interface SurfaceMeshNode {
    /** Its place across the floor, 0 to 1. */
    readonly x: number;
    /** Its place along the floor, 0 to 1. */
    readonly y: number;
    /** Its value on the vertical scale (its logarithm on a logarithmic one), or `null` at a hole. */
    readonly z: number | null;
    /** Whether its value is interpolated, so the entry draws it no dot (ADR-0257). */
    readonly interpolated: boolean;
}

/** A face the 3D entry draws: its corners, as indices of the nodes, and its colour band. */
export interface SurfaceFace {
    /** Four corners for a grid's cell, three for a mesh's triangle. */
    readonly corners: readonly number[];
    /** Its colour band, 0 to `surfaceBandCount` - 1. */
    readonly band: number;
}

/** The colour bar beside a surface (ADR-0310). */
export interface SurfaceColourBar {
    /** The value at the foot of the lowest band, on the vertical scale. */
    readonly from: number;
    /** The value at the head of the highest band. */
    readonly to: number;
    /** The labelled values along it. */
    readonly ticks: readonly SurfaceTick[];
    /** Whether the bands diverge from a centre, which the middle band holds. */
    readonly diverging: boolean;
    /** How many bands it is drawn in: `surfaceBandCount`, handed to the entry so it holds no copy. */
    readonly bands: number;
}

/** A point of a slice's line on the surface, placed as a mesh node is. */
export interface SurfaceMeshPoint {
    /** Its place across the floor, 0 to 1. */
    readonly x: number;
    /** Its place along the floor, 0 to 1. */
    readonly y: number;
    /** Its value on the vertical scale. */
    readonly z: number;
}

/** What the 3D entry is handed to draw a surface (ADR-0255). */
export interface SurfaceMesh {
    /** The x axis' title. */
    readonly xTitle: string;
    /** The y axis' title. */
    readonly yTitle: string;
    /** The values' title. */
    readonly zTitle: string;
    /** The points, counted as the definition's `nodeCount` counts them. */
    readonly nodes: readonly SurfaceMeshNode[];
    /** The faces to draw; a face with a hole at a corner is no face. */
    readonly faces: readonly SurfaceFace[];
    /** A grid's lines along x, one per y value, as node indices; empty for a mesh. */
    readonly rows: readonly (readonly number[])[];
    /** A grid's lines along y, one per x value, as node indices; empty for a mesh. */
    readonly columns: readonly (readonly number[])[];
    /** The floor's lines across x. */
    readonly xLines: readonly SurfaceTick[];
    /** The floor's lines along y. */
    readonly yLines: readonly SurfaceTick[];
    /** The bottom of the vertical scale. */
    readonly zMin: number;
    /** The top of the vertical scale. */
    readonly zMax: number;
    /** The vertical scale's ticks. */
    readonly zTicks: readonly SurfaceTick[];
    /** The colour bar. */
    readonly bar: SurfaceColourBar;
    /** How many points are holes. */
    readonly holes: number;
}

/**
 * How many colour bands a face is coloured from, low to high; each is a Visual Token, `--ex-chart-heat-0`
 * on, or `--ex-chart-diverge-0` on when the colours diverge (ADR-0310).
 */
export const surfaceBandCount = 9;

const half = Math.trunc(surfaceBandCount / 2);

const tick = (value: number, label: string | null): SurfaceTick => Object.freeze({ value, label });

const point = (x: number, y: number, z: number): SurfaceMeshPoint => Object.freeze({ x, y, z });

/** The mesh of `definition`. */
export function surfaceMesh(definition: SurfaceDefinition): SurfaceMesh {
    const frame = new Frame(definition);
    const nodes: SurfaceMeshNode[] = Array.from({ length: definition.nodeCount }, (_, k) => {
        const [x, y, z] = definition.node(k);
        return Object.freeze({ x: frame.atX(k, x), y: frame.atY(k, y), z: z === null ? null : frame.scale(toNumber(z)), interpolated: definition.isInterpolatedNode(k) });
    });
    const present = nodes.flatMap(n => (n.z === null ? [] : [n.z]));
    const lo = least(present);
    const hi = greatest(present);

    let zMin: number;
    let zMax: number;
    let zTicks: readonly number[];
    if (frame.logarithmic) {
        const [dMin, dMax] = decades(10 ** lo, 10 ** hi);
        zMin = Math.log10(dMin);
        zMax = Math.log10(dMax);
        zTicks = logSteps(dMin, dMax).map(Math.log10);
    } else {
        const n = nice(lo, hi);
        zMin = n.min;
        zMax = n.max;
        zTicks = steps(n.min, n.max, n.step);
    }

    // A face is coloured by the mean of its corners on the vertical scale (ADR-0310): through the values'
    // own range, or by its distance from the centre, alike on both sides of it.
    const centre = definition.centre === undefined ? undefined : frame.scale(definition.centre);
    let reach = centre === undefined ? 0 : Math.max(Math.abs(hi - centre), Math.abs(lo - centre));
    if (centre !== undefined && reach === 0) {
        reach = frame.logarithmic || centre === 0 ? 1 : Math.abs(centre) / 10;
    }
    const band = (value: number): number => {
        if (centre !== undefined) {
            const t = (value - centre) / reach;
            const depth = Math.min(half, Math.floor(Math.abs(t) * surfaceBandCount / 2 + 0.5));
            return half + Math.sign(t) * depth;
        }
        const u = hi > lo ? (value - lo) / (hi - lo) : 0.5;
        return Math.min(Math.max(Math.floor(u * surfaceBandCount), 0), surfaceBandCount - 1);
    };

    const nx = definition.isMesh ? 0 : definition.x.values.length;
    const ny = definition.isMesh ? 0 : definition.y.values.length;
    const triangles = definition.triangles;
    const cornerSets = triangles === undefined ? cells(nx, ny) : triangles.map(t => [t.a, t.b, t.c]);
    const faces: SurfaceFace[] = [];
    for (const corners of cornerSets) {
        const zs = corners.flatMap(k => {
            const z = elementAt(nodes, k).z;
            return z === null ? [] : [z];
        });
        if (zs.length < corners.length) {
            continue;
        }
        const sum = zs.reduce((total, z) => total + z, 0);
        faces.push(Object.freeze({ corners: Object.freeze(corners), band: band(sum / corners.length) }));
    }

    const away = (v: number): number => (frame.logarithmic || v === 0 ? 1 : Math.abs(v) / 10);
    const [barFrom, barTo] = centre !== undefined ? [centre - reach, centre + reach]
        : hi > lo ? [lo, hi] : [lo - away(lo), hi + away(hi)];
    const bar: SurfaceColourBar = Object.freeze({
        from: barFrom, to: barTo, ticks: frame.barTicks(barFrom, barTo, centre), diverging: centre !== undefined, bands: surfaceBandCount,
    });

    const rows = Array.from({ length: ny }, (_, j) => Object.freeze(Array.from({ length: nx }, (__, i) => j * nx + i)));
    const columns = Array.from({ length: nx }, (_, i) => Object.freeze(Array.from({ length: ny }, (__, j) => j * nx + i)));

    return Object.freeze({
        xTitle: definition.x.title, yTitle: definition.y.title, zTitle: definition.zTitle,
        nodes: Object.freeze(nodes), faces: Object.freeze(faces), rows: Object.freeze(rows), columns: Object.freeze(columns),
        xLines: frame.floorLines(definition.x), yLines: frame.floorLines(definition.y), zMin, zMax,
        zTicks: Object.freeze(zTicks.map(v => tick(v, frame.label(v)))), bar, holes: nodes.filter(n => n.z === null).length,
    });
}

function cells(nx: number, ny: number): number[][] {
    const result: number[][] = [];
    for (let j = 0; j < ny - 1; j++) {
        for (let i = 0; i < nx - 1; i++) {
            result.push([j * nx + i, j * nx + i + 1, (j + 1) * nx + i + 1, (j + 1) * nx + i]);
        }
    }
    return result;
}

/**
 * The slice's line on the surface, placed as the mesh's points are, in runs broken at a hole: along a
 * grid's line, or across a mesh's triangles where the slice cuts them (ADR-0311). Empty when the slice is
 * not on this surface.
 */
export function surfaceSlicePath(definition: SurfaceDefinition, slice: SurfaceSlice): readonly (readonly SurfaceMeshPoint[])[] {
    const frame = new Frame(definition);
    const onX = slice.axis === 'x';
    const runs: (readonly SurfaceMeshPoint[])[] = [];
    if (definition.isMesh) {
        const cut = definition.cut(slice);
        if (cut === undefined || typeof slice.value !== 'number') {
            return runs;
        }
        const fixedAt = onX ? frame.placeX(slice.value) : frame.placeY(slice.value);
        for (const run of cut) {
            runs.push(Object.freeze(run.map(p => (onX
                ? point(fixedAt, frame.placeY(p.along), frame.scale(toNumber(p.z)))
                : point(frame.placeX(p.along), fixedAt, frame.scale(toNumber(p.z)))))));
        }
        return Object.freeze(runs);
    }

    const fixedAxis = onX ? definition.x : definition.y;
    const index = fixedAxis.indexOf(slice.value);
    if (index < 0) {
        return runs;
    }
    const nx = definition.x.values.length;
    const count = onX ? definition.y.values.length : nx;
    let current: SurfaceMeshPoint[] = [];
    for (let k = 0; k < count; k++) {
        const node = onX ? k * nx + index : index * nx + k;
        const [x, y, z] = definition.node(node);
        if (z === null) {
            if (current.length > 0) {
                runs.push(Object.freeze(current));
            }
            current = [];
            continue;
        }
        current.push(point(frame.atX(node, x), frame.atY(node, y), frame.scale(toNumber(z))));
    }
    if (current.length > 0) {
        runs.push(Object.freeze(current));
    }
    return Object.freeze(runs);
}

// The least distance, as a fraction of the floor, between two labels on an axis placed by value.
const labelRoom = 1 / 12;

// Where each value lies on each of the surface's scales (ADR-0312).
class Frame {
    readonly #definition: SurfaceDefinition;
    readonly logarithmic: boolean;

    constructor(definition: SurfaceDefinition) {
        this.#definition = definition;
        this.logarithmic = definition.effectiveZScale === 'logarithmic';
    }

    scale(value: number): number {
        return this.logarithmic ? Math.log10(value) : value;
    }

    atX(node: number, value: SurfaceValue): number {
        const d = this.#definition;
        const count = d.x.values.length;
        return d.x.scale === 'categories' && !d.isMesh ? even(node % count, count) : this.placeX(numberOf(value));
    }

    atY(node: number, value: SurfaceValue): number {
        const d = this.#definition;
        return d.y.scale === 'categories' && !d.isMesh
            ? even(Math.trunc(node / d.x.values.length), d.y.values.length)
            : this.placeY(numberOf(value));
    }

    placeX(value: number): number {
        return place(this.#definition.x, value);
    }

    placeY(value: number): number {
        return place(this.#definition.y, value);
    }

    // A grid has a line at each of its values: evenly spaced, labelled where twelve fit; placed by value,
    // labelled where a label does not crowd the last one. A mesh has no lines of its own, so its floor's are
    // at round numbers, as a 2D number axis' are (ADR-0312).
    floorLines(axis: SurfaceAxis): readonly SurfaceTick[] {
        const n = axis.values.length;
        const mesh = this.#definition.isMesh;
        if (!mesh && axis.scale === 'categories') {
            const step = Math.ceil(n / 12);
            return Object.freeze(Array.from({ length: n }, (_, i) => tick(even(i, n), i % step === 0 ? axis.write(i) : null)));
        }
        if (!mesh) {
            const lines: SurfaceTick[] = [];
            let last = -Infinity;
            for (let i = 0; i < n; i++) {
                const at = place(axis, axis.number(i));
                const room = at - last >= labelRoom;
                lines.push(tick(at, room ? axis.write(i) : null));
                if (room) {
                    last = at;
                }
            }
            return Object.freeze(lines);
        }
        const min = axis.number(0);
        const max = axis.number(n - 1);
        if (!(max > min)) {
            return Object.freeze([tick(0.5, axis.writeTick(min))]);
        }
        const ticks = axis.scale === 'logarithmic' ? logSteps(min, max) : niceWithin(min, max);
        return Object.freeze(ticks.map(t => tick(place(axis, t), axis.writeTick(t))));
    }

    barTicks(from: number, to: number, centre: number | undefined): readonly SurfaceTick[] {
        let ticks = this.logarithmic ? logSteps(10 ** from, 10 ** to).map(Math.log10) : [...niceWithin(from, to)];
        // The centre is always labelled, clearing the round numbers that would crowd its label; a bar whose
        // span holds fewer than two round numbers is labelled at its ends, so it always reads back.
        if (centre !== undefined) {
            ticks = [...ticks.filter(t => !(Math.abs(t - centre) < (to - from) / 20)), centre];
        }
        if (ticks.length < 2) {
            ticks = [...ticks.filter(t => Math.abs(t - from) >= (to - from) / 20 && Math.abs(t - to) >= (to - from) / 20), from, to];
        }
        return Object.freeze(ticks.toSorted((a, b) => a - b).map(t => tick(t, this.label(t))));
    }

    label(scaled: number): string {
        return tickText(this.logarithmic ? roundToDigits(10 ** scaled, 12) : scaled);
    }
}

function even(index: number, count: number): number {
    return index / (count - 1);
}

function place(axis: SurfaceAxis, value: number): number {
    const min = axis.number(0);
    const max = axis.number(axis.values.length - 1);
    if (!(max > min)) {
        return 0.5;
    }
    return axis.scale === 'logarithmic' ? Math.log(value / min) / Math.log(max / min) : (value - min) / (max - min);
}

// A position the definition placed by value, which it checked is a number.
function numberOf(value: SurfaceValue): number {
    if (typeof value !== 'number') {
        throw new Error(`Internal: ${value} is placed by value, and is not a number.`);
    }
    return value;
}

// Round-number ticks between min and max, never past them.
function niceWithin(min: number, max: number): readonly number[] {
    const n = nice(min, max);
    const slack = 1e-9 * Math.max(Math.abs(min), Math.abs(max));
    return steps(n.min, n.max, n.step).filter(t => t >= min - slack && t <= max + slack);
}
