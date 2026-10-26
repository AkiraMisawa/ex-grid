// A chart laid out in CSS pixels as immutable pieces (ADR-0252): panels stacked down the chart, the
// horizontal axis under the last, and the Notes. The port of the C# engine's `ChartScene.Build`, held to it
// by the shared cases under tests/cases/chart/ (ADR-0370).
import { appearanceOf } from './appearance';
import type { ChartDefinition } from './definition';
import { packedSessions } from './definition';
import { edgePixels, type DrawnEdge } from './edges';
import { grouped, upToPlaces } from './invariant-text';
import { resolveMetrics, type Box, type ChartMetrics, type ChartMetricsResolved, type ChartView } from './metrics';
import type {
    AppearanceKeys, AxisScale, AxisSide, CandlePeriod, ChartAxis, ChartSeries, PixelSummary, SeriesInterval, SeriesKind, SeriesLook,
} from './model';
import { defined, elementAt, unreachable } from './must';
import { greatest, least, roundToDigits } from './numbers';
import { keyColumnWidth, keyGap, maxRadius, pointKeys, pointScaleNotes, type ColourKey, type SizeKey } from './point-scales';
import { candlePeriods, candlesOf, periodName, type DrawnCandle } from './price-ranges';
import { drawSeries, seriesDrawing, type Bounds, type DrawnMarker, type DrawnPoint, type DrawnWhisker, type SeriesDrawing } from './series-drawing';
import { thin, thinnedThrough } from './thinning';
import { decades, horizontalTicks, logSteps, nice, tenor, type AxisStyle } from './ticks';
import { toNumber, writePosition, writeValue } from './values';

/** Where a panel's legend sits (ADR-0271). */
export type LegendPosition =
    /** Above each panel's plot. */
    | 'top'
    /** Below it; under the horizontal axis for the last panel. */
    | 'bottom'
    /** Left of the plots. */
    | 'left'
    /** Right of the plots. */
    | 'right'
    /** No legend. */
    | 'none';

/** How a legend's entries align along its side (ADR-0271). */
export type LegendAlign = 'start' | 'centre' | 'end';

/** The Consumer's placement of the legend: a side and an alignment, never a pixel offset (ADR-0271). */
export interface LegendPlacement {
    /** The side; `top` by default. */
    readonly position?: LegendPosition;
    /** The alignment; `start` by default. */
    readonly align?: LegendAlign;
}

/** One legend entry, laid out. */
export interface LegendEntry {
    /** The key a click hides it by: the series' identity, which for quotes and their Interpolated curve is the quotes' and hides both (ADR-0257). */
    readonly id: string;
    /** Its name. */
    readonly name: string;
    /** Its look, which hiding does not change. */
    readonly look: SeriesLook;
    /** Whether it is hidden, which fades the entry. */
    readonly hidden: boolean;
    /** Where the entry sits, swatch first. */
    readonly box: Box;
}

/** A tick of a vertical axis: its value, its height in pixels, and its label. */
export type VerticalTick = readonly [value: number, y: number, label: string];

/** A vertical axis, laid out. */
export interface VerticalAxis {
    /** Which side it stands on. */
    readonly side: AxisSide;
    /** The value at the plot's foot. */
    readonly min: number;
    /** The value at its top. */
    readonly max: number;
    /** Its ticks. */
    readonly ticks: readonly VerticalTick[];
    /** Whether it does not reach zero, which a break mark at its foot says (ADR-0253). */
    readonly broken: boolean;
    /** Whether it is logarithmic, its `min` and `max` then both above zero (ADR-0290). */
    readonly logarithmic: boolean;
}

/** A Price Range's candle in pixels (ADR-0300). */
export interface PlacedCandle {
    /** Its centre across. */
    readonly x: number;
    /** Half its body's width. */
    readonly halfWidth: number;
    /** Its open's height, or `null` for a High-Low-Close bar. */
    readonly openY: number | null;
    /** Its high's height. */
    readonly highY: number;
    /** Its low's height. */
    readonly lowY: number;
    /** Its close's height. */
    readonly closeY: number;
    /** Its close above its own open, drawn hollow. */
    readonly rising: boolean;
    /** Its close equal to its own open. */
    readonly unchanged: boolean;
    /** How many rows it stands for. */
    readonly rows: number;
    /** The first position it stands for. */
    readonly from: number;
    /** The last. */
    readonly to: number;
}

/** A volume bar in pixels (ADR-0300). */
export interface PlacedVolume {
    /** Its centre across. */
    readonly x: number;
    /** Half its width. */
    readonly halfWidth: number;
    /** Its top's height. */
    readonly topY: number;
    /** Its base's height: zero's. */
    readonly baseY: number;
    /** Whether its candle rose. */
    readonly rising: boolean;
    /** Whether its candle was unchanged. */
    readonly unchanged: boolean;
    /** How many rows it stands for. */
    readonly rows: number;
    /** The first position it stands for. */
    readonly from: number;
    /** The last. */
    readonly to: number;
}

/** A marker of an Observation series as the pointer finds it (ADR-0290). */
export interface ChartMark {
    /** The series' identity. */
    readonly seriesId: string;
    /** The panel it stands in. */
    readonly panel: number;
    /** Its centre across, in pixels. */
    readonly x: number;
    /** Its centre down, in pixels. */
    readonly y: number;
    /** The index of the series' point it stands for, the first where it stands for more than one. */
    readonly point: number;
    /** How many of the series' points it stands for. */
    readonly count: number;
}

/** An edge as the pointer finds it (ADR-0321). */
export interface ChartEdgeMark {
    /** The series' identity. */
    readonly seriesId: string;
    /** The panel it is drawn in. */
    readonly panel: number;
    /** The edge as drawn, cut to the plot; its `index` names which of the series' edges it is. */
    readonly edge: DrawnEdge;
}

/** One series as the scene draws it: everything its component needs (ADR-0252). */
export interface SeriesPiece {
    /** The series' identity. */
    readonly id: string;
    /** Its name. */
    readonly name: string;
    /** The keys its look is chosen by (ADR-0256). */
    readonly keys: AppearanceKeys;
    /** The look they chose. */
    readonly look: SeriesLook;
    /** Its Series Kind. */
    readonly kind: SeriesKind;
    /** Whether it is the Consumer's interpolation (ADR-0257). */
    readonly interpolated: boolean;
    /** Its lines in pixels, each unbroken. */
    readonly lines: readonly (readonly DrawnPoint[])[];
    /** Its markers in pixels. */
    readonly markers: readonly DrawnMarker[];
    /** Its extrapolated ranges in pixels, drawn distinctly. */
    readonly extrapolated: readonly (readonly [from: number, to: number])[];
    /** What each thinned pixel holds (ADR-0254). */
    readonly thinned: readonly PixelSummary[];
    /** A Price Range's candles in pixels (ADR-0300). */
    readonly candles: readonly PlacedCandle[];
    /** The period its candles stand for: a row each, or the market period they were merged into (ADR-0302). */
    readonly period: CandlePeriod;
    /** A volume panel's bars in pixels (ADR-0300). */
    readonly volumes: readonly PlacedVolume[];
    /** Its Interval's whiskers in pixels (ADR-0303). */
    readonly whiskers: readonly DrawnWhisker[];
    /** Its Interval's bands in pixels, each a run of neighbours. */
    readonly bands: readonly (readonly DrawnWhisker[])[];
    /** What its Interval means, or `null` when it has none. */
    readonly intervalMeaning: string | null;
    /** Whether its markers' colour bands diverge from a centre (ADR-0320, ADR-0310). */
    readonly colourDiverging: boolean;
    /** Its edges in pixels, cut to the plot, drawn under its nodes (ADR-0321). */
    readonly edges: readonly DrawnEdge[];
}

/** A panel: its plot's box, its vertical axes and its series. */
export interface PanelScene {
    /** The panel's name. */
    readonly name: string;
    /** The plot's box. */
    readonly plot: Box;
    /** The left axis, or `null` when no series reads on it. */
    readonly left: VerticalAxis | null;
    /** The right axis, or `null`. */
    readonly right: VerticalAxis | null;
    /** The height of zero in pixels, where the plot holds it, or `null`. */
    readonly zeroY: number | null;
    /** The visible series, in the definition's order. */
    readonly series: readonly SeriesPiece[];
    /** Its legend: every series of the panel, hidden ones included (ADR-0271). */
    readonly legend: readonly LegendEntry[];
    /** The Price Range whose volume this panel draws, or `null` for a panel of the Consumer's (ADR-0300). */
    readonly volumeOf: string | null;
}

/** A tick of the horizontal axis: its position, its pixel, and its label or `null`. */
export type HorizontalTick = readonly [position: number, x: number, label: string | null];

/** A position the pointer can be over, and its pixel. */
export type PointerPosition = readonly [position: number, x: number];

/** A chart laid out in CSS pixels as immutable pieces (ADR-0252). */
export interface ChartScene {
    /** The panels, top to bottom. */
    readonly panels: readonly PanelScene[];
    /** The horizontal axis' ticks, each with its pixel position. */
    readonly xTicks: readonly HorizontalTick[];
    /** The first position shown. */
    readonly xFrom: number;
    /** The last. */
    readonly xTo: number;
    /** The lines said under the chart: the Consumer's Notes, each Segment's method, and what the scene itself must say. */
    readonly notes: readonly string[];
    /** The positions the pointer can be over, each with its pixel position, left to right (ADR-0255). */
    readonly positions: readonly PointerPosition[];
    /** Whether a visible series is an Observation: the pointer then finds the nearest mark (ADR-0290). */
    readonly scatter: boolean;
    /** The Observation series' marks, in drawing order (ADR-0290). */
    readonly marks: readonly ChartMark[];
    /** The edges drawn, which the pointer finds the nearest of where no mark is within reach (ADR-0321). */
    readonly edgeMarks: readonly ChartEdgeMark[];
    /** Whether the axis across is logarithmic as drawn (ADR-0290). */
    readonly logarithmicX: boolean;
    /** The colour bars of the visible series that take their colour from a column (ADR-0320). */
    readonly colourKeys: readonly ColourKey[];
    /** The size keys of the visible series that take their size from a column (ADR-0320). */
    readonly sizeKeys: readonly SizeKey[];
    /** The horizontal axis' title as drawn, with what the scene must add to it (ADR-0301, ADR-0302). */
    readonly xTitle: string;
}

/** What a scene is laid out with, beside the definition and the metrics. */
export interface SceneOptions {
    /** What the viewer narrowed the chart to; everything by default. */
    readonly view?: ChartView;
    /** How the horizontal axis is ticked and labelled. */
    readonly style?: AxisStyle;
    /** The panels whose left and right axes the Consumer declared independent (ADR-0251). */
    readonly independentScales?: ReadonlySet<string> | readonly string[];
    /** Where each panel's legend sits (ADR-0271); top, at the start, by default. */
    readonly legend?: LegendPlacement;
}

/** The height in pixels at which `value` stands on `plot`. */
export function yOf(axis: VerticalAxis, value: number, plot: Box): number {
    const share = axis.max === axis.min ? 0.5
        : axis.logarithmic ? Math.log(value / axis.min) / Math.log(axis.max / axis.min) : (value - axis.min) / (axis.max - axis.min);
    return plot.y + plot.height - share * plot.height;
}

/** The value that stands at height `y` on `plot`. */
export function valueAt(axis: VerticalAxis, y: number, plot: Box): number {
    const share = plot.height === 0 ? 0.5 : (plot.y + plot.height - y) / plot.height;
    return axis.logarithmic ? axis.min * (axis.max / axis.min) ** share : axis.min + share * (axis.max - axis.min);
}

/** The position across that stands at `x` pixels, on a proportional or logarithmic axis. */
export function positionAt(scene: ChartScene, x: number): number {
    const first = scene.panels[0];
    if (first === undefined) {
        return scene.xFrom;
    }
    const plot = first.plot;
    const share = plot.width === 0 ? 0.5 : (x - plot.x) / plot.width;
    return scene.logarithmicX ? scene.xFrom * (scene.xTo / scene.xFrom) ** share : scene.xFrom + share * (scene.xTo - scene.xFrom);
}

// A volume panel's height against a price panel's.
const volumeShare = 0.35;

// A candle's body against the width it stands for.
const bodyShare = 0.7;

type Map1 = (position: number) => number;

/** The axis as the viewer switched it, the visible series, the range shown, and the plot's span in pixels and device pixels. */
export interface Frame {
    readonly axis: ChartAxis;
    readonly visible: readonly ChartSeries[];
    readonly from: number;
    readonly to: number;
    readonly plotLeft: number;
    readonly plotWidth: number;
    readonly devicePixels: number;
}

interface PanelSpec {
    readonly name: string;
    readonly volumeOf: ChartSeries | null;
}

interface Merge {
    readonly candles: readonly DrawnCandle[];
    readonly period: CandlePeriod;
}

interface AxisRange {
    readonly lo: number;
    readonly hi: number;
    readonly step: number;
    readonly zero: boolean;
}

/**
 * Lays `definition` out at `metrics`, narrowed by the options' view. `independentScales` lists the panels
 * whose left and right axes the Consumer declared independent (ADR-0251); `legend` says where each panel's
 * legend sits (ADR-0271).
 */
export function buildScene(definition: ChartDefinition, metrics: ChartMetrics, options: SceneOptions = {}): ChartScene {
    const { view = {}, style = {}, independentScales = [], legend = {} } = options;
    const { position: legendPosition = 'top', align: legendAlign = 'start' } = legend;
    const { hidden: hiddenKeys = [] } = view;
    const hidden = new Set<string>(hiddenKeys);
    const independentPanels = new Set<string>(independentScales);
    const m = resolveMetrics(metrics);
    const { looks, coloursRepeat } = appearanceOf(definition, m.colourSlots);
    const { axis, visible, from, to, plotLeft, plotWidth, devicePixels } = frame(definition, m, view, hidden, legendPosition);
    const legendKeys = legendKeysOf(definition.series);
    const categories = axis.effectiveScale === 'categories';
    const logX = axis.effectiveScale === 'logarithmic';
    const scatter = visible.some(s => s.kind === 'observation');
    const lh = m.lineHeight;
    const pad = m.padding;
    const notes: string[] = [...definition.notes];
    const lookOf = (s: ChartSeries): SeriesLook => defined(looks.get(s.id), `the look of '${s.id}'`);
    const keyOf = (s: ChartSeries): string => defined(legendKeys.get(s.id), `the legend key of '${s.id}'`);

    // What no longer matches is said, not ignored (ADR-0272).
    for (const key of [...hidden].filter(k => definition.series.every(s => s.id !== k)).toSorted(ordinal)) {
        notes.push(`"${key}" is hidden, but no series has that key.`);
    }
    // A scale asked for and not drawn is said by name (ADR-0290).
    const bounded = visible.find(s => lowBound(s.xInterval) !== undefined);
    if (view.scale === 'logarithmic' && !logX && axis.values === 'numbers' && bounded !== undefined && !visible.some(reachesZeroAcross)) {
        const low = defined(lowBound(bounded.xInterval), 'the low bound');
        const meaning = defined(bounded.xInterval, 'the horizontal interval').meaning;
        notes.push(`The axis across stays proportional: '${bounded.name}' has a bound of ${upToPlaces(low[1], 4)} in its ${meaning} at ${writePosition(axis, low[0])}, which a logarithmic axis cannot place.`);
    } else if (view.scale === 'logarithmic' && !logX) {
        notes.push(notLogarithmic(visible.find(s => reachesZeroAcross(s) || lowBound(s.xInterval) !== undefined), 'across', axis.values !== 'numbers'));
    }
    let logY = view.yScale === 'logarithmic';
    if (logY) {
        const refusal = logarithmicRefusal(visible, axis, from, to, devicePixels);
        if (refusal !== undefined) {
            notes.push(refusal);
            logY = false;
        }
    }

    // The horizontal map: proportional, or categories evenly spaced, centred in their slots.
    const slots = categorySlots(axis, from, to);
    const map: Map1 = categories
        ? x => {
            const i = slots.get(x);
            return i === undefined ? NaN : plotLeft + (i + 0.5) * plotWidth / Math.max(1, slots.size);
        }
        : logX ? x => plotLeft + (to === from ? 0.5 : Math.log(x / from) / Math.log(to / from)) * plotWidth
        : x => plotLeft + (to === from ? 0.5 : (x - from) / (to - from)) * plotWidth;

    const positions = positionsOf(visible, from, to, plotWidth, map, categories ? [...slots.keys()] : undefined);
    if (visible.length > 0 && positions.length === 0) {
        notes.push(`No series has a point between ${writePosition(axis, from)} and ${writePosition(axis, to)}.`);
    }

    const points = new Set(visible.flatMap(s => s.points.map(p => p.x)));
    // On packed sessions a date tick (a month's start) stands at the first session on or after it (ADR-0301).
    const sessions = categories && axis.values === 'dates' ? [...slots.keys()].toSorted(byNumber) : undefined;
    const tickMap: Map1 = sessions === undefined ? map : x => {
        const i = lowerBound(sessions, x);
        return i < sessions.length ? map(elementAt(sessions, i)) : NaN;
    };
    const xTicks = horizontalTicks(axis, style, from, to, points, tickMap, m.textWidth, lh);
    const ticksBand = xTicks.depth + lh / 2;

    // Every series' panel, hidden ones included, so a panel whose series are all hidden keeps its legend
    // and can show them again.
    const panelNames = [...new Set(definition.series.map(s => s.panel))];
    // A visible Price Range with a volume column gets a panel of its own below its panel (ADR-0300).
    const specs: PanelSpec[] = [];
    for (const name of panelNames) {
        specs.push({ name, volumeOf: null });
        for (const s of visible.filter(v => v.panel === name && v.kind === 'priceRange' && v.bars.some(b => b.volume !== undefined && b.volume !== null))) {
            specs.push({ name: `${s.name} volume`, volumeOf: s });
        }
    }
    const swatch = lh * 1.5;
    const entryWidth = (s: ChartSeries): number => swatch + pad / 2 + m.textWidth(legendName(s)) + pad;
    const rows = specs.map(spec => (spec.volumeOf === null && (legendPosition === 'top' || legendPosition === 'bottom')
        ? legendRows(legendSeries(definition.series, legendKeys, spec.name), entryWidth, plotWidth)
        : []));
    const band = (i: number): number => {
        const count = elementAt(rows, i).length;
        return count === 0 ? 0 : count * lh + pad / 2;
    };
    const weight = (spec: PanelSpec): number => (spec.volumeOf === null ? 1 : volumeShare);

    const bands = specs.reduce((sum, _, i) => sum + band(i), 0);
    const unit = specs.length === 0 ? 0
        : Math.max(1, (m.height - 2 * pad - ticksBand - bands - m.panelGap * (specs.length - 1)) / specs.reduce((sum, spec) => sum + weight(spec), 0));

    // Candles merged into the market's own periods where the plot is too narrow for each (ADR-0302).
    const merged = new Map<string, Merge>();
    for (const s of visible.filter(v => v.kind === 'priceRange')) {
        const { candles, period, unfit } = mergeCandles(s, from, to, devicePixels);
        merged.set(s.id, { candles, period });
        if (unfit !== undefined) {
            notes.push(unfit);
        } else if (period !== 'row') {
            notes.push(`${s.name}: ${periodName(period).toLowerCase()} candles, merged from rows${s.mergeTo === undefined ? ' to fit' : ''}.`);
        }
    }
    const mergedOf = (s: ChartSeries): Merge => defined(merged.get(s.id), `the candles of '${s.id}'`);
    // An interval's meaning, in the legend and here (ADR-0303).
    for (const s of visible) {
        if (s.interval !== undefined) {
            notes.push(`${s.name}: the ${s.xInterval === undefined ? '' : 'vertical '}interval is ${s.interval.meaning}.`);
        }
    }
    for (const s of visible) {
        if (s.xInterval !== undefined) {
            notes.push(`${s.name}: the horizontal interval is ${s.xInterval.meaning}.`);
        }
    }
    const rowWidth = rowWidthOf(visible, map, from, to, plotWidth);
    if (axis.packed) {
        notes.push('Dates are packed: the days nobody traded are left out, so the spacing is sessions, not calendar time.');
    }

    const visibleIds = new Set(visible.map(s => s.id));
    const panels: PanelScene[] = [];
    const marks: ChartMark[] = [];
    const edgeMarks: ChartEdgeMark[] = [];
    let y = pad;
    for (let pi = 0; pi < specs.length; pi++) {
        const spec = elementAt(specs, pi);
        const { name, volumeOf } = spec;
        const panelHeight = weight(spec) * unit;
        const last = pi === specs.length - 1;
        let plotY: number;
        let legendY: number;
        if (legendPosition === 'top') {
            legendY = y;
            plotY = y + band(pi);
            y = plotY + panelHeight;
        } else {
            plotY = y;
            legendY = plotY + panelHeight + (last ? ticksBand : pad / 2);
            y = plotY + panelHeight + band(pi);
        }
        y += m.panelGap;

        const plot: Box = Object.freeze({ x: plotLeft, y: plotY, width: plotWidth, height: panelHeight });
        if (volumeOf !== null) {
            panels.push(volumePanel(name, volumeOf, lookOf(volumeOf), mergedOf(volumeOf).candles, map, rowWidth, plot));
            continue;
        }
        const members = visible.filter(s => s.panel === name);
        const drawings = new Map(members.map(s => [s.id, s.kind === 'priceRange'
            ? seriesDrawing([], [], { candles: mergedOf(s).candles })
            : drawSeries(s, axis, { from, to, pixels: devicePixels, quotes: quotesOf(s, visible) })] as const));
        const drawingOf = (s: ChartSeries): SeriesDrawing => defined(drawings.get(s.id), `the drawing of '${s.id}'`);
        const { left, right, independent } = verticalAxes(members, drawingOf, from, to, plot, independentPanels.has(name), logY, view.yFrom, view.yTo);
        if (independent) {
            notes.push(panelNames.length > 1 && name.length > 0
                ? `${name}: the left and right axes put zero at different heights.`
                : 'The left and right axes put zero at different heights.');
        }
        const pieces = members.map(s => piece(s, lookOf(s), marked(s, visible), drawingOf(s),
            axisFor(s, left, right), map, plot, from, to, m.devicePixelRatio, devicePixels, rowWidth,
            maxRadius(lh), s.kind === 'priceRange' ? mergedOf(s).period : 'row'));
        if (scatter) {
            for (const p of pieces.filter(q => q.kind === 'observation')) {
                for (const mk of p.markers) {
                    marks.push(Object.freeze({ seriesId: p.id, panel: pi, x: mk.x, y: mk.y, point: mk.point, count: mk.count }));
                }
            }
        }
        for (const p of pieces) {
            for (const e of p.edges) {
                edgeMarks.push(Object.freeze({ seriesId: p.id, panel: pi, edge: e }));
            }
        }
        const zero = left !== null && left.min <= 0 && left.max >= 0 ? yOf(left, 0, plot)
            : right !== null && right.min <= 0 && right.max >= 0 ? yOf(right, 0, plot) : null;
        const entries = legendOf(legendSeries(definition.series, legendKeys, name), elementAt(rows, pi), legendPosition, legendAlign, m, plot, legendY, lookOf,
            s => definition.series.filter(o => keyOf(o) === keyOf(s)).every(o => !visibleIds.has(o.id)),
            keyOf, entryWidth);
        panels.push(Object.freeze({ name, plot, left, right, zeroY: zero, series: Object.freeze(pieces), legend: entries, volumeOf: null }));
    }

    for (const s of visible) {
        for (const g of s.segments) {
            notes.push(`${s.name}: ${xLabel(axis, g.from)}–${xLabel(axis, g.to)} ${g.method}${g.extrapolated === true ? ' (extrapolated)' : ''}`);
        }
    }
    for (const s of visible) {
        notes.push(...pointScaleNotes(s));
    }
    const plots = panels.filter(p => p.volumeOf === null);
    const firstPlot = plots[0];
    const lastPlot = plots[plots.length - 1];
    const [colourKeys, sizeKeys] = firstPlot === undefined || lastPlot === undefined ? [[], []] : pointKeys(visible,
        plotLeft + plotWidth + (visible.some(s => s.axis === 'right') ? yAxisWidth(m) : 0) + keyGap(m),
        firstPlot.plot.y, lastPlot.plot.y + lastPlot.plot.height - firstPlot.plot.y, m);
    if (coloursRepeat) {
        notes.push('Colours repeat: there are more series than colours. The legend and the tooltip name each series.');
    }

    return Object.freeze({
        panels: Object.freeze(panels),
        xTicks: Object.freeze(xTicks.ticks
            .map(t => Object.freeze([t.position, tickMap(t.position), t.label] as const))
            .filter(t => Number.isFinite(t[1]))),
        xFrom: from,
        xTo: to,
        notes: Object.freeze(notes),
        positions: Object.freeze(positions),
        scatter,
        marks: Object.freeze(marks),
        edgeMarks: Object.freeze(edgeMarks),
        logarithmicX: logX,
        colourKeys: Object.freeze(colourKeys),
        sizeKeys: Object.freeze(sizeKeys),
        xTitle: titleOf(axis, [...merged].map(([id, mg]) => [mg.period, defined(visible.find(s => s.id === id), id).mergeTo !== undefined] as const)),
    });
}

/**
 * The identities of the series the legend entry keyed `key` stands for (ADR-0257): the quotes and their
 * Interpolated curves for a pair's entry, otherwise the series itself. Showing the entry again shows each
 * of them, however one came to be hidden.
 */
export function legendMembers(definition: ChartDefinition, key: string): readonly string[] {
    const keys = legendKeysOf(definition.series);
    return Object.freeze(definition.series.filter(s => keys.get(s.id) === key).map(s => s.id));
}

const ordinal = (a: string, b: string): number => (a < b ? -1 : a > b ? 1 : 0);

// Doubles in .NET's order, which puts NaN first.
function byNumber(a: number, b: number): number {
    if (a < b) {
        return -1;
    }
    if (a > b) {
        return 1;
    }
    if (a === b) {
        return 0;
    }
    return Number.isNaN(a) ? (Number.isNaN(b) ? 0 : -1) : 1;
}

// The first index whose value is at or after `x`, in a list in order.
function lowerBound(list: readonly number[], x: number): number {
    let lo = 0;
    let hi = list.length;
    while (lo < hi) {
        const mid = (lo + hi) >>> 1;
        if (elementAt(list, mid) < x) {
            lo = mid + 1;
        } else {
            hi = mid;
        }
    }
    return lo;
}

// A series a logarithmic axis across cannot place: a position at or below zero.
function reachesZeroAcross(s: ChartSeries): boolean {
    return s.points.some(p => !(p.x > 0)) || (s.sampler !== undefined && s.sampler.from <= 0);
}

function notLogarithmic(s: ChartSeries | undefined, which: string, notNumbers: boolean): string {
    if (notNumbers) {
        return `The axis ${which} stays proportional: only numbers can be placed on a logarithmic axis.`;
    }
    // The C# engine names no series where none of the visible ones has such a value (a hidden one does),
    // and the shared cases hold the two engines to one sentence.
    const name = s === undefined ? '' : s.name;
    return `The axis ${which} stays proportional: '${name}' has a value at or below zero, which a logarithmic axis cannot place.`;
}

// A vertical log axis is refused, by name, by a series with a value at or below zero, or one that always
// reaches zero (ADR-0290). A value is never dropped to make the axis fit.
function logarithmicRefusal(visible: readonly ChartSeries[], axis: ChartAxis, from: number, to: number, devicePixels: number): string | undefined {
    for (const s of visible) {
        if (s.kind === 'amountPerPeriod') {
            return `The vertical axis stays proportional: '${s.name}' is an Amount per period, which is drawn from zero.`;
        }
        // A function is judged by the samples it would be drawn from, such as a fitted line that crosses
        // zero inside its range.
        if (s.sampler !== undefined) {
            const sample = drawSeries(s, axis, { from, to, pixels: devicePixels, quotes: quotesOf(s, visible) }).lines.flat().find(p => !(p.y > 0));
            if (sample !== undefined) {
                return `The vertical axis stays proportional: '${s.name}' reaches ${upToPlaces(sample.y, 4)} at ${writePosition(axis, sample.x)}, which a logarithmic axis cannot place.`;
            }
        } else {
            const point = s.points.find(p => p.y !== null && !(toNumber(p.y) > 0));
            if (point !== undefined && point.y !== null) {
                return `The vertical axis stays proportional: '${s.name}' has ${writeValue(s, point.y)} at ${writePosition(axis, point.x)}, which a logarithmic axis cannot place.`;
            }
        }
        const low = lowBound(s.interval);
        if (low !== undefined) {
            const meaning = defined(s.interval, 'the interval').meaning;
            return `The vertical axis stays proportional: '${s.name}' has a bound of ${upToPlaces(low[1], 4)} in its ${meaning} at ${writePosition(axis, low[0])}, which a logarithmic axis cannot place.`;
        }
    }
    return undefined;
}

/**
 * What both the scene and the files read (C#'s `ChartScene.Frame`): the frame of `definition` at `metrics`
 * narrowed by `view`, its plot as wide as with a legend above or below it, which is how the files take it.
 */
export function frameOf(definition: ChartDefinition, metrics: ChartMetrics, view: ChartView): Frame {
    const { hidden = [] } = view;
    // Only a legend at the left or the right narrows the plot; C#'s files pass none, which is the top's width.
    return frame(definition, resolveMetrics(metrics), view, new Set<string>(hidden), 'top');
}

// The axis as the viewer switched it, the visible series, the range shown, and the plot's span in pixels
// and in device pixels.
function frame(definition: ChartDefinition, m: ChartMetricsResolved, view: ChartView, hidden: ReadonlySet<string>, legend: LegendPosition): Frame {
    const axis = axisOf(definition, view);
    const keys = legendKeysOf(definition.series);
    const visible = definition.series.filter(s => !hidden.has(s.id) && !hidden.has(defined(keys.get(s.id), s.id)));
    const [from, to] = domain(axis, visible, view);
    const pad = m.padding;
    const column = (legend === 'left' || legend === 'right') && definition.series.length > 0
        ? greatest(definition.series.map(s => m.lineHeight * 1.5 + pad / 2 + m.textWidth(legendName(s)) + pad))
        : 0;
    const plotLeft = pad + (legend === 'left' ? column : 0) + yAxisWidth(m);
    const plotRight = m.width - pad - (legend === 'right' ? column : 0)
        - (visible.some(s => s.axis === 'right') ? yAxisWidth(m) : 0)
        - keyColumnWidth(visible, m);
    const plotWidth = Math.max(1, plotRight - plotLeft);
    return { axis, visible, from, to, plotLeft, plotWidth, devicePixels: Math.ceil(plotWidth * m.devicePixelRatio) };
}

// The axis with another scale, or another list of named positions.
function rescaled(axis: ChartAxis, scale: AxisScale, named: ChartAxis['named'] = axis.named): ChartAxis {
    return Object.freeze({ ...axis, scale, effectiveScale: scale, named });
}

// The axis as the viewer switched it (ADR-0251). A Number or Date axis drawn as categories with no named
// positions takes each position a series has, written as the axis writes it.
function axisOf(d: ChartDefinition, view: ChartView): ChartAxis {
    let axis = d.axis;
    // A packed axis is the candles' positions as categories (ADR-0301).
    if (axis.packed && view.scale !== 'proportional') {
        return rescaled(axis, 'categories', Object.freeze(packedSessions(d.series).toSorted(byNumber)
            .map(x => Object.freeze([x, writePosition(d.axis, x)] as const))));
    }
    if (axis.packed) {
        axis = Object.freeze({ ...axis, packed: false });
    }
    const scale = view.scale;
    if (scale === undefined || scale === axis.effectiveScale || axis.values === 'labels') {
        return axis;
    }
    if (scale === 'categories' && d.series.some(s => s.sampler !== undefined || s.kind === 'observation')) {
        return axis;
    }
    if (scale === 'logarithmic' && (axis.values !== 'numbers'
        || d.series.some(s => reachesZeroAcross(s) || lowBound(s.xInterval) !== undefined))) {
        return axis;
    }
    if (scale === 'categories' && axis.named.length === 0) {
        const named = axis;
        return rescaled(axis, scale, Object.freeze([...new Set(d.series.flatMap(s => s.points.map(p => p.x)))].toSorted(byNumber)
            .map(x => Object.freeze([x, xLabel(named, x)] as const))));
    }
    return rescaled(axis, scale);
}

// The positions the pointer can be over: each point a visible series has in range, every slot of a
// category axis, and for a sampled series one position per CSS pixel, which is as finely as the pointer
// can tell them apart.
function positionsOf(visible: readonly ChartSeries[], from: number, to: number, plotWidth: number, map: Map1, categories: readonly number[] | undefined): PointerPosition[] {
    const xs = categories === undefined
        ? new Set(visible.flatMap(s => s.points.map(p => p.x)).filter(x => x >= from && x <= to))
        : new Set(categories);
    if (categories === undefined) {
        for (const s of visible) {
            const f = s.sampler;
            if (f === undefined) {
                continue;
            }
            const n = Math.max(1, Math.ceil(plotWidth));
            for (let c = 0; c <= n; c++) {
                const x = to === from ? from : from + (to - from) * c / n;
                if (x >= f.from && x <= f.to) {
                    xs.add(x);
                }
            }
        }
    }
    return [...xs].map(x => Object.freeze([x, map(x)] as const)).toSorted((a, b) => (byNumber(a[1], b[1]) === 0 ? byNumber(a[0], b[0]) : byNumber(a[1], b[1])));
}

// Top and bottom legends wrap their entries into rows the plot's width.
function legendRows(series: readonly ChartSeries[], width: (s: ChartSeries) => number, plotWidth: number): ChartSeries[][] {
    const rows: ChartSeries[][] = [];
    let used = 0;
    for (const s of series) {
        const w = width(s);
        const current = rows[rows.length - 1];
        if (current === undefined || (used + w > plotWidth && current.length > 0)) {
            rows.push([s]);
            used = w;
            continue;
        }
        current.push(s);
        used += w;
    }
    return rows;
}

function legendOf(series: readonly ChartSeries[], rows: readonly (readonly ChartSeries[])[], position: LegendPosition, align: LegendAlign, m: ChartMetricsResolved,
    plot: Box, legendY: number, lookOf: (s: ChartSeries) => SeriesLook, hidden: (s: ChartSeries) => boolean, key: (s: ChartSeries) => string,
    width: (s: ChartSeries) => number): readonly LegendEntry[] {
    const lh = m.lineHeight;
    const pad = m.padding;
    const entries: LegendEntry[] = [];
    const entry = (s: ChartSeries, x: number, y: number): LegendEntry => Object.freeze({
        id: key(s), name: legendName(s), look: lookOf(s), hidden: hidden(s), box: Object.freeze({ x, y, width: width(s), height: lh }),
    });
    const offset = (space: number, used: number): number => {
        switch (align) {
            case 'centre':
                return (space - used) / 2;
            case 'end':
                return space - used;
            case 'start':
                return 0;
            default:
                return unreachable(align);
        }
    };
    switch (position) {
        case 'top':
        case 'bottom':
            rows.forEach((row, r) => {
                let x = plot.x + Math.max(0, offset(plot.width, row.reduce((sum, s) => sum + width(s), 0)));
                for (const s of row) {
                    entries.push(entry(s, x, legendY + r * lh));
                    x += width(s);
                }
            });
            break;
        case 'left':
        case 'right': {
            const column = series.length === 0 ? 0 : greatest(series.map(width));
            const x0 = position === 'left' ? pad : m.width - pad - column;
            let y = plot.y + Math.max(0, offset(plot.height, series.length * lh));
            for (const s of series) {
                entries.push(entry(s, x0, y));
                y += lh;
            }
            break;
        }
        case 'none':
            break;
        default:
            return unreachable(position);
    }
    return Object.freeze(entries);
}

const yAxisWidth = (m: ChartMetricsResolved): number => m.textWidth('-0,000.00') + m.padding;

// The categories in range, each with its slot: evenly spaced, in order.
function categorySlots(axis: ChartAxis, from: number, to: number): Map<number, number> {
    const positions = axis.values === 'labels' ? axis.labels.map((_, i) => i) : axis.named.map(n => n[0]);
    const slots = new Map<number, number>();
    for (const x of positions.filter(p => p >= from && p <= to).toSorted(byNumber)) {
        if (slots.has(x)) {
            throw new Error(`Internal: the position ${String(x)} is named twice.`);
        }
        slots.set(x, slots.size);
    }
    return slots;
}

function domain(axis: ChartAxis, visible: readonly ChartSeries[], view: ChartView): readonly [from: number, to: number] {
    if (axis.effectiveScale === 'categories') {
        // Positions stay in their own units; only the map spaces them evenly.
        const [first, last] = axis.values === 'labels' ? [0, Math.max(0, axis.labels.length - 1)]
            : axis.named.length === 0 ? [0, 0] : [least(axis.named.map(n => n[0])), greatest(axis.named.map(n => n[0]))];
        return [view.from ?? first, view.to ?? last];
    }
    const xs = visible.flatMap(s => [
        ...s.points.map(p => p.x),
        ...(s.sampler === undefined ? [] : [s.sampler.from, s.sampler.to]),
        ...boundValues(s.xInterval),
    ]);
    const log = axis.effectiveScale === 'logarithmic';
    let lo = xs.length > 0 ? least(xs) : log ? 1 : 0;
    let hi = xs.length > 0 ? greatest(xs) : log ? 10 : 1;
    // A scatter's marks are not drawn on the plot's edge: its bounds are rounded out, to whole decades on
    // a logarithmic axis (ADR-0290).
    if (visible.some(s => s.kind === 'observation') && axis.values === 'numbers') {
        const n = nice(lo, hi, 6);
        [lo, hi] = log ? decades(lo, hi) : [n.min, n.max];
    }
    return [view.from ?? lo, view.to ?? hi];
}

// The key each series' legend entry hides it by, by its identity (ADR-0257): the quotes and the
// Interpolated curves of one panel sharing a colour key are one entry, keyed by the quotes' identity, so no
// key can name another series; any other series is its own. Two quote series of one colour key are not
// guessed between: each keeps its entry, as its curves do (spine rule 1).
function legendKeysOf(all: readonly ChartSeries[]): Map<string, string> {
    const keys = new Map(all.map(s => [s.id, s.id] as const));
    const groups = new Map<string, ChartSeries[]>();
    for (const s of all) {
        // The panel and the colour key, joined by a character neither can hold unseen.
        const group = JSON.stringify([s.panel, s.effectiveKeys.colour]);
        const members = groups.get(group);
        if (members === undefined) {
            groups.set(group, [s]);
        } else {
            members.push(s);
        }
    }
    for (const shared of groups.values()) {
        const quotes = shared.filter(s => !s.interpolated);
        const only = quotes[0];
        if (shared.some(s => s.interpolated) && quotes.length === 1 && only !== undefined) {
            for (const s of shared) {
                keys.set(s.id, only.id);
            }
        }
    }
    return keys;
}

// The series a panel's legend has an entry for: one per legend key, the quotes standing for a pair.
function legendSeries(all: readonly ChartSeries[], keys: ReadonlyMap<string, string>, panel: string): ChartSeries[] {
    const order: string[] = [];
    for (const s of all) {
        const key = defined(keys.get(s.id), s.id);
        if (s.panel === panel && !order.includes(key)) {
            order.push(key);
        }
    }
    return order.map(key => defined(all.find(s => s.panel === panel && s.id === key), `the series '${key}'`));
}

/** The quotes an Interpolated series is paired with: the positions of the series that share its colour key. */
export function quotesOf(s: ChartSeries, visible: readonly ChartSeries[]): number[] {
    return s.sampler === undefined ? []
        : visible.filter(o => o.id !== s.id && !o.interpolated && o.effectiveKeys.colour === s.effectiveKeys.colour).flatMap(o => o.points.map(p => p.x));
}

function verticalAxes(members: readonly ChartSeries[], drawingOf: (s: ChartSeries) => SeriesDrawing, from: number, to: number, plot: Box,
    declaredIndependent: boolean, logarithmic: boolean, yFrom: number | undefined, yTo: number | undefined):
    { left: VerticalAxis | null; right: VerticalAxis | null; independent: boolean } {
    const inRange = (x: number): boolean => x >= from && x <= to;
    const range = (side: AxisSide): AxisRange | undefined => {
        const ss = members.filter(s => s.axis === side);
        if (ss.length === 0) {
            return undefined;
        }
        const ys = ss.flatMap(s => {
            const d = drawingOf(s);
            return [
                ...d.lines.flat().filter(p => inRange(p.x)).map(p => p.y),
                ...d.markers.filter(mk => inRange(mk.x)).flatMap(mk => (mk.yBounds === undefined ? [mk.y] : [mk.y, mk.yBounds[0], mk.yBounds[1]])),
                ...d.candles.flatMap(c => [c.low, c.high]),
                ...[...d.whiskers, ...d.bands.flat()].filter(w => inRange(w.x)).flatMap(w => [w.lower, w.upper]),
            ];
        });
        let lo = ys.length > 0 ? least(ys) : logarithmic ? 1 : 0;
        let hi = ys.length > 0 ? greatest(ys) : logarithmic ? 10 : 1;
        // The left axis narrowed by a rectangle shows what was asked, unrounded (ADR-0290).
        const narrowed = side === 'left' && (yFrom !== undefined || yTo !== undefined);
        if (narrowed) {
            lo = yFrom ?? lo;
            hi = yTo ?? hi;
        }
        if (logarithmic) {
            const [dlo, dhi] = narrowed && lo > 0 && hi > lo ? [lo, hi] : decades(lo, hi);
            return { lo: dlo, hi: dhi, step: 0, zero: false };
        }
        if (narrowed && hi > lo) {
            return { lo, hi, step: nice(lo, hi).step, zero: lo <= 0 && hi >= 0 };
        }
        if (ss.some(s => s.reachesZero)) {
            lo = Math.min(lo, 0);
            hi = Math.max(hi, 0);
        }
        const n = nice(lo, hi);
        return { lo: n.min, hi: n.max, step: n.step, zero: n.min <= 0 && n.max >= 0 };
    };
    let l = range('left');
    let r = range('right');
    // A logarithmic axis has no zero to align (ADR-0290).
    if (logarithmic) {
        return {
            left: l === undefined ? null : logAxis('left', l.lo, l.hi, plot),
            right: r === undefined ? null : logAxis('right', r.lo, r.hi, plot),
            independent: false,
        };
    }
    let independent = false;
    if (l !== undefined && r !== undefined) {
        if (declaredIndependent || !l.zero || !r.zero) {
            independent = true;
        } else {
            // Zero at the same height on both sides (ADR-0251): each axis is stretched until its share
            // below zero is the larger of the two.
            const share = Math.max(below(l.lo, l.hi), below(r.lo, r.hi));
            l = stretch(l, share);
            r = stretch(r, share);
        }
    }
    return {
        left: l === undefined ? null : linearAxis('left', l, plot),
        right: r === undefined ? null : linearAxis('right', r, plot),
        independent,
    };
}

// The axis a series reads on, which `verticalAxes` laid out because the series is there.
function axisFor(s: ChartSeries, left: VerticalAxis | null, right: VerticalAxis | null): VerticalAxis {
    const axis = s.axis === 'right' ? right : left;
    if (axis === null) {
        throw new Error(`Internal: '${s.id}' reads on the ${s.axis} axis, which was not laid out.`);
    }
    return axis;
}

const below = (lo: number, hi: number): number => (hi === lo ? 0 : -lo / (hi - lo));

function stretch(a: AxisRange, share: number): AxisRange {
    if (share >= 1) {
        return { lo: Math.min(a.lo, -a.step), hi: 0, step: a.step, zero: true };
    }
    if (below(a.lo, a.hi) < share) {
        return { lo: -share * a.hi / (1 - share), hi: a.hi, step: a.step, zero: true };
    }
    if (below(a.lo, a.hi) > share) {
        return { lo: a.lo, hi: -a.lo * (1 - share) / share, step: a.step, zero: true };
    }
    return a;
}

function linearAxis(side: AxisSide, a: AxisRange, plot: Box): VerticalAxis {
    const bare: VerticalAxis = { side, min: a.lo, max: a.hi, ticks: [], broken: !a.zero, logarithmic: false };
    const first = Math.ceil(a.lo / a.step - 1e-9) * a.step;
    const ticks: VerticalTick[] = [];
    for (let t = first; t <= a.hi + a.step * 1e-9; t += a.step) {
        // Never "-0".
        const value = roundToDigits(t, 12) + 0;
        ticks.push(Object.freeze([value, yOf(bare, value, plot), upToPlaces(value, 4)] as const));
    }
    return Object.freeze({ ...bare, ticks: Object.freeze(ticks) });
}

function logAxis(side: AxisSide, lo: number, hi: number, plot: Box): VerticalAxis {
    const bare: VerticalAxis = { side, min: lo, max: hi, ticks: [], broken: false, logarithmic: true };
    const ticks = logSteps(lo, hi).map(t => Object.freeze([t, yOf(bare, t, plot), upToPlaces(t, 4)] as const));
    return Object.freeze({ ...bare, ticks: Object.freeze(ticks) });
}

// A level's points are marked when its marker key asks for it, or when they are the quotes an
// Interpolated series is drawn through, which the markers tell apart from the curve (ADR-0257). A step's
// ends are always marked: filled and open say which end takes the value (ADR-0253).
function marked(s: ChartSeries, visible: readonly ChartSeries[]): boolean {
    return s.kind !== 'level' || s.keys?.marker !== undefined || s.interpolated
        || visible.some(o => o.id !== s.id && o.interpolated && o.effectiveKeys.colour === s.effectiveKeys.colour);
}

interface PieceParts {
    readonly lines?: readonly (readonly DrawnPoint[])[];
    readonly markers?: readonly DrawnMarker[];
    readonly extrapolated?: readonly (readonly [number, number])[];
    readonly thinned?: readonly PixelSummary[];
    readonly candles?: readonly PlacedCandle[];
    readonly period?: CandlePeriod;
    readonly volumes?: readonly PlacedVolume[];
    readonly whiskers?: readonly DrawnWhisker[];
    readonly bands?: readonly (readonly DrawnWhisker[])[];
    readonly intervalMeaning?: string | null;
    readonly colourDiverging?: boolean;
    readonly edges?: readonly DrawnEdge[];
}

function pieceOf(s: ChartSeries, look: SeriesLook, name: string, interpolated: boolean, parts: PieceParts): SeriesPiece {
    const {
        lines = [], markers = [], extrapolated = [], thinned = [], candles = [], period = 'row', volumes = [], whiskers = [], bands = [],
        intervalMeaning = null, colourDiverging = false, edges = [],
    } = parts;
    return Object.freeze({
        id: s.id, name, keys: s.effectiveKeys, look, kind: s.kind, interpolated,
        lines: Object.freeze(lines), markers: Object.freeze(markers), extrapolated: Object.freeze(extrapolated), thinned: Object.freeze(thinned),
        candles: Object.freeze(candles), period, volumes: Object.freeze(volumes), whiskers: Object.freeze(whiskers), bands: Object.freeze(bands),
        intervalMeaning, colourDiverging, edges: Object.freeze(edges),
    });
}

function piece(s: ChartSeries, look: SeriesLook, isMarked: boolean, drawing: SeriesDrawing, axis: VerticalAxis, map: Map1, plot: Box, from: number, to: number,
    dpr: number, devicePixels: number, rowWidth: number, radius: number, period: CandlePeriod): SeriesPiece {
    const y = (v: number): number => yOf(axis, v, plot);
    if (s.kind === 'priceRange') {
        return pieceOf(s, look, s.name, s.interpolated, {
            period,
            candles: drawing.candles.map(c => {
                const [x, half] = extent(c, map, rowWidth);
                return Object.freeze({
                    x, halfWidth: half, openY: c.open === null ? null : y(c.open), highY: y(c.high), lowY: y(c.low), closeY: y(c.close),
                    rising: c.rising, unchanged: c.unchanged, rows: c.rows, from: c.from, to: c.to,
                });
            }),
        });
    }

    const inX = (x: number): boolean => x >= from && x <= to;
    // What the axis does not show is not drawn: a mark outside it is left out, and a line is cut at its
    // edge (a rectangle narrows the left axis, ADR-0290).
    const reach = Math.abs(axis.max - axis.min) * 1e-9;
    const shown = (v: number): boolean => v >= axis.min - reach && v <= axis.max + reach;
    const inRange = seriesDrawing(
        drawing.lines.map(l => l.filter(p => inX(p.x))).filter(l => l.length > 1).flatMap(l => clipLine(l, axis.min, axis.max, axis.logarithmic)),
        drawing.markers.filter(mk => inX(mk.x) && shown(mk.y)));
    if (s.kind === 'observation') {
        const clampY = (v: number): number => y(Math.min(Math.max(v, axis.min), axis.max));
        const clampX = (v: number): number => map(Math.min(Math.max(v, from), to));
        return pieceOf(s, look, s.name, s.interpolated, {
            markers: mergedMarks(inRange.markers.map(mk => Object.freeze({
                ...mk,
                x: map(mk.x),
                y: y(mk.y),
                // A whisker stops at the plot's edge when the range is narrowed past it (ADR-0304).
                ...(mk.yBounds === undefined ? {} : { yBounds: ordered(clampY(mk.yBounds[0]), clampY(mk.yBounds[1])) }),
                ...(mk.xBounds === undefined ? {} : { xBounds: ordered(clampX(mk.xBounds[0]), clampX(mk.xBounds[1])) }),
                ...(mk.radius === undefined ? {} : { radius: Math.max(0.75, mk.radius * radius) }),
            })), dpr),
            colourDiverging: s.colourBy?.centre !== undefined,
            edges: edgePixels(s, map, y, plot),
        });
    }
    const toPixel = (x: number): number => (map(x) - plot.x) * dpr;
    const thinned = thin(inRange, toPixel, devicePixels);
    const [whiskers, bands] = intervals(
        drawing.whiskers.filter(w => inX(w.x)),
        drawing.bands.map(b => b.filter(w => inX(w.x))).filter(b => b.length > 0),
        toPixel, devicePixels);
    const px = (p: DrawnPoint): DrawnPoint => Object.freeze({ x: map(p.x), y: y(p.y) });
    const wpx = (w: DrawnWhisker): DrawnWhisker => Object.freeze({ x: map(w.x), lower: y(w.lower), upper: y(w.upper) });
    return pieceOf(s, look, s.name, s.interpolated, {
        lines: thinned.drawing.lines.map(l => Object.freeze(l.map(px))),
        markers: (isMarked ? thinned.drawing.markers : []).map(mk => Object.freeze({ ...mk, x: map(mk.x), y: y(mk.y) })),
        extrapolated: s.segments.filter(g => g.extrapolated === true).map(g => Object.freeze([map(Math.max(g.from, from)), map(Math.min(g.to, to))] as const)),
        thinned: thinnedThrough(s.thinned.filter(t => t.to >= from && t.from <= to), thinned.thinned, inRange.lines.flatMap(l => l.map(p => p.x))),
        whiskers: whiskers.map(wpx),
        bands: bands.map(b => Object.freeze(b.map(wpx))),
        intervalMeaning: s.interval === undefined ? null : s.interval.meaning,
    });
}

// Past one point a device pixel, whiskers become the band, thinned to the lowest lower and the highest upper
// per pixel column, so no interval is dropped where markers are (ADR-0303).
function intervals(inWhiskers: readonly DrawnWhisker[], inBands: readonly (readonly DrawnWhisker[])[], toPixel: Map1, devicePixels: number):
    readonly [whiskers: readonly DrawnWhisker[], bands: readonly (readonly DrawnWhisker[])[]] {
    let whiskers = inWhiskers;
    let bands = inBands;
    if (whiskers.length > devicePixels) {
        bands = [whiskers];
        whiskers = [];
    }
    if (bands.reduce((sum, b) => sum + b.length, 0) <= devicePixels) {
        return [whiskers, bands];
    }
    return [whiskers, bands.map(b => {
        const columns = new Map<number, DrawnWhisker[]>();
        for (const w of b) {
            const column = Math.floor(toPixel(w.x));
            const group = columns.get(column);
            if (group === undefined) {
                columns.set(column, [w]);
            } else {
                group.push(w);
            }
        }
        return [...columns.values()].map(g => Object.freeze({
            x: (elementAt(g, 0).x + elementAt(g, g.length - 1).x) / 2,
            lower: least(g.map(w => w.lower)),
            upper: greatest(g.map(w => w.upper)),
        }));
    })];
}

// A candle centred on what it stands for, 70% as wide: one row's width, or a merged candle's rows from first
// to last (ADR-0302).
function extent(c: DrawnCandle, map: Map1, rowWidth: number): readonly [x: number, half: number] {
    const a = map(c.from) - rowWidth / 2;
    const b = map(c.to) + rowWidth / 2;
    return [(a + b) / 2, Math.max(0.5, bodyShare / 2 * (b - a))];
}

// The width of one row in pixels: the narrowest gap between neighbouring candles.
function rowWidthOf(visible: readonly ChartSeries[], map: Map1, from: number, to: number, plotWidth: number): number {
    const xs = [...new Set(visible.filter(s => s.kind === 'priceRange').flatMap(s => s.bars.map(b => b.x)).filter(x => x >= from && x <= to))].toSorted(byNumber);
    if (xs.length < 2) {
        return plotWidth / 10;
    }
    let gap = Infinity;
    for (let i = 1; i < xs.length; i++) {
        gap = Math.min(gap, map(elementAt(xs, i)) - map(elementAt(xs, i - 1)));
    }
    return Number.isFinite(gap) && gap > 0 ? gap : plotWidth / xs.length;
}

// The first of the market's periods whose candles fit three device pixels each, or the Consumer's fixed
// one, which says so when it does not fit (ADR-0302).
function mergeCandles(s: ChartSeries, from: number, to: number, devicePixels: number): { candles: readonly DrawnCandle[]; period: CandlePeriod; unfit: string | undefined } {
    const fits = (count: number): boolean => count <= 1 || count * 3 <= devicePixels;
    if (s.mergeTo !== undefined) {
        const fixed = s.mergeTo;
        const candles = candlesOf(s, from, to, fixed);
        return fits(candles.length) ? { candles, period: fixed, unfit: undefined }
            : {
                candles: [], period: fixed,
                unfit: `${s.name}: ${grouped(candles.length)} ${fixed === 'row' ? 'rows' : `${periodName(fixed).toLowerCase()} candles`} do not fit; narrow the range.`,
            };
    }
    let count = 0;
    for (const period of candlePeriods) {
        const candles = candlesOf(s, from, to, period);
        if (fits(candles.length)) {
            return { candles, period, unfit: undefined };
        }
        count = candles.length;
    }
    // Not even years fit: nothing is drawn in slivers, and the chart says so (ADR-0302).
    return { candles: [], period: 'year', unfit: `${s.name}: ${grouped(count)} yearly candles do not fit; narrow the range or widen the chart.` };
}

function volumePanel(name: string, s: ChartSeries, look: SeriesLook, candles: readonly DrawnCandle[], map: Map1, rowWidth: number, plot: Box): PanelScene {
    // A candle with no volume raises no bar, so it does not raise the axis either.
    const max = candles.length === 0 ? 1 : candles.reduce((most, c) => (c.volume === null ? most : Math.max(most, c.volume)), 0);
    const n = nice(0, max === 0 ? 1 : max);
    const axis = linearAxis('left', { lo: n.min, hi: n.max, step: n.step, zero: true }, plot);
    const volumes = candles.flatMap(c => {
        if (c.volume === null) {
            return [];
        }
        const [x, half] = extent(c, map, rowWidth);
        return [Object.freeze({
            x, halfWidth: half, topY: yOf(axis, c.volume, plot), baseY: yOf(axis, 0, plot), rising: c.rising, unchanged: c.unchanged, rows: c.rows, from: c.from, to: c.to,
        })];
    });
    const p = pieceOf(s, look, name, false, { volumes });
    return Object.freeze({
        name, plot, left: axis, right: null, zeroY: yOf(axis, 0, plot), series: Object.freeze([p]), legend: Object.freeze([]), volumeOf: s.id,
    });
}

function titleOf(axis: ChartAxis, periods: readonly (readonly [period: CandlePeriod, fixed: boolean])[]): string {
    let title = axis.title.length > 0 ? axis.title : axis.packed ? 'Trading days' : periods.some(p => p[0] !== 'row') ? 'Date' : '';
    if (axis.packed) {
        title += ' (closed days packed)';
    }
    const distinct = [...new Map(periods.filter(p => p[0] !== 'row').map(p => [`${p[0]}|${String(p[1])}`, p] as const)).values()];
    const only = distinct[0];
    if (distinct.length === 1 && only !== undefined) {
        title += ` · ${periodName(only[0])} (merged from rows${only[1] ? '' : ' to fit'})`;
    }
    return title;
}

// A series' name in its legend, with its interval's meaning (ADR-0303).
function legendName(s: ChartSeries): string {
    const y = s.interval?.meaning;
    const x = s.xInterval?.meaning;
    if (y === undefined) {
        return x === undefined ? s.name : `${s.name} (x: ${x})`;
    }
    if (x === undefined || x === y) {
        return `${s.name} (${y})`;
    }
    return `${s.name} (y: ${y}; x: ${x})`;
}

// The bounds a series hands in, with values (ADR-0303, ADR-0304).
function boundValues(interval: SeriesInterval | undefined): number[] {
    return interval === undefined ? [] : interval.bounds.flatMap(b => [b.lower, b.upper].flatMap(v => (v === null ? [] : [toNumber(v)])));
}

// A bound a logarithmic axis cannot place: one at or below zero (ADR-0304).
function lowBound(interval: SeriesInterval | undefined): readonly [x: number, bound: number] | undefined {
    if (interval === undefined) {
        return undefined;
    }
    for (const b of interval.bounds) {
        for (const v of [b.lower, b.upper]) {
            if (v !== null && !(toNumber(v) > 0)) {
                return [b.x, toNumber(v)];
            }
        }
    }
    return undefined;
}

const ordered = (a: number, b: number): Bounds => Object.freeze([Math.min(a, b), Math.max(a, b)] as const);

// Merged marks carry the union of their intervals, so none is dropped (ADR-0304).
function union(a: Bounds | undefined, b: Bounds | undefined): Bounds | undefined {
    if (a === undefined) {
        return b;
    }
    return b === undefined ? a : Object.freeze([Math.min(a[0], b[0]), Math.max(a[1], b[1])] as const);
}

// Past the pixels, an Observation's marks centred on one device pixel are one mark, which says how many it
// stands for (ADR-0290). The first point drawn there names it.
function mergedMarks(marks: readonly DrawnMarker[], dpr: number): DrawnMarker[] {
    const at = new Map<string, number>();
    const kept: DrawnMarker[] = [];
    for (const mk of marks) {
        // Marks merge only where they look alike, so no colour or size is lost to a merge (ADR-0320).
        const cell = JSON.stringify([Math.floor(mk.x * dpr), Math.floor(mk.y * dpr), mk.band === undefined ? null : mk.band, mk.radius === undefined ? null : mk.radius, mk.filled]);
        const i = at.get(cell);
        if (i === undefined) {
            at.set(cell, kept.length);
            kept.push(mk);
            continue;
        }
        const k = elementAt(kept, i);
        const yBounds = union(k.yBounds, mk.yBounds);
        const xBounds = union(k.xBounds, mk.xBounds);
        kept[i] = Object.freeze({
            ...k,
            count: k.count + mk.count,
            ...(yBounds === undefined ? {} : { yBounds }),
            ...(xBounds === undefined ? {} : { xBounds }),
        });
    }
    return kept;
}

// A line cut where it leaves [lo, hi], each piece ending on the edge it crosses. On a logarithmic axis the
// segment is straight in the logarithm, so the crossing is found there.
function clipLine(line: readonly DrawnPoint[], lo: number, hi: number, logarithmic: boolean): (readonly DrawnPoint[])[] {
    const inside = (p: DrawnPoint): boolean => p.y >= lo && p.y <= hi;
    if (line.every(inside)) {
        return [line];
    }
    const t = (v: number): number => (logarithmic ? Math.log(v) : v);
    const cross = (a: DrawnPoint, b: DrawnPoint, edge: number): DrawnPoint =>
        Object.freeze({ x: a.x + (b.x - a.x) * (t(edge) - t(a.y)) / (t(b.y) - t(a.y)), y: edge });
    const pieces: (readonly DrawnPoint[])[] = [];
    let current: DrawnPoint[] = [];
    line.forEach((p, i) => {
        const pIn = inside(p);
        const q = line[i - 1];
        if (q !== undefined) {
            const qIn = inside(q);
            if (qIn && !pIn) {
                current.push(cross(q, p, p.y > hi ? hi : lo));
            } else if (!qIn && pIn) {
                current.push(cross(q, p, q.y > hi ? hi : lo));
            } else if ((q.y < lo && p.y > hi) || (q.y > hi && p.y < lo)) {
                current.push(cross(q, p, q.y > hi ? hi : lo));
                current.push(cross(q, p, q.y > hi ? lo : hi));
            }
        }
        if (pIn) {
            current.push(p);
        } else if (current.length > 0) {
            if (current.length > 1) {
                pieces.push(Object.freeze(current));
            }
            current = [];
        }
    });
    if (current.length > 1) {
        pieces.push(Object.freeze(current));
    }
    return pieces;
}

function xLabel(axis: ChartAxis, x: number): string {
    return axis.format === undefined && axis.values === 'numbers' && axis.named.every(n => n[0] !== x) ? tenor(x) : writePosition(axis, x);
}
