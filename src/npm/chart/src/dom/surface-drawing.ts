// A 3D surface projected and drawn (ADR-0255, ADR-0274): the port of ex-chart.js's drawSurface. Where each
// point lies on the floor and on the vertical scale, and each face's colour band, are the mesh's (ADR-0312,
// ADR-0310); this turns, projects, orders and writes them as SVG markup, and says where it drew each point,
// each grid line through them, and how a step along x and along y is drawn, which the pointer is read
// against. A hole is a hole: a face with a hole at a corner is not drawn, the hole has no point, and the
// lines through it break there.
import type { SurfaceView } from '../surface';
import type { SurfaceMesh, SurfaceMeshPoint } from '../surface-mesh';
import { element, escapeText } from './svg-markup';

/** A point as it was drawn: its node, and where on the box. */
export interface DrawnSurfacePoint {
    readonly k: number;
    readonly x: number;
    readonly y: number;
}

type Screen = readonly [x: number, y: number];

// A drawn point, with its depth or without it.
type Drawn = readonly [x: number, y: number, ...rest: number[]];

/** What a drawing says besides its markup, for the pointer. */
export interface SurfaceDrawing {
    readonly markup: string;
    readonly points: readonly DrawnSurfacePoint[];
    /** A grid's lines along x and along y as drawn, each broken where a point is a hole; empty for a mesh. */
    readonly rows: readonly (readonly Screen[])[];
    readonly columns: readonly (readonly Screen[])[];
    /** How a step along x and along y is drawn at the floor's centre, for a click on a mesh; `null` when nothing is drawn. */
    readonly steps: { readonly x: Screen; readonly y: Screen } | null;
}

/** The strip at the right of a surface's drawing that holds its colour bar (ADR-0310). */
export const surfaceBarWidth = 80;

type Projected = readonly [x: number, y: number, depth: number];

const f1 = (v: number): string => v.toFixed(1);

// A step beyond the floor's front edge, outward.
const out = (front: number, by: number): number => front + (front === 0 ? -by : by);

const pointsText = (pts: readonly Drawn[]): string => pts.map(p => `${f1(p[0])},${f1(p[1])}`).join(' ');

const poly = (pts: readonly Drawn[], cls: string): string => element('polygon', [['points', pointsText(pts)], ['class', cls]]);

const line = (p: Drawn, q: Drawn, cls: string): string =>
    element('line', [['x1', f1(p[0])], ['y1', f1(p[1])], ['x2', f1(q[0])], ['y2', f1(q[1])], ['class', cls]]);

const text = (x: number, y: number, anchor: string | null, cls: string, content: string): string =>
    element('text', [['x', f1(x)], ['y', f1(y)], ...(anchor === null ? [] : [['text-anchor', anchor] as const]), ['class', cls]], escapeText(content));

/** Draws `mesh` as seen from `view` in a box `width` by `height`, with `slice` drawn on it. */
export function drawSurface(mesh: SurfaceMesh, view: SurfaceView, slice: readonly (readonly SurfaceMeshPoint[])[] | null,
    width: number, height: number): SurfaceDrawing {
    const nodes = mesh.nodes;
    if (nodes.length === 0 || width <= surfaceBarWidth || height <= 0) {
        return { markup: '', points: [], rows: [], columns: [], steps: null };
    }
    const { zMin, zMax } = mesh;
    const plotW = width - surfaceBarWidth;
    const a = (view.yaw * Math.PI) / 180;
    const b = (view.pitch * Math.PI) / 180;
    // World: x across the floor, y along it, z up; each within [-0.5, 0.5], z squashed.
    // A flat surface stands at the middle of its box.
    const zSpan = zMax - zMin === 0 ? 1 : zMax - zMin;
    const world = (x: number, y: number, z: number): Projected => [x - 0.5, y - 0.5, ((z - zMin) / zSpan - 0.5) * 0.6];
    // Turned by the yaw about the vertical, then seen from the front (−y) and above by the pitch, or below
    // where it is negative: screen x, screen y down, and depth, larger farther. The axes stay right-handed,
    // x to y counterclockwise seen from above, so the picture is never a mirror.
    const raw = ([x, y, z]: Projected): Projected => {
        const x1 = x * Math.cos(a) - y * Math.sin(a);
        const y1 = x * Math.sin(a) + y * Math.cos(a);
        return [x1, -(y1 * Math.sin(b) + z * Math.cos(b)), y1 * Math.cos(b) - z * Math.sin(b)];
    };
    // Fitted to the box at every angle by the projected corners of the box the surface stands in, with room
    // round it for the labels; then zoomed about the centre and panned.
    const corners: Projected[] = [];
    for (const cx of [-0.5, 0.5]) {
        for (const cy of [-0.5, 0.5]) {
            for (const cz of [-0.3, 0.3]) {
                corners.push(raw([cx, cy, cz]));
            }
        }
    }
    const minX = Math.min(...corners.map(c => c[0]));
    const maxX = Math.max(...corners.map(c => c[0]));
    const minY = Math.min(...corners.map(c => c[1]));
    const maxY = Math.max(...corners.map(c => c[1]));
    const s = Math.max(1, Math.min((plotW * 0.78) / (maxX - minX), (height * 0.8) / (maxY - minY))) * view.zoom;
    const centreX = plotW / 2 + view.panX * width;
    const centreY = height / 2 + view.panY * height;
    const project = (p: Projected): Projected => {
        const [x, y, depth] = raw(p);
        return [centreX + (x - (minX + maxX) / 2) * s, centreY + (y - (minY + maxY) / 2) * s, depth];
    };
    const at = (x: number, y: number, z: number): Projected => project(world(x, y, z));
    const node = (k: number): Projected => {
        const q = nodes[k];
        if (q === undefined || q.z === null) {
            throw new Error(`Internal: the surface's face names node ${String(k)}, which holds no value.`);
        }
        return at(q.x, q.y, q.z);
    };
    const heat = mesh.bar.diverging ? 'ex-chart-diverge-' : 'ex-chart-heat-';
    let markup = '';

    // The floor and the two walls behind the surface, each with its grid; which walls are behind depends on
    // where the viewer stands. Seen from below, the box's top is behind it instead of its floor. The grid's x
    // and y lines run up the walls too, so a value is read across to them.
    const backX = Math.sin(a) > 0 ? 1 : 0;
    const backY = Math.cos(a) > 0 ? 1 : 0;
    // The vertical's labels, and its title, stand on the outer side of the edge they mark.
    const right = at(backX, 1 - backY, zMin)[0] > at(0.5, 0.5, zMin)[0];
    const zBack = view.pitch >= 0 ? zMin : zMax;
    let walls = poly([at(0, 0, zBack), at(1, 0, zBack), at(1, 1, zBack), at(0, 1, zBack)], 'ex-chart-surface-wall');
    const upright = Math.abs(view.pitch) < 89;
    if (upright) {
        walls += poly([at(0, backY, zMin), at(1, backY, zMin), at(1, backY, zMax), at(0, backY, zMax)], 'ex-chart-surface-wall');
        walls += poly([at(backX, 0, zMin), at(backX, 1, zMin), at(backX, 1, zMax), at(backX, 0, zMax)], 'ex-chart-surface-wall');
        for (const t of mesh.xLines) {
            walls += line(at(t.value, backY, zMin), at(t.value, backY, zMax), 'ex-chart-surface-grid');
        }
        for (const t of mesh.yLines) {
            walls += line(at(backX, t.value, zMin), at(backX, t.value, zMax), 'ex-chart-surface-grid');
        }
        for (const tick of mesh.zTicks) {
            walls += line(at(0, backY, tick.value), at(1, backY, tick.value), 'ex-chart-surface-grid');
            walls += line(at(backX, 0, tick.value), at(backX, 1, tick.value), 'ex-chart-surface-grid');
            const p = at(backX, 1 - backY, tick.value);
            walls += text(p[0] + (right ? 8 : -8), p[1] + 4, right ? 'start' : 'end', 'ex-chart-surface-label', labelOf(tick.label));
        }
    }
    for (const t of mesh.xLines) {
        walls += line(at(t.value, 0, zBack), at(t.value, 1, zBack), 'ex-chart-surface-grid');
    }
    for (const t of mesh.yLines) {
        walls += line(at(0, t.value, zBack), at(1, t.value, zBack), 'ex-chart-surface-grid');
    }
    markup += element('g', [['class', 'ex-chart-surface-walls']], walls);

    // The faces, the farthest first, each in its colour band.
    const faces = mesh.faces.map(face => {
        const pts = face.corners.map(node);
        return { pts, depth: pts.reduce((sum, p) => sum + p[2], 0) / pts.length, band: face.band };
    });
    faces.sort((p, q) => q.depth - p.depth);
    markup += element('g', [['class', 'ex-chart-surface-faces']],
        faces.map(face => poly(face.pts, `ex-chart-surface-face ${heat}${String(face.band)}`)).join(''));

    // The points, and a grid's lines through them, broken at a hole. A dot marks a value the data holds; an
    // interpolated one is the Consumer's surface between them.
    const points: DrawnSurfacePoint[] = [];
    const projected = new Map<number, Screen>();
    let dots = '';
    nodes.forEach((q, k) => {
        if (q.z === null) {
            return;
        }
        const p = at(q.x, q.y, q.z);
        projected.set(k, [p[0], p[1]]);
        points.push({ k, x: p[0], y: p[1] });
        if (!q.interpolated) {
            dots += element('circle', [['cx', f1(p[0])], ['cy', f1(p[1])], ['r', '2.4'], ['class', 'ex-chart-surface-point']]);
        }
    });
    markup += element('g', [['class', 'ex-chart-surface-points']], dots);
    const through = (ks: readonly number[]): readonly Screen[] => ks.flatMap(k => {
        const p = projected.get(k);
        return p === undefined ? [] : [p];
    });
    const rows = mesh.rows.map(through);
    const columns = mesh.columns.map(through);

    // The slice drawn on the surface, so the 2D chart beside it is seen to be this line.
    if (slice !== null && slice.length > 0) {
        markup += element('g', [['class', 'ex-chart-surface-slice']],
            slice.map(run => element('polyline', [['points', pointsText(run.map(q => at(q.x, q.y, q.z)))]])).join(''));
    }

    // The axes' labels along the front edges of the floor.
    const frontY = 1 - backY;
    const frontX = 1 - backX;
    let labels = '';
    const label = (p: Projected, content: string, cls: string): void => {
        labels += text(p[0], p[1] + 4, 'middle', cls, content);
    };
    for (const t of mesh.xLines) {
        if (t.label !== null) {
            label(at(t.value, out(frontY, 0.08), zMin), t.label, 'ex-chart-surface-label');
        }
    }
    for (const t of mesh.yLines) {
        if (t.label !== null) {
            label(at(out(frontX, 0.08), t.value, zMin), t.label, 'ex-chart-surface-label');
        }
    }
    label(at(0.5, out(frontY, 0.22), zMin), mesh.xTitle, 'ex-chart-surface-label ex-chart-surface-title');
    label(at(out(frontX, 0.26), 0.5, zMin), mesh.yTitle, 'ex-chart-surface-label ex-chart-surface-title');
    if (upright && mesh.zTitle !== '') {
        const p = at(backX, 1 - backY, zMax + (zMax - zMin) * 0.12);
        labels += text(p[0] + (right ? 8 : -8), p[1] + 4, right ? 'start' : 'end', 'ex-chart-surface-label ex-chart-surface-title', mesh.zTitle);
    }
    markup += element('g', [['class', 'ex-chart-surface-labels']], labels);

    // The colour bar, which does not turn with the surface: its bands low to high, each the colour a face
    // takes, and its values (ADR-0310).
    const top = height * 0.1;
    const bottom = height * 0.9;
    const left = width - surfaceBarWidth + 8;
    const count = mesh.bar.bands;
    const band = (bottom - top) / count;
    let bar = '';
    for (let k = 0; k < count; k++) {
        bar += element('rect', [['x', f1(left)], ['y', f1(bottom - (k + 1) * band)], ['width', '12'], ['height', f1(band)],
            ['class', `ex-chart-surface-bar-band ${heat}${String(k)}`]]);
    }
    bar += element('rect', [['x', f1(left)], ['y', f1(top)], ['width', '12'], ['height', f1(bottom - top)], ['class', 'ex-chart-surface-bar-frame']]);
    // A bar of one value puts its tick at the foot.
    const span = mesh.bar.to - mesh.bar.from === 0 ? 1 : mesh.bar.to - mesh.bar.from;
    for (const tick of mesh.bar.ticks) {
        const y = bottom - ((tick.value - mesh.bar.from) / span) * (bottom - top);
        bar += line([left + 12, y], [left + 16, y], 'ex-chart-surface-bar-tick');
        bar += text(left + 19, y + 4, null, 'ex-chart-surface-label', labelOf(tick.label));
    }
    markup += element('g', [['class', 'ex-chart-surface-bar']], bar);

    // How a step along x and along y is drawn at a point, for a click on a mesh, which has no grid line.
    const o = at(0.5, 0.5, zMin);
    const ox = at(0.6, 0.5, zMin);
    const oy = at(0.5, 0.6, zMin);
    const steps = { x: [ox[0] - o[0], ox[1] - o[1]] as const, y: [oy[0] - o[0], oy[1] - o[1]] as const };

    return { markup, points, rows, columns, steps };
}

// The vertical scale's ticks and the colour bar's are always labelled; the mesh says so with a label.
function labelOf(label: string | null): string {
    if (label === null) {
        throw new Error('Internal: a tick of the surface\'s vertical scale or colour bar has no label.');
    }
    return label;
}

/** `v` held within `lo` to `hi`. */
export const clamp = (v: number, lo: number, hi: number): number => Math.max(lo, Math.min(hi, v));

/** The distance from (x, y) to a drawn line, or `Infinity` for a line with no point. */
export function distanceToLine(drawn: readonly Screen[] | undefined, x: number, y: number): number {
    if (drawn === undefined || drawn.length === 0) {
        return Infinity;
    }
    let best = Infinity;
    drawn.forEach((p, k) => {
        const q = drawn[k + 1];
        if (q === undefined) {
            best = Math.min(best, Math.hypot(p[0] - x, p[1] - y));
            return;
        }
        const dx = q[0] - p[0];
        const dy = q[1] - p[1];
        const length = dx * dx + dy * dy;
        const t = length === 0 ? 0 : clamp(((x - p[0]) * dx + (y - p[1]) * dy) / length, 0, 1);
        best = Math.min(best, Math.hypot(p[0] + t * dx - x, p[1] + t * dy - y));
    });
    return best;
}

/**
 * The line a click on a drawn point fell on, as the axis held fixed by the slice it draws. A grid point
 * lies on two lines, a tenor's and a strike's; the one nearer the pointer is the one it fell on. A tenor's
 * line runs along x, so a click on it slices that tenor (y held fixed). A mesh's node has no grid line
 * through it: the lines are those a step along x and along y draws through it, and the slice cuts its
 * triangles (ADR-0311).
 */
export function sliceAxisAt(drawing: SurfaceDrawing, point: DrawnSurfacePoint, x: number, y: number): 'x' | 'y' {
    const nx = drawing.columns.length;
    if (nx > 0) {
        const row = distanceToLine(drawing.rows[Math.floor(point.k / nx)], x, y);
        const column = distanceToLine(drawing.columns[point.k % nx], x, y);
        return column < row ? 'x' : 'y';
    }
    const steps = drawing.steps;
    if (steps === null) {
        throw new Error('Internal: a point was read on a surface with nothing drawn.');
    }
    // A step drawn as no length at all (seen edge-on) is measured as it stands.
    const off = (d: Screen): number => Math.abs((x - point.x) * d[1] - (y - point.y) * d[0]) / (Math.hypot(d[0], d[1]) === 0 ? 1 : Math.hypot(d[0], d[1]));
    return off(steps.y) < off(steps.x) ? 'x' : 'y';
}
