// A Board with every input bound to its data (ADR-0341): C#'s BoundBoard over Snapshots held in memory. Each view
// is made what it draws, a link's selection narrows the views it reaches (ADR-0340, Q5), and an output is raised
// only by the user's confirm (Q6). A Board over sources held on a server (ADR-0343 to ADR-0345) is not yet here.

import { ChartSpecRefusedError, readChartSpec, type ChartSpecReading } from '@ex-grid/chart';
import { engineFilterOf, GridViewStateJsonError, InMemoryGridSource, type GridColumn, type GridColumnWidth, type GridSelection } from '@ex-grid/core';
import { BoardRefusedError, type BoardInput, type Snapshot } from '@ex-grid/data';
import { pivotItemKeyAt, publicKeyId, SnapshotPivotSource, type PivotFieldItem, type PivotLayout } from '@ex-grid/pivot-engine';
import { BoardOutput } from './board-output';
import { columnTypeOf, refusedAt } from './grid-view-spec';
import type { BoardChartView, BoardGridView, BoardLink, BoardPivotView, BoardReading } from './board-spec';
import { LinkCondition, narrowedPositions, narrowedSnapshot, valueAt, type BoardValue } from './link-condition';
import { resized, shapedColumns, shapedRows, type ShapedRow } from './shaped-grid';
import { declaredChartData } from './snapshot-values';

/** The cells selected in a pivot, as its selection reads them: each cell's row and column Items, outermost
 * first. A cell on a subtotal names fewer Items, and a grand total none. */
export interface PivotCellItems {
    /** The row's Items, outermost first. */
    readonly row: readonly PivotFieldItem[];
    /** The column's Items, outermost first. */
    readonly column: readonly PivotFieldItem[];
}

/** What a grid's selection is read by: its rectangles, in the rows the grid shows; a {@link @ex-grid/core#GridSelection} is one. */
export type SelectedRows = Pick<GridSelection, 'ranges'>;

/** How a Board is bound. */
export interface BindBoardOptions {
    /** The locale a grid's text is written in, a BCP 47 tag or `invariant`, the default (ADR-0370 section 10). */
    readonly locale?: string;
}

/** A view of a bound Board. */
export type BoundView = BoundGridView | BoundPivotView | BoundChartView;

/**
 * Binds each of `board`'s inputs to the Snapshot `data` holds under its name, and makes each view. Nothing is
 * drawn unless every input binds and every view can be made: an input with no data, a Snapshot whose shape is not
 * the declaration, and a chart its spec cannot draw from the data are refused, with a {@link @ex-grid/data#BoardRefusedError}
 * naming the input or the path. Data for a name the Board does not declare is not read.
 */
export function bindBoard(board: BoardReading, data: Readonly<Record<string, Snapshot>>, options: BindBoardOptions = {}): BoundBoard {
    const { locale = 'invariant' } = options;
    const bound = new Map<string, Snapshot>();
    for (const input of board.inputs) {
        const snapshot = Object.hasOwn(data, input.name) ? data[input.name] : undefined;
        if (snapshot === undefined) throw new BoardRefusedError(`inputs.${input.name}`, `Input '${input.name}' is bound to no data.`);
        bound.set(input.name, input.bind(snapshot));
    }
    const inputs = new Map(board.inputs.map(input => [input.name, input]));
    const views = board.views.map((view, i): BoundView => {
        switch (view.kind) {
            case 'grid': return new BoundGridView(view, held(bound, view.grid.data), locale, `views[${i}].grid`);
            case 'pivot': return new BoundPivotView(view, held(bound, view.input));
            case 'chart': return new BoundChartView(view, readChart(view, bound, inputs, `views[${i}].chart`, undefined));
            default: throw new Error(`Internal: ${String(view satisfies never)} is not a view.`);
        }
    });
    return new BoundBoard(board, bound, inputs, views);
}

function held(bound: ReadonlyMap<string, Snapshot>, name: string): Snapshot {
    const snapshot = bound.get(name);
    if (snapshot === undefined) throw new Error(`Internal: input '${name}' is not bound.`);
    return snapshot;
}

/**
 * A Board with every input bound to its data (ADR-0341), made by {@link bindBoard}. A view narrowed by a link is
 * made again from the rows let through, and every listener {@link BoundBoard.subscribe}d is told once per step.
 */
export class BoundBoard {
    readonly #inputs: ReadonlyMap<string, BoardInput>;
    readonly #conditions = new Map<BoardLink, LinkCondition>();
    readonly #taken = new Map<string, { readonly condition: LinkCondition; readonly from: Snapshot }>();
    readonly #listeners = new Set<() => void>();

    /** The Board. */
    readonly board: BoardReading;
    /** The Snapshot each input is bound to. */
    readonly data: ReadonlyMap<string, Snapshot>;
    /** The views, in the Board's order. */
    readonly views: readonly BoundView[];

    /** @internal Made by {@link bindBoard}. */
    constructor(board: BoardReading, data: ReadonlyMap<string, Snapshot>, inputs: ReadonlyMap<string, BoardInput>, views: readonly BoundView[]) {
        this.board = board;
        this.data = data;
        this.#inputs = inputs;
        this.views = Object.freeze([...views]);
    }

    /** The view whose id is `id`; an id the Board does not have is refused with a `RangeError`. */
    view(id: string): BoundView {
        const found = this.views.find(view => view.view.id === id);
        if (found === undefined) throw new RangeError(`The Board has no view '${id}'.`);
        return found;
    }

    /** Hears each step that narrowed a view or changed what can be confirmed; answers the call that stops it. */
    subscribe(listener: () => void): () => void {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    }

    /** The condition `link` narrows by now, or undefined while its view selects nothing. */
    conditionOf(link: BoardLink): LinkCondition | undefined {
        this.#linkFrom(link);
        return this.#conditions.get(link);
    }

    /**
     * The condition `selection`, made in `link`'s grid, narrows by: the values in the link's columns of every row
     * the selection covers, in any column, read from the rows the grid shows; undefined when it selects nothing,
     * which narrows nothing (ADR-0011). A link not the Board's, or one from a pivot, is refused with a `RangeError`.
     */
    gridCondition(link: BoardLink, selection: SelectedRows): LinkCondition | undefined {
        const from = this.#linkFrom(link);
        if (from.kind !== 'grid') throw new RangeError(`The link starts at pivot '${link.from}', whose selection narrows by the Items its cells stand for.`);
        return from.condition(link.on, selection);
    }

    /**
     * The condition `cells`, selected in `link`'s pivot, narrows by (ADR-0340, Q5): the values in the link's columns
     * of every row of the pivot's data under any of the cells, read from the rows the pivot shows. A cell on a
     * subtotal stands for every Item below it and a grand total for every row; an Item the pivot hides is under
     * none. Undefined when no cell is selected. A link not the Board's, or one from a grid, is refused with a
     * `RangeError`.
     */
    cellCondition(link: BoardLink, cells: readonly PivotCellItems[]): LinkCondition | undefined {
        const from = this.#linkFrom(link);
        if (from.kind !== 'pivot') throw new RangeError(`The link starts at grid '${link.from}', whose selection narrows by its rows.`);
        return cells.length === 0 ? undefined : valuesUnder(from, link.on, cells);
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
     * selection that moves within the same values narrows nothing again. Answers whether any view was narrowed.
     */
    select(from: string, selection: SelectedRows): boolean {
        const grid = this.view(from);
        if (grid.kind !== 'grid') throw new RangeError(`View '${from}' is not a grid.`);
        let changed = false;
        for (const output of this.board.outputs.filter(o => o.from === from)) {
            const condition = grid.condition(output.on, selection);
            const was = this.#taken.get(output.name);
            if (condition === undefined) changed = this.#taken.delete(output.name) || changed;
            else {
                changed = changed || was === undefined || !was.condition.letsThroughAs(condition);
                this.#taken.set(output.name, { condition, from: grid.snapshot });
            }
        }
        const targets = new Set<string>();
        for (const link of this.board.links.filter(l => l.from === from)) {
            if (this.#set(link, grid.condition(link.on, selection))) for (const target of link.to) targets.add(target);
        }
        for (const target of targets) this.#narrowView(target);
        if (changed || targets.size > 0) this.#tell();
        return targets.size > 0;
    }

    /**
     * Narrows by `cells`, selected in pivot view `from`: each link from it narrows by the condition the cells make,
     * and each view they reach is narrowed once. Undefined, a selection the pivot could not read, narrows nothing,
     * as no selection does. Answers whether any view was narrowed.
     */
    selectCells(from: string, cells: readonly PivotCellItems[] | undefined): boolean {
        const pivot = this.view(from);
        if (pivot.kind !== 'pivot') throw new RangeError(`View '${from}' is not a pivot.`);
        const targets = new Set<string>();
        for (const link of this.board.links.filter(l => l.from === from)) {
            const condition = cells === undefined || cells.length === 0 ? undefined : valuesUnder(pivot, link.on, cells);
            if (this.#set(link, condition)) for (const target of link.to) targets.add(target);
        }
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
     * on its input, with the version of the input's data they were taken against; undefined while its grid
     * selects nothing. Nothing is raised by a selection on the way: only this, called when the user confirms. A
     * name the Board does not declare is refused with a `RangeError`.
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

    #input(name: string): BoardInput {
        const input = this.#inputs.get(name);
        if (input === undefined) throw new Error(`Internal: the Board declares no input '${name}'.`);
        return input;
    }

    #tell(): void {
        for (const listener of this.#listeners) listener();
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
        const conditions = [...this.#conditions].filter(([link]) => link.to.includes(id)).map(([, condition]) => condition);
        const view = this.view(id);
        switch (view.kind) {
            case 'grid': {
                view.narrowTo(conditions.length === 0 ? undefined : narrowedPositions(view.snapshot, conditions));
                // Its rows changed, so its selection is dropped (ADR-0011): what it held is let go now, so nothing is
                // confirmed or narrowed by a selection the grid no longer shows.
                for (const output of this.board.outputs.filter(o => o.from === id)) this.#taken.delete(output.name);
                this.#letGo(id);
                break;
            }
            case 'pivot': {
                view.narrowTo(conditions.length === 0 ? undefined : narrowedSnapshot(view.snapshot, narrowedPositions(view.snapshot, conditions)));
                // Its report changed, so its selection is dropped, and what it narrowed is let go now.
                this.#letGo(id);
                break;
            }
            case 'chart': {
                const path = `views[${this.board.views.findIndex(v => v.id === id)}].chart`;
                try {
                    view.read(readChart(view.view, this.data, this.#inputs, path,
                        conditions.length === 0 ? undefined : name => narrowedPositions(held(this.data, name), conditions)));
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
 * the columns' order, widths, pinning and those hidden. The Board's own spec is never rewritten (ADR-0340, Q7).
 */
export class BoundGridView {
    readonly #declared: ReadonlyMap<string, GridColumn<ShapedRow>>;
    readonly #rows: readonly ShapedRow[];
    #shown: { readonly column: string; readonly width: GridColumnWidth; readonly pinned: boolean }[];
    #hidden: string[] = [];

    /** What kind of view it is. */
    readonly kind = 'grid';
    /** The view as the Board writes it. */
    readonly view: BoardGridView;
    /** The Snapshot its input is bound to. */
    readonly snapshot: Snapshot;
    /** The grid's source, over the rows its links let through, under the spec's sort and filter and the user's. */
    readonly source: InMemoryGridSource<ShapedRow>;
    /** The columns shown, in the user's order at the user's widths: a new array each time they change. */
    columns: readonly GridColumn<ShapedRow>[];

    /** @internal Made by {@link bindBoard}. */
    constructor(view: BoardGridView, snapshot: Snapshot, locale: string, path: string) {
        this.view = view;
        this.snapshot = snapshot;
        const columns = shapedColumns(snapshot, view.grid, locale);
        this.#declared = new Map(columns.map(column => [column.name, column]));
        this.#shown = view.grid.viewState.columns.map(state => ({
            column: state.column,
            width: state.width,
            pinned: state.pinned,
        }));
        this.columns = columns;
        this.#rows = shapedRows(snapshot);
        // Its rows change only when a link narrows them, a step the user took, which reaches the grid at once.
        this.source = InMemoryGridSource.from(this.#rows, { rowKey: row => row.at });
        this.source.gatherInterval = 0;
        this.source.setColumns(columns);
        // The spec's reader reads the sort against every column the input declares, as a server's grid may sort by any;
        // a grid over a Snapshot sorts by those it shows (C#'s GridSource.From refuses the rest by name).
        view.grid.viewState.sorts.forEach((sort, i) => {
            if (!this.#declared.has(sort.column)) {
                throw new BoardRefusedError(`${path}.sorts[${i}]`, `The grid sorts by column '${sort.column}', which it does not show.`);
            }
        });
        this.source.setSorts(view.grid.viewState.sorts);
        const kinds = new Map(view.grid.columns.map(column => [column.column, columnTypeOf(column.kind)]));
        try {
            this.source.setFilter(engineFilterOf(view.grid.viewState.filter, name => kinds.get(name)));
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

    /** @internal The condition the selection makes on the rows the grid shows, in the columns `on`; undefined when it
     * selects nothing. */
    condition(on: readonly string[], selection: SelectedRows): LinkCondition | undefined {
        if (selection.ranges.length === 0) return undefined;
        const { window, windowStart } = this.source.snapshot();
        const columns = on.map(name => this.snapshot.column(name));
        const taken = new Set<number>();
        const tuples: BoardValue[][] = [];
        for (const range of selection.ranges) {
            for (let r = range.topRow; r < range.topRow + range.rowCount; r++) {
                if (taken.has(r)) continue;
                taken.add(r);
                const row = window[r - windowStart];
                // A selection taken against other rows than these is dropped, never read against them.
                if (row === undefined) return undefined;
                tuples.push(columns.map(column => valueAt(column, row.at)));
            }
        }
        return new LinkCondition(on, tuples);
    }

    /** @internal Shows the rows at `positions` of its Snapshot, or every row given undefined. */
    narrowTo(positions: readonly number[] | undefined): void {
        this.source.replaceAll(positions === undefined ? this.#rows : positions.map(p => this.#rowAt(p)));
        this.source.publishGathered();
    }

    #rowAt(position: number): ShapedRow {
        const row = this.#rows[position];
        if (row === undefined) throw new Error(`Internal: row ${position} is not one of the grid's.`);
        return row;
    }

    #shownAt(at: number): { readonly column: string; readonly width: GridColumnWidth; readonly pinned: boolean } {
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

/** A pivot view over its input's Snapshot, or, while a link narrows it, over the rows let through. */
export class BoundPivotView {
    readonly #unnarrowed: SnapshotPivotSource;

    /** What kind of view it is. */
    readonly kind = 'pivot';
    /** The view as the Board writes it. */
    readonly view: BoardPivotView;
    /** The Snapshot its input is bound to. */
    readonly snapshot: Snapshot;
    /** The pivot's source: over its input's rows, or, while a link narrows it, over the rows let through; a new
     * source each time, which is what tells the pivot. */
    source: SnapshotPivotSource;
    /** The layout shown: the Board's, until the user changes it. */
    layout: PivotLayout;

    /** @internal Made by {@link bindBoard}. */
    constructor(view: BoardPivotView, snapshot: Snapshot) {
        this.view = view;
        this.snapshot = snapshot;
        this.layout = view.layout;
        this.source = this.#unnarrowed = new SnapshotPivotSource(snapshot);
    }

    /** The user changed the layout from the field list or a header (ADR-0059). */
    changeLayout(layout: PivotLayout): void {
        this.layout = layout;
    }

    /** @internal Shows the rows of `narrowed`, or every row given undefined. */
    narrowTo(narrowed: Snapshot | undefined): void {
        this.source = narrowed === undefined ? this.#unnarrowed : new SnapshotPivotSource(narrowed);
    }
}

/** A chart view, its Chart Spec read from its inputs' data. */
export class BoundChartView {
    /** What kind of view it is. */
    readonly kind = 'chart';
    /** The view as the Board writes it. */
    readonly view: BoardChartView;
    /** The chart's title and definition, read from the rows its links let through; a new reading each time. */
    reading: ChartSpecReading;
    /** Why the chart is not drawn, naming the path, when its spec cannot draw the rows its links let through;
     * undefined while it is drawn. {@link BoundChartView.reading} is then of other rows, and not shown. */
    refusal: string | undefined;

    /** @internal Made by {@link bindBoard}. */
    constructor(view: BoardChartView, reading: ChartSpecReading) {
        this.view = view;
        this.reading = reading;
    }

    /** @internal Takes a reading of the rows let through. */
    read(reading: ChartSpecReading): void {
        this.reading = reading;
        this.refusal = undefined;
    }

    /** @internal Says why the rows let through cannot be drawn. */
    refuse(refusal: string): void {
        this.refusal = refusal;
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

// The values in `on` of every row of the pivot's data under any of `cells`, read from the rows it shows. A step
// naming no column of the data would constrain nothing, and so let through rows the cell does not stand for.
function valuesUnder(pivot: BoundPivotView, on: readonly string[], cells: readonly PivotCellItems[]): LinkCondition {
    const snapshot = pivot.source.snapshot;
    const column = (name: string, what: string) => {
        const found = snapshot.columns.find(c => c.name === name);
        if (found === undefined) throw new RangeError(`${what} '${name}', which pivot '${pivot.view.id}' does not read from its data.`);
        return found;
    };
    const hidden = pivot.layout.rows.concat(pivot.layout.columns, pivot.layout.filters)
        .filter(field => field.hiddenItems.length > 0 && snapshot.columns.some(c => c.name === field.field))
        .map(field => ({ column: column(field.field, 'A field names'), hidden: new Set(field.hiddenItems.map(publicKeyId)) }));
    const groups = new Map<string, { readonly columns: ReturnType<typeof column>[]; readonly items: Set<string> }>();
    for (const cell of cells) {
        const steps = [...cell.row, ...cell.column];
        const fields = steps.map(step => step.field).join('\u001f');
        let group = groups.get(fields);
        if (group === undefined) {
            group = { columns: steps.map(step => column(step.field, 'A cell names field')), items: new Set() };
            groups.set(fields, group);
        }
        group.items.add(steps.map(step => publicKeyId(step.item)).join('\u001f'));
    }
    const columns = on.map(name => column(name, 'The link compares column'));
    const groupList = [...groups.values()];
    const tuples: BoardValue[][] = [];
    for (let row = 0; row < snapshot.rowCount; row++) {
        if (hidden.some(h => h.hidden.has(publicKeyId(pivotItemKeyAt(h.column, row))))) continue;
        const under = groupList.some(g => g.items.has(g.columns.map(c => publicKeyId(pivotItemKeyAt(c, row))).join('\u001f')));
        if (under) tuples.push(columns.map(c => valueAt(c, row)));
    }
    return new LinkCondition(on, tuples);
}


