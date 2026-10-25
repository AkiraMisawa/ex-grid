// An Observation's edges between its nodes (ADR-0321): drawn as lines between their positions, cut at
// the plot's edges.
import type { Box } from './metrics';
import type { ChartSeries } from './model';
import { kindName, toNumber, writeObject } from './values';

/** An edge in CSS pixels, cut at the plot's edges. */
export interface DrawnEdge {
    /** Where it starts across. */
    readonly x1: number;
    /** Where it starts down. */
    readonly y1: number;
    /** Where it ends across. */
    readonly x2: number;
    /** Where it ends down. */
    readonly y2: number;
    /** Which of its series' edges it is. */
    readonly index: number;
}

/** What the definition refuses of a series' edges, or `undefined`. */
export function edgeRefusal(s: ChartSeries): string | undefined {
    if (s.edges.length === 0) {
        return undefined;
    }
    if (s.kind !== 'observation') {
        return `'${s.name}' has edges, which join an observation's nodes; a ${kindName(s.kind)} joins its points itself.`;
    }
    const keys = new Set<unknown>();
    for (let i = 0; i < s.points.length; i++) {
        const p = s.points[i];
        if (p === undefined) {
            break;
        }
        if (p.row === undefined || p.row === null) {
            return `'${s.name}' has edges, but its point at row ${String(i + 1)} has no key for an edge to name.`;
        }
        if (keys.has(p.row)) {
            return `'${s.name}' has two nodes keyed ${writeObject(p.row)}; an edge could not say which it joins.`;
        }
        keys.add(p.row);
        if (p.y === null || !Number.isFinite(p.x)) {
            return `'${s.name}': node ${writeObject(p.row)} has no position, so no edge can reach it.`;
        }
    }
    for (const e of s.edges) {
        for (const end of [e.from, e.to]) {
            if (!keys.has(end)) {
                return `'${s.name}' has an edge from ${writeObject(e.from)} to ${writeObject(e.to)}, but no node is keyed ${writeObject(end)}.`;
            }
        }
    }
    return undefined;
}

/** The series' edges in pixels, each cut to the plot; an edge wholly outside it is left out. */
export function edgePixels(s: ChartSeries, mapX: (x: number) => number, mapY: (y: number) => number, plot: Box): DrawnEdge[] {
    if (s.edges.length === 0) {
        return [];
    }
    const at = new Map<unknown, readonly [number, number]>();
    for (const p of s.points) {
        if (p.y !== null) {
            at.set(p.row, [mapX(p.x), mapY(toNumber(p.y))]);
        }
    }
    const drawn: DrawnEdge[] = [];
    s.edges.forEach((edge, index) => {
        const a = at.get(edge.from);
        const b = at.get(edge.to);
        if (a === undefined || b === undefined) {
            throw new Error('Internal: an edge names a node the definition did not hold.');
        }
        const cut = clip(a[0], a[1], b[0], b[1], plot, index);
        if (cut !== undefined) {
            drawn.push(cut);
        }
    });
    return drawn;
}

// Liang–Barsky: the part of the segment inside the box, or undefined.
function clip(x1: number, y1: number, x2: number, y2: number, box: Box, index: number): DrawnEdge | undefined {
    let t0 = 0;
    let t1 = 1;
    const dx = x2 - x1;
    const dy = y2 - y1;
    for (const [p, q] of [[-dx, x1 - box.x], [dx, box.x + box.width - x1], [-dy, y1 - box.y], [dy, box.y + box.height - y1]] as const) {
        if (p === 0) {
            if (q < 0) {
                return undefined;
            }
            continue;
        }
        const r = q / p;
        if (p < 0) {
            t0 = Math.max(t0, r);
        } else {
            t1 = Math.min(t1, r);
        }
        if (t0 > t1) {
            return undefined;
        }
    }
    return Object.freeze({ x1: x1 + t0 * dx, y1: y1 + t0 * dy, x2: x1 + t1 * dx, y2: y1 + t1 * dy, index });
}
