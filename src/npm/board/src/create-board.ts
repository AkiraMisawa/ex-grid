// A Board drawn into an element (ADR-0340, ADR-0370 section 3): the C# ExBoard component's markup, made of the family's
// own mounts, ExGrid's createGrid, ExPivot's createPivot and ExChart's createChart, laid out as the Board's rows.
// Nothing of a Board that does not bind is drawn: a part drawn beside a refusal would look like the whole.

import { createChart, type ChartChrome, type ChartHandle, type ChartOptions } from '@ex-grid/chart';
import { createGrid, type GridChrome, type GridHandle, type GridOptions, type TemporalLike } from '@ex-grid/core';
import { BoardRefusedError, type Snapshot } from '@ex-grid/data';
import { createPivot, type PivotChrome, type PivotHandle, type PivotSelectionChangedEvent, type PivotViewOptions } from '@ex-grid/pivot';
import type { BoardDiff } from './board-diff';
import type { BoardOutput } from './board-output';
import type { BoardSource } from './board-source';
import type { BoardCell, BoardReading, BoardRow } from './board-spec';
import { bindBoard, bindBoardThrough, type BoundBoard, type BoundChartView, type BoundGridView, type BoundPivotView } from './bound-board';
import { messageOf } from './must';
import type { ShapedRow } from './shaped-grid';

/** The gap between views and between rows, in CSS pixels. */
export const boardGap = 12;
/** The height a view's title takes, in CSS pixels, written on the title itself so the view under it is drawn at
 * exactly what is left. */
export const boardTitleHeight = 28;

/** What a Board is drawn from: the options of {@link createBoard}. */
export interface BoardOptions {
    /** The Board, as {@link readBoard} reads it. A new one binds again. */
    readonly board: BoardReading;
    /** The Snapshot each input held in memory is bound to, by the input's name. A new record binds again; the same one
     * is not read twice. */
    readonly data: Readonly<Record<string, Snapshot>>;
    /**
     * The source each input held where the data lives is bound to, by the input's name (ADR-0343): asked its shape
     * before anything is drawn, while the Board says so, and then asked for each view's rows, points and the values
     * under a pivot's cells, handed the narrowing the Board's links put on it ({@link bindBoardThrough}). An input
     * named here and in `data` is refused. A new record binds again.
     */
    readonly sources?: Readonly<Record<string, BoardSource>> | undefined;
    /** The Board's width in CSS pixels, from which a chart's width is its share of its row (a chart is drawn at a
     * width it is told); grids and pivots stretch to their share. 1200 unless given. */
    readonly width?: number;
    /** The locale a view's text is written in: a BCP 47 tag, or `invariant`, which it is unless given (ADR-0370
     * section 10). */
    readonly locale?: string;
    /** The Temporal namespace a date typed into a grid's filter is made with (ADR-0350 section 16): the browser's own,
     * or the polyfill the page loads. A grid showing a date column is refused without it, naming the column. */
    readonly temporal?: TemporalLike;
    /** Hears an output the user confirmed (ADR-0340, Q6), and only then: a selection on the way raises nothing. */
    readonly onOutput?: (output: BoardOutput) => void;
    /**
     * What the user changed last time, laid over the Board as it binds (ADR-0340, Q7), as {@link BoardDiff.read}
     * reads it back. What no longer fits the Board is dropped and said above its views, and the rest applies; the
     * Diff that did is then raised through `onDiff`, so a host keeping each one raised keeps no part that will not
     * apply. A new one binds the Board again, unless it is the one already shown, as when the host hands back each
     * Diff raised. A Board bound again for new data keeps what the user has changed since.
     */
    readonly diff?: BoardDiff | undefined;
    /** Raised when the user changes a view — a sort, a filter, a column's width, order, pinning or hiding, a pivot's
     * layout, a chart's series, range or axis — with the whole Board Diff. The Board is never rewritten; the host
     * keeps this ({@link BoardDiff.toJson}) and hands it back as `diff` next time. */
    readonly onDiff?: (diff: BoardDiff) => void;
    /** The Chrome each kind of view is drawn with (ADR-0390): a Wrapper's, or the Consumer's own; the built-in
     * markup where a kind has none. A new one dresses the views again, and the same one costs nothing. */
    readonly chrome?: BoardChrome | undefined;
}

/**
 * The Chrome a Board hands its views, by kind, as the Razor ExBoard takes its `Chrome`, `PivotChrome` and
 * `ChartChrome`. ExBoard has no Wrapper of its own (ADR-0370 section 5): a Board is dressed by the Chromes the
 * family's Wrappers make for each product.
 */
export interface BoardChrome {
    /** The grids' Chrome, and the pivots' report grids' where `pivot` names no grid of its own. */
    readonly grid?: GridChrome<unknown>;
    /** The pivots' Chrome. */
    readonly pivot?: PivotChrome;
    /** The charts' Chrome. */
    readonly chart?: ChartChrome;
}

/** A Board drawn into an element, handed back by {@link createBoard}. */
export interface BoardHandle {
    /** Takes the options again, whole. A new Board, new data, another locale or a new Diff binds it again; another
     * width redraws its charts; the rest costs nothing. */
    update(options: BoardOptions): void;
    /** Stops the Board: its views are destroyed and its element emptied. */
    destroy(): void;
    /** The Board as bound now, or undefined while it is refused. */
    readonly bound: BoundBoard | undefined;
}

/** @internal The family's mounts a Board draws its views with: the real ones, or a layer-2 test's own. */
export interface BoardMounts {
    readonly grid: (element: HTMLElement, options: GridOptions<ShapedRow>) => GridHandle<ShapedRow>;
    readonly pivot: (element: HTMLElement, options: PivotViewOptions) => PivotHandle;
    readonly chart: (element: HTMLElement, options: ChartOptions) => ChartHandle;
}

const familyMounts: BoardMounts = Object.freeze({ grid: createGrid<ShapedRow>, pivot: createPivot, chart: createChart });

/**
 * Draws a Board into `element`, which becomes its root (ADR-0370 section 1): its views laid out in its rows, each
 * input bound to the Snapshot `data` holds under its name and checked against its declaration first (ADR-0341). A
 * Board that does not bind draws nothing but the refusal, naming the input or the path. A Framework Adapter renders
 * the element and hands the Board its props; nothing here assumes React's lifecycle or Blazor's.
 */
export function createBoard(element: HTMLElement, options: BoardOptions): BoardHandle {
    const view = new BoardDrawing(element, options, familyMounts);
    return Object.freeze({
        update: (next: BoardOptions) => { view.update(next); },
        destroy: () => { view.destroy(); },
        get bound() { return view.bound; },
    });
}

interface Mounted {
    readonly destroy: () => void;
    /** Hears the Board narrowed, or the width changed. */
    readonly redraw: () => void;
    /** Hands the view the Board's Chrome again, another one. */
    readonly dress: () => void;
}

/** @internal Draws a Board with the mounts it is given; {@link createBoard} hands it the family's. */
export class BoardDrawing {
    readonly #element: HTMLElement;
    readonly #mounts: BoardMounts;
    #options: BoardOptions;
    #laidPivot: { readonly from: BoardChrome | undefined; readonly pivot: PivotChrome | undefined } | undefined;
    #bound: BoundBoard | undefined;
    // The Diff last handed in: one handed again is not new, and one that differs from it is, even an earlier one.
    #handedDiff: BoardDiff | undefined;
    // The inline style the Board writes on its root, and what the root held there before.
    readonly #ownStyle: Readonly<Record<'display' | 'flexDirection' | 'gap', string>>;
    #raised: BoardDiff | undefined;
    #mounted: Mounted[] = [];
    #unsubscribe: (() => void)[] = [];
    // Set while a binding is under way, and left set by one that threw, so the next update binds again rather than
    // keep the emptied Board.
    #binding = false;
    // Each binding's number: a binding over sources answered after another began is dropped, never drawn over it
    // (spine rule 6).
    #generation = 0;
    #asking: AbortController | undefined;
    #destroyed = false;

    constructor(element: HTMLElement, options: BoardOptions, mounts: BoardMounts) {
        this.#element = element;
        this.#mounts = mounts;
        this.#options = options;
        element.classList.add('ex-board');
        const { display, flexDirection, gap } = element.style;
        this.#ownStyle = Object.freeze({ display, flexDirection, gap });
        Object.assign(element.style, { display: 'flex', flexDirection: 'column', gap: `${boardGap}px` });
        this.#handedDiff = options.diff;
        this.#bind(options.diff);
    }

    get bound(): BoundBoard | undefined {
        return this.#bound;
    }

    update(options: BoardOptions): void {
        if (this.#destroyed) return;
        const was = this.#options;
        this.#options = options;
        const bound = this.#bound;
        // A Diff handed in again is not new; another is new unless it is what the Board already shows, as when the host
        // hands back each Diff raised.
        const newDiff = options.diff !== this.#handedDiff;
        this.#handedDiff = options.diff;
        const shown = () => bound !== undefined && (options.diff === undefined ? bound.diff.isEmpty : options.diff.sameAs(bound.diff));
        if (!this.#binding && options.board === was.board && options.data === was.data && options.sources === was.sources
            && options.locale === was.locale && options.temporal === was.temporal && (!newDiff || shown())) {
            if (options.chrome !== was.chrome) for (const mounted of this.#mounted) mounted.dress();
            if (options.width !== was.width) for (const mounted of this.#mounted) mounted.redraw();
            return;
        }
        // Bound again for a new Board or new data, the user's changes since are kept, through a refusal between too (the
        // Diff last raised is them): only a new Diff replaces them.
        this.#bind(newDiff ? options.diff : bound?.diff ?? this.#raised ?? options.diff);
    }

    destroy(): void {
        if (this.#destroyed) return;
        this.#destroyed = true;
        this.#clear();
        this.#element.classList.remove('ex-board');
        Object.assign(this.#element.style, this.#ownStyle);
    }

    #clear(): void {
        // A binding over sources still on its way is dropped when it comes, and asks them nothing more.
        this.#generation++;
        this.#asking?.abort();
        this.#asking = undefined;
        for (const unsubscribe of this.#unsubscribe) unsubscribe();
        this.#unsubscribe = [];
        for (const mounted of this.#mounted) mounted.destroy();
        this.#mounted = [];
        this.#element.replaceChildren();
        // The Board shown lets go of its questions to its sources.
        this.#bound?.dispose();
        this.#bound = undefined;
    }

    #bind(laid: BoardDiff | undefined): void {
        this.#clear();
        this.#binding = true;
        const { board, data, sources, locale = 'invariant' } = this.#options;
        const root = this.#element;
        if (board.title !== '') root.append(made('h2', 'ex-board-title', board.title));
        const options = laid === undefined ? { locale } : { locale, diff: laid };
        if (sources !== undefined && Object.keys(sources).length > 0) {
            // Its sources are asked their shapes before anything is drawn (ADR-0341), and the Board says so meanwhile.
            const generation = this.#generation;
            const asking = new AbortController();
            this.#asking = asking;
            const said = made('div', 'ex-board-binding', 'Asking the sources for their shapes.');
            said.setAttribute('role', 'status');
            root.append(said);
            this.#binding = false;
            // A Board that does not bind is drawn as its refusal. Sources that fail are said in its place too, and the
            // failure goes on to the host's error path; the next update asks them again, as a binding that threw does.
            bindBoardThrough(board, data, sources, { ...options, signal: asking.signal }).then(bound => { // oxlint-disable-line ex-grid/no-catch -- a refusal is drawn in the Board's place, and any other failure is said there and rethrown
                if (generation !== this.#generation || this.#destroyed) {
                    bound.dispose();
                    return;
                }
                said.remove();
                this.#adopt(bound);
            }, (error: unknown) => {
                if (generation !== this.#generation || this.#destroyed) return;
                said.remove();
                if (error instanceof BoardRefusedError) {
                    this.#refuse(error);
                    return;
                }
                this.#refuse(new BoardRefusedError('', `The sources did not answer: ${messageOf(error)}`, { cause: error }));
                this.#binding = true;
                throw error;
            });
            return;
        }
        let bound: BoundBoard;
        try {
            bound = bindBoard(board, data, options);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a Board that does not bind is drawn as its refusal, naming the path
            if (!(error instanceof BoardRefusedError)) throw error;
            this.#refuse(error);
            this.#binding = false;
            return;
        }
        this.#adopt(bound);
    }

    #refuse(error: BoardRefusedError): void {
        const refusal = made('div', 'ex-board-refusal', error.path === '' ? error.message : `${error.path}: ${error.message}`);
        refusal.setAttribute('role', 'alert');
        this.#element.append(refusal);
    }

    // Draws a bound Board's views and hears them.
    #adopt(bound: BoundBoard): void {
        this.#binding = true;
        const { board } = this.#options;
        const root = this.#element;
        // A grid the core would refuse is the Board's refusal, said before any view is drawn: a part drawn beside it
        // would look like the whole.
        const undrawable = this.#undrawable(bound);
        if (undrawable !== undefined) {
            bound.dispose();
            this.#refuse(undrawable);
            this.#binding = false;
            return;
        }
        this.#bound = bound;
        if (bound.dropped.length > 0) {
            // What of the user's last changes no longer fits is said, never applied to something else (ADR-0340, Q7;
            // spine rule 1).
            const said = made('div', 'ex-board-dropped');
            said.setAttribute('role', 'status');
            const list = made('ul', 'ex-board-dropped-list');
            for (const dropped of bound.dropped) list.append(made('li', 'ex-board-dropped-part', dropped));
            said.append(made('span', 'ex-board-dropped-lead', 'Not laid over, as the Board has changed:'), list);
            root.append(said);
        }
        try {
            for (const row of board.layout) root.append(this.#row(bound, row));
        } finally {
            // A view whose mount threw leaves none of the others running behind it.
            if (this.#mounted.length !== bound.views.length) this.#clear();
        }
        this.#unsubscribe.push(bound.subscribe(() => { for (const mounted of this.#mounted) mounted.redraw(); }));
        // A grid's sort and filter are its source's (ADR-0001), which says when they move; a narrowing moves only its
        // rows, and raises no Diff.
        for (const view of bound.views) {
            if (view.kind !== 'grid') continue;
            let { sorts, filter } = view.source.snapshot();
            this.#unsubscribe.push(view.source.subscribe(() => {
                const now = view.source.snapshot();
                if (now.sorts === sorts && now.filter === filter) return;
                ({ sorts, filter } = now);
                this.#changed();
            }));
        }
        // What is laid over is not the user's change: only a change from it is raised, and the Diff that applied, when
        // some of it did not.
        this.#raised = bound.dropped.length > 0 ? undefined : bound.diff;
        this.#binding = false;
        this.#changed();
    }

    // A grid whose filter offers dates is made with Temporal, which the core refuses to draw without (ADR-0350 section
    // 16); the first such grid of a Board given none, named by its path.
    #undrawable(bound: BoundBoard): BoardRefusedError | undefined {
        if (this.#options.temporal !== undefined) return undefined;
        for (const [at, view] of bound.views.entries()) {
            if (view.kind !== 'grid') continue;
            const dated = view.columns.find(column => column.type === 'date' && column.queryable !== false);
            if (dated !== undefined) {
                return new BoardRefusedError(`views[${at}].grid`, `Column '${dated.name}' holds dates, and a date typed into its filter is made with `
                    + 'Temporal, which the Board was not given: pass `temporal`, the browser\'s Temporal or a polyfill\'s (ADR-0350 section 16).');
            }
        }
        return undefined;
    }

    // Raises the Diff when it now differs from the one last raised.
    #changed(): void {
        const bound = this.#bound;
        if (bound === undefined) return;
        const diff = bound.diff;
        if (this.#raised !== undefined && diff.sameAs(this.#raised)) return;
        this.#raised = diff;
        this.#options.onDiff?.(diff);
    }

    #row(bound: BoundBoard, row: BoardRow): HTMLElement {
        const shares = row.cells.reduce((sum, cell) => sum + cell.width, 0);
        const line = made('div', 'ex-board-row');
        // A row's height is each view's own: a grid's or a pivot's viewport, a chart's stage. A chart's toolbar sits
        // above its stage at the height its stylesheet gives it, so the row grows to hold it; nothing is clipped.
        Object.assign(line.style, { display: 'flex', gap: `${boardGap}px`, alignItems: 'flex-start' });
        for (const cell of row.cells) line.append(this.#cell(bound, row, cell, shares));
        return line;
    }

    #cell(bound: BoundBoard, row: BoardRow, cell: BoardCell, shares: number): HTMLElement {
        const view = bound.view(cell.view);
        const section = made('section', 'ex-board-view');
        section.dataset['exView'] = cell.view;
        section.setAttribute('aria-label', view.view.title === '' ? cell.view : view.view.title);
        Object.assign(section.style, { flex: `${cell.width} 1 0`, minWidth: '0', display: 'flex', flexDirection: 'column' });
        if (view.view.title !== '') {
            const title = made('h3', 'ex-board-view-title', view.view.title);
            Object.assign(title.style, {
                margin: '0', height: `${boardTitleHeight}px`, lineHeight: `${boardTitleHeight}px`, fontSize: 'inherit', overflow: 'hidden',
                whiteSpace: 'nowrap', textOverflow: 'ellipsis',
            });
            section.append(title);
        }
        const host = made('div', 'ex-board-view-host');
        section.append(host);
        switch (view.kind) {
            case 'grid': this.#mounted.push(this.#grid(bound, view, row, host, section)); break;
            case 'pivot': this.#mounted.push(this.#pivot(bound, view, row, host, section)); break;
            case 'chart': this.#mounted.push(this.#chart(bound, view, row, cell, shares, host)); break;
            default: throw new Error(`Internal: ${String(view satisfies never)} is not a view.`);
        }
        return section;
    }

    #grid(bound: BoundBoard, view: BoundGridView, row: BoardRow, host: HTMLElement, section: HTMLElement): Mounted {
        const id = view.view.id;
        const statuses: HTMLElement[] = [];
        // Over a source held on a server, what a selection could not read and what the source did not answer are said
        // under the grid (ADR-0343; spine rule 1).
        const linkStatus = made('span', 'ex-board-link-status');
        linkStatus.setAttribute('role', 'status');
        const say = () => { linkStatus.textContent = statusOf(view); };
        // A gesture the view takes redraws the grid with what it now shows; one it refuses, a round trip behind a
        // hide, changes nothing (spine rule 6).
        const taken = (took: boolean) => {
            if (!took || this.#bound !== bound) return;
            grid.update(options());
            this.#changed();
        };
        const options = (): GridOptions<ShapedRow> => ({
            columns: view.columns,
            source: view.source,
            pinnedColumnCount: view.pinnedColumnCount,
            viewportHeight: row.height,
            viewportWidth: 'stretch',
            locale: this.#locale(),
            ...(this.#options.temporal === undefined ? {} : { temporal: this.#options.temporal }),
            ...(this.#options.chrome?.grid === undefined ? {} : { chrome: this.#options.chrome.grid }),
            onSelectionChanged: next => {
                // A selection a grid raises after the Board was bound again is of a grid no longer shown.
                if (this.#bound !== bound) return;
                // A selection narrows the views its links reach; a dropped one, raised as an empty one, takes the
                // narrowing away with it (ADR-0011).
                bound.select(id, next);
                for (const status of statuses) status.textContent = '';
                say();
            },
            onColumnWidthChanged: change => { taken(view.resize(change.column, change.widthPx)); },
            onColumnOrderChanged: order => { taken(view.reorder(order)); },
            onColumnHidden: column => { taken(view.hide(column)); },
            onPinnedCountChanged: count => { taken(view.pin(count)); },
        });
        const grid = this.#mounts.grid(host, options());
        if (view.fetched !== undefined) {
            section.append(linkStatus);
            this.#unsubscribe.push(view.fetched.subscribe(say));
        }
        for (const output of bound.board.outputs.filter(o => o.from === id)) {
            // Never disabled: with nothing selected, a press says so (spine rule 6).
            const line = made('div', 'ex-board-confirm-row');
            Object.assign(line.style, { marginTop: `${boardGap / 2}px`, display: 'flex', gap: `${boardGap / 2}px`, alignItems: 'center' });
            const button = made('button', 'ex-board-confirm', output.confirm);
            button.type = 'button';
            button.dataset['exOutput'] = output.name;
            const status = made('span', 'ex-board-confirm-status');
            status.setAttribute('role', 'status');
            statuses.push(status);
            button.addEventListener('click', () => {
                if (this.#bound !== bound) return;
                // The grid raises its Selection in the microtask after the gesture that made it, before the browser
                // dispatches the next event, so a press reads the selection it was made against.
                const raised = bound.confirm(output.name);
                if (raised === undefined) {
                    // Over a server, a selection reaching rows not fetched says so, rather than that none was made.
                    status.textContent = view.unread === undefined ? 'Select the rows to hand on first.' : view.unread;
                    return;
                }
                status.textContent = '';
                this.#options.onOutput?.(raised);
            });
            line.append(button, status);
            section.append(line);
        }
        // Its source tells it of the rows a link lets through; a narrowing lets go of what was said of its selection.
        return { destroy: () => { grid.destroy(); }, redraw: say, dress: () => { grid.update(options()); } };
    }

    #pivot(bound: BoundBoard, view: BoundPivotView, row: BoardRow, host: HTMLElement, section: HTMLElement): Mounted {
        const id = view.view.id;
        const status = made('span', 'ex-board-link-status');
        status.setAttribute('role', 'status');
        section.append(status);
        const linked = bound.board.links.some(link => link.from === id);
        let shown = view.source;
        const options = (): PivotViewOptions => ({
            source: view.source,
            layout: view.layout,
            locale: this.#locale(),
            showFieldList: false,
            viewportHeight: row.height,
            viewportWidth: 'stretch',
            ...this.#pivotChrome(),
            // A pivot's cells narrow by the Items they stand for, read from the report its grid held as it raised the
            // selection. One the pivot cannot read narrows nothing, and says so.
            ...(linked ? {
                onSelectionChanged: (event: PivotSelectionChangedEvent) => {
                    if (this.#bound !== bound) return;
                    const cells = event.selection.ranges.length === 0 ? [] : event.items;
                    status.textContent = cells === undefined ? 'The selection cannot be read from the report as drawn, so it narrows nothing.' : '';
                    if (!view.served) {
                        bound.selectCells(id, cells);
                        return;
                    }
                    // Over a server, the source is asked for the values under the cells (ADR-0345); one that does not
                    // answer says so here, and the views it reached are let go.
                    bound.selectCellsThrough(id, cells).catch((error: unknown) => { // oxlint-disable-line ex-grid/no-catch -- the source's failure is said in the pivot's place
                        if (this.#bound !== bound) return;
                        status.textContent = `The source did not answer for the cells selected, so they narrow nothing: ${messageOf(error)}`;
                    });
                },
            } : {}),
            onLayoutChanged: ({ layout }) => {
                if (this.#bound !== bound) return;
                // A new layout is a new report, so what its cells narrowed is let go now, as a narrowing of it lets go
                // (ADR-0011).
                view.changeLayout(layout);
                // An empty selection asks no source, so it is done here and now.
                void bound.selectCellsThrough(id, []);
                pivot.update(options());
                this.#changed();
            },
        });
        const pivot = this.#mounts.pivot(host, options());
        return {
            destroy: () => { pivot.destroy(); },
            redraw: () => {
                // A new source is a refresh: the rows a link lets through now. The report drawn again holds no
                // selection (ADR-0011), so what was said of one goes with it.
                if (view.source === shown) return;
                shown = view.source;
                status.textContent = '';
                pivot.update(options());
            },
            dress: () => { pivot.update(options()); },
        };
    }

    #chart(bound: BoundBoard, view: BoundChartView, row: BoardRow, cell: BoardCell, shares: number, host: HTMLElement): Mounted {
        const refusal = made('div', 'ex-board-refusal');
        refusal.setAttribute('role', 'alert');
        refusal.style.height = `${row.height}px`;
        const stage = made('div', 'ex-board-chart');
        const changed = () => {
            if (this.#bound !== bound) return;
            chart.update(options());
            this.#changed();
        };
        const options = (): ChartOptions => {
            const { width = 1200 } = this.#options;
            return {
                definition: view.reading.definition,
                title: view.reading.title,
                width: Math.max(120, Math.floor((width - boardGap * (row.cells.length - 1)) * cell.width / shares)),
                height: row.height,
                hiddenSeries: view.hiddenSeries,
                // The chart's whole range is null; the view's, undefined.
                range: view.range === undefined ? null : view.range,
                ...(view.scale === undefined ? {} : { scale: view.scale }),
                ...(this.#options.chrome?.chart === undefined ? {} : { chrome: this.#options.chrome.chart }),
                onSeriesVisibilityChanged: ({ key, hidden }) => { if (this.#bound === bound) view.changeVisibility(key, hidden); changed(); },
                onRangeChanged: range => { if (this.#bound === bound) view.changeRange(range === null ? undefined : range); changed(); },
                onScaleChanged: scale => { if (this.#bound === bound) view.changeScale(scale); changed(); },
            };
        };
        const chart = this.#mounts.chart(stage, options());
        const show = () => {
            // The rows a link lets through are not a chart its spec draws: said in its place, never the chart of other
            // rows (spine rule 1).
            if (view.refusal === undefined) host.replaceChildren(stage);
            else {
                refusal.textContent = view.refusal;
                host.replaceChildren(refusal);
            }
        };
        show();
        let drawn = { reading: view.reading, refusal: view.refusal, width: this.#options.width };
        return {
            destroy: () => { chart.destroy(); },
            redraw: () => {
                // Told of every narrowing on the Board: only one that reached this chart, or another width, redraws it.
                const now = { reading: view.reading, refusal: view.refusal, width: this.#options.width };
                if (now.reading === drawn.reading && now.refusal === drawn.refusal && now.width === drawn.width) return;
                drawn = now;
                chart.update(options());
                show();
            },
            dress: () => { chart.update(options()); },
        };
    }

    // The pivots' Chrome: the Board's grid Chrome dresses their report grids where the pivot Chrome names none, as the
    // Razor ExBoard hands its Chrome to its pivots. Laid once for each Chrome handed, so a pivot handed the Board's
    // options again is handed the same object, and does not draw its parts again.
    #pivotChrome(): { readonly chrome?: PivotChrome } {
        const chrome = this.#options.chrome;
        if (this.#laidPivot === undefined || this.#laidPivot.from !== chrome) {
            const grid = chrome?.pivot?.grid ?? chrome?.grid;
            const pivot = chrome?.pivot === undefined && grid === undefined ? undefined
                : { ...chrome?.pivot, ...(grid === undefined ? {} : { grid }) };
            this.#laidPivot = { from: chrome, pivot };
        }
        const { pivot } = this.#laidPivot;
        return pivot === undefined ? {} : { chrome: pivot };
    }

    #locale(): string {
        const { locale = 'invariant' } = this.#options;
        return locale;
    }
}

// What is said under a grid: why its selection narrows nothing, or why its source did not answer; nothing otherwise.
function statusOf(view: BoundGridView): string {
    if (view.unread !== undefined) return view.unread;
    if (view.failure !== undefined) return view.failure;
    return '';
}

function made<K extends keyof HTMLElementTagNameMap>(tag: K, className: string, text?: string): HTMLElementTagNameMap[K] {
    const one = document.createElement(tag);
    one.className = className;
    if (text !== undefined) one.textContent = text;
    return one;
}
