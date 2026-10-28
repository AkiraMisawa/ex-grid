// A Board with every input bound to its data (ADR-0341): C#'s BoundBoard, over Snapshots held in memory and over
// sources held where the data lives (ADR-0343 to ADR-0345). Each view is made what it draws, a link's selection
// narrows the views it reaches (ADR-0340, Q5), and an output is raised only by the user's confirm (Q6).

import {
    chartSourceOver, ChartSpecRefusedError, readChartSpec, readChartSpecThrough, type AxisScale, type ChartRange, type ChartSource, type ChartSourceReading,
    type ChartSpecReading,
} from '@ex-grid/chart';
import {
    engineFilterOf, FetchingGridSource, GridViewState, GridViewStateJsonError, InMemoryGridSource, sameGridFilter, type ColumnViewState, type GridColumn, type GridFilter,
    type GridSelection, type GridSource,
} from '@ex-grid/core';
import { BoardRefusedError, type BoardInput, type DataShape, type Snapshot, type SnapshotKind } from '@ex-grid/data';
import { SnapshotPivotSource, writePivotLayoutJson, type PivotLayout, type PivotSource } from '@ex-grid/pivot-engine';
import { BoardChartChange, BoardDiff, BoardGridChange, BoardPivotChange, kindsOf, placed } from './board-diff';
import { BoardOutput } from './board-output';
import { valuesUnder, type BoardSource, type PivotCellItems } from './board-source';
import { refusedAt } from './grid-view-spec';
import { declaredInput, type BoardChartView, type BoardGridView, type BoardLink, type BoardPivotView, type BoardReading } from './board-spec';
import { LinkCondition, narrowedPositions, narrowedSnapshot, valueAt, type BoardValue } from './link-condition';
import { checkShapedPage, fetchedColumns, resized, shapedColumns, shapedRows, type ShapedRow } from './shaped-grid';
import { declaredChartData } from './snapshot-values';
import { messageOf } from './must';
import { taggedFilter } from './tagged-filter';

/** What a grid's selection is read by: its rectangles, in the rows the grid shows; a {@link @ex-grid/core#GridSelection} is one. */
export type SelectedRows = Pick<GridSelection, 'ranges'>;

/** How a Board is bound. */
export interface BindBoardOptions {
    /** The locale a grid's text is written in, a BCP 47 tag or `invariant`, the default (ADR-0370 section 10). */
    readonly locale?: string;
    /** What the user changed last time, laid over the Board (ADR-0340, Q7): what still fits applies, and what does not
     * is dropped and named in {@link BoundBoard.dropped}. */
    readonly diff?: BoardDiff;
}

/** How a Board over sources held where the data lives is bound. */
export interface BindBoardThroughOptions extends BindBoardOptions {
    /** Aborts the questions to the sources: their shapes, and a chart's first points. */
    readonly signal?: AbortSignal;
}

/** A view of a bound Board. */
export type BoundView = BoundGridView | BoundPivotView | BoundChartView;

/**
 * How many pixels a chart over a server input asks its source for (ADR-0344): the device pixels of a plot 1,024 CSS
 * pixels wide, at the two device pixels a CSS pixel ExChart counts in. A wider plot thins on top in the browser, which
 * keeps every extreme the source kept. The counterpart of C#'s `BoundBoard.ChartPixels`.
 */
export const boardChartPixels = 2048;

/**
 * Binds each of `board`'s inputs to the Snapshot `data` holds under its name, and makes each view. Nothing is
 * drawn unless every input binds and every view can be made: an input with no data, a Snapshot whose shape is not
 * the declaration, and a chart its spec cannot draw from the data are refused, with a {@link @ex-grid/data#BoardRefusedError}
 * naming the input or the path. Data for a name the Board does not declare is not read.
 */
export function bindBoard(board: BoardReading, data: Readonly<Record<string, Snapshot>>, options: BindBoardOptions = {}): BoundBoard {
    const made = make(board, bindData(board, data, new Map()), new Map(), options);
    if (made.thinned.length > 0) throw new Error('Internal: a Board over no source reads a chart through one.');
    return made.board;
}

/**
 * Binds each of `board`'s inputs to the Snapshot `data` holds under its name, or to the source held where the data
 * lives that `sources` holds (ADR-0343), and makes each view. Each source is asked its shape, and checked against the
 * input's declaration, before any view is made or any row is fetched (ADR-0341); nothing is drawn unless every input
 * binds and every view can be made. Over a source, a grid's rows are fetched a page at a time, each fetch handed the
 * narrowing the Board's links put on it; a pivot's source is made by the source's {@link BoardSource.pivot}, given
 * that narrowing; a chart is read through its {@link BoardSource.thin} (ADR-0344), from its first answers before the
 * Board is handed back; and a link from a pivot asks its {@link BoardSource.under} (ADR-0345). An input named in both
 * `data` and `sources`, and a pivot, a chart or such a link over a source that does not answer what it asks, is
 * refused with a {@link @ex-grid/data#BoardRefusedError} naming the path. The counterpart of C#'s `BoundBoard.BindAsync`.
 */
export async function bindBoardThrough(board: BoardReading, data: Readonly<Record<string, Snapshot>>, sources: Readonly<Record<string, BoardSource>>,
    options: BindBoardThroughOptions = {}): Promise<BoundBoard> {
    const held = new Map<string, BoardSource>();
    for (const input of board.inputs) {
        const source = Object.hasOwn(sources, input.name) ? sources[input.name] : undefined;
        if (source === undefined) continue;
        if (Object.hasOwn(data, input.name)) {
            throw new BoardRefusedError(`inputs.${input.name}`, `Input '${input.name}' is bound both to a Snapshot and to a source held on a server.`);
        }
        try {
            await input.checkSource(source, options.signal);
        } catch (error) {
            if (!(error instanceof BoardRefusedError)) throw error;
            throw new BoardRefusedError(`inputs.${input.name}`, error.message, { cause: error });
        }
        held.set(input.name, source);
    }
    const made = make(board, bindData(board, data, held), held, options);
    try {
        // A chart over a server input is read from its sources' first answers before the Board is handed back, and a
        // spec they cannot draw is refused by path, as one over a Snapshot is.
        for (const { chart, at } of made.thinned) {
            const path = `views[${at}].chart`;
            try {
                chart.read(await made.board.readThrough(chart, [], options.signal));
            } catch (error) {
                if (!(error instanceof ChartSpecRefusedError)) throw error;
                throw new BoardRefusedError(error.path === '' ? path : `${path}.${error.path}`, error.message, { cause: error });
            }
        }
    } catch (error) {
        // A Board that is not handed back lets go of the sources its grids already asked, and the failure goes on.
        made.board.dispose();
        throw error;
    }
    return made.board;
}

function bindData(board: BoardReading, data: Readonly<Record<string, Snapshot>>, held: ReadonlyMap<string, BoardSource>): Map<string, Snapshot> {
    const bound = new Map<string, Snapshot>();
    for (const input of board.inputs) {
        if (held.has(input.name)) continue;
        const snapshot = Object.hasOwn(data, input.name) ? data[input.name] : undefined;
        if (snapshot === undefined) throw new BoardRefusedError(`inputs.${input.name}`, `Input '${input.name}' is bound to no data.`);
        bound.set(input.name, input.bind(snapshot));
    }
    return bound;
}

// The views made, the Diff laid over them, and each chart to be read through its sources, by its place in the Board.
function make(board: BoardReading, bound: ReadonlyMap<string, Snapshot>, held: ReadonlyMap<string, BoardSource>, options: BindBoardOptions): {
    readonly board: BoundBoard;
    readonly thinned: readonly { readonly chart: BoundChartView; readonly at: number }[];
} {
    const { locale = 'invariant', diff = BoardDiff.empty } = options;
    const inputs = new Map(board.inputs.map(input => [input.name, input]));
    function madeView(view: BoardReading['views'][number], i: number): BoundView {
        switch (view.kind) {
            case 'grid': {
                const source = held.get(view.grid.data);
                return new BoundGridView(view, source === undefined ? { snapshot: heldData(bound, view.grid.data) } : { source }, locale, `views[${i}].grid`);
            }
            case 'pivot': {
                const shape = declaredInput(board, view.input).shape;
                const source = held.get(view.input);
                if (source === undefined) return new BoundPivotView(view, shape, heldData(bound, view.input), undefined);
                if (source.pivot === undefined) {
                    throw new BoardRefusedError(`views[${i}].pivot`, `Input '${view.input}' is held on a server, and its source makes no pivot source (BoardSource.pivot).`);
                }
                return new BoundPivotView(view, shape, undefined, source.pivot([]));
            }
            case 'chart': {
                const unthinned = view.data.find(name => held.has(name) && held.get(name)?.thin === undefined);
                if (unthinned !== undefined) {
                    throw new BoardRefusedError(`views[${i}].chart`, `Input '${unthinned}' is held on a server, and its source thins no chart's points (BoardSource.thin).`);
                }
                // Read through Chart Sources, the server's thinning where the data lives, once bound (ADR-0344).
                if (view.data.some(name => held.has(name))) return new BoundChartView(view, undefined);
                return new BoundChartView(view, readChart(view, bound, inputs, `views[${i}].chart`, undefined));
            }
            default: throw new Error(`Internal: ${String(view satisfies never)} is not a view.`);
        }
    }
    const views: BoundView[] = [];
    try {
        board.views.forEach((view, i) => { views.push(madeView(view, i)); });
        board.links.forEach((link, l) => {
            const from = board.views.find(view => view.id === link.from);
            if (from?.kind !== 'pivot') return;
            const source = held.get(from.input);
            if (source !== undefined && source.under === undefined) {
                throw new BoardRefusedError(`links[${l}].from`, `Pivot '${from.id}' is over input '${from.input}', held on a server, where the rows under a cell are, `
                    + 'and its source answers no values under cells (BoardSource.under).');
            }
        });
    } catch (error) {
        // A Board refused lets go of the sources its grids already asked, and the refusal goes on.
        for (const view of views) if (view.kind === 'grid') view.fetched?.dispose();
        throw error;
    }
    const fitted = diff.fitTo(board);
    const dropped = [...fitted.dropped];
    for (const view of views) {
        switch (view.kind) {
            case 'grid': {
                const change = fitted.diff.grids.get(view.view.id);
                if (change !== undefined) view.layOver(change, dropped);
                break;
            }
            case 'pivot': {
                const change = fitted.diff.pivots.get(view.view.id);
                if (change !== undefined) view.changeLayout(change.layout);
                break;
            }
            case 'chart': {
                const change = fitted.diff.charts.get(view.view.id);
                if (change !== undefined) view.layOver(change);
                break;
            }
            default: throw new Error(`Internal: ${String(view satisfies never)} is not a view.`);
        }
    }
    const made = new BoundBoard(board, bound, held, inputs, views, dropped);
    const thinned = views.flatMap((view, at) => view.kind === 'chart' && view.thinned ? [{ chart: view, at }] : []);
    for (const { chart } of thinned) chart.asks(() => made.askChart(chart.view.id));
    return { board: made, thinned };
}

function heldData(bound: ReadonlyMap<string, Snapshot>, name: string): Snapshot {
    const snapshot = bound.get(name);
    if (snapshot === undefined) throw new Error(`Internal: input '${name}' is not bound.`);
    return snapshot;
}

/**
 * A Board with every input bound to its data (ADR-0341), made by {@link bindBoard} or {@link bindBoardThrough}. A view
 * narrowed by a link is made again from the rows let through, or asks its source again, and every listener
 * {@link BoundBoard.subscribe}d is told once per step, and when a chart's source answers.
 */
export class BoundBoard {
    readonly #inputs: ReadonlyMap<string, BoardInput>;
    readonly #conditions = new Map<BoardLink, LinkCondition>();
    readonly #taken = new Map<string, { readonly condition: LinkCondition; readonly from: Snapshot }>();
    readonly #listeners = new Set<() => void>();
    readonly #disposed = new AbortController();

    /** The Board. */
    readonly board: BoardReading;
    /** The Snapshot each input held in memory is bound to. */
    readonly data: ReadonlyMap<string, Snapshot>;
    /** The source each input held where the data lives is bound to (ADR-0343). */
    readonly sources: ReadonlyMap<string, BoardSource>;
    /** The views, in the Board's order. */
    readonly views: readonly BoundView[];
    /** What of the Diff laid over at binding no longer fit the Board, one sentence per part naming its path
     * ({@link BoardDiff.fitTo}); empty when everything applied. */
    readonly dropped: readonly string[];

    /** @internal Made by {@link bindBoard} and {@link bindBoardThrough}. */
    constructor(board: BoardReading, data: ReadonlyMap<string, Snapshot>, sources: ReadonlyMap<string, BoardSource>, inputs: ReadonlyMap<string, BoardInput>,
        views: readonly BoundView[], dropped: readonly string[]) {
        this.board = board;
        this.data = data;
        this.sources = sources;
        this.#inputs = inputs;
        this.views = Object.freeze([...views]);
        this.dropped = Object.freeze([...dropped]);
    }

    /** What the user has changed, as one Board Diff (ADR-0340, Q7): each view's part that differs from what the Board
     * writes. The Board itself is never rewritten. */
    get diff(): BoardDiff {
        const grids = new Map<string, BoardGridChange>();
        const pivots = new Map<string, BoardPivotChange>();
        const charts = new Map<string, BoardChartChange>();
        for (const view of this.views) {
            const id = view.view.id;
            switch (view.kind) {
                case 'grid': { const change = view.change; if (change !== undefined) grids.set(id, change); break; }
                case 'pivot': { const change = view.change; if (change !== undefined) pivots.set(id, change); break; }
                case 'chart': { const change = view.change; if (change !== undefined) charts.set(id, change); break; }
                default: throw new Error(`Internal: ${String(view satisfies never)} is not a view.`);
            }
        }
        return new BoardDiff({ grids, pivots, charts });
    }

    /** The view whose id is `id`; an id the Board does not have is refused with a `RangeError`. */
    view(id: string): BoundView {
        const found = this.views.find(view => view.view.id === id);
        if (found === undefined) throw new RangeError(`The Board has no view '${id}'.`);
        return found;
    }

    /** Hears each step that narrowed a view or changed what can be confirmed, and each answer a chart's source gave;
     * answers the call that stops it. */
    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    }

    /** Lets go of the Board's questions to its sources: each fetched grid's source is disposed, and a chart's question
     * still on its way is aborted and its answer dropped. A Board drawn again over new data lets go of the old one. */
    dispose(): void {
        if (this.#disposed.signal.aborted) return;
        this.#disposed.abort();
        for (const view of this.views) {
            if (view.kind === 'grid') view.fetched?.dispose();
            else if (view.kind === 'chart') view.stopAsking();
        }
    }

    /** The condition `link` narrows by now, or undefined while its view selects nothing. */
    conditionOf(link: BoardLink): LinkCondition | undefined {
        this.#linkFrom(link);
        return this.#conditions.get(link);
    }

    /**
     * The condition `selection`, made in `link`'s grid, narrows by: the values in the link's columns of every row
     * the selection covers, in any column, read from the rows the grid shows; undefined when it selects nothing,
     * which narrows nothing (ADR-0011), or reaches rows the grid does not hold. A link not the Board's, or one from a
     * pivot, is refused with a `RangeError`.
     */
    gridCondition(link: BoardLink, selection: SelectedRows): LinkCondition | undefined {
        const from = this.#linkFrom(link);
        if (from.kind !== 'grid') throw new RangeError(`The link starts at pivot '${link.from}', whose selection narrows by the Items its cells stand for.`);
        return from.condition(link.on, selection)?.condition;
    }

    /**
     * The condition `cells`, selected in `link`'s pivot, narrows by (ADR-0340, Q5): the values in the link's columns
     * of every row of the pivot's data under any of the cells, read from the rows the pivot shows, as
     * {@link valuesUnder} reads them. Undefined when no cell is selected. A link not the Board's, or one from a grid,
     * is refused with a `RangeError`; one from a pivot over a source held on a server, whose rows are asked for there
     * ({@link BoundBoard.selectCellsThrough}), with an `Error`.
     */
    cellCondition(link: BoardLink, cells: readonly PivotCellItems[]): LinkCondition | undefined {
        const from = this.#linkFrom(link);
        if (from.kind !== 'pivot') throw new RangeError(`The link starts at grid '${link.from}', whose selection narrows by its rows.`);
        return cells.length === 0 ? undefined : valuesUnder(from.shownData(), { view: from.view.id, layout: from.layout, cells, on: link.on, narrowing: [] });
    }

    /**
     * Narrows every view `link` reaches by `condition`, or, given undefined, takes the link's narrowing away. A view
     * narrowed by several links shows the rows all of them let through. A condition comparing other columns than
     * the link does is refused with a `RangeError`.
     */
    narrow(link: BoardLink, condition: LinkCondition | undefined): void {
        if (this.#set(link, condition)) {
            for (const target of link.to) this.#narrowView(target);
            this.#tell();
        }
    }

    /**
     * Narrows by `selection`, made in grid view `from`: each link from it narrows by the condition the selection
     * makes, each view they reach is narrowed once, and each output from it takes what the selection holds. A
     * selection that moves within the same values narrows nothing again. Over a source held on a server, a selection
     * reaching rows not fetched narrows nothing and hands nothing on, and the grid says so in
     * {@link BoundGridView.unread} (ADR-0343). Answers whether any view was narrowed.
     */
    select(from: string, selection: SelectedRows): boolean {
        const grid = this.view(from);
        if (grid.kind !== 'grid') throw new RangeError(`View '${from}' is not a grid.`);
        const unread = grid.unread;
        grid.unread = grid.fetched !== undefined && !grid.holds(selection)
            ? 'The selection reaches rows not fetched from the source, so it narrows nothing and hands nothing on.'
            : undefined;
        let changed = false;
        for (const output of this.board.outputs.filter(o => o.from === from)) {
            const read = grid.condition(output.on, selection);
            if (read !== undefined && read.from === undefined) grid.unread = 'The selected rows were read at different versions of the source, so nothing is handed on.';
            const was = this.#taken.get(output.name);
            if (read?.from === undefined) changed = this.#taken.delete(output.name) || changed;
            else {
                changed = changed || was === undefined || !was.condition.letsThroughAs(read.condition);
                this.#taken.set(output.name, { condition: read.condition, from: read.from });
            }
        }
        changed = changed || grid.unread !== unread;
        const targets = new Set<string>();
        for (const link of this.board.links.filter(l => l.from === from)) {
            if (this.#set(link, grid.condition(link.on, selection)?.condition)) for (const target of link.to) targets.add(target);
        }
        for (const target of targets) this.#narrowView(target);
        if (changed || targets.size > 0) this.#tell();
        return targets.size > 0;
    }

    /**
     * Narrows by `cells`, selected in pivot view `from`: each link from it narrows by the condition the cells make,
     * and each view they reach is narrowed once. Undefined, a selection the pivot could not read, narrows nothing,
     * as no selection does. Answers whether any view was narrowed. A pivot over a source held on a server, whose rows
     * are asked for there, is refused with an `Error`: select its cells with {@link BoundBoard.selectCellsThrough}.
     */
    selectCells(from: string, cells: readonly PivotCellItems[] | undefined): boolean {
        const pivot = this.view(from);
        if (pivot.kind !== 'pivot') throw new RangeError(`View '${from}' is not a pivot.`);
        if (pivot.served) {
            throw new Error(`Pivot '${from}' is over a source held on a server, which is asked for the values under its cells: select them with selectCellsThrough.`);
        }
        const targets = new Set<string>();
        for (const link of this.board.links.filter(l => l.from === from)) {
            const condition = cells === undefined || cells.length === 0 ? undefined : this.cellCondition(link, cells);
            if (this.#set(link, condition)) for (const target of link.to) targets.add(target);
        }
        for (const target of targets) this.#narrowView(target);
        if (targets.size > 0) this.#tell();
        return targets.size > 0;
    }

    /**
     * {@link BoundBoard.selectCells} for any pivot: over a source held on a server, each link from it asks the
     * source's {@link BoardSource.under} for the values under the cells (ADR-0345), all at once, and the views it
     * reaches are narrowed once every answer has come. A selection a later one has replaced narrows nothing when its
     * answers come, and answers false (spine rule 6). A source that fails the newest selection lets its links go, so
     * no view stays narrowed by the selection before it, and the promise rejects with its failure.
     */
    async selectCellsThrough(from: string, cells: readonly PivotCellItems[] | undefined): Promise<boolean> {
        const pivot = this.view(from);
        if (pivot.kind !== 'pivot') throw new RangeError(`View '${from}' is not a pivot.`);
        if (!pivot.served) return this.selectCells(from, cells);
        const selected = pivot.select();
        const links = this.board.links.filter(l => l.from === from);
        let conditions: readonly (LinkCondition | undefined)[] = links.map(() => undefined);
        if (cells !== undefined && cells.length > 0) {
            const narrowing = this.#narrowingOf(from);
            const source = this.#source(pivot.view.input);
            const under = source.under?.bind(source);
            if (under === undefined) throw new Error(`Internal: pivot '${from}' is linked over a source that answers nothing under cells.`);
            const signal = this.#disposed.signal;
            try {
                conditions = await Promise.all(links.map(link => under({ view: from, layout: pivot.layout, cells, on: link.on, narrowing }, signal)));
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a selection a later one replaced is dropped, failure and all (spine rule 6); the newest's failure is rethrown
                // A selection a later one replaced, or a Board let go: its failure says nothing about what is shown.
                if (selected !== pivot.selected || signal.aborted) return false;
                // The newest selection was not answered: its links let go, so no view stays narrowed by the selection
                // before it while the pivot shows this one (spine rule 1).
                const released = new Set<string>();
                for (const link of links) if (this.#set(link, undefined)) for (const target of link.to) released.add(target);
                for (const target of released) this.#narrowView(target);
                if (released.size > 0) this.#tell();
                throw error;
            }
        }
        if (selected !== pivot.selected || this.#disposed.signal.aborted) return false;
        const targets = new Set<string>();
        links.forEach((link, i) => {
            if (this.#set(link, conditions[i])) for (const target of link.to) targets.add(target);
        });
        for (const target of targets) this.#narrowView(target);
        if (targets.size > 0) this.#tell();
        return targets.size > 0;
    }

    /** Whether output `name` can be confirmed: its grid selects something. */
    canConfirm(name: string): boolean {
        return this.#taken.has(name);
    }

    /**
     * What confirming output `name` hands on (ADR-0340, Q6): the values its grid's selection holds in its columns,
     * on its input, with the version of the input's data they were taken against, over a source held on a server the
     * version of the page they were read from; undefined while its grid selects nothing. Nothing is raised by a
     * selection on the way: only this, called when the user confirms. A name the Board does not declare is refused
     * with a `RangeError`.
     */
    confirm(name: string): BoardOutput | undefined {
        const declared = this.board.outputs.find(output => output.name === name);
        if (declared === undefined) throw new RangeError(`The Board declares no output '${name}'.`);
        const taken = this.#taken.get(name);
        if (taken === undefined) return undefined;
        const grid = this.view(declared.from);
        if (grid.kind !== 'grid') throw new Error(`Internal: output '${name}' is from view '${declared.from}', which is not a grid.`);
        const input = this.#input(grid.view.grid.data);
        return new BoardOutput(name, input.name, taken.from, taken.condition, declared.on.map(column => {
            const kind = input.column(column)?.kind;
            if (kind === undefined) throw new Error(`Internal: output '${name}' hands on column '${column}', which input '${input.name}' does not declare.`);
            return kind;
        }));
    }

    /** @internal Asks a chart over a server input again, under the narrowing and the range it has now. Only the
     * newest ask is drawn, and never an answer older than the version the chart shows (ADR-0322); the Board's
     * listeners are told once it is taken, or has said why it is not drawn. */
    async askChart(id: string): Promise<void> {
        const chart = this.view(id);
        if (chart.kind !== 'chart' || !chart.thinned) throw new RangeError(`View '${id}' is not a chart read through its sources.`);
        const { asked, signal } = chart.ask(this.#disposed.signal);
        const at = this.board.views.findIndex(v => v.id === id);
        let refusal: string | undefined;
        let read: ChartSourceReading | undefined;
        try {
            read = await this.readThrough(chart, this.#narrowingOf(id), signal);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a chart its sources do not answer says why in its place (spine rule 1)
            if (error instanceof ChartSpecRefusedError) refusal = `${error.path === '' ? `views[${at}].chart` : `views[${at}].chart.${error.path}`}: ${error.message}`;
            else refusal = `The source did not answer the chart: ${messageOf(error)}`;
        }
        if (!chart.answers(asked) || this.#disposed.signal.aborted) return;
        if (read === undefined) {
            if (refusal === undefined) throw new Error('Internal: a chart was neither read nor refused.');
            chart.refuse(refusal);
        } else {
            const older = [...read.versions].find(([name, version]) => {
                const shown = chart.versions.get(name);
                return shown !== undefined && version < shown;
            });
            // Never drawn over the newer data shown, and not left drawing what was asked before as if it were the
            // answer: the chart says so in its place (spine rule 1).
            if (older === undefined) chart.read(read);
            else {
                chart.refuse(`The source answered the chart from version ${older[1]} of '${older[0]}', older than version ${chart.versions.get(older[0])} `
                    + 'the chart was drawn from, so it is not drawn.');
            }
        }
        this.#tell();
    }

    /** @internal A chart read through Chart Sources: a server input through its source's thin, an input held in
     * memory through the reference implementation over its declared columns, each under `narrowing`. */
    readThrough(chart: BoundChartView, narrowing: readonly LinkCondition[], signal: AbortSignal | undefined): Promise<ChartSourceReading> {
        const id = chart.view.id;
        const sources = new Map<string, ChartSource>();
        for (const name of chart.view.data) {
            const input = this.#inputs.get(name);
            if (input === undefined) continue;
            const source = this.sources.get(name);
            if (source !== undefined) {
                const thin = source.thin?.bind(source);
                if (thin === undefined) throw new Error(`Internal: chart '${id}' reads input '${name}' through a source that thins nothing.`);
                sources.set(name, { thin: (query, asked) => thin({ view: id, chart: query, narrowing }, asked) });
            } else {
                const snapshot = heldData(this.data, name);
                sources.set(name, chartSourceOver(declaredChartData(snapshot, input, narrowing.length === 0 ? undefined : narrowedPositions(snapshot, narrowing)), snapshot.version));
            }
        }
        const range = chart.range;
        // An end the range leaves out is the data's own (ADR-0273).
        return readChartSpecThrough(chart.view.spec, name => sources.get(name), boardChartPixels, {
            ...(range?.from === undefined ? {} : { from: range.from }),
            ...(range?.to === undefined ? {} : { to: range.to }),
            ...(signal === undefined ? {} : { signal }),
        });
    }

    #source(name: string): BoardSource {
        const source = this.sources.get(name);
        if (source === undefined) throw new Error(`Internal: input '${name}' is not held on a server.`);
        return source;
    }

    #input(name: string): BoardInput {
        const input = this.#inputs.get(name);
        if (input === undefined) throw new Error(`Internal: the Board declares no input '${name}'.`);
        return input;
    }

    #tell(): void {
        for (const listener of this.#listeners) listener();
    }

    #narrowingOf(id: string): LinkCondition[] {
        return [...this.#conditions].filter(([link]) => link.to.includes(id)).map(([, condition]) => condition);
    }

    // Records the link's condition; false when it is the one already held, which narrows nothing again.
    #set(link: BoardLink, condition: LinkCondition | undefined): boolean {
        this.#linkFrom(link);
        if (condition !== undefined && (condition.on.length !== link.on.length || condition.on.some((name, i) => link.on[i] !== name))) {
            throw new RangeError(`The link compares ${link.on.join(', ')}, and the condition ${condition.on.join(', ')}.`);
        }
        const was = this.#conditions.get(link);
        if (was === undefined ? condition === undefined : was.letsThroughAs(condition)) return false;
        if (condition === undefined) this.#conditions.delete(link);
        else this.#conditions.set(link, condition);
        return true;
    }

    #linkFrom(link: BoardLink): BoundView {
        if (!this.board.links.includes(link)) throw new RangeError('The link is not one of the Board\'s.');
        return this.view(link.from);
    }

    #narrowView(id: string): void {
        const conditions = this.#narrowingOf(id);
        const view = this.view(id);
        switch (view.kind) {
            case 'grid': {
                // Over a server, the source is asked again, handed the conditions to execute (ADR-0343).
                view.narrowTo(conditions);
                // Its rows changed, so its selection is dropped (ADR-0011): what it held is let go now, so nothing is
                // confirmed or narrowed by a selection the grid no longer shows.
                view.unread = undefined;
                for (const output of this.board.outputs.filter(o => o.from === id)) this.#taken.delete(output.name);
                this.#letGo(id);
                break;
            }
            case 'pivot': {
                if (view.served) {
                    const source = this.#source(view.view.input);
                    const pivot = source.pivot?.bind(source);
                    if (pivot === undefined) throw new Error(`Internal: pivot '${id}' is over a source that makes no pivot source.`);
                    view.narrowTo(undefined, pivot(conditions));
                } else {
                    const snapshot = view.snapshot;
                    if (snapshot === undefined) throw new Error(`Internal: pivot '${id}' holds no Snapshot.`);
                    view.narrowTo(conditions.length === 0 ? undefined : narrowedSnapshot(snapshot, narrowedPositions(snapshot, conditions)), undefined);
                }
                // Its report changed, so its selection is dropped, with an answer still on its way for it, and what it
                // narrowed is let go now (ADR-0345).
                this.#letGo(id);
                break;
            }
            case 'chart': {
                if (view.thinned) {
                    // Asked again, handed the conditions, as a fetched grid is; drawn when the answer comes.
                    void this.askChart(id);
                    break;
                }
                const path = `views[${this.board.views.findIndex(v => v.id === id)}].chart`;
                try {
                    view.read(readChart(view.view, this.data, this.#inputs, path,
                        conditions.length === 0 ? undefined : name => narrowedPositions(heldData(this.data, name), conditions)));
                } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a chart its spec cannot draw from the rows let through says why in its place (spine rule 1)
                    if (!(error instanceof BoardRefusedError)) throw error;
                    view.refuse(`${error.path}: ${error.message}`);
                }
                break;
            }
            default: throw new Error(`Internal: ${String(view satisfies never)} is not a view.`);
        }
    }

    #letGo(id: string): void {
        for (const link of this.board.links.filter(l => l.from === id)) {
            if (this.#set(link, undefined)) for (const target of link.to) this.#narrowView(target);
        }
    }
}

/**
 * A grid view, its columns built from its input's shape (ADR-0342), and shown as the user has changed them since:
 * the columns' order, widths, pinning and those hidden. The Board's own spec is never rewritten (ADR-0340, Q7). Over
 * a source held where the data lives, its rows are fetched a page at a time (ADR-0343).
 */
export class BoundGridView {
    readonly #declared: ReadonlyMap<string, GridColumn<ShapedRow>>;
    readonly #held: InMemoryGridSource<ShapedRow> | undefined;
    readonly #rows: readonly ShapedRow[];
    #shown: ColumnViewState[];
    #hidden: string[] = [];
    // The filter in the View State's form, and the engine's filter it was set as: while the source holds that one, the
    // View State's is the one written, its operands as typed.
    #taggedFilter: GridFilter | undefined;
    #engineSet: GridFilter | undefined;
    readonly #kinds: ReadonlyMap<string, SnapshotKind>;
    // The conditions the Board's links narrow a fetched grid by now, handed to each fetch.
    #narrowing: readonly LinkCondition[] = Object.freeze([]);

    /** What kind of view it is. */
    readonly kind = 'grid';
    /** The view as the Board writes it. */
    readonly view: BoardGridView;
    /** The Snapshot its input is bound to, or undefined where its rows are fetched from a source held on a server. */
    readonly snapshot: Snapshot | undefined;
    /** The grid's source, over the rows its links let through, under the spec's sort and filter and the user's. */
    readonly source: GridSource<ShapedRow>;
    /** The source its rows are fetched from, where its input is held on a server (ADR-0343); undefined over a
     * Snapshot. */
    readonly fetched: FetchingGridSource<ShapedRow> | undefined;
    /** The columns shown, in the user's order at the user's widths: a new array each time they change. */
    columns: readonly GridColumn<ShapedRow>[];
    /** Why the last selection narrowed nothing and hands nothing on, over a source held on a server: it reached rows
     * not fetched, or rows read at different versions (ADR-0343). Undefined otherwise. */
    unread: string | undefined;

    /** @internal Made by {@link bindBoard} and {@link bindBoardThrough}. */
    constructor(view: BoardGridView, rows: { readonly snapshot: Snapshot } | { readonly source: BoardSource }, locale: string, path: string) {
        this.view = view;
        this.#kinds = new Map(view.grid.columns.map(column => [column.column, column.kind]));
        const columns = 'snapshot' in rows ? shapedColumns(rows.snapshot, view.grid, locale) : fetchedColumns(view.grid, locale);
        this.#declared = new Map(columns.map(column => [column.name, column]));
        this.#shown = view.grid.viewState.columns.map(state => ({
            column: state.column,
            width: state.width,
            pinned: state.pinned,
        }));
        this.columns = columns;
        if ('snapshot' in rows) {
            this.snapshot = rows.snapshot;
            this.#rows = shapedRows(rows.snapshot);
            // Its rows change only when a link narrows them, a step the user took, which reaches the grid at once.
            const held = InMemoryGridSource.from(this.#rows, { rowKey: row => row.at });
            held.gatherInterval = 0;
            this.source = this.#held = held;
            held.setColumns(columns);
            // The spec's reader reads the sort against every column the input declares, as a server's grid may sort by
            // any; a grid over a Snapshot sorts by those it shows (C#'s GridSource.From refuses the rest by name).
            view.grid.viewState.sorts.forEach((sort, i) => {
                if (!this.#declared.has(sort.column)) {
                    throw new BoardRefusedError(`${path}.sorts[${i}]`, `The grid sorts by column '${sort.column}', which it does not show.`);
                }
            });
        } else {
            const { source } = rows;
            this.snapshot = undefined;
            this.#rows = Object.freeze([]);
            this.source = this.fetched = FetchingGridSource.fetch<ShapedRow>(async (query, signal) => {
                const page = await source.fetch({ view: view.id, grid: query, narrowing: this.#narrowing }, signal);
                checkShapedPage(page.rows, view.grid);
                return { rows: shapedRows(page.rows), start: page.start, totalCount: page.totalCount };
            }, {
                // Held in the source's lastError until an answer lands, which the Board says in the grid's place
                // (failure), rather than thrown where nobody hears it.
                onFetchFailed: () => undefined,
            });
        }
        this.source.setSorts(view.grid.viewState.sorts);
        this.#taggedFilter = view.grid.viewState.filter;
        this.#engineSet = this.#engineFilter(view.grid.viewState.filter, path);
        this.source.setFilter(this.#engineSet);
        // The query first, so a fetched grid's first page is fetched under it, never once without it and again.
        if (this.#held === undefined) this.source.setColumns(columns);
    }

    /** Why the source did not answer the grid's last question, over a source held on a server; undefined while it
     * answers, or over a Snapshot. The rows shown are then the ones it answered before. */
    get failure(): string | undefined {
        const error = this.fetched?.snapshot().lastError;
        return error === undefined ? undefined : `The source did not answer the grid: ${messageOf(error)}`;
    }

    /** The View State the grid is shown under now (ADR-0207): the columns as the user left them, and the sort and the
     * filter its source holds, the filter's operands typed as ExBoard's C# grid types them. */
    get viewState(): GridViewState {
        const { sorts, filter } = this.source.snapshot();
        // The source holds a copy of the filter it was set, so it is compared, not taken by identity.
        const typed = sameGridFilter(filter, this.#engineSet) ? this.#taggedFilter : taggedFilter(filter, name => this.#kinds.get(name));
        const columns = this.#shown.map(shown => ({ ...shown }));
        return new GridViewState(typed === undefined ? { columns, sorts } : { columns, sorts, filter: typed });
    }

    /** What differs from the Board's spec, or undefined where nothing does. */
    get change(): BoardGridChange | undefined {
        const state = this.viewState;
        if (state.equals(this.view.grid.viewState) && this.#hidden.length === 0) return undefined;
        const filtered = state.filter === undefined ? [] : Object.keys(state.filter.columns);
        return new BoardGridChange(state, this.#hidden, new Map(filtered.map(column => {
            const kind = this.#kinds.get(column);
            if (kind === undefined) throw new Error(`Internal: the grid filters column '${column}', which it does not show.`);
            return [column, kind] as const;
        })));
    }

    /** @internal A part of a Diff already fitted to this view ({@link BoardDiff.fitTo}). A filter whose operands the
     * grid cannot compare is dropped, named in `dropped`, and the rest applies. */
    layOver(change: BoardGridChange, dropped: string[]): void {
        this.#shown = change.viewState.columns.map(shown => ({ ...shown }));
        this.#hidden = [...change.hidden];
        this.#rebuild();
        this.source.setSorts(change.viewState.sorts);
        try {
            this.#engineSet = engineFilterOf(change.viewState.filter, name => this.#declared.get(name));
            this.#taggedFilter = change.viewState.filter;
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a filter the grid cannot compare is dropped by name, as fitTo drops what no longer fits
            if (!(error instanceof GridViewStateJsonError)) throw error;
            dropped.push(`views.${this.view.id}.grid: ${error.message}`);
            return;
        }
        this.source.setFilter(this.#engineSet);
    }

    // The spec's filter as the engine compares it, against the columns the grid is built with; a refusal named under
    // the grid's path. Only the conversion is in the try: a refusal of the source's own is not the spec's.
    #engineFilter(filter: GridFilter | undefined, path: string): GridFilter | undefined {
        try {
            return engineFilterOf(filter, name => this.#declared.get(name));
        } catch (error) {
            if (!(error instanceof GridViewStateJsonError)) throw error;
            throw refusedAt(error, path);
        }
    }

    /** How many leading columns are pinned. */
    get pinnedColumnCount(): number {
        return this.#shown.filter(column => column.pinned).length;
    }

    /** The columns the user hid, in the order of their names. */
    get hidden(): readonly string[] {
        return Object.freeze([...this.#hidden]);
    }

    // A gesture is taken against the columns the grid was shown: one naming a column no longer shown, a round trip
    // behind a hide, is refused rather than applied to others (spine rule 6), and each says whether it was taken.

    /** A resize drag released (ADR-0016): the column takes the width as fixed. False for a column not shown, or a
     * width that is not a positive number. */
    resize(column: string, widthPx: number): boolean {
        const at = this.#at(column);
        const shown = this.#shown[at];
        if (shown === undefined || !Number.isFinite(widthPx) || widthPx <= 0) return false;
        this.#shown[at] = { ...shown, width: { px: widthPx } };
        this.#rebuild();
        return true;
    }

    /** A header drag dropped (ADR-0011/0032): the shown columns take `order`, the pinned ones still leading; a shown
     * column it leaves out keeps its place after them. False for an order naming a column not shown, or one twice. */
    reorder(order: readonly string[]): boolean {
        if (new Set(order).size !== order.length || order.some(name => this.#at(name) < 0)) return false;
        const pinned = this.pinnedColumnCount;
        const moved = [...order.map(name => this.#shownAt(this.#at(name))), ...this.#shown.filter(column => !order.includes(column.column))];
        this.#shown = moved.map((column, i) => ({ ...column, pinned: i < pinned }));
        this.#rebuild();
        return true;
    }

    /** The column menu's "hide this column" (ADR-0010). The last column shown is kept. False for a column not shown,
     * or the last one. */
    hide(column: string): boolean {
        const at = this.#at(column);
        if (at < 0 || this.#shown.length === 1) return false;
        this.#shown.splice(at, 1);
        this.#hidden = [...this.#hidden, column].toSorted();
        this.#rebuild();
        return true;
    }

    /** The column menu's "pin up to this column" (ADR-0004/0010): the leading `count` columns are pinned. False for
     * a count outside [0, the columns shown]. */
    pin(count: number): boolean {
        if (!Number.isInteger(count) || count < 0 || count > this.#shown.length) return false;
        this.#shown = this.#shown.map((column, i) => ({ ...column, pinned: i < count }));
        return true;
    }

    /** @internal Whether every row `selection` covers is one the grid holds. */
    holds(selection: SelectedRows): boolean {
        const { window, windowStart } = this.source.snapshot();
        return selection.ranges.every(range => range.topRow >= windowStart && range.topRow + range.rowCount <= windowStart + window.length);
    }

    /** @internal The condition the selection makes on the rows the grid holds, in the columns `on`, and the data they
     * were read from: the grid's Snapshot, or, over a fetched source, the page they came back in, undefined when they
     * came back in pages of different versions. Undefined when it selects nothing, or reaches past the rows held. */
    condition(on: readonly string[], selection: SelectedRows): { readonly condition: LinkCondition; readonly from: Snapshot | undefined } | undefined {
        // A selection taken against other rows than these is dropped, never read against them.
        if (selection.ranges.length === 0 || !this.holds(selection)) return undefined;
        const { window, windowStart } = this.source.snapshot();
        const taken = new Set<number>();
        const tuples: BoardValue[][] = [];
        const versions = new Set<bigint>();
        let page: Snapshot | undefined;
        let columns: ReturnType<Snapshot['column']>[] = [];
        for (const range of selection.ranges) {
            for (let r = range.topRow; r < range.topRow + range.rowCount; r++) {
                if (taken.has(r)) continue;
                taken.add(r);
                const row = window[r - windowStart];
                if (row === undefined) throw new Error(`Internal: row ${r} is held and not in the window.`);
                if (row.snapshot !== page) {
                    page = row.snapshot;
                    columns = on.map(name => row.snapshot.column(name));
                    versions.add(page.version);
                }
                tuples.push(columns.map(column => valueAt(column, row.at)));
            }
        }
        return { condition: new LinkCondition(on, tuples), from: versions.size === 1 ? page : undefined };
    }

    /** @internal Shows the rows `conditions` let through, or every row given none: over a Snapshot, the rows at their
     * positions; over a source held on a server, the source asked again, handed the conditions (ADR-0343). */
    narrowTo(conditions: readonly LinkCondition[]): void {
        if (this.fetched !== undefined) {
            this.#narrowing = Object.freeze([...conditions]);
            this.fetched.requery();
            return;
        }
        const held = this.#held;
        const snapshot = this.snapshot;
        if (held === undefined || snapshot === undefined) throw new Error('Internal: a grid over a Snapshot holds no rows.');
        held.replaceAll(conditions.length === 0 ? this.#rows : narrowedPositions(snapshot, conditions).map(p => this.#rowAt(p)));
        held.publishGathered();
    }

    #rowAt(position: number): ShapedRow {
        const row = this.#rows[position];
        if (row === undefined) throw new Error(`Internal: row ${position} is not one of the grid's.`);
        return row;
    }

    #shownAt(at: number): ColumnViewState {
        const shown = this.#shown[at];
        if (shown === undefined) throw new Error(`Internal: no column is shown at ${at}.`);
        return shown;
    }

    #at(column: string): number {
        return this.#shown.findIndex(shown => shown.column === column);
    }

    #rebuild(): void {
        this.columns = Object.freeze(this.#shown.map(shown => {
            const declared = this.#declared.get(shown.column);
            if (declared === undefined) throw new Error(`Internal: column '${shown.column}' is not the grid's.`);
            return resized(declared, shown.width);
        }));
    }
}

/** A pivot view over its input's Snapshot, or, while a link narrows it, over the rows let through; or over the source
 * its input's {@link BoardSource.pivot} makes, where the input is held on a server (ADR-0343). */
export class BoundPivotView {
    readonly #unnarrowed: SnapshotPivotSource | undefined;
    #held: SnapshotPivotSource | undefined;
    readonly #shape: DataShape;
    #selected = 0;

    /** What kind of view it is. */
    readonly kind = 'pivot';
    /** The view as the Board writes it. */
    readonly view: BoardPivotView;
    /** The Snapshot its input is bound to, or undefined where the input is held on a server. */
    readonly snapshot: Snapshot | undefined;
    /** Whether its input is held on a server: its source is the one the input's {@link BoardSource.pivot} made, and the
     * rows under its cells are asked of {@link BoardSource.under} ({@link BoundBoard.selectCellsThrough}). */
    readonly served: boolean;
    /** The pivot's source: over its input's rows, or, while a link narrows it, over the rows let through; a new
     * source each time, which is what tells the pivot. */
    source: PivotSource;
    /** The layout shown: the Board's, until the user changes it. */
    layout: PivotLayout;

    /** @internal Made by {@link bindBoard} and {@link bindBoardThrough}: over `snapshot`, or over `served`. */
    constructor(view: BoardPivotView, shape: DataShape, snapshot: Snapshot | undefined, served: PivotSource | undefined) {
        this.view = view;
        this.snapshot = snapshot;
        this.#shape = shape;
        this.layout = view.layout;
        if (snapshot !== undefined) {
            this.served = false;
            this.source = this.#held = this.#unnarrowed = new SnapshotPivotSource(snapshot);
        } else if (served !== undefined) {
            this.served = true;
            this.source = served;
        } else throw new Error('Internal: a pivot over neither a Snapshot nor a source.');
    }

    /** The user changed the layout from the field list or a header (ADR-0059). */
    changeLayout(layout: PivotLayout): void {
        this.layout = layout;
    }

    /** The layout, or undefined where it is the Board's. */
    get change(): BoardPivotChange | undefined {
        return writePivotLayoutJson(this.layout) === writePivotLayoutJson(this.view.layout)
            ? undefined
            : new BoardPivotChange(this.layout, kindsOf(this.#shape, placed(this.layout)));
    }

    /** @internal The rows the pivot shows, which its cells are read against; a pivot over a source held on a server,
     * whose rows are there, is refused with an `Error`. */
    shownData(): Snapshot {
        if (this.#held === undefined) {
            throw new Error(`Pivot '${this.view.id}' is over a source held on a server, which is asked for the values under its cells: select them with selectCellsThrough.`);
        }
        return this.#held.snapshot;
    }

    /** @internal The selections made so far: a newer one, or a narrowing, drops the answer still on its way for an
     * older one (spine rule 6). */
    get selected(): number {
        return this.#selected;
    }

    /** @internal A selection made, answering its number. */
    select(): number {
        return ++this.#selected;
    }

    /** @internal Shows the rows of `narrowed`, or every row given undefined; over a server, the source made for the
     * narrowing now, which drops the selection with any answer still on its way for it. */
    narrowTo(narrowed: Snapshot | undefined, served: PivotSource | undefined): void {
        if (this.served) {
            if (served === undefined) throw new Error('Internal: a served pivot narrowed to no source.');
            this.#selected++;
            this.source = served;
            return;
        }
        const unnarrowed = this.#unnarrowed;
        if (unnarrowed === undefined) throw new Error('Internal: a pivot over a Snapshot holds no source over it.');
        this.source = this.#held = narrowed === undefined ? unnarrowed : new SnapshotPivotSource(narrowed);
    }
}

/** A chart view, its Chart Spec read from its inputs' data, or, where an input is held on a server, through its
 * source's {@link BoardSource.thin} (ADR-0344). */
export class BoundChartView {
    #reading: ChartSpecReading | undefined;
    #ask: (() => Promise<void>) | undefined;
    #asked = 0;
    #controller: AbortController | undefined;
    #versions: ReadonlyMap<string, bigint> = new Map();

    /** What kind of view it is. */
    readonly kind = 'chart';
    /** The view as the Board writes it. */
    readonly view: BoardChartView;
    /** Whether the chart is read through Chart Sources (ADR-0344): an input of it is held on a server, thinned there,
     * so it holds only the points drawn, and is asked again as it is narrowed and as its range changes. */
    readonly thinned: boolean;
    /** Why the chart is not drawn, naming the path, when its spec cannot draw the rows its links let through, or its
     * source did not answer; undefined while it is drawn. {@link BoundChartView.reading} is then of other rows, and
     * not shown. */
    refusal: string | undefined;
    /** The keys of the series the user hid, in order: a new array each time it changes (ADR-0272). */
    hiddenSeries: readonly string[] = Object.freeze([]);
    /** The range the user narrowed the chart to, or undefined for the whole (ADR-0273). */
    range: ChartRange | undefined;
    /** The axis the user switched to, or undefined for the declared one (ADR-0251). */
    scale: AxisScale | undefined;

    /** @internal Made by {@link bindBoard} and {@link bindBoardThrough}: read, or, through its sources, read before the
     * Board is handed back. */
    constructor(view: BoardChartView, reading: ChartSpecReading | undefined) {
        this.view = view;
        this.#reading = reading;
        this.thinned = reading === undefined;
    }

    /** The chart's title and definition, read from the rows its links let through; a new reading each time. */
    get reading(): ChartSpecReading {
        const reading = this.#reading;
        if (reading === undefined) throw new Error(`Internal: chart '${this.view.id}' is not read yet.`);
        return reading;
    }

    /** What the user changed, or undefined where nothing is. */
    get change(): BoardChartChange | undefined {
        const change = new BoardChartChange(this.hiddenSeries, this.range, this.scale);
        return change.isNone ? undefined : change;
    }

    /** A series hidden or shown from the legend (ADR-0271). */
    changeVisibility(key: string, hidden: boolean): void {
        if (this.hiddenSeries.includes(key) === hidden) return;
        // A part's hidden series are its keys once each, in order.
        this.hiddenSeries = new BoardChartChange(hidden ? [...this.hiddenSeries, key] : this.hiddenSeries.filter(k => k !== key)).hiddenSeries;
    }

    /** The range narrowed, or returned to the whole with undefined (ADR-0273). A chart over a server input asks its
     * sources again for the range, drawn at full detail when the answer comes (ADR-0344). */
    changeRange(range: ChartRange | undefined): void {
        const moved = range?.from !== this.range?.from || range?.to !== this.range?.to;
        this.range = range === undefined ? undefined : Object.freeze({ ...range });
        if (moved && this.#ask !== undefined) void this.#ask();
    }

    /** The axis switched under Chart Filters (ADR-0251). */
    changeScale(scale: AxisScale): void {
        this.scale = scale;
    }

    /** @internal A part of a Diff already fitted to this view. */
    layOver(change: BoardChartChange): void {
        this.hiddenSeries = change.hiddenSeries;
        this.range = change.range;
        this.scale = change.scale;
    }

    /** @internal Takes a reading of the rows let through, and, read through its sources, the version of each input
     * it was read at. */
    read(reading: ChartSpecReading | ChartSourceReading): void {
        if ('reading' in reading) {
            this.#reading = reading.reading;
            this.#versions = reading.versions;
        } else this.#reading = reading;
        this.refusal = undefined;
    }

    /** @internal Says why the rows let through cannot be drawn. */
    refuse(refusal: string): void {
        this.refusal = refusal;
    }

    /** @internal The version of each input the reading drawn was read at. */
    get versions(): ReadonlyMap<string, bigint> {
        return this.#versions;
    }

    /** @internal How the Board asks the chart's sources again. */
    asks(ask: () => Promise<void>): void {
        this.#ask = ask;
    }

    /** @internal An ask begun: its number, and the signal that aborts it, aborted by the next ask and by `stop`. */
    ask(stop: AbortSignal): { readonly asked: number; readonly signal: AbortSignal } {
        this.#controller?.abort();
        const controller = new AbortController();
        this.#controller = controller;
        return { asked: ++this.#asked, signal: AbortSignal.any([controller.signal, stop]) };
    }

    /** @internal Whether ask `asked` is the newest, whose answer is drawn. */
    answers(asked: number): boolean {
        return asked === this.#asked;
    }

    /** @internal Aborts the ask on its way, whose answer is then dropped. */
    stopAsking(): void {
        this.#asked++;
        this.#controller?.abort();
    }
}

// A chart reads only what its inputs declare: a column the source has beside them is not there for it (ADR-0341).
function readChart(view: BoardChartView, bound: ReadonlyMap<string, Snapshot>, inputs: ReadonlyMap<string, BoardInput>, path: string,
    narrowed: ((name: string) => readonly number[]) | undefined): ChartSpecReading {
    try {
        return readChartSpec(view.spec, name => {
            const snapshot = bound.get(name);
            const input = inputs.get(name);
            return snapshot === undefined || input === undefined ? undefined : declaredChartData(snapshot, input, narrowed?.(name));
        });
    } catch (error) {
        if (!(error instanceof ChartSpecRefusedError)) throw error;
        throw new BoardRefusedError(error.path === '' ? path : `${path}.${error.path}`, error.message, { cause: error });
    }
}
