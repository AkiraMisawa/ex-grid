// A Board drawn into an element (ADR-0340, ADR-0370 section 3): the C# ExBoard component's markup, made of the family's
// own mounts, ExGrid's createGrid, ExPivot's createPivot and ExChart's createChart, laid out as the Board's rows.
// Nothing of a Board that does not bind is drawn: a part drawn beside a refusal would look like the whole.

import { createChart, type ChartHandle, type ChartOptions } from '@ex-grid/chart';
import { createGrid, type GridHandle, type GridOptions, type GridSelection, type TemporalLike } from '@ex-grid/core';
import { BoardRefusedError, type Snapshot } from '@ex-grid/data';
import { createPivot, type PivotHandle, type PivotViewOptions } from '@ex-grid/pivot';
import type { BoardDiff } from './board-diff';
import type { BoardOutput } from './board-output';
import type { BoardCell, BoardReading, BoardRow } from './board-spec';
import { bindBoard, type BoundBoard, type BoundChartView, type BoundGridView, type BoundPivotView } from './bound-board';
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
    /** The Snapshot each input is bound to, by the input's name. A new record binds again; the same one is not read
     * twice. */
    readonly data: Readonly<Record<string, Snapshot>>;
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
    readonly diff?: BoardDiff;
    /** Raised when the user changes a view — a sort, a filter, a column's width, order, pinning or hiding, a pivot's
     * layout, a chart's series, range or axis — with the whole Board Diff. The Board is never rewritten; the host
     * keeps this ({@link BoardDiff.toJson}) and hands it back as `diff` next time. */
    readonly onDiff?: (diff: BoardDiff) => void;
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
    const view = new BoardView(element, options, familyMounts);
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
}

/** @internal Draws a Board with the mounts it is given; {@link createBoard} hands it the family's. */
export class BoardView {
    readonly #element: HTMLElement;
    readonly #mounts: BoardMounts;
    #options: BoardOptions;
    #bound: BoundBoard | undefined;
    #boundDiff: BoardDiff | undefined;
    #raised: BoardDiff | undefined;
    #mounted: Mounted[] = [];
    #unsubscribe: (() => void)[] = [];
    #destroyed = false;

    constructor(element: HTMLElement, options: BoardOptions, mounts: BoardMounts) {
        this.#element = element;
        this.#mounts = mounts;
        this.#options = options;
        element.classList.add('ex-board');
        Object.assign(element.style, { display: 'flex', flexDirection: 'column', gap: `${boardGap}px` });
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
        const sameDiff = options.diff === this.#boundDiff
            || (bound !== undefined && (options.diff === undefined ? bound.diff.isEmpty : options.diff.sameAs(bound.diff)));
        if (options.board === was.board && options.data === was.data && options.locale === was.locale && sameDiff) {
            if (options.width !== was.width) for (const mounted of this.#mounted) mounted.redraw();
            return;
        }
        // Bound again for a new Board or new data, the user's changes since are kept: only a new Diff replaces them.
        this.#bind(options.diff === this.#boundDiff && bound !== undefined ? bound.diff : options.diff);
    }

    destroy(): void {
        if (this.#destroyed) return;
        this.#destroyed = true;
        this.#clear();
        this.#element.classList.remove('ex-board');
        this.#element.removeAttribute('style');
    }

    #clear(): void {
        for (const unsubscribe of this.#unsubscribe) unsubscribe();
        this.#unsubscribe = [];
        for (const mounted of this.#mounted) mounted.destroy();
        this.#mounted = [];
        this.#element.replaceChildren();
        this.#bound = undefined;
    }

    #bind(laid: BoardDiff | undefined): void {
        this.#clear();
        const { board, data, locale = 'invariant', diff } = this.#options;
        this.#boundDiff = diff;
        const root = this.#element;
        if (board.title !== '') root.append(made('h2', 'ex-board-title', board.title));
        let bound: BoundBoard;
        try {
            bound = bindBoard(board, data, laid === undefined ? { locale } : { locale, diff: laid });
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a Board that does not bind is drawn as its refusal, naming the path
            if (!(error instanceof BoardRefusedError)) throw error;
            const refusal = made('div', 'ex-board-refusal', error.path === '' ? error.message : `${error.path}: ${error.message}`);
            refusal.setAttribute('role', 'alert');
            root.append(refusal);
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
        for (const row of board.layout) root.append(this.#row(bound, row));
        this.#unsubscribe.push(bound.subscribe(() => { for (const mounted of this.#mounted) mounted.redraw(); }));
        // A grid's sort and filter are its source's (ADR-0001), which says when they move; a narrowing moves only its
        // rows, and raises no Diff.
        for (const view of bound.views) if (view.kind === 'grid') this.#unsubscribe.push(view.source.subscribe(() => { this.#changed(); }));
        // What is laid over is not the user's change: only a change from it is raised, and the Diff that applied, when
        // some of it did not.
        this.#raised = bound.dropped.length > 0 ? undefined : bound.diff;
        this.#changed();
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
            case 'pivot': this.#mounted.push(this.#pivot(bound, view, row, host)); break;
            case 'chart': this.#mounted.push(this.#chart(view, row, cell, shares, host)); break;
            default: throw new Error(`Internal: ${String(view satisfies never)} is not a view.`);
        }
        return section;
    }

    #grid(bound: BoundBoard, view: BoundGridView, row: BoardRow, host: HTMLElement, section: HTMLElement): Mounted {
        const id = view.view.id;
        const statuses: HTMLElement[] = [];
        let selection: GridSelection | undefined;
        // A gesture the view takes redraws the grid with what it now shows; one it refuses, a round trip behind a
        // hide, changes nothing (spine rule 6).
        const taken = (took: boolean) => {
            if (!took) return;
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
            onSelectionChanged: next => {
                // A selection narrows the views its links reach; a dropped one, raised as an empty one, takes the
                // narrowing away with it (ADR-0011).
                selection = next;
                bound.select(id, next);
                for (const status of statuses) status.textContent = '';
            },
            onColumnWidthChanged: change => { taken(view.resize(change.column, change.widthPx)); },
            onColumnOrderChanged: order => { taken(view.reorder(order)); },
            onColumnHidden: column => { taken(view.hide(column)); },
            onPinnedCountChanged: count => { taken(view.pin(count)); },
        });
        const grid = this.#mounts.grid(host, options());
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
                if (selection !== undefined) bound.select(id, selection);
                const raised = bound.confirm(output.name);
                if (raised === undefined) {
                    status.textContent = 'Select the rows to hand on first.';
                    return;
                }
                status.textContent = '';
                this.#options.onOutput?.(raised);
            });
            line.append(button, status);
            section.append(line);
        }
        // Its source tells it of the rows a link lets through.
        return { destroy: () => { grid.destroy(); }, redraw: () => { /* Nothing of the grid's own moves. */ } };
    }

    #pivot(bound: BoundBoard, view: BoundPivotView, row: BoardRow, host: HTMLElement): Mounted {
        const id = view.view.id;
        let shown = view.source;
        const options = (): PivotViewOptions => ({
            source: view.source,
            layout: view.layout,
            locale: this.#locale(),
            showFieldList: false,
            viewportHeight: row.height,
            viewportWidth: 'stretch',
            onLayoutChanged: ({ layout }) => {
                // A new layout is a new report, so what its cells narrowed is let go now, as a narrowing of it lets go
                // (ADR-0011).
                view.changeLayout(layout);
                bound.selectCells(id, []);
                pivot.update(options());
                this.#changed();
            },
        });
        const pivot = this.#mounts.pivot(host, options());
        return {
            destroy: () => { pivot.destroy(); },
            redraw: () => {
                // A new source is a refresh: the rows a link lets through now.
                if (view.source === shown) return;
                shown = view.source;
                pivot.update(options());
            },
        };
    }

    #chart(view: BoundChartView, row: BoardRow, cell: BoardCell, shares: number, host: HTMLElement): Mounted {
        const refusal = made('div', 'ex-board-refusal');
        refusal.setAttribute('role', 'alert');
        refusal.style.height = `${row.height}px`;
        const stage = made('div', 'ex-board-chart');
        const changed = () => {
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
                onSeriesVisibilityChanged: ({ key, hidden }) => { view.changeVisibility(key, hidden); changed(); },
                onRangeChanged: range => { view.changeRange(range === null ? undefined : range); changed(); },
                onScaleChanged: scale => { view.changeScale(scale); changed(); },
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
        return {
            destroy: () => { chart.destroy(); },
            redraw: () => {
                chart.update(options());
                show();
            },
        };
    }

    #locale(): string {
        const { locale = 'invariant' } = this.#options;
        return locale;
    }
}

function made<K extends keyof HTMLElementTagNameMap>(tag: K, className: string, text?: string): HTMLElementTagNameMap[K] {
    const one = document.createElement(tag);
    one.className = className;
    if (text !== undefined) one.textContent = text;
    return one;
}
