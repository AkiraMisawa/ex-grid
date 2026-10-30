// A chart in the DOM (ADR-0370 section 1): ExChart's Script Core builds and updates the elements inside
// the instance root itself, to the DOM contract the Razor component writes, so ex-chart.css and layer 3
// read both alike. The drawing is SVG written in parts (ADR-0252): a part whose markup did not change is
// not touched, so a move of the pointer rewrites the tooltip and the guide and nothing else.
//
// It holds no view state of the Consumer's (ADR-0272): what the user changes — a series hidden from the
// legend, the range narrowed by a drag or under Chart Filters, the axis switched — is raised as an event
// and taken back as an option, in the data's terms, so it applies to new data as it is. An option that
// changed replaces what the user last did; one that did not leaves it.
//
// The pointer is heard here, where the Razor component needed a script entry to hear it without a round
// trip per move (ADR-0255): only a move onto another position or mark is read, and nothing is measured —
// a position's pixel is the scene's, and the pointer's is the event's offset on the hit layer it is over,
// whose own offsets the layer carries. A press taken against one drawing is refused when it is released
// against another (spine rule 6).
import { densityText, type DensityText, type GridDensity } from '@ex-grid/core';
import { ChartDefinition } from '../definition';
import type { ChartMetrics, ChartView } from '../metrics';
import type { AxisScale } from '../model';
import { defined, elementAt } from '../must';
import { roundHalfToEven, roundToDigits } from '../numbers';
import { chartCsvWide, chartTable, type ChartTable } from '../csv';
import { readingAt, readingOfEdge, readingOfMark, type ChartReading } from '../reading';
import { buildScene, legendMembers, positionAt, valueAt, type ChartScene, type LegendPlacement } from '../scene';
import { chartSummary } from '../summary';
import type { AxisStyle } from '../ticks';
import { ChartSlot, checkedChartChrome, type ChartChrome, type ChartCommand, type ChartFiltersSlotContext, type ChartMenuSlotContext, type ChartToolbarSlotContext } from './chart-chrome';
import { bandMarkup, drawingParts, guideMarkup, tooltipMarkup, type DrawingPart, type GuideDot } from './chart-drawing';
import { fileName, mediaType, paintPng, save, screenTheme, svgFile, type ChartFileFormat } from './chart-files';
import { chartWord, chartWordIds, type ChartLabel } from './chart-words';
import { readEnd, writeEnd } from './range-text';
import { stylesheetName, stylesheetStamp } from './shipped-stylesheet';
import { n } from './svg-markup';

/**
 * The range of the horizontal axis the user narrowed the chart to (ADR-0273), in the axis' own values — a
 * date's day number, a number, a category's index — never in pixels (ADR-0272). An end left out is the
 * data's own.
 */
export interface ChartRange {
    /** The first position shown; absent for the data's first. */
    readonly from?: number;
    /** The last position shown; absent for the data's last. */
    readonly to?: number;
    /** The lowest value the left axis shows, which a rectangle dragged across a scatter narrows (ADR-0290); absent for the data's. */
    readonly yFrom?: number;
    /** The highest; absent for the data's. */
    readonly yTo?: number;
}

/** A series the user hid or showed from the legend (ADR-0271, ADR-0272). */
export interface SeriesVisibility {
    /** The legend entry's key: the series' identity, which for quotes and their Interpolated curve is the quotes' and hides both (ADR-0257). */
    readonly key: string;
    /** Whether it is now hidden. */
    readonly hidden: boolean;
}

/** A click on a point of a chart (ADR-0258): the series nearest the pointer, at the position clicked. */
export interface ChartPointClick {
    /** The series' identity, as the Chart Definition gives it. */
    readonly seriesId: string;
    /** The position clicked, in the axis' own values. */
    readonly position: number;
    /** The Row Key of an Observation's point clicked, as its point gives it (ADR-0290); absent otherwise. */
    readonly row?: unknown;
}

/** What a chart is drawn from, and what it tells. */
export interface ChartOptions {
    /** The chart, in the data's terms. */
    readonly definition: ChartDefinition;
    /** The drawing's width in CSS pixels; geometry is an option, never only a stylesheet value (ADR-0027). 800 unless given. */
    readonly width?: number;
    /** The drawing's height in CSS pixels; 400 unless given. */
    readonly height?: number;
    /** The density whose font size and glyph widths lay out the labels, as a grid's (ADR-0016, ADR-0028); `standard` unless given. */
    readonly density?: GridDensity;
    /** The chart's name, which its accessible summary and its table's caption begin with (ADR-0275). */
    readonly title?: string;
    /** A description the Consumer adds after the generated summary. */
    readonly description?: string;
    /** Where the legend sits: top (the default), bottom, left or right, aligned, or none (ADR-0271). */
    readonly legend?: LegendPlacement;
    /** How the horizontal axis places and writes its ticks. */
    readonly axisStyle?: AxisStyle;
    /** The panels whose left and right axes the Consumer declared independent (ADR-0251). */
    readonly independentScales?: ReadonlySet<string> | readonly string[];
    /** How many colour slots the stylesheet defines: ten, unless a theme declares more (ADR-0256). */
    readonly colourSlots?: number;
    /** The keys of the series hidden, taken back from `onSeriesVisibilityChanged`. */
    readonly hiddenSeries?: ReadonlySet<string> | readonly string[];
    /** Hears the user hide or show a series from the legend. */
    readonly onSeriesVisibilityChanged?: (change: SeriesVisibility) => void;
    /** The range shown, taken back from `onRangeChanged`; absent or `null` for the whole range. */
    readonly range?: ChartRange | null;
    /** Hears the user narrow the range, or return it to the whole (`null`). */
    readonly onRangeChanged?: (range: ChartRange | null) => void;
    /** The scale the horizontal axis is switched to, taken back from `onScaleChanged`; absent for the declared one. */
    readonly scale?: AxisScale;
    /** Hears the user switch the horizontal axis under Chart Filters. */
    readonly onScaleChanged?: (scale: AxisScale) => void;
    /** The vertical axes' scale: `logarithmic`, or absent for proportional (ADR-0290). A value at or below zero keeps them proportional, with a Note naming it. */
    readonly yScale?: AxisScale;
    /** Hears a click on a point: pressed and released at one position, on the series nearest the pointer that has a value there (ADR-0258). */
    readonly onPointClick?: (click: ChartPointClick) => void;
    /** The name a saved file takes, without its extension; the title's when absent, else `chart` (ADR-0282). */
    readonly fileName?: string;
    /** The words of the built-in markup by id (ADR-0370 section 10): the Consumer's, `japaneseChartWords`, or the English. */
    readonly label?: ChartLabel;
    /** The parts a Wrapper's Chrome draws in place of the built-in markup: the toolbar and the context menu's
     * contents (ADR-0281, ADR-0390). */
    readonly chrome?: ChartChrome;
}

// The lattice, in CSS pixels, a drag across a scatter is followed on (ADR-0290).
const dragCell = 4;

// A summary's id, unique on the page even beside another copy of this package: random, as the Razor
// component's is a GUID.
function uniqueId(): string {
    const bytes = crypto.getRandomValues(new Uint8Array(8));
    return `ex-chart-${Array.from(bytes, b => b.toString(16).padStart(2, '0')).join('')}`;
}

const svgNs = 'http://www.w3.org/2000/svg';

interface Taken {
    readonly hidden: ReadonlySet<string>;
    readonly range: ChartRange | null;
    readonly scale: AxisScale | undefined;
}

interface Press {
    readonly version: number;
    readonly index: number;
}

interface ScatterPress {
    readonly version: number;
    readonly index: number;
    readonly x: number;
    readonly y: number;
    readonly panel: number;
}

interface Pointer {
    readonly x: number;
    readonly y: number;
    readonly panel: number;
}

interface Built {
    readonly metrics: ChartMetrics;
    readonly lineHeight: number;
    readonly fontSize: number;
    readonly view: ChartView;
    readonly scene: ChartScene;
    readonly summary: string;
}

interface Command {
    readonly id: string;
    readonly label: string;
    readonly enabled: boolean;
    readonly run: () => void;
    readonly expanded?: boolean;
    readonly title?: string;
}

const sameRange = (a: ChartRange | null, b: ChartRange | null): boolean =>
    a === b || (a !== null && b !== null && a.from === b.from && a.to === b.to && a.yFrom === b.yFrom && a.yTo === b.yTo);

const sameSet = (a: ReadonlySet<string>, b: ReadonlySet<string>): boolean => a.size === b.size && [...a].every(k => b.has(k));

/** Two plain values alike: numbers, strings, arrays, sets and objects of them, compared by what they hold. */
function plain(value: unknown): value is object {
    if (typeof value !== 'object' || value === null || Array.isArray(value)) {
        return false;
    }
    const prototype: unknown = Object.getPrototypeOf(value);
    return prototype === Object.prototype || prototype === null;
}

function alike(a: unknown, b: unknown): boolean {
    if (Object.is(a, b)) {
        return true;
    }
    if (a instanceof Set && b instanceof Set) {
        return a.size === b.size && [...a].every(k => b.has(k));
    }
    if (Array.isArray(a) && Array.isArray(b)) {
        return a.length === b.length && a.every((v, i) => alike(v, b[i]));
    }
    // Only plain objects compare by their entries: an instance, such as a Chart Definition, holds its state where
    // entries do not reach, so it is the same only when it is the same instance.
    if (plain(a) && plain(b)) {
        const ea: [string, unknown][] = Object.entries(a);
        const eb = new Map<string, unknown>(Object.entries(b));
        return ea.length === eb.size && ea.every(([k, v]) => eb.has(k) && alike(v, eb.get(k)));
    }
    return false;
}

const setOf = (keys: ReadonlySet<string> | readonly string[] | undefined): ReadonlySet<string> => new Set(keys);

/** The chart behind {@link createChart}'s handle. */
export class ChartElement {
    readonly #root: HTMLElement;
    readonly #summaryId: string;
    #options: ChartOptions;
    #destroyed = false;

    // What the user changed, as last taken from an option or a gesture, and the options it was taken from.
    #hidden: ReadonlySet<string> = new Set();
    #range: ChartRange | null = null;
    #scale: AxisScale | undefined;
    #taken: Taken | undefined;

    // The scene and what it was built from.
    #text: DensityText | undefined;
    #textDensity: GridDensity | undefined;
    #built: Built;
    #table: ChartTable | null = null;
    #builtFrom: readonly unknown[] = [];

    // The drawing's version (ADR-0272): a press taken against another is refused when it is released.
    #version = 0;

    // The pointer: where it rests over a hit layer, the position or mark it is over, and what is read there.
    #over: Pointer | null = null;
    #pointerIndex: number | null = null;
    #pointerPanel = 0;
    #readingY = 0;
    #reading: ChartReading | null = null;
    #dots: readonly GuideDot[] = [];
    #press: Press | null = null;
    #pressAt: ScatterPress | null = null;
    #dragAt: { readonly x: number; readonly y: number } | null = null;
    #lastCell = '';

    #filtersOpen = false;
    #tableOpen = false;
    #fromText: string | null = null;
    #toText: string | null = null;
    #filterError: string | null = null;
    #menu: { readonly x: number; readonly y: number } | null = null;
    #menuShape: string | null = null;

    // The elements the chart keeps between paints.
    readonly #toolbar: HTMLDivElement;
    readonly #stage: HTMLDivElement;
    readonly #svg: SVGSVGElement;
    readonly #title: SVGTitleElement;
    #filters: HTMLDivElement | null = null;
    readonly #notice: HTMLDivElement;
    // The root's own properties the chart sets, so destroy() takes back only those.
    static readonly #rootProperties = ['width', 'font-size', '--ex-chart-line-height'] as const;
    // The SVG parts of the scene last drawn, kept while the scene and the angle are the same.
    #drawn: { readonly built: Built; readonly angle: number; readonly parts: readonly DrawingPart[] } | null = null;
    #menuParts: readonly HTMLElement[] = [];
    // The Chrome's parts, while a slot is given for them (ADR-0390).
    #toolbarSlot: ChartSlot<ChartToolbarSlotContext> | null = null;
    #menuSlot: ChartSlot<ChartMenuSlotContext> | null = null;
    #notes: HTMLUListElement | null = null;
    #tableBlock: HTMLElement | null = null;
    #tableShown: ChartTable | 'notice' | null = null;
    #parts: readonly (DrawingPart & { readonly nodes: readonly ChildNode[] })[] = [];
    #hits: readonly HTMLDivElement[] = [];
    #hitPlots = '';
    #tooltip: Element | null = null;
    #tooltipMarkup = '';
    #notesShown: readonly string[] = [];
    readonly #unlisten: readonly (() => void)[];

    constructor(root: HTMLElement, options: ChartOptions) {
        this.#root = root;
        this.#summaryId = uniqueId();
        this.#options = ChartElement.#checked(options);

        root.replaceChildren();
        root.classList.add('ex-chart');
        const notice = document.createElement('div');
        notice.setAttribute('data-ex-notice', 'stylesheet');
        notice.setAttribute('data-ex-stylesheet', stylesheetName);
        notice.setAttribute('data-ex-stamp', stylesheetStamp);
        notice.style.display = 'none';
        root.append(notice);
        this.#notice = notice;
        this.#toolbar = document.createElement('div');
        this.#toolbar.className = 'ex-chart-toolbar';
        this.#toolbar.setAttribute('role', 'toolbar');
        root.append(this.#toolbar);
        this.#stage = document.createElement('div');
        this.#stage.className = 'ex-chart-stage';
        this.#svg = document.createElementNS(svgNs, 'svg');
        this.#svg.setAttribute('class', 'ex-chart-svg');
        this.#svg.setAttribute('role', 'img');
        this.#svg.setAttribute('aria-labelledby', this.#summaryId);
        this.#title = document.createElementNS(svgNs, 'title');
        this.#title.id = this.#summaryId;
        this.#svg.append(this.#title);
        this.#stage.append(this.#svg);
        root.append(this.#stage);

        const unlisten: (() => void)[] = [];
        const listen = <K extends keyof HTMLElementEventMap>(type: K, handle: (event: HTMLElementEventMap[K]) => void): void => {
            root.addEventListener(type, handle);
            unlisten.push(() => { root.removeEventListener(type, handle); });
        };
        listen('pointermove', e => { this.#onMove(e); });
        listen('pointerdown', e => { this.#onDown(e); });
        listen('pointerup', e => { this.#onUp(e); });
        listen('pointercancel', () => { this.#onCancel(); });
        listen('pointerleave', () => { this.#onLeave(); });
        listen('click', e => { this.#onClick(e); });
        listen('dblclick', e => { this.#onDoubleClick(e); });
        listen('keydown', e => { this.#onKey(e); });
        listen('contextmenu', e => { this.#onContextMenu(e); });
        listen('input', e => { this.#onInput(e); });
        listen('change', e => { this.#onChange(e); });
        this.#unlisten = unlisten;
        this.#take(this.#options);
        this.#builtFrom = this.#inputs();
        this.#built = this.#layout();
        this.#version = 1;
        // A chart its Chrome cannot draw is not made: what it attached is taken back, and the error thrown on.
        try {
            this.#paint();
        } catch (error) {
            this.destroy();
            throw error;
        }
    }

    /** Takes the options again, whole: what did not change costs nothing. */
    update(options: ChartOptions): void {
        this.#alive();
        this.#options = ChartElement.#checked(options);
        this.#take(this.#options);
        this.#rebuild();
        this.#paint();
    }

    /**
     * Returns the range the user narrowed to the whole without raising `onRangeChanged`: a Framework Adapter's
     * refusal of a narrowing its host learned was taken against data replaced before it ended (ADR-0272, spine rule 6).
     */
    dropRange(): void {
        this.#alive();
        if (this.#range !== null) {
            this.#showRange(null);
        }
    }

    /** Stops the chart: it hears nothing more and empties its element. */
    destroy(): void {
        if (this.#destroyed) {
            return;
        }
        this.#destroyed = true;
        for (const stop of this.#unlisten) {
            stop();
        }
        // The Chrome's contents go before the elements they stand in.
        this.#toolbarSlot?.hide();
        this.#menuSlot?.hide();
        this.#root.replaceChildren();
        this.#root.classList.remove('ex-chart');
        for (const property of ChartElement.#rootProperties) {
            this.#root.style.removeProperty(property);
        }
    }

    /**
     * Saves the chart as a file, as the user sees it: CSV of the visible series in the range shown, the SVG of
     * the drawing, or that SVG painted to PNG at the screen's Device Pixel (ADR-0282). One call is one save.
     * False for a CSV of a chart its Chart Source thinned, which would hold only the points drawn (ADR-0344),
     * and once the chart is destroyed.
     */
    async download(format: ChartFileFormat): Promise<boolean> {
        // Taken now: the file is the drawing on screen at the click, whatever is drawn meanwhile.
        const { definition } = this.#options;
        const scene = this.#scene;
        const metrics = this.#metrics;
        const view = this.#view;
        if (this.#destroyed || (format === 'csv' && this.#thinnedAtSource)) {
            return false;
        }
        const name = fileName(this.#options.fileName, this.#options.title, format);
        if (format === 'csv') {
            save(name, mediaType(format), chartCsvWide(definition, metrics, view));
            return true;
        }
        const svg = svgFile({
            scene, width: this.#width, height: this.#height, fontSize: this.#fontSize, lineHeight: this.#lineHeight,
            angle: this.#angle, summary: this.#summary, theme: screenTheme(this.#root),
        });
        if (format === 'svg') {
            save(name, mediaType(format), svg);
            return true;
        }
        save(name, mediaType(format), await paintPng(svg, this.#width, this.#height));
        return true;
    }

    // ---- Options ----

    static #checked(options: ChartOptions): ChartOptions {
        // An untyped caller's options are checked here, at the boundary (CONVENTIONS.md).
        const { definition, width, height, onSeriesVisibilityChanged, onRangeChanged, onScaleChanged, onPointClick, label, chrome }: {
            readonly definition?: unknown; readonly width?: unknown; readonly height?: unknown; readonly onSeriesVisibilityChanged?: unknown;
            readonly onRangeChanged?: unknown; readonly onScaleChanged?: unknown; readonly onPointClick?: unknown; readonly label?: unknown;
            readonly chrome?: unknown;
        } = options;
        checkedChartChrome(chrome);
        if (!(definition instanceof ChartDefinition)) {
            throw new TypeError('An ExChart is given its definition, a ChartDefinition.');
        }
        for (const [name, value] of [['width', width], ['height', height]] as const) {
            if (value !== undefined && !(typeof value === 'number' && Number.isFinite(value) && value > 0)) {
                throw new RangeError(`An ExChart's ${name} is a finite, positive number of CSS pixels. (${name}: ${typeof value === 'number' ? String(value) : `a ${typeof value}`})`);
            }
        }
        for (const [name, value] of [['onSeriesVisibilityChanged', onSeriesVisibilityChanged], ['onRangeChanged', onRangeChanged],
            ['onScaleChanged', onScaleChanged], ['onPointClick', onPointClick], ['label', label]] as const) {
            if (value !== undefined && typeof value !== 'function') {
                throw new TypeError(`An ExChart's ${name} is a function.`);
            }
        }
        return options;
    }

    // Taken back as plain options (ADR-0272): an option that changed replaces what the user last did; one
    // that did not leaves it. Compared by what they hold, so a set or a range made again on every render is
    // the same option.
    #take(options: ChartOptions): void {
        const hidden = setOf(options.hiddenSeries);
        const range = options.range === undefined ? null : options.range;
        const taken = this.#taken;
        if (taken === undefined || !sameSet(taken.hidden, hidden)) {
            this.#hidden = hidden;
        }
        if (taken === undefined || !sameRange(taken.range, range)) {
            this.#range = range;
        }
        if (taken === undefined || taken.scale !== options.scale) {
            this.#scale = options.scale;
        }
        this.#taken = { hidden, range, scale: options.scale };
    }

    get #width(): number {
        const { width = 800 } = this.#options;
        return width;
    }

    get #height(): number {
        const { height = 400 } = this.#options;
        return height;
    }

    get #angle(): number {
        const style = this.#options.axisStyle;
        return style?.angle === undefined ? 0 : style.angle;
    }

    #word(id: string, ...args: readonly string[]): string {
        return chartWord(id, this.#options.label, ...args);
    }

    #alive(): void {
        if (this.#destroyed) {
            throw new Error('This chart was destroyed; create another.');
        }
    }

    // ---- The scene ----

    get #metrics(): ChartMetrics {
        return this.#built.metrics;
    }

    get #lineHeight(): number {
        return this.#built.lineHeight;
    }

    get #fontSize(): number {
        return this.#built.fontSize;
    }

    get #view(): ChartView {
        return this.#built.view;
    }

    get #scene(): ChartScene {
        return this.#built.scene;
    }

    get #summary(): string {
        return this.#built.summary;
    }

    get #density(): GridDensity {
        const { density = 'standard' } = this.#options;
        return density;
    }

    #inputs(): readonly unknown[] {
        const o = this.#options;
        return [o.definition, this.#width, this.#height, this.#density, o.legend, o.axisStyle, o.independentScales, o.colourSlots,
            this.#hidden, this.#range, this.#scale, o.yScale, o.title, o.description];
    }

    #rebuild(): void {
        const builtFrom = this.#inputs();
        if (builtFrom.every((v, i) => alike(v, this.#builtFrom[i]))) {
            return;
        }
        const definitionChanged = this.#builtFrom[0] !== this.#options.definition;
        this.#builtFrom = builtFrom;
        const previous = this.#scene;
        this.#built = this.#layout();
        this.#table = this.#tableOpen && !this.#thinnedAtSource ? chartTable(this.#options.definition, this.#metrics, this.#view) : null;

        const scene = this.#scene;
        if (definitionChanged || !alike(previous.positions, scene.positions) || !alike(previous.marks, scene.marks)
            || !alike(previous.edgeMarks, scene.edgeMarks) || !alike(previous.panels.map(p => p.plot), scene.panels.map(p => p.plot))) {
            this.#version += 1;
            this.#clearPointer();
            // A pointer at rest is read against the drawing now shown, as the Razor component's script re-reported it.
            if (this.#over !== null && this.#press === null && this.#pressAt === null) {
                this.#report(this.#over);
            }
        } else if (this.#pointerIndex !== null) {
            this.#read(this.#pointerIndex, this.#pointerPanel, this.#readingY);
        }
    }

    #layout(): Built {
        const o = this.#options;
        const density = this.#density;
        if (this.#text === undefined || this.#textDensity !== density) {
            this.#text = densityText(density);
            this.#textDensity = density;
        }
        const text = this.#text;
        const font = text.fontSizePx;
        const lineHeight = Math.ceil(font * 1.4);
        // Two device pixels a CSS pixel: Thinning keeps every peak at up to twice the resolution the chart is
        // told nothing about, and no more (ADR-0254).
        const metrics: ChartMetrics = {
            width: this.#width, height: this.#height, devicePixelRatio: 2, lineHeight, padding: roundHalfToEven(font * 0.6),
            panelGap: roundHalfToEven(font * 1.2), textWidth: s => text.textWidthPx(s), ...(o.colourSlots === undefined ? {} : { colourSlots: o.colourSlots }),
        };
        const range = this.#range;
        const view: ChartView = {
            hidden: this.#hidden,
            ...(range?.from === undefined ? {} : { from: range.from }),
            ...(range?.to === undefined ? {} : { to: range.to }),
            ...(this.#scale === undefined ? {} : { scale: this.#scale }),
            ...(range?.yFrom === undefined ? {} : { yFrom: range.yFrom }),
            ...(range?.yTo === undefined ? {} : { yTo: range.yTo }),
            ...(o.yScale === undefined ? {} : { yScale: o.yScale }),
        };
        const scene = buildScene(o.definition, metrics, {
            view,
            ...(o.axisStyle === undefined ? {} : { style: o.axisStyle }),
            ...(o.independentScales === undefined ? {} : { independentScales: o.independentScales }),
            ...(o.legend === undefined ? {} : { legend: o.legend }),
        });
        const summary = chartSummary(o.definition, scene, o.title, o.description);
        return { metrics, lineHeight, fontSize: font, view, scene, summary };
    }

    get #thinnedAtSource(): boolean {
        // A chart whose Chart Source thinned it holds only the points drawn (ADR-0344): a CSV or a table of
        // them would look like the data and not be it (spine rule 1), so neither is offered.
        return this.#options.definition.series.some(s => s.thinned.length > 0);
    }

    // ---- The pointer (ADR-0255, ADR-0270) ----

    #clearPointer(): void {
        this.#pointerIndex = null;
        this.#reading = null;
        this.#dots = [];
    }

    #read(index: number, panel: number, y: number): void {
        const scene = this.#scene;
        if (scene.scatter) {
            this.#readMark(index);
            return;
        }
        if (index < 0 || index >= scene.positions.length || panel < 0 || panel >= scene.panels.length) {
            this.#clearPointer();
            return;
        }
        this.#pointerIndex = index;
        this.#pointerPanel = panel;
        this.#readingY = y;
        const reading = readingAt(this.#options.definition, scene, this.#metrics, elementAt(scene.positions, index)[0], panel, y);
        this.#reading = reading;
        this.#dots = reading.lines.flatMap(l => (l.y === null ? [] : [{ y: l.y, look: l.look }]));
    }

    // On a scatter the pointer is over the nearest mark, if any (ADR-0290), or else the nearest edge,
    // counted after the marks (ADR-0321).
    #readMark(index: number): void {
        const scene = this.#scene;
        if (index < 0 || index >= scene.marks.length + scene.edgeMarks.length) {
            this.#clearPointer();
            return;
        }
        const at = this.#markAt(index);
        this.#pointerIndex = index;
        this.#pointerPanel = at.panel;
        this.#readingY = at.y;
        const reading = index < scene.marks.length
            ? readingOfMark(this.#options.definition, scene, index)
            : readingOfEdge(this.#options.definition, scene, index - scene.marks.length);
        this.#reading = reading;
        this.#dots = reading.lines.map(l => ({ y: at.y, look: l.look }));
    }

    // Where the pointer's mark stands: a marker's centre, or an edge's middle.
    #markAt(index: number): Pointer {
        const scene = this.#scene;
        if (index < scene.marks.length) {
            const m = elementAt(scene.marks, index);
            return { x: m.x, y: m.y, panel: m.panel };
        }
        const e = elementAt(scene.edgeMarks, index - scene.marks.length);
        return { x: (e.edge.x1 + e.edge.x2) / 2, y: (e.edge.y1 + e.edge.y2) / 2, panel: e.panel };
    }

    // The position nearest an offset: positions are sorted left to right, so the boundary between two is
    // halfway between them. Offsets are a hundredth of a pixel, as the Razor component handed them over.
    #nearest(x: number): number {
        const xs = this.#scene.positions;
        let lo = 0;
        let hi = xs.length - 1;
        if (hi < 0) {
            return -1;
        }
        while (lo < hi) {
            const mid = (lo + hi) >> 1;
            const between = (roundToDigits(elementAt(xs, mid)[1], 2) + roundToDigits(elementAt(xs, mid + 1)[1], 2)) / 2;
            if (x < between) {
                hi = mid;
            } else {
                lo = mid + 1;
            }
        }
        return lo;
    }

    // The mark nearest the point, in its panel and within reach; where none is, the nearest edge in reach,
    // counted after the marks (ADR-0321); or -1.
    #nearestMark(point: Pointer): number {
        const scene = this.#scene;
        const reach = roundToDigits(this.#lineHeight * 0.6, 2);
        let best = -1;
        let bestDistance = reach * reach;
        scene.marks.forEach((m, i) => {
            if (m.panel !== point.panel) {
                return;
            }
            const dx = roundToDigits(m.x, 2) - point.x;
            const dy = roundToDigits(m.y, 2) - point.y;
            const distance = dx * dx + dy * dy;
            if (distance <= bestDistance) {
                best = i;
                bestDistance = distance;
            }
        });
        if (best >= 0) {
            return best;
        }
        scene.edgeMarks.forEach((m, i) => {
            if (m.panel !== point.panel) {
                return;
            }
            const x1 = roundToDigits(m.edge.x1, 2);
            const y1 = roundToDigits(m.edge.y1, 2);
            const x2 = roundToDigits(m.edge.x2, 2);
            const y2 = roundToDigits(m.edge.y2, 2);
            const length = (x2 - x1) * (x2 - x1) + (y2 - y1) * (y2 - y1);
            const t = length === 0 ? 0 : Math.max(0, Math.min(1, ((point.x - x1) * (x2 - x1) + (point.y - y1) * (y2 - y1)) / length));
            const dx = x1 + t * (x2 - x1) - point.x;
            const dy = y1 + t * (y2 - y1) - point.y;
            const distance = dx * dx + dy * dy;
            if (distance <= bestDistance) {
                best = scene.marks.length + i;
                bestDistance = distance;
            }
        });
        return best;
    }

    #indexAt(point: Pointer): number {
        return this.#scene.scatter ? this.#nearestMark(point) : this.#nearest(point.x);
    }

    // Where on the drawing an event is: the hit layer's own offsets, which the chart wrote on it, plus the
    // event's offsets within it. During a drag the pointer is captured, so the target stays the layer the
    // press was on even when the pointer has left it.
    #locate(event: MouseEvent): Pointer | null {
        const layer = event.target instanceof Element ? event.target.closest('.ex-chart-hit') : null;
        if (layer === null || !this.#root.contains(layer)) {
            return null;
        }
        const left = Number(layer.getAttribute('data-ex-x'));
        const top = Number(layer.getAttribute('data-ex-y'));
        const panel = Number(layer.getAttribute('data-ex-panel'));
        if (!Number.isFinite(left) || !Number.isFinite(top) || !Number.isInteger(panel)) {
            throw new Error('Internal: a hit layer carries no offsets of its own.');
        }
        return { x: left + event.offsetX, y: top + event.offsetY, panel };
    }

    readonly #snap = (value: number): number => (Math.floor(value / dragCell) + 0.5) * dragCell;

    // A move onto another position or mark reads it; a move within one reads nothing again.
    #report(point: Pointer): void {
        if (this.#pressAt !== null) {
            const cell = `${String(Math.floor(point.x / dragCell))},${String(Math.floor(point.y / dragCell))}`;
            if (cell !== this.#lastCell) {
                this.#lastCell = cell;
                if (this.#pressAt.version === this.#version) {
                    this.#dragAt = { x: this.#snap(point.x), y: this.#snap(point.y) };
                }
            }
        }
        const index = this.#indexAt(point);
        if (index === this.#pointerIndex && point.panel === this.#pointerPanel) {
            return;
        }
        if (index < 0) {
            this.#clearPointer();
        } else {
            this.#read(index, point.panel, point.y);
        }
    }

    #onMove(event: PointerEvent): void {
        const point = this.#locate(event);
        const pressed = this.#press !== null || this.#pressAt !== null;
        if (point === null) {
            if (!pressed) {
                this.#leave();
            }
            return;
        }
        if (!pressed) {
            this.#over = point;
        }
        this.#report(point);
        this.#paintPointer();
    }

    #leave(): void {
        this.#over = null;
        if (this.#pointerIndex === null) {
            return;
        }
        this.#clearPointer();
        this.#paintPointer();
    }

    #onLeave(): void {
        if (this.#press === null && this.#pressAt === null) {
            this.#leave();
        }
    }

    #onDown(event: PointerEvent): void {
        if (event.button !== 0) {
            return;
        }
        const at = this.#locate(event);
        if (at === null) {
            return;
        }
        if (event.target instanceof Element) {
            event.target.setPointerCapture(event.pointerId);
        }
        this.#lastCell = `${String(Math.floor(at.x / dragCell))},${String(Math.floor(at.y / dragCell))}`;
        const index = this.#indexAt(at);
        const scene = this.#scene;
        if (scene.scatter) {
            if (at.panel < 0 || at.panel >= scene.panels.length) {
                return;
            }
            this.#pressAt = { version: this.#version, index, x: this.#snap(at.x), y: this.#snap(at.y), panel: at.panel };
            this.#dragAt = null;
            return;
        }
        if (index < 0 || index >= scene.positions.length) {
            return;
        }
        this.#press = { version: this.#version, index };
    }

    #onUp(event: PointerEvent): void {
        if (this.#press === null && this.#pressAt === null) {
            return;
        }
        const point = this.#locate(event);
        this.#over = point;
        const index = point === null ? -1 : this.#indexAt(point);
        if (this.#scene.scatter || this.#pressAt !== null) {
            // A line chart's press that a scatter replaced while it was held ends here too.
            this.#press = null;
            this.#releaseScatter(index, point === null ? -1 : this.#snap(point.x), point === null ? -1 : this.#snap(point.y));
        } else {
            this.#pressAt = null;
            this.#release(index);
        }
    }

    // The browser took the gesture (a system gesture, a scroll): the press is dropped, never released, so
    // nothing is narrowed or clicked by a pointer that did not let go here.
    #onCancel(): void {
        if (this.#press === null && this.#pressAt === null) {
            return;
        }
        this.#press = null;
        this.#pressAt = null;
        this.#dragAt = null;
        this.#paintPointer();
    }

    // A drag ends (ADR-0273): narrowed to the positions it began and ended on, unless the drawing it began
    // against is no longer the one shown (ADR-0272).
    #release(index: number): void {
        const press = this.#press;
        this.#press = null;
        const scene = this.#scene;
        if (press === null) {
            return;
        }
        if (press.version !== this.#version || index < 0 || index >= scene.positions.length) {
            this.#paintPointer();
            return;
        }
        if (index === press.index) {
            this.#paintPointer();
            // A click, read from the reading the pointer's position holds: one taken at another position, or
            // none, names no point, and nothing is raised (spine rule 6).
            const reading = this.#pointerIndex === index ? this.#reading : null;
            let line: { readonly id: string; readonly distance: number } | undefined;
            for (const l of reading === null ? [] : reading.lines) {
                if (l.y === null || l.value === null) {
                    continue;
                }
                const distance = Math.abs(l.y - this.#readingY);
                if (line === undefined || distance < line.distance) {
                    line = { id: l.id, distance };
                }
            }
            if (line === undefined) {
                return;
            }
            const position = elementAt(scene.positions, index)[0];
            // A merged candle stands for no one row: a click narrows to its period (ADR-0300, ADR-0302).
            const lineId = line.id;
            const merged = scene.panels.flatMap(p => p.series).find(piece => piece.id === lineId && piece.period !== 'row');
            const candle = merged?.candles.find(c => c.from <= position && position <= c.to);
            if (candle !== undefined && candle.rows > 0) {
                this.#setRange({ from: candle.from, to: candle.to });
            } else {
                this.#options.onPointClick?.({ seriesId: lineId, position });
            }
            return;
        }
        const a = elementAt(scene.positions, Math.min(press.index, index))[0];
        const b = elementAt(scene.positions, Math.max(press.index, index))[0];
        this.#setRange({ from: a, to: b });
    }

    // A scatter's release (ADR-0290): on the mark it was pressed on, a click on that point; elsewhere, the
    // rectangle from the press narrows both axes. Against another drawing, nothing (spine rule 6).
    #releaseScatter(index: number, x: number, y: number): void {
        const press = this.#pressAt;
        this.#pressAt = null;
        this.#dragAt = null;
        if (press === null) {
            return;
        }
        const scene = this.#scene;
        if (press.version !== this.#version || x < 0 || y < 0) {
            this.#paintPointer();
            return;
        }
        // Within one cell of the lattice it was pressed on, a click; a slip of a pixel across a cell's edge is
        // still a click.
        if (Math.abs(x - press.x) <= dragCell && Math.abs(y - press.y) <= dragCell) {
            this.#paintPointer();
            if (index >= 0 && index === press.index && index < scene.marks.length) {
                const m = elementAt(scene.marks, index);
                const series = defined(this.#options.definition.series.find(s => s.id === m.seriesId), `the series ${m.seriesId}`);
                const point = elementAt(series.points, m.point);
                this.#options.onPointClick?.({ seriesId: m.seriesId, position: point.x, ...(point.row === undefined ? {} : { row: point.row }) });
            }
            return;
        }
        const panel = elementAt(scene.panels, press.panel);
        const x0 = positionAt(scene, Math.min(press.x, x));
        const x1 = positionAt(scene, Math.max(press.x, x));
        // The left axis is narrowed only where it is the chart's one vertical axis a rectangle can mean: one
        // panel. Elsewhere the rectangle narrows the axis across alone.
        if (panel.left === null || scene.panels.length > 1) {
            this.#setRange({ from: x0, to: x1 });
            return;
        }
        // Down the page is down the axis: the lower edge is the larger pixel.
        this.#setRange({
            from: x0, to: x1, yFrom: valueAt(panel.left, Math.max(press.y, y), panel.plot), yTo: valueAt(panel.left, Math.min(press.y, y), panel.plot),
        });
    }

    // ---- What the user changes ----

    #setRange(range: ChartRange | null): void {
        this.#showRange(range);
        this.#options.onRangeChanged?.(range);
    }

    #showRange(range: ChartRange | null): void {
        this.#range = range;
        this.#filterError = null;
        // Chart Filters shows the range as it now stands, however it was set (ADR-0273).
        this.#writeFilterTexts();
        this.#rebuild();
        this.#paint();
    }

    #writeFilterTexts(): void {
        const axis = this.#options.definition.axis;
        this.#fromText = this.#range?.from === undefined ? null : writeEnd(axis, this.#range.from);
        this.#toText = this.#range?.to === undefined ? null : writeEnd(axis, this.#range.to);
        if (this.#filters !== null) {
            found(this.#filters.querySelector<HTMLInputElement>('.ex-chart-from'), 'the From field').value = fieldText(this.#fromText);
            found(this.#filters.querySelector<HTMLInputElement>('.ex-chart-to'), 'the To field').value = fieldText(this.#toText);
        }
    }

    #resetRange(): void {
        if (this.#range !== null) {
            this.#setRange(null);
        }
    }

    #toggle(key: string): void {
        const hidden = new Set(this.#hidden);
        const nowHidden = !hidden.delete(key);
        if (nowHidden) {
            hidden.add(key);
        } else {
            // Shown again, an entry shows every series it stands for, even one hidden by its own key (ADR-0257).
            for (const member of legendMembers(this.#options.definition, key)) {
                hidden.delete(member);
            }
        }
        this.#hidden = hidden;
        this.#rebuild();
        this.#paint();
        this.#options.onSeriesVisibilityChanged?.({ key, hidden: nowHidden });
    }

    #toggleFilters(): void {
        this.#filtersOpen = !this.#filtersOpen;
        if (this.#filtersOpen) {
            this.#writeFilterTexts();
            this.#filterError = null;
        }
        this.#paint();
    }

    #toggleTable(): void {
        this.#tableOpen = !this.#tableOpen;
        this.#table = this.#tableOpen && !this.#thinnedAtSource ? chartTable(this.#options.definition, this.#metrics, this.#view) : null;
        this.#paint();
    }

    // The range typed under Chart Filters (ADR-0273), in the axis' own values. A value that does not read as
    // one is said, never guessed (spine rule 1).
    #applyTypedRange(): void {
        const axis = this.#options.definition.axis;
        const from = readEnd(axis, this.#fromText);
        const to = readEnd(axis, this.#toText);
        if (!from.read || !to.read) {
            const bad = !from.read ? this.#fromText : this.#toText;
            const kind = axis.values === 'dates' ? chartWordIds.aDate : axis.values === 'labels' ? chartWordIds.aCategory : chartWordIds.aNumber;
            this.#filterError = this.#word(chartWordIds.notOnAxis, found(bad, 'the text not read'), this.#word(kind));
            this.#paint();
            return;
        }
        if (from.position !== null && to.position !== null && from.position > to.position) {
            this.#filterError = this.#word(chartWordIds.fromAfterTo);
            this.#paint();
            return;
        }
        this.#setRange(from.position === null && to.position === null ? null : {
            ...(from.position === null ? {} : { from: from.position }),
            ...(to.position === null ? {} : { to: to.position }),
        });
    }

    #setScale(scale: AxisScale): void {
        if (scale !== 'proportional' && scale !== 'categories') {
            throw new RangeError(`The axis switch switches to 'proportional' or 'categories', not '${scale}' (ADR-0251).`);
        }
        if (!this.#scaleSwitchable) {
            throw new Error('This chart\'s axis cannot be switched: it holds categories, or a series is sampled (ADR-0251).');
        }
        if (scale === (this.#scale ?? this.#options.definition.axis.effectiveScale)) {
            return;
        }
        this.#scale = scale;
        this.#rebuild();
        this.#paint();
        this.#options.onScaleChanged?.(scale);
    }

    get #scaleSwitchable(): boolean {
        const d = this.#options.definition;
        return d.axis.values !== 'labels' && d.series.every(s => s.sampler === undefined);
    }

    // ---- The DOM's events ----

    #onClick(event: MouseEvent): void {
        const target = event.target instanceof Element ? event.target : null;
        if (target === null) {
            return;
        }
        const entry = target.closest('.ex-chart-legend-entry');
        if (entry !== null) {
            this.#toggle(found(entry.getAttribute('data-series'), 'a legend entry\'s key'));
            return;
        }
        if (target.closest('.ex-chart-menu-backdrop') !== null) {
            this.#closeMenu();
            return;
        }
        // A Chrome's contents run what they draw through their context, never through the built-in controls' classes.
        if (inSlot(target)) {
            return;
        }
        const button = target.closest('button');
        if (button === null || button.disabled || !this.#root.contains(button)) {
            return;
        }
        const command = button.getAttribute('data-ex-command');
        if (command !== null && button.closest('.ex-chart-menu') !== null) {
            const chosen = this.#menuCommands().find(c => c.id === command);
            if (chosen !== undefined && chosen.enabled) {
                this.#closeMenu();
                chosen.run();
            }
            return;
        }
        const toolbar = this.#toolbarCommands().find(c => button.classList.contains(toolbarClass(c.id)));
        if (toolbar !== undefined) {
            toolbar.run();
            return;
        }
        if (button.classList.contains('ex-chart-apply')) {
            this.#applyTypedRange();
        }
    }

    #onDoubleClick(event: MouseEvent): void {
        if (event.target instanceof Element && event.target.closest('.ex-chart-hit') !== null) {
            this.#resetRange();
        }
    }

    #onKey(event: KeyboardEvent): void {
        const target = event.target instanceof Element ? event.target : null;
        if (target === null) {
            return;
        }
        const entry = target.closest('.ex-chart-legend-entry');
        if (entry !== null && (event.key === 'Enter' || event.key === ' ')) {
            event.preventDefault();
            this.#toggle(found(entry.getAttribute('data-series'), 'a legend entry\'s key'));
            return;
        }
        if (target.closest('.ex-chart-menu') !== null && event.key === 'Escape') {
            this.#closeMenu();
        }
    }

    // Asked for by the right button or by the keyboard (Shift+F10, the menu key), both of which the browser
    // raises as contextmenu on the chart. Its own menu replaces the browser's (ADR-0281).
    #onContextMenu(event: MouseEvent): void {
        const target = event.target instanceof Element ? event.target : null;
        if (target === null) {
            return;
        }
        if (target.closest('.ex-chart-menu-backdrop') !== null) {
            event.preventDefault();
            this.#closeMenu();
            return;
        }
        if (target.closest('.ex-chart-stage') !== null) {
            event.preventDefault();
            this.#menu = { x: event.clientX, y: event.clientY };
            this.#paint();
            const frame = this.#menuParts[1];
            if (frame !== undefined) {
                frame.focus();
            }
        }
    }

    #closeMenu(): void {
        this.#menu = null;
        this.#paint();
    }

    #onInput(event: Event): void {
        const target = event.target;
        if (!(target instanceof HTMLInputElement) || inSlot(target)) {
            return;
        }
        if (target.classList.contains('ex-chart-from')) {
            this.#fromText = target.value;
        } else if (target.classList.contains('ex-chart-to')) {
            this.#toText = target.value;
        }
    }

    #onChange(event: Event): void {
        const target = event.target;
        if (target instanceof HTMLSelectElement && target.classList.contains('ex-chart-scale') && !inSlot(target)) {
            const value = target.value;
            if (value !== 'Proportional' && value !== 'Categories') {
                throw new Error(`Internal: the axis switch offered '${value}', which is no scale it switches to.`);
            }
            this.#setScale(value === 'Proportional' ? 'proportional' : 'categories');
        }
    }

    // ---- Commands (ADR-0281) ----

    #toolbarCommands(): readonly Command[] {
        return [
            { id: chartWordIds.filters, label: this.#word(chartWordIds.filters), enabled: true, expanded: this.#filtersOpen, run: () => { this.#toggleFilters(); } },
            { id: chartWordIds.resetRange, label: this.#word(chartWordIds.resetRange), enabled: this.#range !== null, run: () => { this.#resetRange(); } },
            { id: chartWordIds.table, label: this.#word(chartWordIds.table), enabled: true, expanded: this.#tableOpen, run: () => { this.#toggleTable(); } },
            {
                id: chartWordIds.downloadCsv, label: this.#word(chartWordIds.downloadCsv), enabled: !this.#thinnedAtSource,
                ...(this.#thinnedAtSource ? { title: this.#word(chartWordIds.thinnedAtSource) } : {}), run: () => { void this.download('csv'); },
            },
            { id: chartWordIds.downloadSvg, label: this.#word(chartWordIds.downloadSvg), enabled: true, run: () => { void this.download('svg'); } },
            { id: chartWordIds.downloadPng, label: this.#word(chartWordIds.downloadPng), enabled: true, run: () => { void this.download('png'); } },
        ];
    }

    #menuCommands(): readonly Command[] {
        return [
            { id: chartWordIds.resetRange, label: this.#word(chartWordIds.resetRange), enabled: this.#range !== null, run: () => { this.#resetRange(); } },
            { id: chartWordIds.table, label: this.#word(this.#tableOpen ? chartWordIds.hideTable : chartWordIds.table), enabled: true, run: () => { this.#toggleTable(); } },
            { id: chartWordIds.downloadCsv, label: this.#word(chartWordIds.menuDownloadCsv), enabled: !this.#thinnedAtSource, run: () => { void this.download('csv'); } },
            { id: chartWordIds.downloadSvg, label: this.#word(chartWordIds.menuDownloadSvg), enabled: true, run: () => { void this.download('svg'); } },
            { id: chartWordIds.downloadPng, label: this.#word(chartWordIds.menuDownloadPng), enabled: true, run: () => { void this.download('png'); } },
        ];
    }

    // ---- Painting ----

    get #name(): string {
        const title = this.#options.title;
        return title === undefined || title.trim() === '' ? this.#word(chartWordIds.chart) : title;
    }

    #paint(): void {
        const root = this.#root;
        // Only the chart's own properties, so a style the Consumer gave the element stays.
        root.style.setProperty('width', `${n(this.#width)}px`);
        root.style.setProperty('font-size', `${n(this.#fontSize)}px`);
        root.style.setProperty('--ex-chart-line-height', `${n(this.#lineHeight)}px`);
        setText(this.#notice, this.#word(chartWordIds.stylesheetNotice));
        const toolbarMount = this.#options.chrome?.toolbar;
        if (toolbarMount === undefined) {
            this.#paintToolbar();
            this.#paintFilters();
        } else {
            this.#placeToolbarSlot();
        }
        this.#paintStage();
        this.#paintMenu();
        this.#paintNotes();
        this.#paintTable();
        // A Chrome's contents are mounted last: what the chart draws itself stands whatever they do.
        if (toolbarMount !== undefined) {
            this.#showToolbarSlot(toolbarMount);
        }
        this.#showMenuSlot();
    }

    #paintToolbar(): void {
        const toolbar = this.#toolbar;
        if (this.#toolbarSlot !== null) {
            this.#toolbarSlot.hide();
            this.#toolbarSlot.element.remove();
            this.#toolbarSlot = null;
        }
        if (toolbar.parentElement !== this.#root) {
            this.#stage.before(toolbar);
        }
        toolbar.setAttribute('aria-label', this.#name);
        if (toolbar.childElementCount === 0) {
            const downloads = document.createElement('span');
            downloads.className = 'ex-chart-downloads';
            downloads.setAttribute('role', 'group');
            for (const command of this.#toolbarCommands()) {
                const button = document.createElement('button');
                button.type = 'button';
                button.className = `ex-chart-button ${toolbarClass(command.id)}`;
                (command.id.startsWith('exchart.download.') ? downloads : toolbar).append(button);
            }
            toolbar.append(downloads);
        }
        toolbar.querySelector('.ex-chart-downloads')?.setAttribute('aria-label', this.#word(chartWordIds.downloads));
        for (const command of this.#toolbarCommands()) {
            const button = found(toolbar.querySelector<HTMLButtonElement>(`.${toolbarClass(command.id)}`), `the ${command.id} button`);
            setText(button, command.label);
            button.disabled = !command.enabled;
            if (command.id === chartWordIds.filters) {
                button.setAttribute('aria-expanded', this.#filtersOpen ? 'true' : 'false');
            } else if (command.id === chartWordIds.table) {
                button.setAttribute('aria-expanded', this.#tableOpen ? 'true' : 'false');
            }
            if (command.id === chartWordIds.downloadCsv && this.#thinnedAtSource) {
                button.title = this.#word(chartWordIds.thinnedAtSource);
            } else {
                button.removeAttribute('title');
            }
        }
    }

    // The toolbar a Chrome draws (ADR-0281, ADR-0390), in place of the built-in one.
    #placeToolbarSlot(): void {
        this.#toolbar.remove();
        this.#filters?.remove();
        this.#filters = null;
        let slot = this.#toolbarSlot;
        if (slot === null) {
            slot = new ChartSlot<ChartToolbarSlotContext>('toolbar', document.createElement('div'));
            slot.element.className = 'ex-chart-toolbar-slot';
            this.#toolbarSlot = slot;
        }
        if (slot.element.parentElement !== this.#root) {
            this.#stage.before(slot.element);
        }
    }

    // Its contents are mounted again only when what they are handed changed, so a pointer move draws nothing of them
    // (ADR-0252); the context is made only then.
    #showToolbarSlot(mount: NonNullable<ChartChrome['toolbar']>): void {
        const slot = this.#toolbarSlot;
        if (slot === null) {
            throw new Error('Internal: the toolbar slot is shown before it is placed.');
        }
        const commands = this.#toolbarCommands();
        const filters = this.#filtersView();
        const words = [chartWordIds.from, chartWordIds.to, chartWordIds.apply, chartWordIds.axis, chartWordIds.proportional, chartWordIds.categories,
            chartWordIds.downloads, chartWordIds.filters].map(id => this.#word(id));
        const key = JSON.stringify([this.#name, commandShape(commands), words, filters]);
        slot.show(mount, key, standing => Object.freeze({
            name: this.#name,
            commands: this.#slotCommands(commands, standing, false),
            ...(filters === undefined ? {} : { filters: this.#filtersContext(filters, standing) }),
            label: (id: string) => this.#word(id),
        }));
    }

    // What Chart Filters shows while it is open (ADR-0273): plain values, which tell whether its contents would be drawn
    // the same.
    #filtersView(): Omit<ChartFiltersSlotContext, 'apply' | 'setScale'> | undefined {
        if (!this.#filtersOpen) {
            return undefined;
        }
        const axis = this.#options.definition.axis;
        return {
            values: axis.values,
            labels: axis.values === 'labels' ? Object.freeze([...axis.labels]) : Object.freeze([]),
            from: this.#fromText,
            to: this.#toText,
            hint: this.#word(axis.values === 'dates' ? chartWordIds.dateHint : axis.values === 'labels' ? chartWordIds.categoryHint : chartWordIds.numberHint),
            error: this.#filterError,
            ...(this.#scaleSwitchable ? { scale: (this.#scale ?? axis.effectiveScale) === 'categories' ? 'categories' as const : 'proportional' as const } : {}),
        };
    }

    // Chart Filters as a Chrome is handed it: what it shows, and what it answers through while its contents stand.
    #filtersContext(view: Omit<ChartFiltersSlotContext, 'apply' | 'setScale'>, standing: () => boolean): ChartFiltersSlotContext {
        return Object.freeze({
            ...view,
            apply: (from: string | null, to: string | null) => {
                if ((from !== null && typeof from !== 'string') || (to !== null && typeof to !== 'string')) {
                    throw new TypeError('Chart Filters applies the text typed for each end, or null for the axis\' own end.');
                }
                if (!standing()) {
                    return;
                }
                this.#fromText = from;
                this.#toText = to;
                this.#applyTypedRange();
            },
            setScale: (scale: AxisScale) => {
                if (standing()) {
                    this.#setScale(scale);
                }
            },
        });
    }

    // The commands as a Chrome invokes them, as the built-in controls run them: one not enabled runs nothing, and none
    // runs once the contents it was handed to no longer stand (spine rule 6); from the menu, the menu closes first.
    #slotCommands(commands: readonly Command[], standing: () => boolean, closesMenu: boolean): readonly ChartCommand[] {
        return Object.freeze(commands.map(command => Object.freeze({
            id: command.id,
            label: command.label,
            enabled: command.enabled,
            ...(command.expanded === undefined ? {} : { expanded: command.expanded }),
            ...(command.title === undefined ? {} : { title: command.title }),
            invoke: () => {
                if (!standing() || !command.enabled) {
                    return;
                }
                if (closesMenu) {
                    this.#closeMenu();
                }
                command.run();
            },
        })));
    }

    #paintFilters(): void {
        if (!this.#filtersOpen) {
            this.#filters?.remove();
            this.#filters = null;
            return;
        }
        const axis = this.#options.definition.axis;
        const hint = this.#word(axis.values === 'dates' ? chartWordIds.dateHint : axis.values === 'labels' ? chartWordIds.categoryHint : chartWordIds.numberHint);
        let filters = this.#filters;
        if (filters === null) {
            filters = document.createElement('div');
            filters.className = 'ex-chart-filters';
            filters.setAttribute('role', 'group');
            const field = (cls: string, value: string | null): HTMLLabelElement => {
                const label = document.createElement('label');
                label.className = 'ex-chart-filter';
                const span = document.createElement('span');
                const input = document.createElement('input');
                input.className = cls;
                input.type = 'text';
                input.value = fieldText(value);
                label.append(span, input);
                return label;
            };
            const apply = document.createElement('button');
            apply.type = 'button';
            apply.className = 'ex-chart-button ex-chart-apply';
            filters.append(field('ex-chart-from', this.#fromText), field('ex-chart-to', this.#toText), apply);
            this.#toolbar.after(filters);
            this.#filters = filters;
        }
        filters.setAttribute('aria-label', this.#word(chartWordIds.filters));
        for (const input of filters.querySelectorAll('input')) {
            input.placeholder = hint;
        }
        // The words are written on every paint, so a label changed while the panel is open reaches them.
        for (const [cls, id] of [['ex-chart-from', chartWordIds.from], ['ex-chart-to', chartWordIds.to]] as const) {
            const input = found(filters.querySelector(`.${cls}`), `the ${cls} field`);
            setText(found(input.previousElementSibling, `the ${cls} field's label`), this.#word(id));
        }
        setText(found(filters.querySelector('.ex-chart-apply'), 'the Apply button'), this.#word(chartWordIds.apply));
        const existing = filters.querySelector('.ex-chart-scale');
        let scaleField = existing === null ? null : existing.closest('label');
        if (this.#scaleSwitchable && scaleField === null) {
            scaleField = document.createElement('label');
            scaleField.className = 'ex-chart-filter';
            const span = document.createElement('span');
            const select = document.createElement('select');
            select.className = 'ex-chart-scale';
            for (const value of ['Proportional', 'Categories']) {
                const option = document.createElement('option');
                option.value = value;
                select.append(option);
            }
            scaleField.append(span, select);
            found(filters.querySelector('.ex-chart-apply'), 'the Apply button').after(scaleField);
        } else if (!this.#scaleSwitchable && scaleField !== null) {
            scaleField.remove();
            scaleField = null;
        }
        const select = scaleField === null ? null : scaleField.querySelector('select');
        if (select !== null) {
            setText(found(select.previousElementSibling, 'the Axis label'), this.#word(chartWordIds.axis));
            for (const option of select.options) {
                setText(option, this.#word(option.value === 'Categories' ? chartWordIds.categories : chartWordIds.proportional));
            }
            select.value = (this.#scale ?? axis.effectiveScale) === 'categories' ? 'Categories' : 'Proportional';
        }
        let error = filters.querySelector('.ex-chart-filter-error');
        if (this.#filterError === null) {
            error?.remove();
        } else {
            if (error === null) {
                error = document.createElement('span');
                error.className = 'ex-chart-filter-error';
                error.setAttribute('role', 'alert');
                filters.append(error);
            }
            setText(error, this.#filterError);
        }
    }

    #paintStage(): void {
        const scene = this.#scene;
        const stage = this.#stage;
        const width = this.#width;
        const height = this.#height;
        stage.style.cssText = `width:${n(width)}px;height:${n(height)}px`;
        const svg = this.#svg;
        svg.setAttribute('width', n(width));
        svg.setAttribute('height', n(height));
        setText(this.#title, this.#summary);

        // The parts, each rewritten only when its markup changed, then the band and the guide over them.
        const lh = this.#lineHeight;
        const kept = new Map(this.#parts.map(p => [p.key, p]));
        const drawn = this.#drawn !== null && this.#drawn.built === this.#built && this.#drawn.angle === this.#angle
            ? this.#drawn : { built: this.#built, angle: this.#angle, parts: drawingParts(scene, lh, this.#angle) };
        this.#drawn = drawn;
        const parts = [...drawn.parts, this.#bandPart(), this.#guidePart()].map(part => {
            const old = kept.get(part.key);
            kept.delete(part.key);
            if (old !== undefined && old.markup === part.markup) {
                return old;
            }
            old?.nodes.forEach(node => { node.remove(); });
            return { ...part, nodes: svgNodes(part.markup) };
        });
        for (const gone of kept.values()) {
            gone.nodes.forEach(node => { node.remove(); });
        }
        let cursor: ChildNode = this.#title;
        for (const part of parts) {
            for (const node of part.nodes) {
                if (cursor.nextSibling !== node) {
                    cursor.after(node);
                }
                cursor = node;
            }
        }
        this.#parts = parts;

        // A hit layer over each plot, carrying the plot's offsets, which the pointer's offsets are added to.
        const plots = scene.panels.map(p => `${n(p.plot.x)},${n(p.plot.y)},${n(p.plot.width)},${n(p.plot.height)}`).join(';');
        if (plots !== this.#hitPlots) {
            this.#hits.forEach(hit => { hit.remove(); });
            this.#hits = scene.panels.map((panel, i) => {
                const hit = document.createElement('div');
                hit.className = 'ex-chart-hit';
                hit.setAttribute('data-ex-panel', String(i));
                hit.setAttribute('data-ex-x', n(panel.plot.x));
                hit.setAttribute('data-ex-y', n(panel.plot.y));
                hit.style.cssText = `left:${n(panel.plot.x)}px;top:${n(panel.plot.y)}px;width:${n(panel.plot.width)}px;height:${n(panel.plot.height)}px`;
                return hit;
            });
            svg.after(...this.#hits);
            this.#hitPlots = plots;
        }
        this.#paintTooltip();
    }

    // What the pointer changes: the band, the guide and the tooltip, and nothing else (ADR-0252).
    #paintPointer(): void {
        const parts = [...this.#parts];
        for (const [i, fresh] of [[parts.length - 2, this.#bandPart()], [parts.length - 1, this.#guidePart()]] as const) {
            const old = elementAt(parts, i);
            if (old.markup === fresh.markup) {
                continue;
            }
            const nodes = svgNodes(fresh.markup);
            const before = parts.slice(0, i).flatMap(p => p.nodes).at(-1);
            old.nodes.forEach(node => { node.remove(); });
            if (before === undefined) {
                this.#title.after(...nodes);
            } else {
                before.after(...nodes);
            }
            parts[i] = { ...fresh, nodes };
        }
        this.#parts = parts;
        this.#paintTooltip();
    }

    #paintTooltip(): void {
        const markup = tooltipMarkup(this.#reading, this.#tooltipPlace(), this.#lineHeight,
            count => this.#word(count === 1 ? chartWordIds.oneMoreSeries : chartWordIds.moreSeries, String(count)));
        if (markup === this.#tooltipMarkup) {
            return;
        }
        this.#tooltipMarkup = markup;
        this.#tooltip?.remove();
        this.#tooltip = null;
        if (markup !== '') {
            const holder = document.createElement('div');
            holder.innerHTML = markup;
            const tooltip = found(holder.firstElementChild, 'the tooltip');
            this.#stage.append(tooltip);
            this.#tooltip = tooltip;
        }
    }

    get #plotsTop(): number {
        const first = this.#scene.panels[0];
        return first === undefined ? 0 : first.plot.y;
    }

    get #plotsBottom(): number {
        const last = this.#scene.panels.at(-1);
        return last === undefined ? 0 : last.plot.y + last.plot.height;
    }

    get #guideX(): number | null {
        const i = this.#pointerIndex;
        if (i === null) {
            return null;
        }
        return this.#scene.scatter ? this.#markAt(i).x : elementAt(this.#scene.positions, i)[1];
    }

    #guidePart(): DrawingPart {
        const i = this.#pointerIndex;
        const scatter = this.#scene.scatter && i !== null;
        const top = scatter ? this.#markAt(i).y : this.#plotsTop;
        const bottom = scatter ? this.#markAt(i).y : this.#plotsBottom;
        return { key: 'guide', markup: guideMarkup(this.#guideX, top, bottom, this.#dots, this.#lineHeight) };
    }

    #bandPart(): DrawingPart {
        const scene = this.#scene;
        if (scene.scatter) {
            const q = this.#pressAt;
            const d = this.#dragAt;
            const span = q !== null && q.version === this.#version && d !== null ? [q.x, d.x] as const : null;
            const top = q !== null && d !== null ? Math.min(q.y, d.y) : this.#plotsTop;
            const bottom = q !== null && d !== null ? Math.max(q.y, d.y) : this.#plotsBottom;
            return { key: 'band', markup: bandMarkup(span, top, bottom) };
        }
        const p = this.#press;
        const i = this.#pointerIndex;
        const span = p !== null && p.version === this.#version && i !== null && i !== p.index
            ? [elementAt(scene.positions, p.index)[1], elementAt(scene.positions, i)[1]] as const : null;
        return { key: 'band', markup: bandMarkup(span, this.#plotsTop, this.#plotsBottom) };
    }

    #tooltipPlace(): { readonly left: number; readonly top: number; readonly flip: boolean } {
        const x = this.#guideX;
        const flip = x !== null && x > this.#width / 2;
        const half = this.#lineHeight / 2;
        return {
            left: x === null ? 0 : flip ? x - half : x + half,
            top: this.#pointerIndex === null ? 0 : elementAt(this.#scene.panels, this.#pointerPanel).plot.y,
            flip,
        };
    }

    #paintMenu(): void {
        const at = this.#menu;
        const commands = this.#menuCommands();
        // An open menu is kept while it would be drawn the same, so a paint behind it never takes its focus. A Chrome's
        // frame is kept while it stands where it was asked for: its contents are mounted again in it (#showMenuSlot).
        const mount = this.#options.chrome?.menu;
        const shape = at === null ? null : JSON.stringify(mount === undefined ? [at, this.#name, commandShape(commands)] : [at, 'slot']);
        if (shape === this.#menuShape) {
            return;
        }
        this.#menuShape = shape;
        this.#menuSlot?.hide();
        this.#menuSlot = null;
        this.#menuParts.forEach(part => { part.remove(); });
        this.#menuParts = [];
        if (at === null) {
            return;
        }
        // The menu's frame is ExChart's (ADR-0281): it opens where it was asked for, and a click anywhere else,
        // Escape or a chosen command closes it.
        const backdrop = document.createElement('div');
        backdrop.className = 'ex-chart-menu-backdrop';
        const menu = document.createElement('div');
        menu.className = 'ex-chart-menu';
        menu.setAttribute('role', 'menu');
        menu.setAttribute('aria-label', this.#name);
        menu.tabIndex = -1;
        menu.style.cssText = `left:${n(at.x)}px;top:${n(at.y)}px`;
        if (mount !== undefined) {
            // A Chrome draws the items inside ExChart's frame: the frame is the slot.
            this.#stage.after(backdrop, menu);
            this.#menuParts = [backdrop, menu];
            this.#menuSlot = new ChartSlot<ChartMenuSlotContext>('menu', menu);
            return;
        }
        for (const command of commands) {
            const item = document.createElement('button');
            item.type = 'button';
            item.setAttribute('role', 'menuitem');
            item.className = 'ex-chart-menu-item';
            item.setAttribute('data-ex-command', command.id);
            item.disabled = !command.enabled;
            item.textContent = command.label;
            menu.append(item);
        }
        this.#stage.after(backdrop, menu);
        this.#menuParts = [backdrop, menu];
    }

    // The menu's contents a Chrome draws, mounted again in the frame only when what they are handed changed.
    #showMenuSlot(): void {
        const slot = this.#menuSlot;
        const mount = this.#options.chrome?.menu;
        if (slot === null || mount === undefined) {
            return;
        }
        const commands = this.#menuCommands();
        slot.show(mount, JSON.stringify([this.#name, commandShape(commands)]), standing => Object.freeze({
            name: this.#name,
            commands: this.#slotCommands(commands, standing, true),
            close: () => {
                if (standing()) {
                    this.#closeMenu();
                }
            },
        }));
    }

    #paintNotes(): void {
        const notes = this.#scene.notes;
        if (alike(notes, this.#notesShown)) {
            return;
        }
        this.#notesShown = notes;
        this.#notes?.remove();
        this.#notes = null;
        if (notes.length === 0) {
            return;
        }
        const list = document.createElement('ul');
        list.className = 'ex-chart-notes';
        for (const note of notes) {
            const item = document.createElement('li');
            item.textContent = note;
            list.append(item);
        }
        (this.#menuParts.at(-1) ?? this.#stage).after(list);
        this.#notes = list;
    }

    #paintTable(): void {
        const shown: ChartTable | 'notice' | null = !this.#tableOpen ? null : this.#thinnedAtSource ? 'notice' : this.#table;
        const caption = this.#options.title === undefined || this.#options.title.trim() === '' ? this.#word(chartWordIds.chartData) : this.#options.title;
        if (shown === 'notice' && this.#tableShown === 'notice' && this.#tableBlock !== null) {
            setText(this.#tableBlock, this.#word(chartWordIds.thinnedAtSource));
            return;
        }
        if (shown === this.#tableShown && this.#tableBlock?.querySelector('caption')?.textContent === caption) {
            return;
        }
        if (shown === this.#tableShown && shown !== 'notice' && shown !== null) {
            setText(found(this.#tableBlock === null ? null : this.#tableBlock.querySelector('caption'), 'the table\'s caption'), caption);
            return;
        }
        this.#tableShown = shown;
        this.#tableBlock?.remove();
        this.#tableBlock = null;
        if (shown === null) {
            return;
        }
        if (shown === 'notice') {
            const notice = document.createElement('p');
            notice.className = 'ex-chart-table-notice';
            notice.setAttribute('role', 'note');
            notice.textContent = this.#word(chartWordIds.thinnedAtSource);
            this.#root.append(notice);
            this.#tableBlock = notice;
            return;
        }
        const wrap = document.createElement('div');
        wrap.className = 'ex-chart-table-wrap';
        const table = document.createElement('table');
        table.className = 'ex-chart-table';
        const captionElement = document.createElement('caption');
        captionElement.textContent = caption;
        const head = document.createElement('thead');
        const headRow = document.createElement('tr');
        for (const h of shown.header) {
            const th = document.createElement('th');
            th.scope = 'col';
            th.textContent = h;
            headRow.append(th);
        }
        head.append(headRow);
        const body = document.createElement('tbody');
        for (const row of shown.rows) {
            const tr = document.createElement('tr');
            row.forEach((value, c) => {
                const cell = document.createElement(c === 0 ? 'th' : 'td');
                if (c === 0) {
                    cell.setAttribute('scope', 'row');
                }
                cell.textContent = value;
                tr.append(cell);
            });
            body.append(tr);
        }
        table.append(captionElement, head, body);
        wrap.append(table);
        this.#root.append(wrap);
        this.#tableBlock = wrap;
    }
}

/** The class the built-in toolbar's button for a command carries, which the DOM contract and layer 3 name. */
function toolbarClass(id: string): string {
    switch (id) {
        case chartWordIds.filters: return 'ex-chart-filters-toggle';
        case chartWordIds.resetRange: return 'ex-chart-reset';
        case chartWordIds.table: return 'ex-chart-table-toggle';
        case chartWordIds.downloadCsv: return 'ex-chart-download-csv';
        case chartWordIds.downloadSvg: return 'ex-chart-download-svg';
        case chartWordIds.downloadPng: return 'ex-chart-download-png';
        default: throw new Error(`Internal: the toolbar has no button for ${id}.`);
    }
}

// What a command shows, for telling whether a Chrome's part would be drawn the same (JSON writes an absent one as null).
const commandShape = (commands: readonly Command[]): unknown[] =>
    commands.map(c => [c.id, c.label, c.enabled, c.expanded, c.title]);

// Whether `element` stands in a Chrome's slot, whose contents answer through their context (ADR-0390).
const inSlot = (element: Element): boolean => element.closest('[data-ex-chart-slot]') !== null;

/** The value, which the chart knows is there: `null` throws, naming what was missing. */
function found<T>(value: T | null, what: string): T {
    if (value === null) {
        throw new Error(`Internal: ${what} is missing.`);
    }
    return value;
}

// A field's text: an end not typed is an empty field.
const fieldText = (text: string | null): string => (text === null ? '' : text);

function setText(element: Element, text: string): void {
    if (element.textContent !== text) {
        element.textContent = text;
    }
}

// SVG markup parsed in SVG's namespace, as the nodes it is.
function svgNodes(markup: string): readonly ChildNode[] {
    if (markup === '') {
        return [];
    }
    const holder = document.createElementNS(svgNs, 'g');
    holder.innerHTML = markup;
    return [...holder.childNodes];
}
