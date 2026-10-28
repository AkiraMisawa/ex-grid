// An input held where the data lives (ADR-0343 to ADR-0345): C#'s BoardSource, the host's own code, which says the
// input's shape, fetches a grid's rows, makes a pivot's source and, where it can, thins a chart's points and answers
// the values under a pivot's cells. The Board never talks to a server itself; each call is handed the narrowing the
// Board's links put on the view, for the host to execute where the data lives.

import type { ChartSourceAnswer, ChartSourceQuery } from '@ex-grid/chart';
import type { GridQuery } from '@ex-grid/core';
import type { DataShape, ShapedSource, Snapshot } from '@ex-grid/data';
import { pivotItemKeyAt, pivotItemKeyId, type PivotFieldItem, type PivotLayout, type PivotSource } from '@ex-grid/pivot-engine';
import { LinkCondition, narrowedPositions, valueAt, type BoardValue } from './link-condition';

/** The cells selected in a pivot, as its selection reads them: each cell's row and column Items, outermost
 * first. A cell on a subtotal names fewer Items, and a grand total none. */
export interface PivotCellItems {
    /** The row's Items, outermost first. */
    readonly row: readonly PivotFieldItem[];
    /** The column's Items, outermost first. */
    readonly column: readonly PivotFieldItem[];
}

/**
 * What a grid view over a {@link BoardSource} asks (ADR-0343): the grid's own query, its range, sort and filter
 * (ADR-0025), and the conditions the Board's links narrow the view by now, every one of which a row must meet. The
 * counterpart of C#'s `BoardQuery`.
 */
export interface BoardQuery {
    /** The id of the grid view asking. */
    readonly view: string;
    /** The grid's query. */
    readonly grid: GridQuery;
    /** The links' conditions, each over its link's columns; empty while nothing narrows the view. */
    readonly narrowing: readonly LinkCondition[];
}

/**
 * What a chart view over a {@link BoardSource} asks for each of its series (ADR-0344): the Chart Source's question,
 * and the conditions the Board's links narrow the view by now. The counterpart of C#'s `BoardChartQuery`.
 */
export interface BoardChartQuery {
    /** The id of the chart view asking. */
    readonly view: string;
    /** The column, the range and the pixels asked for. */
    readonly chart: ChartSourceQuery;
    /** The links' conditions, each over its link's columns; empty while nothing narrows the view. */
    readonly narrowing: readonly LinkCondition[];
}

/**
 * What a link from a pivot view over a {@link BoardSource} asks as its cells are selected (ADR-0345): the values of
 * `on` among the rows under any of `cells`. The counterpart of C#'s `BoardCellsQuery`.
 */
export interface BoardCellsQuery {
    /** The id of the pivot view the cells are selected in. */
    readonly view: string;
    /** The layout the cells were read from, with the Items it hides. */
    readonly layout: PivotLayout;
    /** The cells selected, by the Items their paths name. */
    readonly cells: readonly PivotCellItems[];
    /** The link's columns. */
    readonly on: readonly string[];
    /** The conditions the pivot's own links narrow it by now, every one of which a row must meet. */
    readonly narrowing: readonly LinkCondition[];
}

/**
 * One answer of a {@link BoardSource} to a fetched grid's query (ADR-0343): the rows as a Snapshot, in the query's
 * order, whose version is the version of the data they were read from; the position the first holds in that order;
 * and how many rows the whole filtered result holds. The counterpart of C#'s `ShapedPage`.
 */
export interface ShapedPage {
    /** The rows, in the query's order. */
    readonly rows: Snapshot;
    /** The position of the first row in the query's order. */
    readonly start: number;
    /** How many rows the whole filtered result holds. */
    readonly totalCount: number;
}

/**
 * An input held where the data lives (ADR-0343): the host's own code, bound to a Board's input in place of a
 * Snapshot. It answers the input's shape, checked against the declaration before any row (ADR-0341), and fetches a
 * grid's rows a page at a time; a pivot, a chart or a link from a pivot over the input asks what only the host can
 * answer, and a Board with one over a source that does not answer it is refused by path. The counterpart of C#'s
 * `BoardSource`.
 */
export interface BoardSource extends ShapedSource {
    /** Answers the input's shape, before any row. */
    shape(signal?: AbortSignal): Promise<DataShape>;
    /** Answers a grid view's query, narrowed by its links, with a page of rows; `signal` is aborted when the grid no
     * longer waits for it. */
    fetch(query: BoardQuery, signal: AbortSignal): Promise<ShapedPage>;
    /**
     * Makes a pivot view's source over the input's rows `narrowing` lets through (all of them; none for the whole
     * input), such as a `FetchingPivotSource` answering Leaf Aggregates from the server (ADR-0370 section 3). Called
     * again each time the view's narrowing changes.
     */
    pivot?(narrowing: readonly LinkCondition[]): PivotSource;
    /** Answers a chart view's series with the points it draws, thinned where the data lives (ADR-0344), as
     * `thinChartData` thins, under the narrowing the Board's links put on the view. */
    thin?(query: BoardChartQuery, signal: AbortSignal): Promise<ChartSourceAnswer>;
    /** Answers a link from a pivot view over this input with the values of the link's columns among the rows under
     * the selected cells (ADR-0345), as {@link valuesUnder} reads them. */
    under?(query: BoardCellsQuery, signal: AbortSignal): Promise<LinkCondition>;
}

/**
 * The reference implementation of {@link BoardSource.under} (ADR-0345), and the reading the Board makes of a pivot
 * over a Snapshot: the values of `query.on` of every row of `data` the narrowing lets through and any of the cells
 * stands for. A cell is over the rows whose values are the Items its paths name, so a cell on a subtotal stands for
 * every Item below it and a grand total for every row; an Item the layout hides is under none. A cell naming a field,
 * or a link naming a column, that `data` does not hold is refused with a `TypeError`. The counterpart of C#'s
 * `BoardSource.ValuesUnder`.
 */
export function valuesUnder(data: Snapshot, query: BoardCellsQuery): LinkCondition {
    const column = (name: string, what: string) => {
        const found = data.columns.find(c => c.name === name);
        if (found === undefined) throw new TypeError(`${what} '${name}', which pivot '${query.view}' does not read from its data.`);
        return found;
    };
    const { layout } = query;
    const hidden = layout.rows.concat(layout.columns, layout.filters)
        .filter(field => field.hiddenItems.length > 0 && data.columns.some(c => c.name === field.field))
        .map(field => ({ column: column(field.field, 'A field names'), hidden: new Set(field.hiddenItems.map(pivotItemKeyId)) }));
    // The cells grouped by the fields their paths name, each group a set of the Items they name there, so a row's keys
    // are read once and looked up, not compared with every cell. A step naming no column of the data would constrain
    // nothing, and so let through rows the cell does not stand for.
    const groups = new Map<string, { readonly columns: ReturnType<typeof column>[]; readonly items: Set<string> }>();
    for (const cell of query.cells) {
        const steps = [...cell.row, ...cell.column];
        const fields = steps.map(step => step.field).join('\u001f');
        let group = groups.get(fields);
        if (group === undefined) {
            group = { columns: steps.map(step => column(step.field, 'A cell names field')), items: new Set() };
            groups.set(fields, group);
        }
        group.items.add(steps.map(step => pivotItemKeyId(step.item)).join('\u001f'));
    }
    const columns = query.on.map(name => column(name, 'The link compares column'));
    const groupList = [...groups.values()];
    const tuples: BoardValue[][] = [];
    const take = (row: number) => {
        if (hidden.some(h => h.hidden.has(pivotItemKeyId(pivotItemKeyAt(h.column, row))))) return;
        if (groupList.some(g => g.items.has(g.columns.map(c => pivotItemKeyId(pivotItemKeyAt(c, row))).join('\u001f')))) {
            tuples.push(columns.map(c => valueAt(c, row)));
        }
    };
    // The rows the narrowing lets through, read in place rather than copied.
    if (query.narrowing.length === 0) for (let row = 0; row < data.rowCount; row++) take(row);
    else for (const row of narrowedPositions(data, query.narrowing)) take(row);
    return new LinkCondition(query.on, tuples);
}
