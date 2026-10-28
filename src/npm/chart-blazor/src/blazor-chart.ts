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
import { ScriptFunctionError, ScriptModules, type ScriptCalls, type WireScript } from './scripts';
import { readDefinition, readSurface, scriptsOf, surfaceScriptsOf, type WireDefinition, type WireSurface } from './wire';

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
    /** Takes a {@link WireChart}, once every update before it is taken. */
    update(json: string): Promise<void>;
    /** Saves the chart as a file, as the user sees it (`ChartHandle.download`); false while the chart is refused. */
    download(format: ChartFileFormat): Promise<boolean>;
    /** Stops the chart and empties its element. */
    dispose(): void;
}

/**
 * Mounts ExChart's Script Core on `root`, the element the Blazor component rendered, with `dotnet` the component's
 * `DotNetObjectReference` and `json` a {@link WireChart} carrying the definition. What it answers is the component's
 * handle on the chart, once the modules of the Consumer's functions the definition names are loaded (ADR-0420 section 3).
 */
export async function attachChart(root: HTMLElement, dotnet: DotNetHandle, json: string): Promise<BlazorChart> {
    const init = parsed<WireChart>(json);
    if (init.definition === undefined) throw new Error('Internal: .NET attached a chart without its definition.');
    let generation = generationOf(init);
    const send = heard(dotnet);
    const handlers = chartHandlers(send, () => generation);
    const place = new Place<ChartHandle, ChartOptions>(root, (options: ChartOptions) => createChart(root, options));
    // The definition drawn, or none while the chart is refused in its place. It and its generation move only once the
    // chart has drawn it: until then a click is on the definition still drawn (spine rule 6).
    let definition: ChartDefinition | undefined;
    const take = async (next: WireChart): Promise<void> => {
        const size = next.options;
        let drawing = definition;
        let drawn = generation;
        if (next.definition !== undefined) {
            const wire = next.definition;
            drawn = generationOf(next);
            drawing = await place.read(() => scriptsOf(wire), calls => readDefinition(wire, calls), size);
            if (drawing === undefined) {
                definition = undefined;
                return;
            }
        } else if (place.refused) {
            drawing = undefined;
        }
        if (drawing === undefined) {
            place.resize(size);
            return;
        }
        const ready = drawing;
        if (place.draw(() => chartOptions(next.options, ready, handlers), size)) {
            definition = ready;
            generation = drawn;
        } else {
            definition = undefined;
        }
    };
    await take(init);
    return Object.freeze({
        update: (text: string) => place.queue(() => take(parsed<WireChart>(text))),
        download: (format: ChartFileFormat) => {
            const checked = fileFormat(format);
            return place.drawing === undefined ? Promise.resolve(false) : place.drawing.download(checked);
        },
        dispose: () => {
            send.stop();
            place.destroy();
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
    /** Takes a {@link WireSurfaceUpdate}, once every update before it is taken. */
    update(json: string): Promise<void>;
    /** Saves the drawing as it stands (`SurfaceHandle.download`); false while the surface is refused. */
    download(format: ChartFileFormat): Promise<boolean>;
    /** The drawing as it stands as SVG text, or `null` once the surface is stopped or while it is refused. */
    svgText(): string | null;
    /** Stops the surface and empties its element. */
    dispose(): void;
}

/** Mounts a 3D surface on `root`, as {@link attachChart} mounts a chart. */
export async function attachSurface(root: HTMLElement, dotnet: DotNetHandle, json: string): Promise<BlazorSurface> {
    const init = parsed<WireSurfaceUpdate>(json);
    if (init.definition === undefined) throw new Error('Internal: .NET attached a surface without its definition.');
    let generation = generationOf(init);
    const send = heard(dotnet);
    const handlers: Required<Pick<SurfaceOptions, 'onViewChanged' | 'onSliceChanged'>> = Object.freeze({
        onViewChanged: (view: SurfaceView) => { send('viewChanged', view); },
        onSliceChanged: (slice: SurfaceSlice) => { send('sliceChanged', { generation, axis: slice.axis, value: slice.value }); },
    });
    const place = new Place<SurfaceHandle, SurfaceOptions>(root, (options: SurfaceOptions) => createSurface(root, options));
    let definition: SurfaceDefinition | undefined;
    const take = async (next: WireSurfaceUpdate): Promise<void> => {
        const size = { width: next.options.width + next.options.sliceWidth, height: next.options.height };
        let drawing = definition;
        let drawn = generation;
        if (next.definition !== undefined) {
            const wire = next.definition;
            drawn = generationOf(next);
            drawing = await place.read(() => surfaceScriptsOf(wire), calls => readSurface(wire, calls), size);
            if (drawing === undefined) {
                definition = undefined;
                return;
            }
        } else if (place.refused) {
            drawing = undefined;
        }
        if (drawing === undefined) {
            place.resize(size);
            return;
        }
        const ready = drawing;
        if (place.draw(() => surfaceOptions(next.options, ready, handlers), size)) {
            definition = ready;
            generation = drawn;
        } else {
            definition = undefined;
        }
    };
    await take(init);
    return Object.freeze({
        update: (text: string) => place.queue(() => take(parsed<WireSurfaceUpdate>(text))),
        download: (format: ChartFileFormat) => {
            const checked = fileFormat(format);
            return place.drawing === undefined ? Promise.resolve(false) : place.drawing.download(checked);
        },
        svgText: () => {
            const text = place.drawing?.svgText();
            return text === undefined ? null : text;
        },
        dispose: () => {
            send.stop();
            place.destroy();
        },
    });
}

/** The drawing's size, which the refusal said in its place takes. */
interface Size {
    readonly width: number;
    readonly height: number;
}

/**
 * The element a chart or a surface is drawn in, and what stands there: the drawing, or the refusal of the Consumer's
 * script in its place (ADR-0420 section 3), said by name in a box of the drawing's size, as .NET says a refusal of its
 * own. The modules of the Consumer's functions are imported once, and updates are taken one at a time in the order .NET
 * sent them, so a module that loads late never draws over a later definition (spine rule 6).
 */
class Place<H extends { update(options: O): void; destroy(): void }, O> {
    readonly #root: HTMLElement;
    readonly #create: (options: O) => H;
    readonly #modules = new ScriptModules();
    #drawing: H | undefined;
    #refusal: HTMLElement | undefined;
    #taking = false;
    #disposed = false;
    #size: Size = { width: 0, height: 0 };
    #queue: Promise<void> = Promise.resolve();

    constructor(root: HTMLElement, create: (options: O) => H) {
        this.#root = root;
        this.#create = create;
    }

    /** The drawing, or none while the script is refused in its place. */
    get drawing(): H | undefined {
        return this.#drawing;
    }

    /** Whether a refusal stands in the drawing's place: only another definition draws again. */
    get refused(): boolean {
        return this.#refusal !== undefined;
    }

    /** Takes `step` once every step before it is taken; what it throws is the caller's, and the next step is still taken. */
    queue(step: () => Promise<void>): Promise<void> {
        const run = this.#queue.then(step);
        this.#queue = run.then(() => undefined, () => undefined); // oxlint-disable-line ex-grid/no-catch -- the chain only orders the steps; each failure reaches its own caller through `run`
        return run;
    }

    /**
     * Loads the functions `scripts` names and reads the definition with them; undefined when the Consumer's script
     * cannot be had or fails as the definition is read, the chart refused in its place, or when the component let go
     * while the modules loaded.
     */
    async read<D>(scripts: () => readonly WireScript[], read: (calls: ScriptCalls) => D, size: Size): Promise<D | undefined> {
        this.#size = size;
        try {
            const functionOf = await this.#modules.load(scripts());
            if (this.#disposed) return undefined;
            return this.#taken(() => read({ functionOf, failed: error => this.#failed(error) }));
        } catch (e: unknown) { // oxlint-disable-line ex-grid/no-catch -- the Consumer's script refused is said in the chart's place; anything else is rethrown
            if (!(e instanceof ScriptFunctionError)) throw e;
            this.#refuse(e.message);
            return undefined;
        }
    }

    /** Draws with the options `options` makes, the refusal that stood in its place taken away; false when the Consumer's
     * script failed as it drew, and is refused in its place. */
    draw(options: () => O, size: Size): boolean {
        this.#size = size;
        if (this.#disposed) return false;
        try {
            this.#taken(() => {
                const made = options();
                if (this.#drawing === undefined) {
                    this.#refusal?.remove();
                    this.#refusal = undefined;
                    this.#drawing = this.#create(made);
                } else {
                    this.#drawing.update(made);
                }
            });
            return true;
        } catch (e: unknown) { // oxlint-disable-line ex-grid/no-catch -- the Consumer's script refused is said in the chart's place; anything else is rethrown
            if (!(e instanceof ScriptFunctionError)) throw e;
            this.#refuse(e.message);
            return false;
        }
    }

    /** The refusal standing in the drawing's place takes the new size. */
    resize(size: Size): void {
        this.#size = size;
        if (this.#refusal !== undefined) this.#sized(this.#refusal);
    }

    /** Stops the drawing and empties the element; a step still loading draws nothing once it loads. */
    destroy(): void {
        this.#disposed = true;
        this.#drawing?.destroy();
        this.#drawing = undefined;
        this.#root.replaceChildren();
        this.#refusal = undefined;
    }

    #taken<T>(step: () => T): T {
        this.#taking = true;
        try {
            return step();
        } finally {
            this.#taking = false;
        }
    }

    // While a definition is read or drawn, a failure is thrown to the step, which refuses. As the pointer moves, the
    // Script Core's own handler asked: the refusal replaces the drawing before the browser paints, so what the function
    // answered is never seen, and nothing is thrown into a handler that would not catch it.
    #failed(error: ScriptFunctionError): boolean {
        if (this.#taking) return true;
        queueMicrotask(() => { this.#refuse(error.message); });
        return false;
    }

    #refuse(message: string): void {
        if (this.#disposed) return;
        this.#drawing?.destroy();
        this.#drawing = undefined;
        const box = document.createElement('div');
        box.className = 'ex-chart';
        const said = document.createElement('p');
        said.className = 'ex-chart-source-refusal';
        said.setAttribute('role', 'alert');
        said.textContent = message;
        box.append(said);
        this.#sized(box);
        this.#root.replaceChildren(box);
        this.#refusal = box;
    }

    #sized(box: HTMLElement): void {
        box.style.width = `${String(this.#size.width)}px`;
        box.style.minHeight = `${String(this.#size.height)}px`;
    }
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
