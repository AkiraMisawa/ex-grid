// The geometry a chart is laid out with, in CSS pixels (ADR-0027/0028), and what the viewer narrowed it to.
import type { AxisScale } from './model';
import { defaultColourSlots } from './appearance';

/** A rectangle in CSS pixels. */
export interface Box {
    /** Its left. */
    readonly x: number;
    /** Its top. */
    readonly y: number;
    /** Its width. */
    readonly width: number;
    /** Its height. */
    readonly height: number;
}

/** The geometry a chart is laid out with, in CSS pixels, resolved by the caller and never paired with a stylesheet literal. */
export interface ChartMetrics {
    /** The chart's width. */
    readonly width: number;
    /** The chart's height. */
    readonly height: number;
    /** Device pixels per CSS pixel, which Thinning and sampling count in (ADR-0090); 1 by default. */
    readonly devicePixelRatio?: number;
    /** A label's height; 14 by default. */
    readonly lineHeight?: number;
    /** The space around the plots; 8 by default. */
    readonly padding?: number;
    /** The space between panels; 16 by default. */
    readonly panelGap?: number;
    /** A label's estimated width; text is never measured (ADR-0021). By default 0.6 of the line height a character. */
    readonly textWidth?: (text: string) => number;
    /** How many colour slots the stylesheet defines: ten, unless a theme declares more (ADR-0256). */
    readonly colourSlots?: number;
}

/** The metrics with their defaults applied. */
export interface ChartMetricsResolved {
    readonly width: number;
    readonly height: number;
    readonly devicePixelRatio: number;
    readonly lineHeight: number;
    readonly padding: number;
    readonly panelGap: number;
    readonly textWidth: (text: string) => number;
    readonly colourSlots: number;
}

/** The metrics with their defaults applied, as C#'s `ChartMetrics` holds them. */
export function resolveMetrics(metrics: ChartMetrics): ChartMetricsResolved {
    const { width, height, devicePixelRatio = 1, lineHeight = 14, padding = 8, panelGap = 16, colourSlots = defaultColourSlots } = metrics;
    const textWidth = metrics.textWidth === undefined ? (text: string): number => text.length * lineHeight * 0.6 : metrics.textWidth;
    return { width, height, devicePixelRatio, lineHeight, padding, panelGap, textWidth, colourSlots };
}

/** What the viewer narrowed a chart to — its Chart Filters — which narrow the drawing, never the definition. */
export interface ChartView {
    /** The identities of the series hidden. */
    readonly hidden?: ReadonlySet<string> | readonly string[];
    /** The first position shown; absent for the data's first. */
    readonly from?: number;
    /** The last position shown; absent for the data's last. */
    readonly to?: number;
    /** The scale the viewer switched the axis to; absent for the declared one (ADR-0251). */
    readonly scale?: AxisScale;
    /** The lowest value the left axis shows; absent for the data's (ADR-0290). */
    readonly yFrom?: number;
    /** The highest; absent for the data's. */
    readonly yTo?: number;
    /** The vertical axes' scale: `logarithmic`, or absent or `proportional` (ADR-0290). */
    readonly yScale?: AxisScale;
}
