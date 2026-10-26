// What the user changed in a running Board (ADR-0340, Q7): C#'s BoardDiff, read and written as the same text, and laid
// against a Board with what no longer fits dropped by name. The shared cases of tests/cases/board/diff.json hold the two
// to one another.

import type { AxisScale, ChartRange } from '@ex-grid/chart';
import {
    GridViewState, GridViewStateJsonError, readGridViewStateJson, writeGridViewStateJson,
    type ColumnViewState, type FilterSpec, type GridFilter, type SortSpec,
} from '@ex-grid/core';
import {
    BoardRefusedError, boardKindNames, isJsonArray, isJsonObject, jsonDoubleText, jsonMember, jsonMemberNames, jsonRawText,
    jsonStringText, JsonNumber, JsonTextError, readJsonText, shapeColumn, writeJsonText,
    type DataShape, type JsonObject, type JsonValue, type SnapshotKind,
} from '@ex-grid/data';
import {
    PivotFormatError, PivotVersionError, readPivotLayoutJson, writePivotLayoutJson, type PivotLayout,
} from '@ex-grid/pivot-engine';
import { declaredInput, type BoardGridView, type BoardReading, type BoardView } from './board-spec';
import { members } from './json-members';

const rootKeys = ['version', 'views'];
const partKinds = ['grid', 'pivot', 'chart'] as const;
const gridKeys = ['viewState', 'hidden', 'kinds'];
const pivotKeys = ['layout', 'kinds'];
const chartKeys = ['hidden', 'range', 'scale'];
const rangeKeys = ['from', 'to', 'yFrom', 'yTo'] as const;
const scales: readonly AxisScale[] = Object.freeze(['proportional', 'categories', 'logarithmic']);

/** The kinds a part depends on, by column. */
export type BoardColumnKinds = ReadonlyMap<string, SnapshotKind>;

/** A grid's part of a Board Diff: the View State it is shown under, and the columns the user hid, which the View State
 * leaves out — so that a column the Board adds later is shown, not taken for one hidden. */
export class BoardGridChange {
    /** The columns shown, their order, widths and pinning, and the sort and the filter. */
    readonly viewState: GridViewState;
    /** The columns the user hid, in the order of their names. */
    readonly hidden: readonly string[];
    /** The kinds of the columns its filter reads, as the input declared them when it was set, so a filter on a column
     * declared another kind since is dropped, not laid over it. Empty where they are not known, which checks nothing. */
    readonly kinds: BoardColumnKinds;

    /** A grid's part. A column both shown and hidden is refused with a `TypeError`. */
    constructor(viewState: GridViewState, hidden: Iterable<string>, kinds: BoardColumnKinds = new Map()) {
        this.viewState = viewState;
        this.hidden = Object.freeze(ordinal(new Set(hidden)));
        const both = this.hidden.find(column => viewState.columns.some(shown => shown.column === column));
        if (both !== undefined) throw new TypeError(`Column '${both}' is both shown and hidden.`);
        this.kinds = new Map(kinds);
        Object.freeze(this);
    }
}

/** A pivot's part of a Board Diff: the layout the user made, and the kinds of the fields it places, as the input
 * declared them then. */
export class BoardPivotChange {
    /** The layout. */
    readonly layout: PivotLayout;
    /** The kinds of the fields it places, as declared when it was made; empty where they are not known. */
    readonly kinds: BoardColumnKinds;

    /** A pivot's part. */
    constructor(layout: PivotLayout, kinds: BoardColumnKinds = new Map()) {
        this.layout = layout;
        this.kinds = new Map(kinds);
        Object.freeze(this);
    }
}

/** A chart's part of a Board Diff (ADR-0272), in the data's terms. */
export class BoardChartChange {
    /** The keys of the series hidden, in order. */
    readonly hiddenSeries: readonly string[];
    /** The range shown, or undefined for the whole. */
    readonly range: ChartRange | undefined;
    /** The axis the user switched to, or undefined for the declared one. */
    readonly scale: AxisScale | undefined;

    /** A chart's part: the series hidden, the range narrowed to, and the axis switched to. */
    constructor(hiddenSeries: Iterable<string>, range?: ChartRange, scale?: AxisScale) {
        this.hiddenSeries = Object.freeze(ordinal(new Set(hiddenSeries)));
        this.range = range === undefined ? undefined : Object.freeze({ ...range });
        this.scale = scale;
        Object.freeze(this);
    }

    /** Whether the part changes nothing. */
    get isNone(): boolean {
        return this.hiddenSeries.length === 0 && this.range === undefined && this.scale === undefined;
    }
}

/** The parts of a Board Diff, each keyed by its view's id; a view has one part at most. */
export interface BoardDiffParts {
    /** The grids' parts. */
    readonly grids?: ReadonlyMap<string, BoardGridChange>;
    /** The pivots' parts. */
    readonly pivots?: ReadonlyMap<string, BoardPivotChange>;
    /** The charts' parts. */
    readonly charts?: ReadonlyMap<string, BoardChartChange>;
}

/** A Board Diff laid against a Board ({@link BoardDiff.fitTo}). */
export interface BoardDiffReading {
    /** The parts that still fit. */
    readonly diff: BoardDiff;
    /** One sentence per part, or part of one, that no longer fits, naming its path. */
    readonly dropped: readonly string[];
}

/**
 * What the user changed in a running Board (ADR-0340, Q7), one part per view that differs from what the Board writes:
 * a grid's View State and the columns hidden from it, a pivot's layout, and a chart's hidden series, range and axis
 * (ADR-0272). Running a Board never rewrites it; the host may keep this per user ({@link BoardDiff.toJson}), read it
 * back ({@link BoardDiff.read}) and lay it over the Board next time ({@link BoardDiff.fitTo}). Making it the default
 * is writing a new Board.
 */
export class BoardDiff {
    /** The version this writer writes and the only one this reader reads. */
    static readonly version = 1;
    /** A Diff of nothing: the Board as it is written. */
    static readonly empty = new BoardDiff({});

    /** The grids' parts, by view id. */
    readonly grids: ReadonlyMap<string, BoardGridChange>;
    /** The pivots' parts, by view id. */
    readonly pivots: ReadonlyMap<string, BoardPivotChange>;
    /** The charts' parts, by view id. */
    readonly charts: ReadonlyMap<string, BoardChartChange>;

    /** A Diff of these parts. A view with more than one part is refused with a `TypeError`. */
    constructor(parts: BoardDiffParts) {
        this.grids = new Map(parts.grids);
        this.pivots = new Map(parts.pivots);
        this.charts = new Map(parts.charts);
        const named = new Set<string>();
        for (const id of [...this.grids.keys(), ...this.pivots.keys(), ...this.charts.keys()]) {
            if (named.has(id)) throw new TypeError(`View '${id}' has more than one part; a view is one of grid, pivot and chart.`);
            named.add(id);
        }
        Object.freeze(this);
    }

    /** Whether the user changed nothing. */
    get isEmpty(): boolean {
        return this.grids.size === 0 && this.pivots.size === 0 && this.charts.size === 0;
    }

    /**
     * The Diff as JSON, byte for byte as ExBoard writes it, its views in the order of their ids:
     * `{"version":1,"views":{"blotter":{"grid":{"viewState":{…},"hidden":[…],"kinds":{"pnl":"decimal"}}},…}}`. A grid's
     * View State is the core's View State JSON and a pivot's layout the Pivot Layout's, each with its own version;
     * `kinds` are the kinds the input declared the columns a part depends on as, when it was made.
     */
    toJson(): string {
        const ids = ordinal([...this.grids.keys(), ...this.pivots.keys(), ...this.charts.keys()]);
        const views = ids.map(id => `${jsonStringText(id)}:${this.#part(id)}`);
        return `{"version":${BoardDiff.version},"views":{${views.join(',')}}}`;
    }

    #part(id: string): string {
        const grid = this.grids.get(id);
        if (grid !== undefined) {
            return `{"grid":{"viewState":${writeGridViewStateJson(grid.viewState)},"hidden":${strings(grid.hidden)},"kinds":${kindsJson(grid.kinds)}}}`;
        }
        const pivot = this.pivots.get(id);
        if (pivot !== undefined) {
            // The Pivot Layout's own writer, its strings written again as ExBoard writes them.
            return `{"pivot":{"layout":${writeJsonText(readJsonText(writePivotLayoutJson(pivot.layout)))},"kinds":${kindsJson(pivot.kinds)}}}`;
        }
        const chart = this.charts.get(id);
        if (chart === undefined) throw new Error(`Internal: view '${id}' has no part.`);
        const written = [`"hidden":${strings(chart.hiddenSeries)}`];
        const { range } = chart;
        if (range !== undefined) {
            const ends = rangeKeys.flatMap(key => {
                const end = range[key];
                return end === undefined ? [] : [`${jsonStringText(key)}:${jsonDoubleText(end)}`];
            });
            written.push(`"range":{${ends.join(',')}}`);
        }
        if (chart.scale !== undefined) written.push(`"scale":${jsonStringText(chart.scale)}`);
        return `{"chart":{${written.join(',')}}}`;
    }

    /** Whether `other` holds the same parts: what {@link BoardDiff.toJson} writes is the same. */
    sameAs(other: BoardDiff | undefined): boolean {
        return other !== undefined && (other === this || other.toJson() === this.toJson());
    }

    /**
     * The Diff `json` holds, as {@link BoardDiff.toJson} wrote it. It is not yet laid against a Board:
     * {@link BoardDiff.fitTo} does that, and says what no longer fits. Refused, with a
     * {@link @ex-grid/data#BoardRefusedError} naming the path as a Board's refusal does: text that is not JSON, a
     * version other than {@link BoardDiff.version}, a member it does not have, a view that is not one of grid, pivot and
     * chart, a column both shown and hidden, or a part its own reader refuses.
     */
    static read(json: string): BoardDiff {
        let root: JsonValue;
        try {
            root = readJsonText(json);
        } catch (error) {
            if (!(error instanceof JsonTextError)) throw error;
            throw new BoardRefusedError('', `A Board Diff is JSON: ${error.message}`, { cause: error });
        }
        if (!isJsonObject(root)) throw new BoardRefusedError('', 'A Board Diff is an object.');
        members(root, rootKeys, '', 'A Board Diff');
        const version = jsonMember(root, 'version');
        const number = version instanceof JsonNumber ? version.int32 : undefined;
        if (number === undefined) throw new BoardRefusedError('version', `A Board Diff names its version; this reader reads version ${BoardDiff.version}.`);
        if (number !== BoardDiff.version) {
            throw new BoardRefusedError('version', `Version ${number} is not one this reader reads; it reads version ${BoardDiff.version}.`);
        }
        const views = jsonMember(root, 'views');
        if (!isJsonObject(views)) throw new BoardRefusedError('views', "A Board Diff holds its views' parts in 'views', an object keyed by view id.");

        const grids = new Map<string, BoardGridChange>();
        const pivots = new Map<string, BoardPivotChange>();
        const charts = new Map<string, BoardChartChange>();
        for (const id of jsonMemberNames(views)) {
            const part = jsonMember(views, id);
            const names = isJsonObject(part) ? jsonMemberNames(part) : [];
            const kind = partKinds.find(k => names.length === 1 && names[0] === k);
            if (!isJsonObject(part) || kind === undefined) {
                throw new BoardRefusedError(`views.${id}`, `View '${id}''s part is an object holding one of grid, pivot and chart.`);
            }
            const path = `views.${id}.${kind}`;
            const inner = jsonMember(part, kind);
            switch (kind) {
                case 'grid': grids.set(id, readGrid(inner, path)); break;
                case 'pivot': pivots.set(id, readPivot(inner, path)); break;
                case 'chart': charts.set(id, readChart(inner, path)); break;
                default: throw new Error(`Internal: ${String(kind satisfies never)} is not a kind of view.`);
            }
        }
        return new BoardDiff({ grids, pivots, charts });
    }

    /**
     * This Diff laid against `board`: each part kept where it still fits, and what does not dropped by name, never
     * applied to something else (spine rule 1). A part of a view the Board no longer has, or no longer has of that
     * kind, is dropped; so is a column, a sort level or a column's filter on a column the grid no longer shows, a filter
     * set on a column declared another kind since, and a pivot layout placing a field its input no longer declares, or
     * declares another kind. A column the grid shows that the part neither shows nor hides — one the Board has added
     * since — is shown, last. A part that comes to what the Board writes is left out.
     */
    fitTo(board: BoardReading): BoardDiffReading {
        const dropped: string[] = [];
        const grids = new Map<string, BoardGridChange>();
        const pivots = new Map<string, BoardPivotChange>();
        const charts = new Map<string, BoardChartChange>();
        for (const [id, change] of this.grids) {
            const view = viewOf(board, id, 'grid', dropped);
            if (view?.kind !== 'grid') continue;
            const fitted = fitGrid(view, change, dropped);
            if (fitted !== undefined) grids.set(id, fitted);
        }
        for (const [id, change] of this.pivots) {
            const view = viewOf(board, id, 'pivot', dropped);
            if (view?.kind !== 'pivot') continue;
            const declared = declaredInput(board, view.input).shape;
            const misfit = pivotMisfit(change, declared, view.input);
            if (misfit !== undefined) dropped.push(`views.${id}.pivot: ${misfit}`);
            else if (writePivotLayoutJson(change.layout) !== writePivotLayoutJson(view.layout)) {
                pivots.set(id, new BoardPivotChange(change.layout, kindsOf(declared, placed(change.layout))));
            }
        }
        for (const [id, change] of this.charts) {
            const view = viewOf(board, id, 'chart', dropped);
            if (view?.kind === 'chart' && !change.isNone) charts.set(id, change);
        }
        return Object.freeze({ diff: new BoardDiff({ grids, pivots, charts }), dropped: Object.freeze(dropped) });
    }
}

/** The fields a layout places: its filters, rows, columns and values, in that order. */
export function placed(layout: PivotLayout): readonly string[] {
    return [...layout.filters, ...layout.rows, ...layout.columns, ...layout.values].map(field => field.field);
}

/** The kinds `shape` declares `columns` as, leaving out a column it does not declare. */
export function kindsOf(shape: DataShape, columns: Iterable<string>): BoardColumnKinds {
    const found = new Map<string, SnapshotKind>();
    for (const column of columns) {
        const declared = shapeColumn(shape, column);
        if (declared !== undefined) found.set(column, declared.kind);
    }
    return found;
}


function pivotMisfit(change: BoardPivotChange, declared: DataShape, input: string): string | undefined {
    for (const field of new Set(placed(change.layout))) {
        const column = shapeColumn(declared, field);
        if (column === undefined) return `the layout places field '${field}', which input '${input}' no longer declares.`;
        const was = change.kinds.get(field);
        if (was !== undefined && was !== column.kind) {
            return `the layout places field '${field}' as ${was}, which input '${input}' now declares ${column.kind}.`;
        }
    }
    return undefined;
}

function viewOf(board: BoardReading, id: string, kind: BoardView['kind'], dropped: string[]): BoardView | undefined {
    const view = board.views.find(one => one.id === id);
    if (view === undefined) {
        dropped.push(`views.${id}.${kind}: the Board no longer has view '${id}'.`);
        return undefined;
    }
    if (view.kind !== kind) {
        dropped.push(`views.${id}.${kind}: view '${id}' is now a ${view.kind}, not a ${kind}.`);
        return undefined;
    }
    return view;
}

function fitGrid(view: BoardGridView, change: BoardGridChange, dropped: string[]): BoardGridChange | undefined {
    const path = `views.${view.id}.grid`;
    const declared = new Map(view.grid.columns.map(column => [column.column, column.kind]));
    const state = change.viewState;
    const columns: ColumnViewState[] = [];
    for (const column of state.columns) {
        if (declared.has(column.column)) columns.push(column);
        else dropped.push(`${path}: column '${column.column}' is no longer shown by view '${view.id}'.`);
    }
    const hidden: string[] = [];
    for (const column of change.hidden) {
        if (declared.has(column)) hidden.push(column);
        else dropped.push(`${path}: column '${column}', hidden, is no longer shown by view '${view.id}'.`);
    }
    const placedColumns = new Set([...columns.map(column => column.column), ...hidden]);
    for (const added of view.grid.viewState.columns) {
        if (!placedColumns.has(added.column)) columns.push({ column: added.column, width: added.width, pinned: false });
    }
    if (columns.length === 0) {
        dropped.push(`${path}: none of the columns it shows is shown by view '${view.id}' now, and a grid shows one at least.`);
        return undefined;
    }
    const sorts: SortSpec[] = [];
    for (const sort of state.sorts) {
        if (declared.has(sort.column)) sorts.push(sort);
        else dropped.push(`${path}: the sort on column '${sort.column}', which view '${view.id}' no longer shows.`);
    }
    const filter = fitFilter(state.filter, change.kinds, declared, path, view.id, dropped);
    const viewState = new GridViewState(filter === undefined ? { columns, sorts } : { columns, sorts, filter });
    const filtered = filter === undefined ? [] : Object.keys(filter.columns);
    const fitted = new BoardGridChange(viewState, hidden, new Map(filtered.map(column => [column, kindAt(declared, column)])));
    return viewState.equals(view.grid.viewState) && hidden.length === 0 ? undefined : fitted;
}

function fitFilter(
    held: GridFilter | undefined, was: BoardColumnKinds, declared: ReadonlyMap<string, SnapshotKind>, path: string, id: string, dropped: string[],
): GridFilter | undefined {
    if (held === undefined) return undefined;
    const kept: Record<string, FilterSpec> = {};
    for (const [column, spec] of Object.entries(held.columns)) {
        const then = was.get(column);
        // A filter's operands are of its column's kind: one set on a column declared another kind then would be laid over
        // a column it no longer reads (spine rule 1).
        if (!declared.has(column)) dropped.push(`${path}: the filter on column '${column}', which view '${id}' no longer shows.`);
        else if (then !== undefined && then !== kindAt(declared, column)) {
            dropped.push(`${path}: the filter on column '${column}', set on ${then}, which the column now is not; it is ${kindAt(declared, column)}.`);
        } else kept[column] = spec;
    }
    if (Object.keys(kept).length === 0 && held.opaque === undefined) return undefined;
    return held.opaque === undefined ? { columns: kept } : { columns: kept, opaque: held.opaque };
}

function kindAt(declared: ReadonlyMap<string, SnapshotKind>, column: string): SnapshotKind {
    const kind = declared.get(column);
    if (kind === undefined) throw new Error(`Internal: column '${column}' was kept and is not shown.`);
    return kind;
}

function readGrid(node: JsonValue | undefined, path: string): BoardGridChange {
    if (!isJsonObject(node)) throw new BoardRefusedError(path, "A grid's part is an object holding its 'viewState' and its 'hidden' columns.");
    members(node, gridKeys, path, "A grid's part");
    const viewState = jsonMember(node, 'viewState');
    if (!isJsonObject(viewState)) throw new BoardRefusedError(`${path}.viewState`, "A grid's part holds its View State in 'viewState'.");
    let state: GridViewState;
    try {
        // Read against the names it holds itself: whether each is still the grid's is fitTo's, which drops by name what
        // this reader would refuse whole.
        state = readGridViewStateJson(jsonRawText(viewState), namesIn(viewState));
    } catch (error) {
        if (!(error instanceof GridViewStateJsonError)) throw error;
        throw new BoardRefusedError(`${path}.viewState`, error.message, { cause: error });
    }
    const hidden = texts(jsonMember(node, 'hidden'), `${path}.hidden`, "A grid's 'hidden' is a list of column names.");
    const both = hidden.find(column => state.columns.some(shown => shown.column === column));
    if (both !== undefined) throw new BoardRefusedError(`${path}.hidden`, `Column '${both}' is both shown and hidden.`);
    return new BoardGridChange(state, hidden, readKinds(jsonMember(node, 'kinds'), `${path}.kinds`));
}

// The column names a View State's JSON writes in its columns, its sorts and its filter.
function namesIn(viewState: JsonObject): Set<string> {
    const names = new Set<string>();
    for (const list of [jsonMember(viewState, 'columns'), jsonMember(viewState, 'sorts')]) {
        if (!isJsonArray(list)) continue;
        for (const item of list) {
            const name = isJsonObject(item) ? jsonMember(item, 'column') : undefined;
            if (typeof name === 'string') names.add(name);
        }
    }
    const filter = jsonMember(viewState, 'filter');
    const filtered = isJsonObject(filter) ? jsonMember(filter, 'columns') : undefined;
    if (isJsonObject(filtered)) for (const name of jsonMemberNames(filtered)) names.add(name);
    return names;
}

function readPivot(node: JsonValue | undefined, path: string): BoardPivotChange {
    if (!isJsonObject(node)) throw new BoardRefusedError(path, "A pivot's part is an object holding its 'layout'.");
    members(node, pivotKeys, path, "A pivot's part");
    const layout = jsonMember(node, 'layout');
    if (!isJsonObject(layout)) throw new BoardRefusedError(`${path}.layout`, "A pivot's part holds its Pivot Layout in 'layout'.");
    let read: PivotLayout;
    try {
        read = readPivotLayoutJson(jsonRawText(layout));
    } catch (error) {
        if (!(error instanceof PivotFormatError || error instanceof PivotVersionError || error instanceof TypeError || error instanceof RangeError)) throw error;
        throw new BoardRefusedError(`${path}.layout`, `The pivot's layout cannot be read: ${error.message}`, { cause: error });
    }
    return new BoardPivotChange(read, readKinds(jsonMember(node, 'kinds'), `${path}.kinds`));
}

function readKinds(node: JsonValue | undefined, path: string): BoardColumnKinds {
    if (node === undefined || node === null) return new Map();
    if (!isJsonObject(node)) throw new BoardRefusedError(path, "A part's 'kinds' is an object naming each column's kind.");
    const read = new Map<string, SnapshotKind>();
    for (const column of jsonMemberNames(node)) {
        const name = jsonMember(node, column);
        const kind = boardKindNames.find(k => k === name);
        if (kind === undefined) throw new BoardRefusedError(`${path}.${column}`, `A column's kind is one of ${boardKindNames.join(', ')}.`);
        read.set(column, kind);
    }
    return read;
}

function readChart(node: JsonValue | undefined, path: string): BoardChartChange {
    if (!isJsonObject(node)) throw new BoardRefusedError(path, "A chart's part is an object.");
    members(node, chartKeys, path, "A chart's part");
    const hidden = texts(jsonMember(node, 'hidden'), `${path}.hidden`, "A chart's 'hidden' is a list of series keys.");
    let range: ChartRange | undefined;
    const rangeNode = jsonMember(node, 'range');
    if (rangeNode !== undefined && rangeNode !== null) {
        if (!isJsonObject(rangeNode)) throw new BoardRefusedError(`${path}.range`, "A chart's range is an object.");
        members(rangeNode, rangeKeys, `${path}.range`, "A chart's range");
        const ends: Partial<Record<(typeof rangeKeys)[number], number>> = {};
        for (const key of rangeKeys) {
            const end = jsonMember(rangeNode, key);
            if (end === undefined || end === null) continue;
            const value = end instanceof JsonNumber ? Number(end.raw) : Number.NaN;
            if (!Number.isFinite(value)) throw new BoardRefusedError(`${path}.range.${key}`, "An end of a range is a number in the axis' values.");
            ends[key] = value;
        }
        range = ends;
    }
    let scale: AxisScale | undefined;
    const scaleNode = jsonMember(node, 'scale');
    if (scaleNode !== undefined && scaleNode !== null) {
        scale = scales.find(known => known === scaleNode);
        if (scale === undefined) throw new BoardRefusedError(`${path}.scale`, `A chart's scale is one of ${scales.join(', ')}.`);
    }
    return new BoardChartChange(hidden, range, scale);
}

function texts(node: JsonValue | undefined, path: string, message: string): readonly string[] {
    if (node === undefined || node === null) return [];
    if (!isJsonArray(node)) throw new BoardRefusedError(path, message);
    return node.map(item => {
        if (typeof item !== 'string') throw new BoardRefusedError(path, message);
        return item;
    });
}

function kindsJson(kindsHeld: BoardColumnKinds): string {
    return `{${ordinal([...kindsHeld.keys()]).map(column => `${jsonStringText(column)}:${jsonStringText(kindAt(kindsHeld, column))}`).join(',')}}`;
}

function strings(values: readonly string[]): string {
    return `[${values.map(jsonStringText).join(',')}]`;
}

// Sorted by UTF-16 code units, as StringComparer.Ordinal sorts.
function ordinal(values: Iterable<string>): string[] {
    return [...values].toSorted((a, b) => (a < b ? -1 : a > b ? 1 : 0));
}
