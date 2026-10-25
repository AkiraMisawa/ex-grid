// Thinning (ADR-0254): past the plot's device pixels, each pixel column keeps a line's first, last,
// lowest and highest point, so the drawn shape is the full series' at that width, every peak and trough
// kept. It runs on the drawing, never on the data: the files keep every point.
import type { PixelSummary } from './model';
import { elementAt } from './must';
import { greatest, least } from './numbers';
import { seriesDrawing, type DrawnPoint, type SeriesDrawing } from './series-drawing';

/** A series' drawing after Thinning, and what each thinned pixel holds. */
export interface ThinnedDrawing {
    /** What is drawn. */
    readonly drawing: SeriesDrawing;
    /** One entry per pixel that held more than one point; empty when nothing was thinned. */
    readonly thinned: readonly PixelSummary[];
}

const summary = (from: number, to: number, min: number, max: number, count: number): PixelSummary =>
    Object.freeze({ from, to, min, max, count });

/**
 * Thins `drawing` for a plot where `toPixel` gives a position's device pixel column. A drawing with no more
 * points than pixels is returned as it is.
 */
export function thin(drawing: SeriesDrawing, toPixel: (position: number) => number, pixels: number): ThinnedDrawing {
    const points = drawing.lines.reduce((sum, l) => sum + l.length, 0);
    if (points <= pixels) {
        return Object.freeze({ drawing, thinned: Object.freeze([]) });
    }
    const summaries: PixelSummary[] = [];
    const isSteps = drawing.lines.length > 0 && drawing.lines.every(l => l.length === 2 && elementAt(l, 0).y === elementAt(l, 1).y);
    const lines = isSteps ? stepLevels(drawing.lines, toPixel, summaries) : drawing.lines.map(l => line(l, toPixel, summaries));
    // Past one point a pixel, a marker stands for no one point: markers go, the shape stays.
    return Object.freeze({
        drawing: seriesDrawing(lines, []),
        thinned: Object.freeze(summaries),
    });
}

/**
 * What each pixel holds when the points were thinned before they came (ADR-0344): a pixel the drawing thinned
 * again counts every row the source's pixels inside it held, and a source pixel the drawing left alone says
 * what it held. A source pixel whose kept points fall in two of the drawing's pixels joins them into one.
 */
export function thinnedThrough(source: readonly PixelSummary[], drawn: readonly PixelSummary[], kept: readonly number[]): readonly PixelSummary[] {
    if (source.length === 0) {
        return drawn;
    }
    // Every summary, in order across; overlapping ones are one region, swept once.
    const all = [...source.map(p => ({ pixel: p, source: true })), ...drawn.map(p => ({ pixel: p, source: false }))]
        .toSorted((a, b) => (a.pixel.from === b.pixel.from ? a.pixel.to - b.pixel.to : a.pixel.from - b.pixel.from));
    const points = kept.toSorted((a, b) => a - b);
    const result: PixelSummary[] = [];
    let next = 0;
    let i = 0;
    while (i < all.length) {
        const head = elementAt(all, i).pixel;
        const from = head.from;
        let { to, min, max } = head;
        const sources: PixelSummary[] = [];
        for (; i < all.length && elementAt(all, i).pixel.from <= to; i++) {
            const entry = elementAt(all, i);
            const p = entry.pixel;
            to = Math.max(to, p.to);
            min = Math.min(min, p.min);
            max = Math.max(max, p.max);
            if (entry.source) {
                sources.push(p);
            }
        }
        // A source pixel stands for its rows; a point kept outside every one stands for itself.
        let count = sources.reduce((sum, p) => sum + p.count, 0);
        let s = 0;
        for (; next < points.length && elementAt(points, next) <= to; next++) {
            const x = elementAt(points, next);
            if (x < from) {
                continue;
            }
            while (s < sources.length && elementAt(sources, s).to < x) {
                s++;
            }
            if (s === sources.length || x < elementAt(sources, s).from) {
                count++;
            }
        }
        result.push(summary(from, to, min, max, Math.min(count, 2_147_483_647)));
    }
    return Object.freeze(result);
}

function line(points: readonly DrawnPoint[], toPixel: (position: number) => number, summaries: PixelSummary[]): readonly DrawnPoint[] {
    const kept: DrawnPoint[] = [];
    let i = 0;
    while (i < points.length) {
        const column = Math.floor(toPixel(elementAt(points, i).x));
        let j = i;
        while (j + 1 < points.length && Math.floor(toPixel(elementAt(points, j + 1).x)) === column) {
            j++;
        }
        if (j === i) {
            kept.push(elementAt(points, i));
            i++;
            continue;
        }
        let lo = i;
        let hi = i;
        for (let k = i; k <= j; k++) {
            if (elementAt(points, k).y < elementAt(points, lo).y) {
                lo = k;
            }
            if (elementAt(points, k).y > elementAt(points, hi).y) {
                hi = k;
            }
        }
        for (const k of [...new Set([i, lo, hi, j])].toSorted((a, b) => a - b)) {
            kept.push(elementAt(points, k));
        }
        summaries.push(summary(elementAt(points, i).x, elementAt(points, j).x, elementAt(points, lo).y, elementAt(points, hi).y, j - i + 1));
        i = j + 1;
    }
    return Object.freeze(kept);
}

// A step thinned by its levels: each pixel column draws its lowest and its highest level across the column,
// so no end is drawn at a level the function does not take there.
function stepLevels(levels: readonly (readonly DrawnPoint[])[], toPixel: (position: number) => number, summaries: PixelSummary[]): (readonly DrawnPoint[])[] {
    const groups = new Map<number, (readonly DrawnPoint[])[]>();
    for (const l of levels) {
        const key = Math.floor(toPixel((elementAt(l, 0).x + elementAt(l, 1).x) / 2));
        const group = groups.get(key);
        if (group === undefined) {
            groups.set(key, [l]);
        } else {
            group.push(l);
        }
    }
    const result: (readonly DrawnPoint[])[] = [];
    for (const g of groups.values()) {
        if (g.length === 1) {
            result.push(elementAt(g, 0));
            continue;
        }
        const a = least(g.map(l => elementAt(l, 0).x));
        const b = greatest(g.map(l => elementAt(l, 1).x));
        const min = least(g.map(l => elementAt(l, 0).y));
        const max = greatest(g.map(l => elementAt(l, 0).y));
        result.push(Object.freeze([point(a, min), point(b, min)]));
        if (max !== min) {
            result.push(Object.freeze([point(a, max), point(b, max)]));
        }
        summaries.push(summary(a, b, min, max, g.length));
    }
    return result;
}

const point = (x: number, y: number): DrawnPoint => Object.freeze({ x, y });
