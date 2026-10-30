// A Board's chart view (ADR-0320 as rewritten by ADR-0340, Q4), read as ExBoard's BoardSpec reads it: the inputs its
// series name are checked in the Board's words, and the rest is the Chart Spec's reader's, read against the
// declarations alone so that a column an input does not declare is refused with the rest of the Board.

import { BoardRefusedError, isJsonArray, isJsonObject, jsonMember, jsonRawText, type DataShape, type JsonObject, type JsonValue } from '@ex-grid/data';
import { ChartSpecRefusedError, readChartSpec, type ChartData } from '@ex-grid/chart';

/** Said where a Board's chart is a Chart Spec of a 3D surface. */
export const surfaceRefusal =
    "A Board's chart view draws series; a Chart Spec of a 3D surface is drawn on its own, by ExSurface, and is not yet a view of a Board.";

/** Reads a chart view's spec standing at `path`, and returns the inputs its series read, in the order first named. */
export function readChartView(spec: JsonValue, path: string, shape: (name: string) => DataShape | undefined): readonly string[] {
    if (!isJsonObject(spec)) throw new BoardRefusedError(path, 'A chart is a Chart Spec, an object.');
    // A Board draws a chart of series; a spec of a 3D surface is drawn by ExSurface on its own.
    if (jsonMember(spec, 'surface') !== undefined) throw new BoardRefusedError(`${path}.surface`, surfaceRefusal);
    const data: string[] = [];
    const series = jsonMember(spec, 'series');
    if (isJsonArray(series)) {
        series.forEach((one, i) => {
            const at = `${path}.series[${i}]`;
            if (!isJsonObject(one)) return;
            checkData(one, at, shape, data);
            const edges = jsonMember(one, 'edges');
            if (isJsonObject(edges)) checkData(edges, `${at}.edges`, shape, data);
        });
    }
    try {
        readChartSpec(jsonRawText(spec), name => {
            const declared = shape(name);
            return declared === undefined ? undefined : declarationOf(declared);
        });
    } catch (error) {
        if (!(error instanceof ChartSpecRefusedError)) throw error;
        throw new BoardRefusedError(error.path === '' ? path : `${path}.${error.path}`, error.message, { cause: error });
    }
    return Object.freeze(data);
}

/** An input's declared columns as a table of no rows. */
export function declarationOf(shape: DataShape): ChartData {
    return Object.freeze({
        columns: Object.freeze(shape.columns.map(column => column.name)),
        rowCount: 0,
        value(_column: string, row: number): unknown {
            throw new RangeError(`Row ${row}: a declaration holds no rows.`);
        },
    });
}

function checkData(owner: JsonObject, path: string, shape: (name: string) => DataShape | undefined, data: string[]): void {
    const name = jsonMember(owner, 'data');
    if (typeof name !== 'string') return;
    if (shape(name) === undefined) throw new BoardRefusedError(`${path}.data`, `The chart reads input '${name}', which the Board does not declare.`);
    if (!data.includes(name)) data.push(name);
}
