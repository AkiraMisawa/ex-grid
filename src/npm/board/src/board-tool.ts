// The tool an AI calls with a Board (ADR-0340, in ADR-0320's form), as ExBoard's BoardSpec answers it: the Board read
// and bound to the tables its Data Handles name, then said in words, or refused with one sentence naming the path and
// the reason, in the words C# uses. And a Chart Spec as a Board of one view, as draw_chart draws it while an AI
// composes. A host in TypeScript answers the model as a .NET host does (ADR-0370 section 3).

import { buildScene, chartSummary, ChartRefusedError, ChartSpecRefusedError, readChartSpec, type ChartScene } from '@ex-grid/chart';
import {
    BoardRefusedError, isJsonObject, jsonMember, jsonMemberNames, jsonRawText, jsonStringText, JsonTextError, readJsonText,
    type DataShape, type JsonValue, type Snapshot,
} from '@ex-grid/data';
import { aggregationTitle } from '@ex-grid/pivot-engine';
import { bindBoard, type BindBoardOptions, type BoundBoard, type BoundView } from './bound-board';
import { boardInput, boardVersion, readBoard, type BoardReading } from './board-spec';
import { declarationOf, surfaceRefusal } from './chart-view';
import { members } from './json-members';
import { defined, unreachable } from './must';

/** The tool an AI calls with a Board, as a host hands it to a model: the counterpart of C#'s `BoardSpecTool`. */
export interface BoardSpecTool {
    /** The tool's name. */
    readonly name: string;
    /** What it does, for the model. */
    readonly description: string;
    /** The JSON Schema of its input. */
    readonly inputSchema: string;
}

/** What the tool answers the AI: the counterpart of C#'s `BoardSpecAnswer`. */
export type BoardSpecAnswer =
    | {
        /** The Board can be drawn. */
        readonly accepted: true;
        /** The Board said in words, to check against what was meant. */
        readonly text: string;
        /** The Board read: what a workflow saves and runs again. */
        readonly board: BoardReading;
        /** The Board bound to the tables its Data Handles name: what the host draws now. */
        readonly bound: BoundBoard;
    }
    | {
        /** The Board cannot be drawn. */
        readonly accepted: false;
        /** The refusal: one sentence naming the path and the reason, for the AI to act on. */
        readonly text: string;
        /** No reading: nothing was drawn. */
        readonly board: undefined;
        /** Nothing bound. */
        readonly bound: undefined;
    };

/** The id of the one view of a Board {@link boardOfChart} makes. */
export const boardChartViewId = 'chart';

const chartPath = 'views[0].chart';

/**
 * What the AI is told of a source while it composes (ADR-0340): its shape without its rows, in the form a Board
 * declares an input, `{"columns":[{"name":"desk","type":"text"},…]}`. The counterpart of C#'s `BoardSpec.DeclarationOf`.
 */
export function boardDeclarationOf(shape: DataShape): string {
    return `{"columns":[${shape.columns.map(column => `{"name":${jsonStringText(column.name)},"type":${jsonStringText(column.kind)}}`).join(',')}]}`;
}

/**
 * A Chart Spec as a Board of one view (ADR-0320 as rewritten by ADR-0340, Q4), as `draw_chart` draws it while an AI
 * composes: each Data Handle the spec names becomes an input declared with the shape `tables` gives for it, and the
 * spec is the Board's one view, {@link boardChartViewId}. Bind it with each handle's Snapshot under the handle's name.
 * The spec is read first as the tool reads it, so a handle `tables` does not know, or a spec the tool refuses, is
 * refused with a {@link @ex-grid/data#BoardRefusedError} in the spec's own path. The counterpart of C#'s `BoardSpec.OfChart`.
 */
export function boardOfChart(chartSpec: string, tables: (handle: string) => DataShape | undefined): BoardReading {
    let root: JsonValue | undefined;
    try {
        root = readJsonText(chartSpec);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- not JSON: the Chart Spec's reader says so below, in its own words, as C#'s
        if (!(error instanceof JsonTextError)) throw error;
    }
    if (isJsonObject(root) && jsonMember(root, 'surface') !== undefined) throw new BoardRefusedError('surface', surfaceRefusal);
    // Each handle the spec names, in the order first named, with the shape the host gave for it then.
    const named = new Map<string, DataShape>();
    try {
        readChartSpec(chartSpec, handle => {
            const shape = tables(handle);
            if (shape === undefined) return undefined;
            if (!named.has(handle)) named.set(handle, shape);
            return declarationOf(shape);
        });
    } catch (error) {
        if (!(error instanceof ChartSpecRefusedError)) throw error;
        throw new BoardRefusedError(error.path, error.message, { cause: error });
    }
    const inputs = [...named].map(([handle, shape]) => `${jsonStringText(handle)}:${boardDeclarationOf(shape)}`);
    const id = jsonStringText(boardChartViewId);
    const json = `{"version":${String(boardVersion)},"inputs":{${inputs.join(',')}},"views":[{"id":${id},"chart":${chartSpec}}],"layout":[{"views":[${id}]}]}`;
    try {
        return readBoard(json);
    } catch (error) {
        if (!(error instanceof BoardRefusedError)) throw error;
        // Anything else is in the inputs made from the host's tables, which the spec does not write, or the spec nested
        // past the reader's depth once it stands three levels down in the Board: said as C#'s OfChart says it.
        if (error.path.startsWith(chartPath)) throw new BoardRefusedError(error.path.slice(chartPath.length).replace(/^\.+/, ''), error.message, { cause: error });
        throw new BoardRefusedError('', `A table the spec names cannot be a Board Input: ${error.message}`, { cause: error });
    }
}

/**
 * Reads the tool's input, `{"board": {…}, "data": {"trades": "h-7f3a"}}`, and answers as the tool answers the AI: the
 * refusal, or the Board said in words, bound to the tables `tables` holds for its Data Handles (`undefined` for a
 * handle the host does not know), its grids' text written in `locale`. Anything thrown but a refusal is thrown on.
 * The counterpart of C#'s `BoardSpec.Answer`.
 */
export function answerBoardSpec(json: string, tables: (handle: string) => Snapshot | undefined, options: Pick<BindBoardOptions, 'locale'> = {}): BoardSpecAnswer {
    try {
        const { board, data } = readCall(json, tables);
        let bound: BoundBoard;
        try {
            bound = bindBoard(board, data, options);
        } catch (error) {
            if (!(error instanceof BoardRefusedError)) throw error;
            // A table whose shape is not its input's is said of the binding the AI wrote; anything else the binding
            // refuses, of the Board.
            const input = error.path.startsWith('inputs.') ? error.path.slice('inputs.'.length) : error.path;
            if (boardInput(board, input) !== undefined) throw new BoardRefusedError(`data.${input}`, error.message, { cause: error });
            throw new BoardRefusedError(error.path === '' ? 'board' : `board.${error.path}`, error.message, { cause: error });
        }
        return { accepted: true, text: sayBoard(bound), board, bound };
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- ADR-0340: a refused Board is the tool's answer, said to the AI; anything else is rethrown
        if (!(error instanceof BoardRefusedError)) throw error;
        return { accepted: false, text: error.path === '' ? error.message : `${error.path}: ${error.message}`, board: undefined, bound: undefined };
    }
}

function readCall(json: string, tables: (handle: string) => Snapshot | undefined): { board: BoardReading; data: Readonly<Record<string, Snapshot>> } {
    let root: JsonValue;
    try {
        root = readJsonText(json);
    } catch (error) {
        if (!(error instanceof JsonTextError)) throw error;
        throw new BoardRefusedError('', `The call is not JSON: ${error.message}`, { cause: error });
    }
    if (!isJsonObject(root)) throw new BoardRefusedError('', "The call is a JSON object of 'board' and 'data'.");
    members(root, ['board', 'data'], '', 'The call');
    const boardValue = jsonMember(root, 'board');
    if (boardValue === undefined) throw new BoardRefusedError('board', "The call holds the Board in 'board'.");
    let board: BoardReading;
    try {
        board = readBoard(jsonRawText(boardValue));
    } catch (error) {
        if (!(error instanceof BoardRefusedError)) throw error;
        throw new BoardRefusedError(error.path === '' ? 'board' : `board.${error.path}`, error.message, { cause: error });
    }
    const dataValue = jsonMember(root, 'data');
    if (!isJsonObject(dataValue)) throw new BoardRefusedError('data', "The call binds each input to a Data Handle in 'data'.");
    // A Map, then own properties: an input may be named `__proto__`, which an assignment would take for the prototype.
    const data = new Map<string, Snapshot>();
    for (const name of jsonMemberNames(dataValue)) {
        if (boardInput(board, name) === undefined) throw new BoardRefusedError(`data.${name}`, `The call binds input '${name}', which the Board does not declare.`);
        const handle = jsonMember(dataValue, name);
        if (typeof handle !== 'string' || handle === '') throw new BoardRefusedError(`data.${name}`, `Input '${name}' is bound to a Data Handle, a string.`);
        const table = tables(handle);
        if (table === undefined) throw new BoardRefusedError(`data.${name}`, `"${handle}" is not known: name a Data Handle a capability returned.`);
        data.set(name, table);
    }
    for (const input of board.inputs) {
        if (!data.has(input.name)) throw new BoardRefusedError('data', `Input '${input.name}' is bound to no Data Handle.`);
    }
    return { board, data: Object.fromEntries(data) };
}

/**
 * The Board said in words, as the tool answers an accepted one: its views in the layout's order, each with what it
 * shows, then its links and its outputs. The counterpart of C#'s `BoardSpec.Say`.
 */
export function sayBoard(bound: BoundBoard): string {
    const reading = bound.board;
    const cells = reading.layout.flatMap(row => row.cells);
    let text = `${reading.title === '' ? 'The Board' : `Board "${reading.title}"`}: ${count(cells.length, 'view')} in ${count(reading.layout.length, 'row')}.`;
    for (const cell of cells) text += ` ${sayView(bound.view(cell.view))}`;
    for (const link of reading.links) text += ` A selection in ${link.from} narrows ${and(link.to)} on ${and(link.on)}.`;
    for (const output of reading.outputs) text += ` "${output.confirm}" hands on ${and(output.on)} of the rows selected in ${output.from}, as output ${output.name}.`;
    if (reading.links.length === 0 && reading.outputs.length === 0) text += ' No view narrows another, and nothing is handed on.';
    return text;
}

function sayView(view: BoundView): string {
    const { id, title } = view.view;
    const named = title === '' ? id : `${id} ("${title}")`;
    switch (view.kind) {
        case 'grid': {
            const rows = view.snapshot === undefined ? 'rows fetched' : count(view.snapshot.rowCount, 'row');
            const headers = view.columns.map(column => defined(column.header, `the header of column '${column.name}'`));
            return `${named} is a grid of ${rows} over ${view.view.grid.data}, showing ${and(headers)}.`;
        }
        case 'pivot': {
            const layout = view.layout;
            const parts: string[] = [];
            if (layout.rows.length > 0) parts.push(`rows ${and(layout.rows.map(f => f.field))}`);
            if (layout.columns.length > 0) parts.push(`columns ${and(layout.columns.map(f => f.field))}`);
            if (layout.filters.length > 0) parts.push(`filtered by ${and(layout.filters.map(f => f.field))}`);
            parts.push(layout.values.length === 0 ? 'no values' : `values ${and(layout.values.map(v => `${aggregationTitle(v.aggregation)} of ${v.field}`))}`);
            const rows = view.snapshot === undefined ? 'a report' : count(view.snapshot.rowCount, 'row');
            return `${named} is a pivot of ${rows} of ${view.view.input}: ${parts.join('; ')}.`;
        }
        case 'chart': {
            const reading = view.reading;
            let scene: ChartScene;
            try {
                scene = buildScene(reading.definition, { width: 800, height: 400 });
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- ADR-0340: a chart its definition refuses is said in the answer, as C#'s; anything else is rethrown
                if (!(error instanceof ChartRefusedError)) throw error;
                return `${named} is a chart that cannot be drawn: ${error.message}`;
            }
            return `${named} is a chart. ${chartSummary(reading.definition, scene, reading.title)}`;
        }
        default:
            return unreachable(view);
    }
}

// A count as C#'s N0 writes it under the invariant culture, a comma between each three digits, and its noun.
function count(value: number, noun: string): string {
    if (!Number.isSafeInteger(value) || value < 0) throw new RangeError(`${String(value)} ${noun}s is not a count.`);
    return `${String(value).replace(/\B(?=(\d{3})+(?!\d))/g, ',')} ${noun}${value === 1 ? '' : 's'}`;
}

function and(items: readonly string[]): string {
    if (items.length === 0) return 'nothing';
    if (items.length === 1) return defined(items[0], 'the one item');
    return `${items.slice(0, -1).join(', ')} and ${defined(items.at(-1), 'the last item')}`;
}

/** The tool an AI calls to draw a Board: its name, what it does, and the JSON Schema of its input, which is the Board
 * (`board`) and the Data Handle each of its inputs is bound to now (`data`). The counterpart of C#'s `BoardSpec.Tool`. */
export const boardSpecTool: BoardSpecTool = Object.freeze({
    name: 'draw_board',
    description: 'Draws a Board: grids, pivots and charts laid out in rows over data capabilities already made, with links that '
        + 'narrow one view by what is selected in another, and outputs the user hands on with a confirm. Declare each input\'s '
        + 'columns and types as the capability said its shape, bind each input to the Data Handle the capability returned in '
        + '\'data\', and never write the values themselves. The Board, not the handles, is what is saved and run again without '
        + 'you. A Board that cannot be drawn is refused with the path and the reason; an accepted one is answered with the '
        + 'Board said in words, to check against what was meant.',
    inputSchema: String.raw`{
  "type": "object",
  "required": ["board", "data"],
  "additionalProperties": false,
  "properties": {
    "board": {
      "type": "object",
      "description": "The Board, as it is saved and run again.",
      "required": ["version", "inputs", "views", "layout"],
      "additionalProperties": false,
      "properties": {
        "version": { "const": 1 },
        "title": { "type": "string" },
        "inputs": {
          "type": "object",
          "description": "Each input by name, its columns declared as the capability said its shape.",
          "additionalProperties": {
            "type": "object", "required": ["columns"], "additionalProperties": false,
            "properties": { "columns": { "type": "array", "minItems": 1, "items": {
              "type": "object", "required": ["name", "type"], "additionalProperties": false,
              "properties": { "name": { "type": "string" }, "type": { "enum": ["text", "decimal", "double", "integer", "date", "boolean"] } } } } }
          }
        },
        "views": {
          "type": "array", "minItems": 1,
          "items": {
            "type": "object", "required": ["id"], "additionalProperties": false,
            "description": "One of grid, pivot and chart.",
            "properties": {
              "id": { "type": "string" },
              "title": { "type": "string" },
              "grid": {
                "type": "object", "required": ["data", "columns"], "additionalProperties": false,
                "properties": {
                  "data": { "type": "string", "description": "The input it shows." },
                  "columns": { "type": "array", "minItems": 1, "items": {
                    "type": "object", "required": ["column"], "additionalProperties": false,
                    "properties": {
                      "column": { "type": "string" }, "header": { "type": "string" },
                      "format": { "type": "string", "description": "An Excel format code; a decimal or double column always gives one, as its places are yours to choose." },
                      "width": { "type": "number" }, "pinned": { "type": "boolean" } } } },
                  "sorts": { "type": "array", "items": { "type": "object", "properties": { "column": { "type": "string" }, "direction": { "enum": ["Ascending", "Descending"] } } } },
                  "filter": { "type": "object", "description": "A grid filter, in the View State's JSON." }
                }
              },
              "pivot": {
                "type": "object", "required": ["data", "layout"], "additionalProperties": false,
                "properties": {
                  "data": { "type": "string", "description": "The input it pivots." },
                  "layout": { "type": "object", "description": "A pivot layout: {\"version\": 1, \"rows\": [{\"field\": …}], \"columns\": […], \"filters\": […], \"values\": [{\"field\": …, \"aggregation\": \"sum\"}]}." }
                }
              },
              "chart": { "type": "object", "description": "A Chart Spec, as draw_chart takes it, each series' data naming an input." }
            }
          }
        },
        "layout": {
          "type": "array", "minItems": 1, "description": "Rows of views, each view placed once.",
          "items": { "type": "object", "required": ["views"], "additionalProperties": false,
            "properties": { "views": { "type": "array", "items": { "type": "string" } }, "widths": { "type": "array", "items": { "type": "integer", "minimum": 1 } }, "height": { "type": "number" } } }
        },
        "links": {
          "type": "array", "description": "A selection in a grid's rows or a pivot's cells narrows other views to the rows whose values in 'on' it selects.",
          "items": { "type": "object", "required": ["from", "to", "on"], "additionalProperties": false,
            "properties": { "from": { "type": "string" }, "to": { "type": "array", "items": { "type": "string" } }, "on": { "type": "array", "items": { "type": "string" } } } }
        },
        "outputs": {
          "type": "object", "description": "What the user hands on from a grid's selected rows, only when they press its confirm.",
          "additionalProperties": { "type": "object", "required": ["from", "on", "confirm"], "additionalProperties": false,
            "properties": { "from": { "type": "string" }, "on": { "type": "array", "items": { "type": "string" } }, "confirm": { "type": "string" } } }
        }
      }
    },
    "data": {
      "type": "object", "description": "The Data Handle each input is bound to now, by the input's name.",
      "additionalProperties": { "type": "string" }
    }
  }
}`,
});
