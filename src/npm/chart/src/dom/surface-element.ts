// A 3D surface in the DOM (ADR-0274, ADR-0370 section 1): ExChart's Script Core builds the elements inside
// the instance root to the DOM contract ExSurface.razor writes, so ex-chart.css and layer 3 read both alike.
// It is turned by dragging or with the arrow keys, zoomed with the wheel or + and -, and panned with Shift or
// the right button; the tooltip names the point nearest the pointer, and a click on a tenor or a strike draws
// that slice as a 2D chart beside the surface, where its values are read exactly.
//
// The Razor component needed a script entry to project the surface while it turns (ADR-0255); here the
// projection runs where the pointer is heard, with no round trip. It holds no view state of the Consumer's
// (ADR-0272): the view and the slice are raised when a gesture ends and taken back as options, in the data's
// terms. A gesture begun against one surface is ended unreported when another is drawn (spine rule 6).
import { densityText, type GridDensity } from '@ex-grid/core';
import type { ChartDefinition } from '../definition';
import { roundToDigits } from '../numbers';
import { defaultSurfaceView, SurfaceDefinition, type SurfaceSlice, type SurfaceView } from '../surface';
import { surfaceMesh, surfaceSlicePath, surfaceSummary, type SurfaceMesh, type SurfaceMeshPoint } from '../surface-mesh';
import { ChartElement } from './chart-element';
import { fileName, mediaType, paintPng, save, type ChartFileFormat } from './chart-files';
import { chartWord, chartWordIds, type ChartLabel } from './chart-words';
import { stylesheetName, stylesheetStamp } from './shipped-stylesheet';
import { drawSurface, sliceAxisAt, type DrawnSurfacePoint, type SurfaceDrawing } from './surface-drawing';
import { n } from './svg-markup';

/** What a 3D surface is drawn from, and what it raises. */
export interface SurfaceOptions {
    /** The surface, in the data's terms. */
    readonly definition: SurfaceDefinition;
    /** Its width in CSS pixels: 640 when not given. Geometry is an option, never only a stylesheet value (ADR-0027). */
    readonly width?: number;
    /** Its height in CSS pixels, which the slice's chart beside it shares: 420 when not given. */
    readonly height?: number;
    /** The width of the slice's chart, in CSS pixels: 420 when not given. */
    readonly sliceWidth?: number;
    /** The density the font size is resolved from, as a grid's is (ADR-0028): `standard` when not given. */
    readonly density?: GridDensity;
    /** The surface's title, which opens its accessible summary. */
    readonly title?: string;
    /** A sentence the accessible summary ends with. */
    readonly description?: string;
    /** The view, taken back from `onViewChanged`; `null` or not given is the default view. */
    readonly view?: SurfaceView | null;
    /** Raised when the user ends a turn, a zoom or a pan, with the view it left (ADR-0272). */
    readonly onViewChanged?: (view: SurfaceView) => void;
    /** The slice drawn beside the surface, taken back from `onSliceChanged`; `null` or not given draws none. */
    readonly slice?: SurfaceSlice | null;
    /** Raised when the user clicks a tenor or a strike, with the slice it names (ADR-0274). */
    readonly onSliceChanged?: (slice: SurfaceSlice) => void;
    /** The name a saved file takes, without its extension; the title when not set, then `surface` (ADR-0282). */
    readonly fileName?: string;
    /** The Consumer's words for ExChart's own (ADR-0370 section 10); `japaneseChartWords` for Japanese. */
    readonly label?: ChartLabel;
}

/** How far, in CSS pixels, the pointer may be from a point and still read it. */
const reach = 32;

/** How far a press may move and still be a click, not a turn. */
const clickSlop = 4;

/** How far a drag turns the surface: a whole turn for a drag the box's height, either way (as Plotly's turntable and three.js's OrbitControls do). */
const turnPerHeight = 360;

/** How far one wheel step of 100 pixels zooms; the wheel zooms in proportion to how far it moved. */
const wheelZoom = Math.log(1.1) / 100;

const svgNs = 'http://www.w3.org/2000/svg';

interface Drag {
    readonly id: number;
    readonly x: number;
    readonly y: number;
    readonly view: SurfaceView;
    readonly pan: boolean;
    readonly version: number;
    moved: boolean;
}

interface Tip {
    readonly k: number;
    readonly x: number;
    readonly y: number;
}

const clamp = (v: number, lo: number, hi: number): number => Math.max(lo, Math.min(hi, v));

/** An angle brought into (-180, 180]. */
function turn(degrees: number): number {
    const d = ((degrees % 360) + 360) % 360;
    return d > 180 ? d - 360 : d;
}

const sameView = (a: SurfaceView, b: SurfaceView): boolean =>
    a.yaw === b.yaw && a.pitch === b.pitch && a.zoom === b.zoom && a.panX === b.panX && a.panY === b.panY;

const sameSlice = (a: SurfaceSlice | null, b: SurfaceSlice | null): boolean =>
    a === b || (a !== null && b !== null && a.axis === b.axis && a.value === b.value);

// A view as it is raised: to a hundredth of a degree and a ten-thousandth of the zoom and the box, as the
// Razor component rounds it, so a view taken back and raised again is the same view.
const rounded = (v: SurfaceView): SurfaceView => Object.freeze({
    yaw: roundToDigits(v.yaw, 2), pitch: roundToDigits(v.pitch, 2), zoom: roundToDigits(v.zoom, 4),
    panX: roundToDigits(v.panX, 4), panY: roundToDigits(v.panY, 4),
});

const sameTakenView = (a: SurfaceView | null, b: SurfaceView | null): boolean =>
    a === b || (a !== null && b !== null && sameView(a, b));

const nameOr = (title: string, fallback: string): string => (title.trim() === '' ? fallback : title);

const blank = (text: string | undefined): text is undefined => text === undefined || text.trim() === '';

/** A 3D surface drawn into an element, which becomes its instance root. */
export class SurfaceElement {
    readonly #root: HTMLElement;
    #options: SurfaceOptions;
    #destroyed = false;

    // What the user changed, as last taken from an option or a gesture.
    #view: SurfaceView = defaultSurfaceView;
    #held: SurfaceView = defaultSurfaceView;
    #slice: SurfaceSlice | null = null;
    #taken: { readonly view: SurfaceView | null; readonly slice: SurfaceSlice | null } | undefined;

    // The mesh and what it was built from.
    #builtFrom: readonly unknown[] = [];
    #mesh: SurfaceMesh | undefined;
    #summary = '';
    #version = 0;
    #slicePath: readonly (readonly SurfaceMeshPoint[])[] | null = null;
    #sliceChart: ChartDefinition | undefined;
    #sliceNote: string | null = null;
    #sliceFrom: readonly unknown[] = [];

    // The pointer.
    #drawing: SurfaceDrawing | null = null;
    #drag: Drag | null = null;
    #tip: Tip | null = null;
    #tableOpen = false;

    // The elements.
    readonly #notice: HTMLDivElement;
    readonly #toolbar: HTMLDivElement;
    readonly #reset: HTMLButtonElement;
    readonly #tableToggle: HTMLButtonElement;
    readonly #downloads: HTMLSpanElement;
    readonly #svgButton: HTMLButtonElement;
    readonly #pngButton: HTMLButtonElement;
    readonly #row: HTMLDivElement;
    readonly #stage: HTMLDivElement;
    readonly #box: HTMLDivElement;
    readonly #svg: SVGSVGElement;
    #markup = '';
    #tooltip: HTMLDivElement | null = null;
    #tooltipShape = '';
    #sliceBlock: HTMLDivElement | null = null;
    #sliceHandle: ChartElement | null = null;
    #notes: HTMLUListElement | null = null;
    #notesShape = '';
    #tableBlock: HTMLDivElement | null = null;
    #tableShape = '';
    #tableOf: SurfaceDefinition | undefined;
    readonly #unlisten: readonly (() => void)[];

    constructor(root: HTMLElement, options: SurfaceOptions) {
        this.#root = root;
        this.#options = SurfaceElement.#checked(options);

        root.replaceChildren();
        root.classList.add('ex-chart', 'ex-chart-surface');
        this.#notice = document.createElement('div');
        this.#notice.setAttribute('data-ex-notice', 'stylesheet');
        this.#notice.setAttribute('data-ex-stylesheet', stylesheetName);
        this.#notice.setAttribute('data-ex-stamp', stylesheetStamp);
        this.#notice.style.display = 'none';
        this.#toolbar = document.createElement('div');
        this.#toolbar.className = 'ex-chart-toolbar';
        this.#toolbar.setAttribute('role', 'toolbar');
        this.#reset = toolbarButton('ex-chart-surface-reset');
        this.#tableToggle = toolbarButton('ex-chart-table-toggle');
        this.#downloads = document.createElement('span');
        this.#downloads.className = 'ex-chart-downloads';
        this.#downloads.setAttribute('role', 'group');
        this.#svgButton = toolbarButton('ex-chart-download-svg');
        this.#pngButton = toolbarButton('ex-chart-download-png');
        this.#downloads.append(this.#svgButton, this.#pngButton);
        this.#toolbar.append(this.#reset, this.#tableToggle, this.#downloads);
        this.#row = document.createElement('div');
        this.#row.className = 'ex-chart-surface-row';
        this.#stage = document.createElement('div');
        this.#stage.className = 'ex-chart-stage';
        this.#box = document.createElement('div');
        this.#box.className = 'ex-chart-surface-box';
        this.#box.tabIndex = 0;
        this.#box.setAttribute('role', 'img');
        this.#svg = document.createElementNS(svgNs, 'svg');
        this.#svg.setAttribute('class', 'ex-chart-surface-svg');
        this.#svg.setAttribute('aria-hidden', 'true');
        this.#box.append(this.#svg);
        this.#stage.append(this.#box);
        this.#row.append(this.#stage);
        root.append(this.#notice, this.#toolbar, this.#row);

        const unlisten: (() => void)[] = [];
        const listen = <K extends keyof HTMLElementEventMap>(target: HTMLElement, type: K, handle: (event: HTMLElementEventMap[K]) => void,
            passive = true): void => {
            target.addEventListener(type, handle, { passive });
            unlisten.push(() => { target.removeEventListener(type, handle); });
        };
        const box = this.#box;
        listen(box, 'pointerdown', e => { this.#onDown(e); });
        listen(box, 'pointermove', e => { this.#onMove(e); });
        listen(box, 'pointerup', e => { this.#onUp(e); });
        listen(box, 'pointercancel', e => { this.#onCancel(e); });
        listen(box, 'pointerleave', () => { this.#onLeave(); });
        listen(box, 'keydown', e => { this.#onKey(e); }, false);
        listen(box, 'wheel', e => { this.#onWheel(e); }, false);
        listen(box, 'contextmenu', e => { this.#onMenu(e); }, false);
        listen(this.#toolbar, 'click', e => { this.#onToolbar(e); });
        this.#unlisten = unlisten;
        this.#take(this.#options);
        this.#rebuild();
        this.#paint();
    }

    /** Takes the options again, whole: what did not change costs nothing. */
    update(options: SurfaceOptions): void {
        this.#alive();
        this.#options = SurfaceElement.#checked(options);
        this.#take(this.#options);
        this.#rebuild();
        this.#paint();
    }

    /** Stops the surface: it hears nothing more and empties its element. */
    destroy(): void {
        if (this.#destroyed) {
            return;
        }
        this.#destroyed = true;
        for (const stop of this.#unlisten) {
            stop();
        }
        this.#sliceHandle?.destroy();
        this.#sliceHandle = null;
        this.#root.replaceChildren();
        this.#root.classList.remove('ex-chart', 'ex-chart-surface');
        this.#root.style.removeProperty('font-size');
        this.#root.style.removeProperty('--ex-chart-line-height');
    }

    /**
     * The drawing as it stands — turned, zoomed and panned as the user left it — as SVG text that carries its
     * size and its colours resolved, for a file (ADR-0282): painted off the page, it has no stylesheet to read.
     * `undefined` once the surface is destroyed.
     */
    svgText(): string | undefined {
        if (this.#destroyed) {
            return undefined;
        }
        const width = this.#width;
        const height = this.#height;
        const svg = this.#svg;
        const copy = svg.cloneNode(true);
        if (!(copy instanceof SVGSVGElement)) {
            throw new Error('Internal: a copy of the surface\'s drawing is not an SVG element.');
        }
        copy.setAttribute('xmlns', svgNs);
        copy.setAttribute('width', n(width));
        copy.setAttribute('height', n(height));
        copy.setAttribute('viewBox', `0 0 ${n(width)} ${n(height)}`);
        copy.removeAttribute('aria-hidden');
        copy.removeAttribute('class');
        const rootStyle = getComputedStyle(svg);
        copy.setAttribute('style', `font-family:${rootStyle.fontFamily};font-size:${rootStyle.fontSize};background:${getComputedStyle(this.#box).backgroundColor}`);
        const from = svg.querySelectorAll('*');
        const to = copy.querySelectorAll('*');
        const properties = ['fill', 'fill-opacity', 'stroke', 'stroke-width', 'stroke-opacity', 'opacity', 'paint-order', 'stroke-linejoin', 'font-weight'];
        from.forEach((element, k) => {
            const style = getComputedStyle(element);
            const target = to.item(k);
            target.setAttribute('style', properties.map(p => `${p}:${style.getPropertyValue(p)}`).join(';'));
            target.removeAttribute('class');
        });
        return new XMLSerializer().serializeToString(copy);
    }

    /**
     * Saves the drawing as it stands as an SVG or a PNG file, the PNG painted from that SVG at the screen's
     * Device Pixel (ADR-0282). One call is one save. False for a CSV — a surface's values are read in "View as
     * table" — and once the surface is destroyed.
     */
    async download(format: ChartFileFormat): Promise<boolean> {
        const text = format === 'csv' ? undefined : this.svgText();
        if (text === undefined) {
            return false;
        }
        const { title } = this.#options;
        const name = fileName(this.#options.fileName, blank(title) ? 'surface' : title, format);
        if (format === 'svg') {
            save(name, mediaType(format), text);
        } else {
            save(name, mediaType(format), await paintPng(text, this.#width, this.#height));
        }
        return true;
    }

    // ---- Options ----

    static #checked(options: SurfaceOptions): SurfaceOptions {
        // An untyped caller's options are checked here, at the boundary (CONVENTIONS.md).
        const { definition, width, height, sliceWidth, onViewChanged, onSliceChanged, label }: {
            readonly definition?: unknown; readonly width?: unknown; readonly height?: unknown; readonly sliceWidth?: unknown;
            readonly onViewChanged?: unknown; readonly onSliceChanged?: unknown; readonly label?: unknown;
        } = options;
        if (!(definition instanceof SurfaceDefinition)) {
            throw new TypeError('An ExSurface is given its definition, a SurfaceDefinition.');
        }
        for (const [name, value] of [['width', width], ['height', height], ['sliceWidth', sliceWidth]] as const) {
            if (value !== undefined && !(typeof value === 'number' && Number.isFinite(value) && value > 0)) {
                throw new RangeError(`An ExSurface's ${name} is a finite, positive number of CSS pixels. (${name}: ${typeof value === 'number' ? String(value) : `a ${typeof value}`})`);
            }
        }
        for (const [name, value] of [['onViewChanged', onViewChanged], ['onSliceChanged', onSliceChanged], ['label', label]] as const) {
            if (value !== undefined && typeof value !== 'function') {
                throw new TypeError(`An ExSurface's ${name} is a function.`);
            }
        }
        return options;
    }

    // Taken back as plain options (ADR-0272): an option that changed replaces what the user last did; one
    // that did not leaves it. A view raised here comes back as an echo, equal to what is held, and leaves the
    // view where the keys and the drags left it, unrounded.
    #take(options: SurfaceOptions): void {
        const view = options.view === undefined ? null : options.view;
        const slice = options.slice === undefined ? null : options.slice;
        const taken = this.#taken;
        if (taken === undefined || !sameTakenView(taken.view, view)) {
            const next = view === null ? defaultSurfaceView : rounded(view);
            if (taken === undefined || !sameView(next, this.#held)) {
                this.#view = view === null ? defaultSurfaceView : view;
                this.#drag = null;
            }
            this.#held = next;
        }
        if (taken === undefined || !sameSlice(taken.slice, slice)) {
            this.#slice = slice;
        }
        this.#taken = { view, slice };
    }

    get #width(): number {
        const { width = 640 } = this.#options;
        return width;
    }

    get #height(): number {
        const { height = 420 } = this.#options;
        return height;
    }

    get #sliceWidth(): number {
        const { sliceWidth = 420 } = this.#options;
        return sliceWidth;
    }

    get #density(): GridDensity {
        const { density = 'standard' } = this.#options;
        return density;
    }

    get #definition(): SurfaceDefinition {
        return this.#options.definition;
    }

    #word(id: string, ...args: readonly string[]): string {
        return chartWord(id, this.#options.label, ...args);
    }

    #alive(): void {
        if (this.#destroyed) {
            throw new Error('This surface was destroyed; create another.');
        }
    }

    #rebuild(): void {
        const o = this.#options;
        const builtFrom = [o.definition, this.#width, this.#height, o.title, o.description];
        if (!builtFrom.every((v, i) => v === this.#builtFrom[i])) {
            if (this.#builtFrom[0] !== o.definition || this.#mesh === undefined) {
                this.#mesh = surfaceMesh(o.definition);
            }
            this.#builtFrom = builtFrom;
            this.#summary = surfaceSummary(o.definition, o.title, o.description);
            // A gesture begun against another surface ends unreported, and the point read on it is gone.
            this.#version += 1;
            this.#drag = null;
            this.#tip = null;
        }
        this.#buildSlice();
    }

    // The slice, in the data's terms: one the surface does not hold is said, not drawn (ADR-0272).
    #buildSlice(): void {
        const definition = this.#definition;
        const slice = this.#slice;
        const from = [definition, slice?.axis, slice?.value];
        if (from.every((v, i) => v === this.#sliceFrom[i])) {
            return;
        }
        this.#sliceFrom = from;
        this.#sliceChart = slice === null ? undefined : definition.sliceChart(slice);
        this.#slicePath = slice === null || this.#sliceChart === undefined ? null : surfaceSlicePath(definition, slice);
        if (slice === null || this.#sliceChart !== undefined) {
            this.#sliceNote = null;
            return;
        }
        const axis = slice.axis === 'x' ? definition.x : definition.y;
        this.#sliceNote = this.#word(definition.isMesh ? chartWordIds.noSliceOnMesh : chartWordIds.noSliceOnGrid,
            nameOr(axis.title, slice.axis), axis.writeValue(slice.value));
    }

    // ---- The pointer and the keys (ADR-0274) ----

    #redraw(): void {
        const mesh = this.#mesh;
        if (mesh === undefined) {
            throw new Error('Internal: the surface is drawn before its mesh is built.');
        }
        const drawing = drawSurface(mesh, this.#view, this.#slicePath, this.#width, this.#height);
        this.#drawing = drawing;
        const svg = this.#svg;
        svg.setAttribute('width', n(this.#width));
        svg.setAttribute('height', n(this.#height));
        svg.setAttribute('viewBox', `0 0 ${n(this.#width)} ${n(this.#height)}`);
        if (drawing.markup !== this.#markup) {
            this.#markup = drawing.markup;
            svg.innerHTML = drawing.markup;
        }
    }

    #nearest(x: number, y: number): DrawnSurfacePoint | null {
        let best: DrawnSurfacePoint | null = null;
        let bestDistance = reach * reach;
        for (const p of this.#drawing === null ? [] : this.#drawing.points) {
            const d = (p.x - x) ** 2 + (p.y - y) ** 2;
            if (d <= bestDistance) {
                best = p;
                bestDistance = d;
            }
        }
        return best;
    }

    // The point read in the tooltip changes only when the pointer comes nearer another (ADR-0255).
    #point(point: DrawnSurfacePoint | null, x: number, y: number): void {
        const tip = point === null || this.#definition.node(point.k)[2] === null ? null : { k: point.k, x, y };
        if ((tip === null) === (this.#tip === null) && (tip === null || tip.k === this.#tip?.k)) {
            return;
        }
        this.#tip = tip;
        this.#paintTooltip();
    }

    // The positions are the event's offsets on the box itself: the drawing takes no pointer events, so the
    // box is always the target.
    #own(event: Event): boolean {
        return event.target === this.#box;
    }

    #onDown(event: PointerEvent): void {
        // One pointer at a time: a second finger, or a second button, does not join the gesture.
        if (this.#drag !== null || (event.button !== 0 && event.button !== 2) || !this.#own(event)) {
            return;
        }
        this.#drag = {
            id: event.pointerId, x: event.offsetX, y: event.offsetY, view: this.#view, pan: event.shiftKey || event.button === 2,
            moved: false, version: this.#version,
        };
        this.#box.setPointerCapture(event.pointerId);
    }

    #onMove(event: PointerEvent): void {
        const drag = this.#drag;
        if (drag !== null && event.pointerId !== drag.id) {
            return;
        }
        if (drag !== null) {
            const dx = event.offsetX - drag.x;
            const dy = event.offsetY - drag.y;
            if (!drag.moved && dx * dx + dy * dy < clickSlop * clickSlop) {
                return;
            }
            drag.moved = true;
            if (drag.pan) {
                this.#view = { ...drag.view, panX: drag.view.panX + dx / this.#width, panY: drag.view.panY + dy / this.#height };
            } else {
                // The surface is held where it was pressed: a drag to the right brings its near side to the
                // right, a drag down tips its top toward the viewer.
                const degrees = turnPerHeight / Math.max(1, this.#height);
                this.#view = { ...drag.view, yaw: turn(drag.view.yaw + dx * degrees), pitch: clamp(drag.view.pitch + dy * degrees, -90, 90) };
            }
            this.#point(null, 0, 0);
            this.#redraw();
            return;
        }
        if (!this.#own(event)) {
            return;
        }
        this.#point(this.#nearest(event.offsetX, event.offsetY), event.offsetX, event.offsetY);
    }

    #onUp(event: PointerEvent): void {
        const pressed = this.#drag;
        if (pressed !== null && event.pointerId !== pressed.id) {
            return;
        }
        this.#drag = null;
        if (pressed === null || pressed.version !== this.#version) {
            return;
        }
        if (pressed.moved) {
            this.#raiseView();
            return;
        }
        if (pressed.pan) {
            return;
        }
        const point = this.#nearest(pressed.x, pressed.y);
        const drawing = this.#drawing;
        if (point === null || drawing === null) {
            return;
        }
        const axis = sliceAxisAt(drawing, point, pressed.x, pressed.y);
        // A click slices along the line it fell on: a grid's tenor or strike, or on a mesh the position of
        // the node it fell on, which cuts its triangles (ADR-0311).
        const [x, y] = this.#definition.node(point.k);
        const slice: SurfaceSlice = Object.freeze({ axis, value: axis === 'x' ? x : y });
        this.#slice = slice;
        this.#buildSlice();
        this.#paint();
        this.#options.onSliceChanged?.(slice);
    }

    #onCancel(event: PointerEvent): void {
        const drag = this.#drag;
        if (drag !== null && event.pointerId !== drag.id) {
            return;
        }
        this.#drag = null;
        if (drag !== null && drag.moved && drag.version === this.#version) {
            this.#raiseView();
        }
    }

    #onLeave(): void {
        if (this.#drag === null) {
            this.#point(null, 0, 0);
        }
    }

    #onKey(event: KeyboardEvent): void {
        if (event.altKey || event.ctrlKey || event.metaKey) {
            return;
        }
        const step = arrowStep(event.key);
        const view = this.#view;
        if (step !== null && event.shiftKey) {
            this.#view = { ...view, panX: view.panX + step[0] * 0.05, panY: view.panY + step[1] * 0.05 };
        } else if (step !== null) {
            // An arrow turns the surface as a drag the same way would.
            this.#view = { ...view, yaw: turn(view.yaw + step[0] * 5), pitch: clamp(view.pitch + step[1] * 5, -90, 90) };
        } else if (event.key === '+' || event.key === '=') {
            this.#view = { ...view, zoom: clamp(view.zoom * 1.25, 0.5, 8) };
        } else if (event.key === '-') {
            this.#view = { ...view, zoom: clamp(view.zoom / 1.25, 0.5, 8) };
        } else {
            return;
        }
        event.preventDefault();
        this.#redraw();
        this.#raiseView();
    }

    #onWheel(event: WheelEvent): void {
        if (event.deltaY === 0) {
            return;
        }
        event.preventDefault();
        // A trackpad sends many small steps and a mouse a few large ones; each zooms by how far it moved, in
        // pixels whatever the event's unit.
        const pixels = event.deltaY * (event.deltaMode === 1 ? 16 : event.deltaMode === 2 ? Math.max(1, this.#height) : 1);
        this.#view = { ...this.#view, zoom: clamp(this.#view.zoom * Math.exp(-pixels * wheelZoom), 0.5, 8) };
        this.#redraw();
        this.#raiseView();
    }

    // The right button pans, so it opens no menu over the drawing.
    #onMenu(event: MouseEvent): void {
        if (this.#own(event)) {
            event.preventDefault();
        }
    }

    #raiseView(): void {
        const view = rounded(this.#view);
        if (sameView(view, this.#held)) {
            return;
        }
        this.#held = view;
        this.#paintToolbar();
        this.#options.onViewChanged?.(view);
    }

    #onToolbar(event: MouseEvent): void {
        const target = event.target;
        if (!(target instanceof Element)) {
            return;
        }
        const button = target.closest('button');
        if (button === this.#reset) {
            this.#view = defaultSurfaceView;
            this.#held = defaultSurfaceView;
            this.#drag = null;
            this.#paint();
            this.#options.onViewChanged?.(defaultSurfaceView);
        } else if (button === this.#tableToggle) {
            this.#tableOpen = !this.#tableOpen;
            this.#paint();
        } else if (button === this.#svgButton) {
            void this.download('svg');
        } else if (button === this.#pngButton) {
            void this.download('png');
        }
    }

    // ---- Painting ----

    #paint(): void {
        const text = densityText(this.#density);
        const root = this.#root;
        // Only the surface's own properties, so a style the Consumer gave the element stays.
        root.style.setProperty('font-size', `${n(text.fontSizePx)}px`);
        root.style.setProperty('--ex-chart-line-height', `${n(Math.ceil(text.fontSizePx * 1.4))}px`);
        const size = `width:${n(this.#width)}px;height:${n(this.#height)}px`;
        this.#stage.style.cssText = size;
        this.#box.style.cssText = size;
        this.#box.setAttribute('aria-roledescription', this.#word(chartWordIds.surfaceRole));
        this.#box.setAttribute('aria-label', this.#summary);
        this.#paintToolbar();
        this.#redraw();
        this.#paintTooltip();
        this.#paintSliceChart();
        this.#paintNotes();
        this.#paintTable();
    }

    #paintToolbar(): void {
        setText(this.#notice, this.#word(chartWordIds.stylesheetNotice));
        const { title } = this.#options;
        this.#toolbar.setAttribute('aria-label', blank(title) ? this.#word(chartWordIds.surface) : title);
        setText(this.#reset, this.#word(chartWordIds.resetView));
        this.#reset.disabled = sameView(this.#held, defaultSurfaceView);
        setText(this.#tableToggle, this.#word(chartWordIds.table));
        this.#tableToggle.setAttribute('aria-expanded', this.#tableOpen ? 'true' : 'false');
        this.#downloads.setAttribute('aria-label', this.#word(chartWordIds.downloads));
        setText(this.#svgButton, this.#word(chartWordIds.downloadSvg));
        setText(this.#pngButton, this.#word(chartWordIds.downloadPng));
    }

    #paintTooltip(): void {
        const tip = this.#tip;
        const definition = this.#definition;
        const font = densityText(this.#density).fontSizePx;
        const lines: (readonly [cls: string, name: string, value: string])[] = [];
        let note: string | null = null;
        if (tip !== null) {
            const [x, y, z] = definition.node(tip.k);
            if (z === null) {
                throw new Error('Internal: the tooltip reads a hole.');
            }
            lines.push(['ex-chart-tooltip-line', nameOr(definition.x.title, 'x'), definition.x.writeValue(x)]);
            lines.push(['ex-chart-tooltip-line', nameOr(definition.y.title, 'y'), definition.y.writeValue(y)]);
            lines.push(['ex-chart-tooltip-line ex-chart-tooltip-position', nameOr(definition.zTitle, this.#word(chartWordIds.surfaceValue)), definition.writeZ(z)]);
            const method = definition.method;
            if (method !== undefined) {
                note = definition.isInterpolatedNode(tip.k) ? this.#word(chartWordIds.interpolated, method) : this.#word(chartWordIds.quoted);
            }
        }
        const gap = Math.ceil(font);
        const flip = tip !== null && tip.x > this.#width / 2;
        const left = tip === null ? 0 : flip ? tip.x - gap : tip.x + gap;
        const top = tip === null ? 0 : tip.y + gap;
        const shape = tip === null ? '' : JSON.stringify([lines, note, flip, left, top]);
        if (shape === this.#tooltipShape) {
            return;
        }
        this.#tooltipShape = shape;
        this.#tooltip?.remove();
        this.#tooltip = null;
        if (tip === null) {
            return;
        }
        const tooltip = document.createElement('div');
        tooltip.className = `ex-chart-tooltip ex-chart-surface-tooltip${flip ? ' ex-chart-tooltip-flip' : ''}`;
        tooltip.style.cssText = `left:${n(left)}px;top:${n(top)}px`;
        tooltip.setAttribute('role', 'status');
        for (const [cls, name, value] of lines) {
            const line = document.createElement('div');
            line.className = cls;
            const nameSpan = document.createElement('span');
            nameSpan.className = 'ex-chart-tooltip-name';
            nameSpan.textContent = name;
            const valueSpan = document.createElement('span');
            valueSpan.className = 'ex-chart-tooltip-value';
            valueSpan.textContent = value;
            line.append(nameSpan, valueSpan);
            tooltip.append(line);
        }
        if (note !== null) {
            const noteLine = document.createElement('div');
            noteLine.className = 'ex-chart-tooltip-note';
            noteLine.textContent = note;
            tooltip.append(noteLine);
        }
        this.#stage.append(tooltip);
        this.#tooltip = tooltip;
    }

    #paintSliceChart(): void {
        const chart = this.#sliceChart;
        if (chart === undefined) {
            this.#sliceHandle?.destroy();
            this.#sliceHandle = null;
            this.#sliceBlock?.remove();
            this.#sliceBlock = null;
            return;
        }
        const definition = this.#definition;
        const first = chart.series[0];
        const options = {
            definition: chart,
            ...(first === undefined ? {} : { title: first.name }),
            width: this.#sliceWidth,
            height: this.#height,
            density: this.#density,
            // The slice is read on the surface's vertical scale (ADR-0312).
            ...(definition.effectiveZScale === 'logarithmic' ? { yScale: 'logarithmic' as const } : {}),
            ...(this.#options.label === undefined ? {} : { label: this.#options.label }),
        };
        if (this.#sliceHandle === null || this.#sliceBlock === null) {
            const block = document.createElement('div');
            block.className = 'ex-chart-surface-slice-chart';
            const element = document.createElement('div');
            block.append(element);
            this.#row.append(block);
            this.#sliceBlock = block;
            this.#sliceHandle = new ChartElement(element, options);
        } else {
            this.#sliceHandle.update(options);
        }
    }

    #paintNotes(): void {
        const notes = [...this.#definition.allNotes, ...(this.#sliceNote === null ? [] : [this.#sliceNote])];
        const shape = JSON.stringify(notes);
        if (shape === this.#notesShape) {
            return;
        }
        this.#notesShape = shape;
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
        this.#row.after(list);
        this.#notes = list;
    }

    #paintTable(): void {
        const definition = this.#definition;
        const { title } = this.#options;
        const caption = blank(title) ? this.#word(chartWordIds.surfaceData) : title;
        const shape = this.#tableOpen ? JSON.stringify([caption, this.#word(chartWordIds.surfaceValue)]) : '';
        if (shape === this.#tableShape && this.#tableOf === definition) {
            return;
        }
        this.#tableShape = shape;
        this.#tableOf = definition;
        this.#tableBlock?.remove();
        this.#tableBlock = null;
        if (!this.#tableOpen) {
            return;
        }
        const x = nameOr(definition.x.title, 'x');
        const y = nameOr(definition.y.title, 'y');
        const headRow = document.createElement('tr');
        const body = document.createElement('tbody');
        const nodes = definition.nodes;
        if (nodes === undefined) {
            headRow.append(cell('th', `${y} \\ ${x}`, 'col'), ...definition.x.values.map((_, i) => cell('th', definition.x.write(i), 'col')));
            definition.y.values.forEach((_, j) => {
                const tr = document.createElement('tr');
                tr.append(cell('th', definition.y.write(j), 'row'));
                definition.x.values.forEach((__, i) => {
                    const v = definition.at(i, j);
                    tr.append(cell('td', v === null ? '' : definition.writeZ(v)));
                });
                body.append(tr);
            });
        } else {
            // A mesh has no grid to tabulate: its nodes are listed, each where it lies (ADR-0311).
            headRow.append(cell('th', x, 'col'), cell('th', y, 'col'), cell('th', nameOr(definition.zTitle, this.#word(chartWordIds.surfaceValue)), 'col'));
            for (const q of nodes) {
                const tr = document.createElement('tr');
                tr.append(cell('td', definition.x.writeValue(q.x)), cell('td', definition.y.writeValue(q.y)),
                    cell('td', q.z === null ? '' : definition.writeZ(q.z)));
                body.append(tr);
            }
        }
        const head = document.createElement('thead');
        head.append(headRow);
        const captionElement = document.createElement('caption');
        captionElement.textContent = caption;
        const table = document.createElement('table');
        table.className = 'ex-chart-table';
        table.append(captionElement, head, body);
        const wrap = document.createElement('div');
        wrap.className = 'ex-chart-table-wrap';
        wrap.append(table);
        this.#root.append(wrap);
        this.#tableBlock = wrap;
    }

}

function toolbarButton(cls: string): HTMLButtonElement {
    const b = document.createElement('button');
    b.type = 'button';
    b.className = `ex-chart-button ${cls}`;
    return b;
}

function cell(tag: 'th' | 'td', content: string, scope?: 'col' | 'row'): HTMLTableCellElement {
    const c = document.createElement(tag);
    if (scope !== undefined) {
        c.setAttribute('scope', scope);
    }
    c.textContent = content;
    return c;
}

function arrowStep(key: string): readonly [number, number] | null {
    switch (key) {
        case 'ArrowLeft': return [-1, 0];
        case 'ArrowRight': return [1, 0];
        case 'ArrowUp': return [0, -1];
        case 'ArrowDown': return [0, 1];
        default: return null;
    }
}

function setText(element: Element, text: string): void {
    if (element.textContent !== text) {
        element.textContent = text;
    }
}
