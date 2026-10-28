// The Board Sources a Blazor ExBoard hands its Script Core (ADR-0343, ADR-0344, ADR-0370 section 1): the Consumer's C#
// BoardSource, seen from the browser through .NET. Each question crosses as JSON the adapter's C# reads, and each
// answer comes back in the form its data already travels in: a page of rows as the Arrow stream a Snapshot crosses as
// (ADR-0065), in bytes; a shape and a chart's points as JSON whose values are read as the input declares them, as a
// Board Input reads its rows. Neither side reads the other's objects, and nothing is decided here: the Board asks, and
// the Consumer's C# answers.

import type { BoardChartQuery, BoardQuery, BoardSource, BoardValue, LinkCondition, ShapedPage } from '@ex-grid/board';
import type { ChartData, ChartSourceAnswer } from '@ex-grid/chart';
import { plainDateTimeOf, writeGridQueryJson } from '@ex-grid/core';
import { BoardInput, boardKindNames, decimalText, readArrowSnapshot, ticksText, type DataShape, type Snapshot, type SnapshotColumn, type SnapshotKind } from '@ex-grid/data';

/** What a source asks of the .NET object a Blazor ExBoard hands the bridge: Blazor's `DotNetObjectReference`. */
export interface DotNetCaller {
    invokeMethodAsync(method: string, ...args: readonly unknown[]): Promise<unknown>;
}

/** What .NET says of a source it sends: the number it is asked under, and whether it thins a chart's points. */
export interface WireSource {
    /** The number .NET holds the source under for this input; what it is asked names it. */
    readonly number: number;
    /** Whether the C# source answers a chart's points (`BoardSource.Thin`). */
    readonly thin: boolean;
}

let calls = 0;

/**
 * The C# source bound to an input, asked through .NET by its number. .NET numbers a source anew under each Board's
 * declaration of the input, so `declared` is the one it holds the number under: what the source is asked carries the
 * narrowing the Board's links put on the view, each value written as `declared` declares its column, and what it
 * answers is read as `declared` declares the columns it holds. `cancel` tells .NET a question no longer waits, and
 * tells it nothing once the Board is stopped; a question too long for a message of a circuit is held by `dotnet`, the
 * bridge's crossing calls.
 */
export function dotNetBoardSource(dotnet: DotNetCaller, cancel: (call: number) => void, declared: BoardInput, wire: WireSource): BoardSource {
    const { number } = wire;
    // One question, with its call's number, so an abort tells .NET which question no longer waits, and what it asks,
    // where it asks anything but its call.
    const ask = async (method: string, json: string | undefined, signal: AbortSignal | undefined): Promise<unknown> => {
        signal?.throwIfAborted();
        const call = ++calls;
        const aborted = (): void => { cancel(call); };
        signal?.addEventListener('abort', aborted, { once: true });
        try {
            return await (json === undefined
                ? dotnet.invokeMethodAsync(method, number, call)
                : dotnet.invokeMethodAsync(method, number, call, json));
        } finally {
            signal?.removeEventListener('abort', aborted);
        }
    };
    const source: BoardSource = {
        shape: async signal => shapeOf(await ask('Shape', undefined, signal), declared.name),
        fetch: async (query, signal) => pageOf(await ask('Fetch', fetchJson(query, declared), signal), declared.name),
        ...(wire.thin ? { thin: async (query: BoardChartQuery, signal: AbortSignal) => answerOf(await ask('Thin', thinJson(query, declared), signal), query, declared) } : {}),
    };
    return Object.freeze(source);
}

function fetchJson(query: BoardQuery, input: BoardInput): string {
    return `{"view":${JSON.stringify(query.view)},"grid":${JSON.stringify(writeGridQueryJson(query.grid))},"narrowing":${narrowingOf(query.narrowing, input)}}`;
}

function thinJson(query: BoardChartQuery, input: BoardInput): string {
    const { chart } = query;
    const at = { data: chart.data, x: chart.x, y: chart.y, values: chart.values, from: chart.from, to: chart.to, pixels: chart.pixels };
    return `{"view":${JSON.stringify(query.view)},"chart":${JSON.stringify(at)},"narrowing":${narrowingOf(query.narrowing, input)}}`;
}

// Each condition as its columns and its tuples, every value written as the input declares its column: the form an
// output's values take (BoardOutput.toJson), which C# reads back as a Board Input reads a row.
function narrowingOf(narrowing: readonly LinkCondition[], input: BoardInput): string {
    const conditions = narrowing.map(condition => {
        const kinds = condition.on.map(name => declaredKind(input, name));
        const tuples = condition.values.map(tuple => `[${tuple.map((value, i) => written(value, kinds[i])).join(',')}]`);
        return `{"on":${JSON.stringify(condition.on)},"values":[${tuples.join(',')}]}`;
    });
    return `[${conditions.join(',')}]`;
}

function declaredKind(input: BoardInput, name: string): SnapshotKind {
    const column = input.columns.find(c => c.name === name);
    if (column === undefined) throw new Error(`Internal: a link narrows input '${input.name}' on column '${name}', which it does not declare.`);
    return column.kind;
}

// A value as JSON text, as BoardOutput.toJson writes one: a Decimal and an Integer as their exact numbers, a double
// that is no number as .NET writes it, and a Date as `yyyy-MM-dd`, or with its time where it has one.
function written(value: BoardValue, kind: SnapshotKind | undefined): string {
    if (value === null) return 'null';
    switch (typeof value) {
        case 'string': return JSON.stringify(value);
        case 'boolean': return value ? 'true' : 'false';
        case 'number': {
            if (Number.isFinite(value)) return JSON.stringify(value);
            // JSON has no NaN or infinity: written as .NET writes them, never as a number.
            return Number.isNaN(value) ? '"NaN"' : value > 0 ? '"Infinity"' : '"-Infinity"';
        }
        case 'bigint': {
            if (kind !== 'date') return value.toString();
            const day = ticksText(value);
            return JSON.stringify(day.endsWith('T00:00:00') ? day.slice(0, -'T00:00:00'.length) : day);
        }
        case 'object': return decimalText(value);
        case 'function':
        case 'symbol':
        case 'undefined': throw new TypeError(`A ${typeof value} is no value a Snapshot holds.`);
        default: throw new Error(`Internal: typeof gave a type the Board does not know (${typeof value}).`);
    }
}

function shapeOf(answer: unknown, input: string): DataShape {
    const json = text(answer, 'Shape');
    const read: unknown = JSON.parse(json);
    if (!isRecord(read) || !Array.isArray(read['columns'])) throw new TypeError(`Internal: .NET answered the shape of input '${input}' with something other than its columns.`);
    const columns = read['columns'].map((column: unknown) => {
        if (!isRecord(column) || typeof column['name'] !== 'string' || typeof column['type'] !== 'string') {
            throw new TypeError(`Internal: .NET answered the shape of input '${input}' with a column that has no name or type.`);
        }
        const kind = kindOf(column['type']);
        return Object.freeze({ name: column['name'], kind });
    });
    return Object.freeze({ columns: Object.freeze(columns) });
}

function kindOf(type: string): SnapshotKind {
    const kind = boardKindNames.find(name => name === type);
    if (kind === undefined) throw new TypeError(`Internal: .NET answered a column of the type '${type}', which no Snapshot holds.`);
    return kind;
}

async function pageOf(answer: unknown, input: string): Promise<ShapedPage> {
    // The rows' Arrow stream comes as the bytes Blazor hands over for a .NET byte[].
    if (!isRecord(answer) || typeof answer['start'] !== 'number' || typeof answer['totalCount'] !== 'number' || !(answer['rows'] instanceof Uint8Array)) {
        throw new TypeError(`Internal: .NET answered a fetch of input '${input}' with something other than a page.`);
    }
    const rows = await readArrowSnapshot(answer['rows']);
    return Object.freeze({ rows, start: answer['start'], totalCount: answer['totalCount'] });
}

function answerOf(answer: unknown, query: BoardChartQuery, input: BoardInput): ChartSourceAnswer {
    const json: unknown = JSON.parse(text(answer, 'Thin'));
    if (!isRecord(json) || typeof json['version'] !== 'string' || typeof json['points'] !== 'string' || !Array.isArray(json['thinned'])) {
        throw new TypeError(`Internal: .NET answered the points of chart '${query.view}' with something other than an answer.`);
    }
    const { x, y } = query.chart;
    const columns = [x, y].filter((name, i, all) => all.indexOf(name) === i);
    // The points as rows of the query's columns, as the JSON text .NET wrote them, read as the input declares them, as
    // a Board Input reads its rows: never through a double on the way.
    const points = new BoardInput(input.name, columns.map(name => ({ name, kind: declaredKind(input, name) }))).fromJson(json['points']);
    const thinned = json['thinned'].map((pixel: unknown) => {
        if (!Array.isArray(pixel) || pixel.length !== 5 || !pixel.every(n => typeof n === 'number')) {
            throw new TypeError(`Internal: .NET answered chart '${query.view}' with a thinned pixel that is not five numbers.`);
        }
        const [from, to, min, max, count] = pixel as [number, number, number, number, number]; // oxlint-disable-line typescript/no-unsafe-type-assertion -- checked just above: five numbers
        return Object.freeze({ from, to, min, max, count });
    });
    return Object.freeze({ version: BigInt(json['version']), points: chartData(points), thinned: Object.freeze(thinned) });
}

// A chart's table over the points read: a Date as a PlainDateTime, which ExChart recognises, every other value as the
// Snapshot hands it out.
function chartData(points: Snapshot): ChartData {
    const columns = Object.freeze(points.columns.map(column => column.name));
    const held = new Map<string, SnapshotColumn>(points.columns.map(column => [column.name, column]));
    return Object.freeze({
        columns,
        rowCount: points.rowCount,
        value(name: string, row: number): unknown {
            const column = held.get(name);
            if (column === undefined) throw new RangeError(`The points answered hold no column '${name}'.`);
            if (column.kind !== 'date') return column.valueAt(row);
            const ticks = column.valueAt(row);
            return ticks === null ? null : plainDateTimeOf(ticks);
        },
    });
}

function text(answer: unknown, method: string): string {
    if (typeof answer !== 'string') throw new TypeError(`Internal: .NET answered ${method} with something other than JSON text.`);
    return answer;
}

function isRecord(value: unknown): value is Record<string, unknown> {
    return typeof value === 'object' && value !== null;
}
