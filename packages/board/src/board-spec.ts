// The Board's JSON (ADR-0340), read as ExBoard's BoardSpec reads it: every refusal names the path and the reason
// in the words C# uses, so the shared cases of tests/cases/board/ hold both readers to one text, and a .NET host
// answering an AI's tool and a browser drawing the Board refuse the same Board alike (ADR-0370 section 3).

import {
    BoardInput, BoardRefusedError, JsonNumber, JsonTextError, isJsonArray, isJsonObject, jsonMember, jsonMemberNames, jsonRawText,
    readJsonText, shapeColumn, type DataShape, type JsonObject, type JsonValue,
} from '@ex-grid/data';
import { PivotFormatError, PivotVersionError, readPivotLayoutJson, type PivotLayout } from '@ex-grid/pivot-engine';
import { readChartView } from './chart-view';
import { readGridViewSpec, type GridViewSpecReading } from './grid-view-spec';
import { members, optionalText } from './json-members';
import { elementAt } from './must';

/** A grid view (ADR-0342): its spec read against its input's declaration. */
export interface BoardGridView {
    /** What the view is. */
    readonly kind: 'grid';
    /** The view's id, by which the layout places it. */
    readonly id: string;
    /** The view's title, or empty when it gives none. */
    readonly title: string;
    /** The grid's spec. */
    readonly grid: GridViewSpecReading;
    /** The inputs the view reads: the grid's one. */
    readonly data: readonly string[];
}

/** A pivot view: the input it reads and its Pivot Layout (ADR-0059), every field it places declared by the input. */
export interface BoardPivotView {
    /** What the view is. */
    readonly kind: 'pivot';
    /** The view's id, by which the layout places it. */
    readonly id: string;
    /** The view's title, or empty when it gives none. */
    readonly title: string;
    /** The input the pivot reads. */
    readonly input: string;
    /** The pivot's layout. */
    readonly layout: PivotLayout;
    /** The inputs the view reads: the pivot's one. */
    readonly data: readonly string[];
}

/** A chart view: a Chart Spec whose tables are the Board's inputs (ADR-0320 as rewritten by ADR-0340), kept as
 * written and read against its inputs' data when the Board is drawn. */
export interface BoardChartView {
    /** What the view is. */
    readonly kind: 'chart';
    /** The view's id, by which the layout places it. */
    readonly id: string;
    /** The view's title, or empty when it gives none. */
    readonly title: string;
    /** The Chart Spec, as JSON. */
    readonly spec: string;
    /** The inputs its series read, in the order they first name them. */
    readonly data: readonly string[];
}

/** A view of a Board: a grid, a pivot or a chart over its inputs. */
export type BoardView = BoardGridView | BoardPivotView | BoardChartView;

/** A view's place in a row: its id, and its share of the row's width, a whole number from 1 to 12. */
export interface BoardCell {
    /** The view's id. */
    readonly view: string;
    /** Its share of the row's width: views of widths 2 and 1 take two thirds and one third. */
    readonly width: number;
}

/** A row of a Board's layout: its views, left to right, and the height their data is drawn at. */
export interface BoardRow {
    /** The views in the row. */
    readonly cells: readonly BoardCell[];
    /** The height, in CSS pixels, of each view's data: a grid's or a pivot's viewport, a chart's stage. A view's
     * title and a chart's toolbar stand above it. */
    readonly height: number;
}

/** A link (ADR-0340, Q5): a selection in one view narrows the views it reaches by the values of `on`. */
export interface BoardLink {
    /** The view whose selection narrows. */
    readonly from: string;
    /** The views it narrows. */
    readonly to: readonly string[];
    /** The columns compared. */
    readonly on: readonly string[];
}

/** An output a Board declares (ADR-0340, Q6): a grid's selected rows, handed on by a confirm action. */
export interface BoardOutputDeclaration {
    /** The output's name. */
    readonly name: string;
    /** The grid whose selection it hands on. */
    readonly from: string;
    /** The columns whose values it hands on. */
    readonly on: readonly string[];
    /** The confirm action's label. */
    readonly confirm: string;
}

/** A Board read (ADR-0340): its title, inputs, views, layout, links and outputs, each in the order written. */
export interface BoardReading {
    /** The Board's title, or empty when it gives none. */
    readonly title: string;
    /** The inputs, in the order the Board declares them. */
    readonly inputs: readonly BoardInput[];
    /** The views, in the order the Board lists them. */
    readonly views: readonly BoardView[];
    /** The rows the views are laid out in, top to bottom. */
    readonly layout: readonly BoardRow[];
    /** The links, in the order the Board lists them. */
    readonly links: readonly BoardLink[];
    /** The outputs, in the order the Board declares them. */
    readonly outputs: readonly BoardOutputDeclaration[];
}

/** The version of the Board this reader reads. */
export const boardVersion = 1;

/** A row's height when the layout gives none, in CSS pixels. */
export const boardDefaultRowHeight = 320;

const rootKeys = ['version', 'title', 'inputs', 'views', 'layout', 'links', 'outputs'];
const outputKeys = ['from', 'on', 'confirm'];
const linkKeys = ['from', 'to', 'on'];
const viewKeys = ['id', 'title', 'grid', 'pivot', 'chart'];
const kinds = ['grid', 'pivot', 'chart'] as const;
const pivotKeys = ['data', 'layout'];
const rowKeys = ['views', 'widths', 'height'];

type Shapes = (name: string) => DataShape | undefined;

/**
 * Reads a Board's JSON (ADR-0340). What it refuses, with a {@link @ex-grid/data#BoardRefusedError} naming the path and the reason
 * in one sentence: text that is not JSON, or a member written twice; a version missing, or other than
 * {@link boardVersion}; a member the Board does not have; an input's declaration; a view with no id, an id twice, or
 * other than one of grid, pivot and chart; a view naming an input the Board does not declare, or a column or field the
 * input does not declare; a pivot layout its reader refuses; a Chart Spec its reader refuses when read against the
 * declarations; a layout that places a view twice, leaves one out, names one that is not there, or gives widths or
 * heights that are no widths or heights; a link from a view that is not a grid or a pivot, on a field a pivot does
 * not place in its rows or columns, to a view that is not there, to its own view or one that narrows it, on a column
 * its views' inputs do not all declare of one kind; and an output from a view that is not a grid, on a column its
 * input does not declare, or with no confirm action to raise it.
 */
export function readBoard(json: string): BoardReading {
    let root: JsonValue;
    try {
        root = readJsonText(json);
    } catch (error) {
        if (!(error instanceof JsonTextError)) throw error;
        throw new BoardRefusedError('', `The Board is not JSON: ${error.message}`, { cause: error });
    }
    return readRoot(root);
}

/** The input of `board` named `name`, or `undefined`. */
export function boardInput(board: BoardReading, name: string): BoardInput | undefined {
    return board.inputs.find(input => input.name === name);
}

/** @internal The input a view of `board` reads, which the reader has checked the Board declares. */
export function declaredInput(board: BoardReading, name: string): BoardInput {
    const input = boardInput(board, name);
    if (input === undefined) throw new Error(`Internal: a view reads input '${name}', which its Board's reader let through undeclared.`);
    return input;
}

/** The view of `board` whose id is `id`, or `undefined`. */
export function boardView(board: BoardReading, id: string): BoardView | undefined {
    return board.views.find(view => view.id === id);
}

function readRoot(root: JsonValue): BoardReading {
    if (!isJsonObject(root)) throw new BoardRefusedError('', 'A Board is a JSON object.');
    // The version first: a Board of another version is told so, not about a member it may have.
    const version = jsonMember(root, 'version');
    if (!(version instanceof JsonNumber)) throw new BoardRefusedError('version', `A Board says its version; this reader reads version ${boardVersion}.`);
    if (version.int32 !== boardVersion) {
        throw new BoardRefusedError('version', `The Board is version ${version.raw}, and this reader reads version ${boardVersion} only.`);
    }
    members(root, rootKeys, '', 'The Board');
    const title = optionalText(root, 'title', 'title', "The Board's title");

    const inputsValue = jsonMember(root, 'inputs');
    if (!isJsonObject(inputsValue)) throw new BoardRefusedError('inputs', "A Board declares its inputs, by name, in 'inputs'.");
    const inputs = jsonMemberNames(inputsValue).map(name => BoardInput.read(name, member(inputsValue, name), `inputs.${name}`));
    if (inputs.length === 0) throw new BoardRefusedError('inputs', 'A Board declares at least one input.');
    const shape: Shapes = name => inputs.find(input => input.name === name)?.shape;

    const viewsValue = jsonMember(root, 'views');
    if (!isJsonArray(viewsValue) || viewsValue.length === 0) throw new BoardRefusedError('views', "A Board lists its views in 'views'.");
    const views: BoardView[] = [];
    viewsValue.forEach((view, i) => {
        const read = readView(view, `views[${i}]`, shape);
        if (views.some(v => v.id === read.id)) throw new BoardRefusedError(`views[${i}].id`, `Two views have the id '${read.id}'.`);
        views.push(read);
    });

    const layout = readLayout(root, views);
    const links = readLinks(root, views, shape);
    return Object.freeze({
        title, inputs: Object.freeze(inputs), views: Object.freeze(views), layout, links, outputs: readOutputs(root, views, shape),
    });
}

function member(object: JsonObject, name: string): JsonValue {
    const value = jsonMember(object, name);
    if (value === undefined) throw new Error(`Internal: member '${name}' is named and missing.`);
    return value;
}

function declared(shape: Shapes, name: string): DataShape {
    const found = shape(name);
    if (found === undefined) throw new Error(`Internal: input '${name}' was checked and is missing.`);
    return found;
}

function kindOf(view: BoardView): string {
    return view.kind;
}

function readOutputs(root: JsonObject, views: readonly BoardView[], shape: Shapes): readonly BoardOutputDeclaration[] {
    const outputs: BoardOutputDeclaration[] = [];
    const list = jsonMember(root, 'outputs');
    if (list === undefined) return Object.freeze(outputs);
    if (!isJsonObject(list)) throw new BoardRefusedError('outputs', "A Board declares its outputs, by name, in 'outputs'.");
    for (const name of jsonMemberNames(list)) {
        const path = `outputs.${name}`;
        const output = member(list, name);
        if (name === '') throw new BoardRefusedError('outputs', 'An output has a name.');
        if (!isJsonObject(output)) throw new BoardRefusedError(path, 'An output is an object.');
        members(output, outputKeys, path, 'An output');
        const fromId = jsonMember(output, 'from');
        if (typeof fromId !== 'string' || fromId === '') {
            throw new BoardRefusedError(`${path}.from`, "An output names the view whose selection it hands on, in 'from'.");
        }
        const from = views.find(v => v.id === fromId);
        if (from === undefined) throw new BoardRefusedError(`${path}.from`, `Output '${name}' is taken from view '${fromId}', which the Board does not have.`);
        if (from.kind !== 'grid') {
            throw new BoardRefusedError(`${path}.from`,
                `Output '${name}' is taken from view '${fromId}', a ${kindOf(from)}; an output is taken from a grid's selected rows.`);
        }
        const on = names(output, 'on', `${path}.on`, "An output names the columns whose values it hands on, in 'on'.");
        const input = declared(shape, from.grid.data);
        on.forEach((column, c) => {
            if (shapeColumn(input, column) === undefined) {
                throw new BoardRefusedError(`${path}.on[${c}]`, `Output '${name}' hands on column '${column}', which input '${from.grid.data}' does not declare.`);
            }
        });
        // Raised only by a confirm the Board declares, never by a selection on the way (Q6).
        const confirm = jsonMember(output, 'confirm');
        if (typeof confirm !== 'string' || confirm === '') {
            throw new BoardRefusedError(`${path}.confirm`, `Output '${name}' is raised by a confirm action, whose label is its 'confirm'.`);
        }
        outputs.push(Object.freeze({ name, from: fromId, on, confirm }));
    }
    return Object.freeze(outputs);
}

function readLinks(root: JsonObject, views: readonly BoardView[], shape: Shapes): readonly BoardLink[] {
    const links: BoardLink[] = [];
    const list = jsonMember(root, 'links');
    if (list === undefined) return Object.freeze(links);
    if (!isJsonArray(list)) throw new BoardRefusedError('links', "A Board lists its links in 'links'.");
    list.forEach((link, l) => {
        const path = `links[${l}]`;
        if (!isJsonObject(link)) throw new BoardRefusedError(path, 'A link is an object.');
        members(link, linkKeys, path, 'A link');
        const fromId = jsonMember(link, 'from');
        if (typeof fromId !== 'string' || fromId === '') throw new BoardRefusedError(`${path}.from`, "A link names the view whose selection narrows in 'from'.");
        const from = views.find(v => v.id === fromId);
        if (from === undefined) throw new BoardRefusedError(`${path}.from`, `The link starts at view '${fromId}', which the Board does not have.`);
        let fromData: string;
        switch (from.kind) {
            case 'grid': fromData = from.grid.data; break;
            case 'pivot': fromData = from.input; break;
            case 'chart':
                throw new BoardRefusedError(`${path}.from`,
                    `The link starts at view '${fromId}', a ${kindOf(from)}; a link starts at a grid, whose selected rows narrow, or a pivot, whose selected cells do.`);
            default: throw new Error(`Internal: ${String(from satisfies never)} is not a view.`);
        }

        const to = names(link, 'to', `${path}.to`, "A link names the views it narrows, in 'to'.");
        to.forEach((target, t) => {
            if (target === fromId) throw new BoardRefusedError(`${path}.to[${t}]`, `The link narrows its own view '${fromId}'; a view's selection narrows other views.`);
            if (!views.some(v => v.id === target)) throw new BoardRefusedError(`${path}.to[${t}]`, `The link narrows view '${target}', which the Board does not have.`);
        });

        const on = names(link, 'on', `${path}.on`, "A link names the columns it compares, in 'on'.");
        const fromShape = declared(shape, fromData);
        on.forEach((column, c) => {
            const kind = shapeColumn(fromShape, column);
            if (kind === undefined) {
                throw new BoardRefusedError(`${path}.on[${c}]`, `The link compares column '${column}', which input '${fromData}' of view '${fromId}' does not declare.`);
            }
            // A pivot's cell names the Items of its rows and columns, never of a value or a filter: a link on one
            // would narrow by nothing the cell says (ADR-0340, Q5).
            if (from.kind === 'pivot' && ![...from.layout.rows, ...from.layout.columns].some(f => f.field === column)) {
                throw new BoardRefusedError(`${path}.on[${c}]`, `The link compares field '${column}', which pivot '${fromId}' does not place in its rows or columns; `
                    + "a pivot's cell names the items of its rows and columns.");
            }
            for (const target of to) {
                const view = views.find(v => v.id === target);
                if (view === undefined) throw new Error(`Internal: view '${target}' was checked and is missing.`);
                for (const data of view.data) {
                    const other = shapeColumn(declared(shape, data), column);
                    if (other === undefined) {
                        throw new BoardRefusedError(`${path}.on[${c}]`, `The link compares column '${column}', which input '${data}' of view '${target}' does not declare.`);
                    }
                    if (other.kind !== kind.kind) {
                        throw new BoardRefusedError(`${path}.on[${c}]`, `The link compares column '${column}', a ${kind.kind} in input `
                            + `'${fromData}' and a ${other.kind} in input '${data}'; a value is compared only with one of its kind.`);
                    }
                }
            }
        });
        links.push(Object.freeze({ from: fromId, to, on }));

        // Links run one way: a view narrowed, directly or along other links, by the view it would narrow would
        // drop its own selection each time it narrowed, and never settle.
        for (const target of to) {
            if (reaches(links, target, fromId)) {
                throw new BoardRefusedError(`${path}.to`, `The link narrows view '${target}', which already narrows view '${fromId}'; links run one way.`);
            }
        }
    });
    return Object.freeze(links);
}

function reaches(links: readonly BoardLink[], from: string, to: string): boolean {
    const seen = new Set<string>();
    const next = [from];
    for (let at = next.pop(); at !== undefined; at = next.pop()) {
        if (at === to) return true;
        if (seen.has(at)) continue;
        seen.add(at);
        for (const link of links) {
            if (link.from === at) next.push(...link.to);
        }
    }
    return false;
}

function names(owner: JsonObject, name: string, path: string, missing: string): readonly string[] {
    const list = jsonMember(owner, name);
    if (!isJsonArray(list) || list.length === 0) throw new BoardRefusedError(path, missing);
    const read: string[] = [];
    list.forEach((item, i) => {
        if (typeof item !== 'string' || item === '') throw new BoardRefusedError(`${path}[${i}]`, 'A name is text.');
        if (read.includes(item)) throw new BoardRefusedError(`${path}[${i}]`, `'${item}' is named twice.`);
        read.push(item);
    });
    return Object.freeze(read);
}

function readView(view: JsonValue, path: string, shape: Shapes): BoardView {
    if (!isJsonObject(view)) throw new BoardRefusedError(path, 'A view is an object.');
    members(view, viewKeys, path, 'A view');
    const id = jsonMember(view, 'id');
    if (typeof id !== 'string' || id === '') throw new BoardRefusedError(`${path}.id`, 'A view has an id, by which the layout places it.');
    const title = optionalText(view, 'title', `${path}.title`, `View '${id}''s title`);
    const written = kinds.filter(k => jsonMember(view, k) !== undefined);
    const [kind] = written;
    if (written.length !== 1 || kind === undefined) {
        throw new BoardRefusedError(path, `View '${id}' is one of grid, pivot and chart${written.length === 0 ? '' : `, not ${written.join(' and ')}`}.`);
    }
    const at = `${path}.${kind}`;
    const spec = member(view, kind);
    switch (kind) {
        case 'grid': {
            const grid = readGridViewSpec(spec, shape, at);
            return Object.freeze({ kind, id, title, grid, data: Object.freeze([grid.data]) });
        }
        case 'pivot': return readPivot(id, title, spec, at, shape);
        case 'chart': {
            const data = readChartView(spec, at, shape);
            return Object.freeze({ kind, id, title, spec: jsonRawText(spec), data });
        }
        default: throw new Error(`Internal: ${String(kind satisfies never)} is not a kind.`);
    }
}

function readPivot(id: string, title: string, spec: JsonValue, path: string, shape: Shapes): BoardPivotView {
    if (!isJsonObject(spec)) throw new BoardRefusedError(path, "A pivot's spec is an object.");
    members(spec, pivotKeys, path, "A pivot's spec");
    const data = jsonMember(spec, 'data');
    if (typeof data !== 'string' || data === '') throw new BoardRefusedError(`${path}.data`, "A view names the input it reads in 'data'.");
    const input = shape(data);
    if (input === undefined) throw new BoardRefusedError(`${path}.data`, `The view reads input '${data}', which the Board does not declare.`);
    const layoutValue = jsonMember(spec, 'layout');
    if (!isJsonObject(layoutValue)) throw new BoardRefusedError(`${path}.layout`, "A pivot holds its Pivot Layout in 'layout'.");
    let layout: PivotLayout;
    try {
        layout = readPivotLayoutJson(jsonRawText(layoutValue));
    } catch (error) {
        if (!(error instanceof PivotFormatError || error instanceof PivotVersionError || error instanceof TypeError || error instanceof RangeError)) throw error;
        throw new BoardRefusedError(`${path}.layout`, `The pivot's layout cannot be read: ${error.message}`, { cause: error });
    }
    const placed = [...layout.filters, ...layout.rows, ...layout.columns, ...layout.values].map(f => f.field);
    for (const field of placed) {
        if (shapeColumn(input, field) === undefined) {
            throw new BoardRefusedError(`${path}.layout`, `The pivot places field '${field}', which input '${data}' does not declare.`);
        }
    }
    return Object.freeze({ kind: 'pivot', id, title, input: data, layout, data: Object.freeze([data]) });
}

function readLayout(root: JsonObject, views: readonly BoardView[]): readonly BoardRow[] {
    const layout = jsonMember(root, 'layout');
    if (!isJsonArray(layout) || layout.length === 0) throw new BoardRefusedError('layout', "A Board lays its views out in rows, in 'layout'.");
    const placed = new Set<string>();
    const rows = layout.map((row, r): BoardRow => {
        const path = `layout[${r}]`;
        if (!isJsonObject(row)) throw new BoardRefusedError(path, 'A row of the layout is an object.');
        members(row, rowKeys, path, 'A row of the layout');
        const ids = jsonMember(row, 'views');
        if (!isJsonArray(ids) || ids.length === 0) throw new BoardRefusedError(`${path}.views`, "A row lists its views, left to right, in 'views'.");
        const placedHere = ids.map((id, k) => {
            const at = `${path}.views[${k}]`;
            if (typeof id !== 'string') throw new BoardRefusedError(at, 'A row names a view by its id.');
            if (!views.some(v => v.id === id)) throw new BoardRefusedError(at, `The layout places view '${id}', which the Board does not have.`);
            if (placed.has(id)) throw new BoardRefusedError(at, `The layout places view '${id}' twice; a view stands in one place.`);
            placed.add(id);
            return id;
        });
        let widths = placedHere.map(() => 1);
        const widthsValue = jsonMember(row, 'widths');
        if (widthsValue !== undefined) {
            if (!isJsonArray(widthsValue) || widthsValue.length !== placedHere.length) {
                throw new BoardRefusedError(`${path}.widths`, `A row gives one width per view; this row has ${placedHere.length}.`);
            }
            widths = widthsValue.map((width, w) => {
                const share = width instanceof JsonNumber ? width.int32 : undefined;
                if (share === undefined || share < 1 || share > 12) {
                    throw new BoardRefusedError(`${path}.widths[${w}]`, "A width is a whole number from 1 to 12, the view's share of the row.");
                }
                return share;
            });
        }
        let height = boardDefaultRowHeight;
        const heightValue = jsonMember(row, 'height');
        if (heightValue !== undefined) {
            const written = heightValue instanceof JsonNumber ? heightValue.double : Number.NaN;
            if (!Number.isFinite(written) || written < 80 || written > 4000) {
                throw new BoardRefusedError(`${path}.height`, "A row's height is a number of CSS pixels from 80 to 4000.");
            }
            height = written;
        }
        return Object.freeze({
            cells: Object.freeze(placedHere.map((view, i) => Object.freeze({ view, width: elementAt(widths, i) }))),
            height,
        });
    });
    for (const view of views) {
        if (!placed.has(view.id)) throw new BoardRefusedError('layout', `The layout leaves out view '${view.id}'; every view stands in it once.`);
    }
    return Object.freeze(rows);
}
