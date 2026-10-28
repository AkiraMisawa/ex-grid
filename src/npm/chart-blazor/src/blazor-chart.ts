// Blazor's ExChart and ExSurface, the browser's half (ADR-0370 section 1): ExChart's Script Core mounted on the element
// the component renders, given the component's parameters as options and its definition as data, and every event it
// raises handed to .NET in the order it was raised. It decides nothing and computes nothing: the drawing, the tooltip,
// the legend, Chart Filters and what each gesture means are the Script Core's; the data is the Consumer's, in .NET.

import {
    createChart, createSurface, type AxisScale, type AxisStyle, type ChartDefinition, type ChartFileFormat, type ChartHandle, type ChartOptions,
    type ChartPointClick, type ChartRange, type LegendPlacement, type SeriesVisibility, type SurfaceDefinition, type SurfaceHandle, type SurfaceOptions,
    type SurfaceSlice, type SurfaceView,
} from '@ex-grid/chart';
import type { GridDensity } from '@ex-grid/core';
import { readDefinition, readSurface, type WireDefinition, type WireSurface } from './wire';

/** What .NET calls in the browser: `Heard` with each event the chart raised. */
export interface DotNetHandle {
    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown>;
}

/** The events .NET listens to: each one the component has a handler for, and no other. */
export type ChartHeard = 'seriesVisibilityChanged' | 'rangeChanged' | 'scaleChanged' | 'pointClick';

/** A chart's parameters, as the Script Core's options are spelt. .NET sends what the Consumer set and the Razor
 * component's defaults, so a chart drawn by either is drawn alike. */
export interface WireChartOptions {
    readonly width: number;
    readonly height: number;
    readonly density: GridDensity;
    readonly title: string | null;
    readonly description: string | null;
    readonly legend: LegendPlacement | null;
    readonly axisStyle: AxisStyle | null;
    readonly independentScales: readonly string[] | null;
    readonly colourSlots: number;
    readonly hiddenSeries: readonly string[] | null;
    readonly range: ChartRange | null;
    readonly scale: AxisScale | null;
    readonly yScale: AxisScale | null;
    readonly fileName: string | null;
    readonly hears: readonly ChartHeard[];
}

/** What `attachChart` and `update` are handed: the parameters, and the definition when the Consumer handed another. */
export interface WireChart {
    readonly options: WireChartOptions;
    /** The definition, sent with the attach and again only when the Consumer hands another. */
    readonly definition?: WireDefinition;
    /** Which of the Consumer's definitions it is, counted by .NET; with `definition`. A click carries it back, so .NET
     * reads its Row Key against the definition it was taken on, and refuses one taken on another. */
    readonly generation?: number;
}

/** What the component asks of the chart it attached. Each call carries its data as the JSON text .NET wrote. */
export interface BlazorChart {
    /** Takes a {@link WireChart}. */
    update(json: string): void;
    /** Saves the chart as a file, as the user sees it (`ChartHandle.download`). */
    download(format: ChartFileFormat): Promise<boolean>;
    /** Stops the chart and empties its element. */
    dispose(): void;
}

/**
 * Mounts ExChart's Script Core on `root`, the element the Blazor component rendered, with `dotnet` the component's
 * `DotNetObjectReference` and `json` a {@link WireChart} carrying the definition. What it answers is the component's
 * handle on the chart.
 */
export function attachChart(root: HTMLElement, dotnet: DotNetHandle, json: string): BlazorChart {
    const init = parsed<WireChart>(json);
    if (init.definition === undefined) throw new Error('Internal: .NET attached a chart without its definition.');
    let definition = readDefinition(init.definition);
    let generation = generationOf(init);
    const send = heard(dotnet);
    const handlers = chartHandlers(send, () => generation);
    const chart: ChartHandle = createChart(root, chartOptions(init.options, definition, handlers));
    return Object.freeze({
        update: (text: string) => {
            const next = parsed<WireChart>(text);
            // Taken only once the chart has: a definition it refuses changes nothing.
            const nextDefinition = next.definition === undefined ? definition : readDefinition(next.definition);
            chart.update(chartOptions(next.options, nextDefinition, handlers));
            definition = nextDefinition;
            if (next.definition !== undefined) generation = generationOf(next);
        },
        download: (format: ChartFileFormat) => chart.download(fileFormat(format)),
        dispose: () => {
            send.stop();
            chart.destroy();
        },
    });
}

/** The events a surface raises, which .NET listens to when it has a handler. */
export type SurfaceHeard = 'viewChanged' | 'sliceChanged';

/** A surface's parameters, as the Script Core's options are spelt. */
export interface WireSurfaceOptions {
    readonly width: number;
    readonly height: number;
    readonly sliceWidth: number;
    readonly density: GridDensity;
    readonly title: string | null;
    readonly description: string | null;
    readonly view: SurfaceView | null;
    /** The slice drawn beside the surface, its value as the axis holds it; `null` for none. */
    readonly slice: SurfaceSlice | null;
    readonly fileName: string | null;
    readonly hears: readonly SurfaceHeard[];
}

/** What `attachSurface` and `update` are handed: the parameters, and the definition when the Consumer handed another. */
export interface WireSurfaceUpdate {
    readonly options: WireSurfaceOptions;
    readonly definition?: WireSurface;
    /** Which of the Consumer's definitions it is; with `definition`. A slice carries it back, as a click does. */
    readonly generation?: number;
}

/** What the component asks of the surface it attached. */
export interface BlazorSurface {
    /** Takes a {@link WireSurfaceUpdate}. */
    update(json: string): void;
    /** Saves the drawing as it stands (`SurfaceHandle.download`). */
    download(format: ChartFileFormat): Promise<boolean>;
    /** The drawing as it stands as SVG text, or `null` once the surface is stopped. */
    svgText(): string | null;
    /** Stops the surface and empties its element. */
    dispose(): void;
}

/** Mounts a 3D surface on `root`, as {@link attachChart} mounts a chart. */
export function attachSurface(root: HTMLElement, dotnet: DotNetHandle, json: string): BlazorSurface {
    const init = parsed<WireSurfaceUpdate>(json);
    if (init.definition === undefined) throw new Error('Internal: .NET attached a surface without its definition.');
    let definition = readSurface(init.definition);
    let generation = generationOf(init);
    const send = heard(dotnet);
    const handlers: Required<Pick<SurfaceOptions, 'onViewChanged' | 'onSliceChanged'>> = Object.freeze({
        onViewChanged: (view: SurfaceView) => { send('viewChanged', view); },
        onSliceChanged: (slice: SurfaceSlice) => { send('sliceChanged', { generation, axis: slice.axis, value: slice.value }); },
    });
    const surface: SurfaceHandle = createSurface(root, surfaceOptions(init.options, definition, handlers));
    return Object.freeze({
        update: (text: string) => {
            const next = parsed<WireSurfaceUpdate>(text);
            const nextDefinition = next.definition === undefined ? definition : readSurface(next.definition);
            surface.update(surfaceOptions(next.options, nextDefinition, handlers));
            definition = nextDefinition;
            if (next.definition !== undefined) generation = generationOf(next);
        },
        download: (format: ChartFileFormat) => surface.download(fileFormat(format)),
        svgText: () => {
            const text = surface.svgText();
            return text === undefined ? null : text;
        },
        dispose: () => {
            send.stop();
            surface.destroy();
        },
    });
}

/** Hands an event to .NET, until the component stops: then .NET has let go of the object it calls, and nothing more
 * crosses. */
interface Send {
    (kind: string, payload: unknown): void;
    stop(): void;
}

function heard(dotnet: DotNetHandle): Send {
    let stopped = false;
    const send = (kind: string, payload: unknown): void => {
        if (!stopped) void dotnet.invokeMethodAsync('Heard', kind, payload);
    };
    return Object.assign(send, { stop: () => { stopped = true; } });
}

type ChartHandlers = Required<Pick<ChartOptions, 'onSeriesVisibilityChanged' | 'onRangeChanged' | 'onScaleChanged' | 'onPointClick'>>;

function generationOf(wire: { readonly generation?: number }): number {
    if (wire.generation === undefined) throw new Error('Internal: .NET sent a definition without its generation.');
    return wire.generation;
}

// One function per event, made once, so the chart sees the same handler on every update.
function chartHandlers(send: Send, generation: () => number): ChartHandlers {
    return Object.freeze({
        onSeriesVisibilityChanged: (change: SeriesVisibility) => { send('seriesVisibilityChanged', change); },
        onRangeChanged: (range: ChartRange | null) => { send('rangeChanged', range); },
        onScaleChanged: (scale: AxisScale) => { send('scaleChanged', scale); },
        // The Row Key is the JSON value .NET sent for it, and goes back as it came.
        onPointClick: (click: ChartPointClick) => {
            send('pointClick', { generation: generation(), seriesId: click.seriesId, position: click.position, row: click.row === undefined ? null : click.row });
        },
    });
}

function chartOptions(wire: WireChartOptions, definition: ChartDefinition, handlers: ChartHandlers): ChartOptions {
    const hears = new Set(wire.hears);
    return {
        definition,
        width: wire.width,
        height: wire.height,
        density: wire.density,
        ...(wire.title === null ? {} : { title: wire.title }),
        ...(wire.description === null ? {} : { description: wire.description }),
        ...(wire.legend === null ? {} : { legend: wire.legend }),
        ...(wire.axisStyle === null ? {} : { axisStyle: wire.axisStyle }),
        ...(wire.independentScales === null ? {} : { independentScales: wire.independentScales }),
        colourSlots: wire.colourSlots,
        ...(wire.hiddenSeries === null ? {} : { hiddenSeries: wire.hiddenSeries }),
        range: wire.range,
        ...(wire.scale === null ? {} : { scale: wire.scale }),
        ...(wire.yScale === null ? {} : { yScale: wire.yScale }),
        ...(wire.fileName === null ? {} : { fileName: wire.fileName }),
        ...(hears.has('seriesVisibilityChanged') ? { onSeriesVisibilityChanged: handlers.onSeriesVisibilityChanged } : {}),
        ...(hears.has('rangeChanged') ? { onRangeChanged: handlers.onRangeChanged } : {}),
        ...(hears.has('scaleChanged') ? { onScaleChanged: handlers.onScaleChanged } : {}),
        ...(hears.has('pointClick') ? { onPointClick: handlers.onPointClick } : {}),
    };
}

function surfaceOptions(
    wire: WireSurfaceOptions, definition: SurfaceDefinition, handlers: Required<Pick<SurfaceOptions, 'onViewChanged' | 'onSliceChanged'>>,
): SurfaceOptions {
    const hears = new Set(wire.hears);
    return {
        definition,
        width: wire.width,
        height: wire.height,
        sliceWidth: wire.sliceWidth,
        density: wire.density,
        ...(wire.title === null ? {} : { title: wire.title }),
        ...(wire.description === null ? {} : { description: wire.description }),
        view: wire.view,
        slice: wire.slice,
        ...(wire.fileName === null ? {} : { fileName: wire.fileName }),
        ...(hears.has('viewChanged') ? { onViewChanged: handlers.onViewChanged } : {}),
        ...(hears.has('sliceChanged') ? { onSliceChanged: handlers.onSliceChanged } : {}),
    };
}

/** A file format as .NET names it, checked at the boundary. */
function fileFormat(format: unknown): ChartFileFormat {
    if (format === 'csv' || format === 'svg' || format === 'png') return format;
    throw new TypeError(`Internal: .NET asked for a file of the format ${JSON.stringify(format)}.`);
}

/** JSON .NET wrote, in a shape the adapter's own C# writes, member for member. */
// oxlint-disable-next-line typescript/no-unnecessary-type-parameters -- the caller names the shape .NET wrote, and so the one assertion is here
export function parsed<T>(json: string): T {
    if (typeof json !== 'string') throw new TypeError('Internal: .NET sent the bridge something other than JSON text.');
    return JSON.parse(json) as T; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the shapes ExChart.Script writes; the Script Core checks what it is handed
}
