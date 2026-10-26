// The parts of a chart's drawing as SVG markup (ADR-0252): a panel's axes, each series, a panel's legend,
// the keys of colours and sizes, the pointer's guide and a drag's band, and the tooltip. Each part is
// written from the immutable piece of the scene it draws, so the element that holds it is rewritten only
// when its markup changed, and a move of the pointer rewrites the tooltip and the guide and nothing else.
// The same writers draw a saved file (ADR-0282), so a file cannot drift from the screen.
import type { Box } from '../metrics';
import type { ChartReading } from '../reading';
import type { ColourKey, SizeKey } from '../point-scales';
import type { ChartScene, HorizontalTick, LegendEntry, PanelScene, SeriesPiece, VerticalAxis } from '../scene';
import type { DrawnWhisker } from '../series-drawing';
import type { SeriesLook } from '../model';
import { element, escapeText, lookClasses, markerPath, n } from './svg-markup';

/** One part of the drawing: the key it is kept by between drawings, and its markup. */
export interface DrawingPart {
    readonly key: string;
    readonly markup: string;
}

/** A panel's plot frame, grid lines, zero line, break mark and vertical axes; under the last panel, the horizontal axis. */
export function axesMarkup(panel: PanelScene, xTicks: readonly HorizontalTick[], last: boolean, lineHeight: number, angle: number): string {
    const p = panel.plot;
    const right = p.x + p.width;
    const bottom = p.y + p.height;
    let out = element('rect', [['class', 'ex-chart-plot'], ['x', n(p.x)], ['y', n(p.y)], ['width', n(p.width)], ['height', n(p.height)]]);
    // Vertical grid lines at the horizontal axis' ticks, in every panel, so a date can be followed down.
    for (const [, x] of xTicks) {
        out += element('line', [['class', 'ex-chart-grid'], ['x1', n(x)], ['x2', n(x)], ['y1', n(p.y)], ['y2', n(bottom)]]);
    }
    if (panel.left !== null) {
        out += vertical(panel.left, p.x - lineHeight / 4, 'end', p, lineHeight);
    }
    if (panel.right !== null) {
        out += vertical(panel.right, right + lineHeight / 4, 'start', p, lineHeight);
    }
    if (panel.zeroY !== null) {
        out += element('line', [['class', 'ex-chart-zero'], ['x1', n(p.x)], ['x2', n(right)], ['y1', n(panel.zeroY)], ['y2', n(panel.zeroY)]]);
    }
    if (last) {
        const y = bottom + lineHeight;
        for (const [, x, label] of xTicks) {
            if (label === null) {
                continue;
            }
            const attributes: (readonly [string, string])[] = [
                ['class', 'ex-chart-tick ex-chart-tick-x'], ['x', n(x)], ['y', n(y)], ['text-anchor', angle === 0 ? 'middle' : 'end'],
            ];
            if (angle !== 0) {
                attributes.push(['transform', `rotate(${String(-angle)} ${n(x)} ${n(y)})`]);
            }
            out += element('text', attributes, escapeText(label));
        }
    }
    return element('g', [['class', 'ex-chart-axes']], out);
}

function vertical(axis: VerticalAxis, x: number, anchor: string, p: Box, lineHeight: number): string {
    let out = '';
    for (const [, y, label] of axis.ticks) {
        if (axis.side === 'left') {
            out += element('line', [['class', 'ex-chart-grid'], ['x1', n(p.x)], ['x2', n(p.x + p.width)], ['y1', n(y)], ['y2', n(y)]]);
        }
        out += element('text', [['class', 'ex-chart-tick'], ['x', n(x)], ['y', n(y)], ['dy', '0.35em'], ['text-anchor', anchor]], escapeText(label));
    }
    if (axis.broken) {
        // The axis does not reach zero (ADR-0253): a break mark at its foot says so.
        const edge = axis.side === 'left' ? p.x : p.x + p.width;
        const s = lineHeight / 4;
        const foot = p.y + p.height;
        out += element('path', [['class', 'ex-chart-break'],
            ['d', `M${n(edge - s)},${n(foot - s * 1.5)}l${n(2 * s)},${n(-s)}M${n(edge - s)},${n(foot - s * 0.5)}l${n(2 * s)},${n(-s)}`]]);
    }
    return out;
}

const path = (cls: string, d: string): string => (d === '' ? '' : element('path', [['class', cls], ['d', d]]));

const rect = (x0: number, y0: number, x1: number, y1: number): string =>
    `M${n(x0)},${n(Math.min(y0, y1))}H${n(x1)}V${n(Math.max(y0, y1))}H${n(x0)}Z`;

const vLine = (x: number, y0: number, y1: number): string => `M${n(x)},${n(y0)}V${n(y1)}`;

/** One series: its lines as one path, its markers as two (filled and open), its extrapolated ranges. Its node count does not grow with its points (CH-3). */
export function seriesMarkup(piece: SeriesPiece, plot: Box, lineHeight: number): string {
    let out = '';
    for (const [from, to] of piece.extrapolated) {
        out += element('rect', [['class', 'ex-chart-extrapolated'], ['x', n(from)], ['y', n(plot.y)], ['width', n(Math.max(0, to - from))], ['height', n(plot.height)]]);
    }
    if (piece.edges.length > 0) {
        // A node's edges, under its markers (ADR-0321).
        out += element('path', [['class', 'ex-chart-edges'], ['d', piece.edges.map(e => `M${n(e.x1)},${n(e.y1)}L${n(e.x2)},${n(e.y2)}`).join('')]]);
    }
    let d = '';
    for (const line of piece.lines) {
        line.forEach((point, i) => { d += `${i === 0 ? 'M' : 'L'}${n(point.x)},${n(point.y)}`; });
    }
    out += element('path', [['class', 'ex-chart-path'], ['d', d]]);

    const r = lineHeight / 5;
    let filled = '';
    let open = '';
    // A marker coloured from a column is filled with its band's Visual Token (ADR-0320).
    const banded = new Map<number, string[]>();
    for (const m of piece.markers) {
        const outline = markerPath(piece.look.shape, m.x, m.y, m.radius ?? r);
        if (!m.filled) {
            open += outline;
        } else if (m.band !== undefined) {
            const into = banded.get(m.band);
            if (into === undefined) {
                banded.set(m.band, [outline]);
            } else {
                into.push(outline);
            }
        } else {
            filled += outline;
        }
    }
    out += element('path', [['class', 'ex-chart-markers'], ['d', filled]]);
    out += element('path', [['class', 'ex-chart-markers ex-chart-markers-open'], ['d', open]]);
    for (const [band, outlines] of [...banded].toSorted(([a], [b]) => a - b)) {
        out += element('path', [['class', `ex-chart-markers ex-chart-${piece.colourDiverging ? 'diverge' : 'heat'}-${String(band)}`], ['d', outlines.join('')]]);
    }
    if (piece.candles.length > 0 || piece.volumes.length > 0) {
        out += candles(piece);
    }
    if (piece.whiskers.length > 0 || piece.bands.length > 0) {
        out += intervals(piece, r);
    }
    if (piece.markers.some(m => m.yBounds !== undefined || m.xBounds !== undefined)) {
        out += markIntervals(piece, r);
    }
    const classes = `ex-chart-series ${lookClasses(piece.look)}${piece.interpolated ? ' ex-chart-interpolated' : ''}`;
    return element('g', [['class', classes], ['data-series', piece.id]], out);
}

// A candle's wick runs from its high to the body and from the body to its low, never through a hollow
// body; a High-Low-Close bar is its range with the close ticked to the right (ADR-0300).
function candles(piece: SeriesPiece): string {
    let rising = '';
    let falling = '';
    let unchanged = '';
    let hlc = '';
    for (const c of piece.candles) {
        const o = c.openY;
        if (o === null) {
            hlc += `${vLine(c.x, c.highY, c.lowY)}M${n(c.x)},${n(c.closeY)}H${n(c.x + c.halfWidth)}`;
            continue;
        }
        if (c.unchanged) {
            unchanged += `${vLine(c.x, c.highY, c.lowY)}M${n(c.x - c.halfWidth)},${n(o)}H${n(c.x + c.halfWidth)}`;
            continue;
        }
        const top = Math.min(o, c.closeY);
        const bottom = Math.max(o, c.closeY);
        const d = vLine(c.x, c.highY, top) + vLine(c.x, bottom, c.lowY) + rect(c.x - c.halfWidth, top, c.x + c.halfWidth, bottom);
        if (c.rising) {
            rising += d;
        } else {
            falling += d;
        }
    }
    for (const v of piece.volumes) {
        const d = rect(v.x - v.halfWidth, v.topY, v.x + v.halfWidth, v.baseY);
        if (v.unchanged) {
            unchanged += d;
        } else if (v.rising) {
            rising += d;
        } else {
            falling += d;
        }
    }
    return path('ex-chart-candles ex-chart-rising', rising) + path('ex-chart-candles ex-chart-falling', falling)
        + path('ex-chart-candles ex-chart-unchanged', unchanged) + path('ex-chart-candles ex-chart-hlc', hlc);
}

// An Interval: whiskers with a cap at each bound, or a band between the bounds over each run of
// neighbours; a run of one is drawn as a whisker, never dropped (ADR-0303).
function intervals(piece: SeriesPiece, r: number): string {
    let whiskers = '';
    let band = '';
    const whisker = (w: DrawnWhisker): void => {
        whiskers += `${vLine(w.x, w.lower, w.upper)}M${n(w.x - r)},${n(w.lower)}h${n(2 * r)}M${n(w.x - r)},${n(w.upper)}h${n(2 * r)}`;
    };
    piece.whiskers.forEach(whisker);
    for (const run of piece.bands) {
        const [only] = run;
        if (run.length === 1 && only !== undefined) {
            whisker(only);
            continue;
        }
        run.forEach((w, i) => { band += `${i === 0 ? 'M' : 'L'}${n(w.x)},${n(w.upper)}`; });
        run.toReversed().forEach(w => { band += `L${n(w.x)},${n(w.lower)}`; });
        band += 'Z';
    }
    return path('ex-chart-interval-band', band) + path('ex-chart-whiskers', whiskers);
}

// An Observation's intervals: a whisker up and down and one across, each capped, through its mark; a
// merged mark's is the union of its points' (ADR-0304).
function markIntervals(piece: SeriesPiece, r: number): string {
    let d = '';
    for (const m of piece.markers) {
        if (m.yBounds !== undefined) {
            const [lower, upper] = m.yBounds;
            d += `${vLine(m.x, lower, upper)}M${n(m.x - r)},${n(lower)}h${n(2 * r)}M${n(m.x - r)},${n(upper)}h${n(2 * r)}`;
        }
        if (m.xBounds !== undefined) {
            const [lower, upper] = m.xBounds;
            d += `M${n(lower)},${n(m.y)}H${n(upper)}M${n(lower)},${n(m.y - r)}v${n(2 * r)}M${n(upper)},${n(m.y - r)}v${n(2 * r)}`;
        }
    }
    return path('ex-chart-whiskers', d);
}

/** A panel's legend (ADR-0271): every series of the panel, a hidden one faded; a click or Enter hides or shows it. */
export function legendMarkup(entries: readonly LegendEntry[], lineHeight: number): string {
    if (entries.length === 0) {
        return '';
    }
    const lh = lineHeight;
    const swatch = lh * 1.5;
    const written = entries.map(e => {
        const box = e.box;
        const mid = box.y + box.height / 2;
        const content = element('rect', [['class', 'ex-chart-legend-hit'], ['x', n(box.x)], ['y', n(box.y)], ['width', n(box.width)], ['height', n(box.height)]])
            + element('line', [['class', 'ex-chart-path'], ['x1', n(box.x)], ['x2', n(box.x + swatch)], ['y1', n(mid)], ['y2', n(mid)]])
            + element('path', [['class', 'ex-chart-markers'], ['d', markerPath(e.look.shape, box.x + swatch / 2, mid, lh / 5)]])
            + element('text', [['class', 'ex-chart-legend-name'], ['x', n(box.x + swatch + lh / 4)], ['y', n(mid)], ['dy', '0.35em']], escapeText(e.name));
        return element('g', [
            ['class', `ex-chart-legend-entry ${lookClasses(e.look)}${e.hidden ? ' ex-chart-hidden' : ''}`], ['role', 'button'], ['tabindex', '0'],
            ['aria-pressed', e.hidden ? 'false' : 'true'], ['data-series', e.id],
        ], content);
    });
    return element('g', [['class', 'ex-chart-legend']], written.join(''));
}

const keyTitle = (title: string, x: number, y: number): string =>
    element('text', [['class', 'ex-chart-key-title'], ['x', n(x)], ['y', n(y)]], escapeText(title));

/** The key of colours read from a column, beside the plot (ADR-0320): a colour bar of the bands with its ticks. */
export function colourKeyMarkup(key: ColourKey, lineHeight: number): string {
    const lh = lineHeight;
    const box = key.box;
    let out = keyTitle(key.title, box.x, box.y - lh / 2);
    const band = box.height / key.bands;
    for (let i = 0; i < key.bands; i++) {
        out += element('rect', [['class', `ex-chart-key-band ex-chart-${key.diverging ? 'diverge' : 'heat'}-${String(i)}`],
            ['x', n(box.x)], ['y', n(box.y + box.height - (i + 1) * band)], ['width', n(box.width)], ['height', n(band)]]);
    }
    out += element('rect', [['class', 'ex-chart-key-frame'], ['x', n(box.x)], ['y', n(box.y)], ['width', n(box.width)], ['height', n(box.height)]]);
    for (const t of key.ticks) {
        out += element('text', [['class', 'ex-chart-tick'], ['x', n(box.x + box.width + lh / 3)], ['y', n(t.y)], ['dy', '0.35em']], escapeText(t.label));
    }
    return element('g', [['class', 'ex-chart-key ex-chart-colour-key'], ['data-series', key.seriesId]], out);
}

/** The key of sizes read from a column (ADR-0320): markers at round values. */
export function sizeKeyMarkup(key: SizeKey, lineHeight: number): string {
    const lh = lineHeight;
    const box = key.box;
    let out = keyTitle(key.title, box.x, box.y - lh / 2);
    let y = box.y;
    const cx = box.x + box.width / 2;
    for (const e of key.entries) {
        const row = Math.max(2 * e.radius, lh);
        const cy = y + row / 2;
        out += element('circle', [['class', 'ex-chart-key-size'], ['cx', n(cx)], ['cy', n(cy)], ['r', n(e.radius)]]);
        out += element('text', [['class', 'ex-chart-tick'], ['x', n(box.x + box.width + lh / 3)], ['y', n(cy)], ['dy', '0.35em']], escapeText(e.label));
        y += row + lh / 4;
    }
    return element('g', [['class', 'ex-chart-key ex-chart-size-key'], ['data-series', key.seriesId]], out);
}

/** Every part of the panels, in drawing order and keyed as the screen keeps them: each panel's axes, series and legend, then the keys. */
export function drawingParts(scene: ChartScene, lineHeight: number, angle: number): DrawingPart[] {
    const parts: DrawingPart[] = [];
    scene.panels.forEach((panel, i) => {
        parts.push({ key: `axes:${panel.name}`, markup: axesMarkup(panel, scene.xTicks, i === scene.panels.length - 1, lineHeight, angle) });
        for (const piece of panel.series) {
            parts.push({ key: `${panel.volumeOf === null ? 'series:' : 'volume:'}${piece.id}`, markup: seriesMarkup(piece, panel.plot, lineHeight) });
        }
        parts.push({ key: `legend:${panel.name}`, markup: legendMarkup(panel.legend, lineHeight) });
    });
    for (const key of scene.colourKeys) {
        parts.push({ key: `colour:${key.seriesId}`, markup: colourKeyMarkup(key, lineHeight) });
    }
    for (const key of scene.sizeKeys) {
        parts.push({ key: `size:${key.seriesId}`, markup: sizeKeyMarkup(key, lineHeight) });
    }
    return parts;
}

/** A dot on the guide, where a series' value stands at the pointer's position. */
export interface GuideDot {
    readonly y: number;
    readonly look: SeriesLook;
}

/** The vertical guide at the pointer's position, across every panel, and the dots of the pointer's panel (ADR-0270). */
export function guideMarkup(x: number | null, top: number, bottom: number, dots: readonly GuideDot[], lineHeight: number): string {
    if (x === null) {
        return '';
    }
    const line = element('line', [['class', 'ex-chart-guide-line'], ['x1', n(x)], ['x2', n(x)], ['y1', n(top)], ['y2', n(bottom)]]);
    const circles = dots.map(d => element('circle', [['class', `ex-chart-dot ${lookClasses(d.look)}`], ['cx', n(x)], ['cy', n(d.y)], ['r', n(lineHeight / 5)]]));
    return element('g', [['class', 'ex-chart-guide']], line + circles.join(''));
}

/** The band a drag across the plot draws (ADR-0273). */
export function bandMarkup(span: readonly [from: number, to: number] | null, top: number, bottom: number): string {
    if (span === null) {
        return '';
    }
    const [from, to] = span;
    return element('rect', [['class', 'ex-chart-band'], ['x', n(Math.min(from, to))], ['y', n(top)], ['width', n(Math.abs(to - from))], ['height', n(bottom - top)]]);
}

/** Where the tooltip stands, and whether it opens to the left of the guide. */
export interface TooltipPlace {
    readonly left: number;
    readonly top: number;
    readonly flip: boolean;
}

/** The tooltip (ADR-0270): the position, then a line per series, then "and M more series", as HTML. */
export function tooltipMarkup(reading: ChartReading | null, place: TooltipPlace, lineHeight: number, more: (count: number) => string): string {
    if (reading === null) {
        return '';
    }
    const lh = lineHeight;
    let out = element('div', [['class', 'ex-chart-tooltip-position']], escapeText(reading.position));
    for (const line of reading.lines) {
        const swatch = element('svg', [['class', 'ex-chart-swatch'], ['width', n(lh * 1.5)], ['height', n(lh)], ['aria-hidden', 'true']],
            element('line', [['class', 'ex-chart-path'], ['x1', '0'], ['x2', n(lh * 1.5)], ['y1', n(lh / 2)], ['y2', n(lh / 2)]])
            + element('path', [['class', 'ex-chart-markers'], ['d', markerPath(line.look.shape, lh * 0.75, lh / 2, lh / 5)]]));
        const note = line.note === null ? '' : element('span', [['class', 'ex-chart-tooltip-note']], escapeText(line.note));
        out += element('div', [['class', `ex-chart-tooltip-line ${lookClasses(line.look)}`], ['data-series', line.id]],
            swatch
            + element('span', [['class', 'ex-chart-tooltip-name']], escapeText(line.name))
            // oxlint-disable-next-line ex-grid/no-stand-in-default -- ADR-0270: a series with no value at the position shows a dash, its note saying why
            + element('span', [['class', 'ex-chart-tooltip-value']], escapeText(line.value ?? '–'))
            + note);
    }
    if (reading.more > 0) {
        out += element('div', [['class', 'ex-chart-tooltip-more']], escapeText(more(reading.more)));
    }
    return element('div', [
        ['class', `ex-chart-tooltip${place.flip ? ' ex-chart-tooltip-flip' : ''}`],
        ['style', `left:${n(place.left)}px;top:${n(place.top)}px;line-height:${n(lh)}px`],
    ], out);
}
