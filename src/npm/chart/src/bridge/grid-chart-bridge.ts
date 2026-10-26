// The grid's side of a chart, ready-made (ADR-0258): the counterpart of C#'s GridChartBridge. ExGrid does not
// know it exists (ADR-0250's dependency direction); where the chart is shown is the Consumer's.
import type {
    CellPosition, GridColumn, GridCommand, GridContextMenuContext, GridHeaderGroup, GridSource, SelectionRange,
} from '@ex-grid/core';
import type { ChartDefinition } from '../definition';
import type { ChartPointClick } from '../dom/chart-element';
import type { ChartOrientation, ChartTarget, GridCell, GridChart } from './grid-chart';
import type { GridChartDeclarations } from './grid-chart-declarations';
import {
    axisX, declaredOrientations, definitionOf, drawingOf, readSelection, sameDrawing, type BridgeGridState, type Drawing,
} from './selection-reader';

const addPrefix = 'exchart.add:';

/** The stable identities of the commands a {@link GridChartBridge} adds to a grid's menu (ADR-0036). */
export const gridChartCommandIds = Object.freeze({
    /** "Chart": the selection in the one orientation its columns declare. */
    chart: 'exchart.chart',
    /** "Series in columns", offered beside `inRows` only when a selection declares both. */
    inColumns: 'exchart.chart.columns',
    /** "Series in rows", offered beside `inColumns` only when a selection declares both. */
    inRows: 'exchart.chart.rows',
    /** The prefix of "Add to chart: <name>"; the target's name follows it. */
    addPrefix,
} as const);

/** The wording of one of the bridge's commands, for the grid's `commandLabel`; `undefined` for any other. */
export function gridChartCommandLabel(id: string): string | undefined {
    switch (id) {
        case gridChartCommandIds.chart: return 'Chart';
        case gridChartCommandIds.inColumns: return 'Series in columns';
        case gridChartCommandIds.inRows: return 'Series in rows';
        default: return id.startsWith(addPrefix) ? `Add to chart: ${id.slice(addPrefix.length)}` : undefined;
    }
}

/**
 * The grid whose selections are charted, as the Consumer handed it to `createGrid` or `<ExGrid>`: its columns
 * and Header Groups as they stand, and the Grid Source its rows come from, whose snapshot is the Window and the
 * Row Sequence Version the grid shows.
 */
export interface GridChartGrid<Row> {
    /** The columns, in display order. */
    readonly columns: readonly GridColumn<Row>[];
    /** The Header Groups over them; none unless given. */
    readonly headerGroups?: readonly GridHeaderGroup[];
    /** The source the grid reads its rows from. */
    readonly source: GridSource<Row>;
}

/**
 * Where the bridge asks the grid to put the Focus (ADR-0258, M3-Q6): the cell a point stands for, as the
 * Selection, under the Row Sequence Version the bridge found it in, which the grid's placement refuses under
 * another order.
 */
export interface GridSelectionPlacement {
    /** The Selection to place: the one cell. */
    readonly range: SelectionRange;
    /** Where the Focus goes: the same cell. */
    readonly focus: CellPosition;
    /** The Row Sequence Version the positions are written in. */
    readonly rowSequenceVersion: number;
}

/** What a {@link GridChartBridge} is made with. */
export interface GridChartBridgeOptions<Row> {
    /** What the Consumer declared for the grid's selections. */
    readonly declarations: GridChartDeclarations<Row>;
    /** The grid whose selections are charted, read each time it is needed; `undefined` while there is none. */
    readonly grid: () => GridChartGrid<Row> | undefined;
    /** The open charts the menu offers "Add to chart" for (ADR-0258, U7), in the menu's order, read each time the
     * menu opens; none unless given. */
    readonly targets?: () => readonly ChartTarget[];
    /** Hears a chart made from the selection; the Consumer decides where it is shown. */
    readonly onChart?: (chart: GridChart) => void;
    /** Hears a target and the chart it becomes with the selection's series added. */
    readonly onAdded?: (target: ChartTarget, chart: GridChart) => void;
    /** Hears the sentence that says why a selection was not charted, or a point not shown in the grid. */
    readonly onRefused?: (why: string) => void;
    /**
     * Places the Selection and the Focus on a cell and scrolls the grid to it, as C#'s `PlaceSelectionAsync`
     * does, answering whether it was placed. The Script Core's grid has no such member yet, so the Consumer
     * supplies it; without it `showInGrid` refuses to be asked.
     */
    readonly placeSelection?: (placement: GridSelectionPlacement) => boolean | Promise<boolean>;
}

/**
 * The grid's side of a chart, ready-made (ADR-0258): context-menu commands a Consumer adds to a grid in one
 * line, which turn the selection into a {@link GridChart}, and the reading of that chart, by Row Key from the
 * Consumer's data, into the Chart Definition an ExChart draws. The grid does not know it exists; where the
 * chart is shown is the Consumer's.
 */
export class GridChartBridge<Row> {
    readonly #options: GridChartBridgeOptions<Row>;
    readonly #drawn = new WeakMap<GridChart, { readonly drawing: Drawing; readonly definition: ChartDefinition }>();

    /** A bridge that reads selections by `options.declarations`. */
    constructor(options: GridChartBridgeOptions<Row>) {
        // Checked as an untyped caller may hand them, where they arrive (CONVENTIONS.md).
        const given: { readonly declarations?: unknown; readonly grid?: unknown } = options;
        if (typeof given.grid !== 'function') throw new TypeError('A GridChartBridge reads its grid through a function: grid: () => the grid.');
        if (typeof given.declarations !== 'object' || given.declarations === null) {
            throw new TypeError('A GridChartBridge reads selections by declarations: { rowKey, rowOf, ... } (ADR-0258).');
        }
        const { rowKey, rowOf }: { readonly rowKey?: unknown; readonly rowOf?: unknown } = given.declarations;
        if (typeof rowKey !== 'function' || typeof rowOf !== 'function') {
            throw new TypeError('A chart follows its rows by Row Key: declarations name rowKey and rowOf, both functions (ADR-0258).');
        }
        this.#options = Object.freeze({ ...options });
    }

    /** What the Consumer declared for the grid's selections. */
    get declarations(): GridChartDeclarations<Row> {
        return this.#options.declarations;
    }

    /**
     * The commands for the grid's `contextCommands`, worded by {@link gridChartCommandLabel}: "Chart", or "Series
     * in columns" and "Series in rows" when the selection declares both, and "Add to chart" for each target. It is
     * one function for the bridge's life, so it can be handed to the grid as it is on every render (ADR-0003).
     */
    readonly commands = (context: GridContextMenuContext<Row>): readonly GridCommand[] => {
        const enabled = context.selection.length > 0;
        const g = this.#state();
        const both = this.declarations.orientation === undefined && g !== undefined
            && declaredOrientations(g, context.selection, this.declarations).length === 2;
        const command = (id: string, orientation: ChartOrientation | undefined, target: ChartTarget | undefined): GridCommand =>
            Object.freeze({ id, enabled, invoke: () => { void this.#make(context, orientation, target); } });
        const commands = both
            ? [command(gridChartCommandIds.inColumns, 'seriesInColumns', undefined), command(gridChartCommandIds.inRows, 'seriesInRows', undefined)]
            : [command(gridChartCommandIds.chart, undefined, undefined)];
        const targets = this.#options.targets === undefined ? [] : this.#options.targets();
        for (const target of targets) commands.push(command(addPrefix + target.name, undefined, target));
        return Object.freeze(commands);
    };

    /**
     * The Chart Definition `chart` stands for now, read by Row Key from the declarations' `rowOf`: a recalculated
     * value is redrawn, and a row that no longer exists is a gap with a Note (ADR-0258). While nothing it reads
     * has changed, the definition last returned for the same chart is returned again, so the chart skips its
     * render (ADR-0252). A chart that can no longer be drawn, such as one whose axis column left the grid, is
     * refused with a `ChartRefusedError`.
     */
    draw(chart: GridChart): ChartDefinition {
        const g = this.#state();
        if (g === undefined) throw new Error('The bridge has no grid to draw from; its grid() answered none.');
        const drawing = drawingOf(g.columns, g.groups, chart, this.declarations);
        const last = this.#drawn.get(chart);
        if (last !== undefined && sameDrawing(last.drawing, drawing)) return last.definition;
        const definition = definitionOf(drawing, true);
        this.#drawn.set(chart, { drawing, definition });
        return definition;
    }

    /** The grid cell a click on a point of `chart` stands for, or `undefined` when its row no longer exists. */
    cellOf(chart: GridChart, click: ChartPointClick): GridCell | undefined {
        const d = this.declarations;
        const series = chart.series.find(s => s.id === click.seriesId);
        if (series === undefined) return undefined;
        if (series.type === 'column') {
            const axis = this.#state()?.columns.find(c => c.name === chart.axisColumn);
            for (const r of series.rows) {
                const row = d.rowOf(r.key);
                if (row === undefined) continue;
                const x = axis === undefined ? r.x : axisX(axis.value(row)) ?? r.x;
                if (x === click.position) return Object.freeze({ rowKey: r.key, column: series.column });
            }
            return undefined;
        }
        if (d.rowOf(series.rowKey) === undefined) return undefined;
        const { positionOf } = d;
        const column = positionOf === undefined ? undefined : series.columns.find(c => positionOf(c) === click.position);
        return column === undefined ? undefined : Object.freeze({ rowKey: series.rowKey, column });
    }

    /**
     * Scrolls the grid to the cell a click on a point stands for and puts the Focus on it (ADR-0258, M3-Q6),
     * through the Consumer's `placeSelection`. Answers whether the Focus was placed; false where the point's row
     * no longer exists.
     */
    async showInGrid(chart: GridChart, click: ChartPointClick): Promise<boolean> {
        const cell = this.cellOf(chart, click);
        return cell === undefined ? false : this.showCellInGrid(cell);
    }

    /**
     * Scrolls the grid to `cell` and puts the Focus on it, as a click on a point does (ADR-0258): for a chart the
     * Consumer built by hand, such as a scatter whose points carry their Row Keys, whose click names the row
     * (ADR-0290). Answers whether the Focus was placed; when the row is not among the rows the grid shows,
     * `onRefused` says so.
     */
    async showCellInGrid(cell: GridCell): Promise<boolean> {
        const place = this.#options.placeSelection;
        if (place === undefined) {
            throw new Error('The bridge was given no placeSelection, so it cannot move the grid\'s Focus (ADR-0258, M3-Q6).');
        }
        const g = this.#state();
        if (g === undefined) return false;
        const column = g.columns.findIndex(c => c.name === cell.column);
        const at = g.window.findIndex(r => this.declarations.rowKey(r) === cell.rowKey);
        const row = at < 0 ? -1 : g.windowStart + at;
        if (row < 0 || column < 0) {
            this.#refuse(row < 0 ? 'That row is not among the rows the grid shows: it may be filtered out.' : `${cell.column} is not among the grid's columns.`);
            return false;
        }
        return place(Object.freeze({
            range: Object.freeze({ topRow: row, leftColumn: column, rowCount: 1, columnCount: 1 }),
            focus: Object.freeze({ row, column }),
            rowSequenceVersion: g.rowSequenceVersion,
        }));
    }

    #state(): BridgeGridState<Row> | undefined {
        const grid = this.#options.grid();
        if (grid === undefined) return undefined;
        const { source } = grid;
        const snapshot = source.snapshot();
        return {
            columns: grid.columns,
            groups: grid.headerGroups === undefined ? [] : grid.headerGroups,
            rowSequenceVersion: snapshot.rowSequenceVersion,
            window: snapshot.window,
            windowStart: snapshot.windowStart,
            fetch: range => source.rows(range, new AbortController().signal),
        };
    }

    async #make(context: GridContextMenuContext<Row>, orientation: ChartOrientation | undefined, target: ChartTarget | undefined): Promise<void> {
        const g = this.#state();
        if (g === undefined) return;
        const read = await readSelection(g, context.selection, context.rowSequenceVersion, this.declarations, orientation, target);
        if (read.chart === undefined) this.#refuse(read.refusal);
        else if (target !== undefined) this.#options.onAdded?.(target, read.chart);
        else this.#options.onChart?.(read.chart);
    }

    #refuse(why: string): void {
        this.#options.onRefused?.(why);
    }
}
