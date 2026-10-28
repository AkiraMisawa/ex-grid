// The draw_board tool and a Chart Spec as a Board (ADR-0340, in ADR-0320's form), as ExBoard.Tests' BoardToolTests
// and BoardChartSpecTests hold C#'s BoardSpec to them, in the same words.
import { describe, expect, it } from 'vitest';
import { answerChartSpec } from '@ex-grid/chart';
import { BoardInput, BoardRefusedError, createSnapshot, decimalFromText, readJsonText, isJsonObject, jsonMember, jsonMemberNames, ticksFromText, type DataShape, type JsonValue, type Snapshot } from '@ex-grid/data';
import { answerBoardSpec, bindBoard, boardChartViewId, boardDeclarationOf, boardInput, boardOfChart, boardSpecTool } from '../src/index';
import { declaredChartData } from '../src/snapshot-values';

const board = `{"version": 1, "title": "Desk P&L",
 "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "day", "type": "date"}, {"name": "pnl", "type": "decimal"}]}},
 "views": [
   {"id": "blotter", "title": "Trades", "grid": {"data": "trades", "columns": [{"column": "day", "header": "Day"}, {"column": "desk"}, {"column": "pnl", "header": "P&L", "format": "#,##0"}]}},
   {"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}},
   {"id": "daily", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "observation", "data": "trades", "x": "day", "y": "pnl"}]}}],
 "layout": [{"views": ["blotter", "byDesk"]}, {"views": ["daily"]}],
 "links": [{"from": "blotter", "to": ["byDesk", "daily"], "on": ["desk"]}],
 "outputs": {"review": {"from": "blotter", "on": ["day"], "confirm": "Send to review"}}}`;

const days = ['2026-10-19', '2026-10-20', '2026-10-21'].map(ticksFromText);

function trades(pnlAsDouble = false): Snapshot {
    return createSnapshot({
        columns: [
            { name: 'desk', kind: 'text', values: ['Rates', 'FX', 'Rates'] },
            { name: 'day', kind: 'date', values: days },
            pnlAsDouble ? { name: 'pnl', kind: 'double', values: [1, 2, 3] } : { name: 'pnl', kind: 'decimal', values: ['100', '-50', '75'].map(decimalFromText) },
        ],
    });
}

const call = (data = '{"trades": "h-1"}', spec = board): string => `{"board": ${spec}, "data": ${data}}`;

const handles = (handle: string): Snapshot | undefined => handle === 'h-1' ? trades() : handle === 'h-2' ? trades(true) : undefined;

function object(value: JsonValue | undefined, name: string) {
    if (!isJsonObject(value)) throw new Error(`'${name}' is not an object.`);
    const member = jsonMember(value, name);
    if (!isJsonObject(member)) throw new Error(`'${name}' is not an object.`);
    return member;
}

describe('the draw_board tool (ADR-0340, in ADR-0320\'s form)', () => {
    it('is named, and its schema names the Board\'s members', () => {
        expect(boardSpecTool.name).toBe('draw_board');
        const members = object(object(readJsonText(boardSpecTool.inputSchema), 'properties'), 'board');
        const properties = object(members, 'properties');
        expect(jsonMemberNames(properties)).toEqual(['version', 'title', 'inputs', 'views', 'layout', 'links', 'outputs']);
        const type = object(object(object(object(object(object(properties, 'inputs'), 'additionalProperties'), 'properties'), 'columns'), 'items'), 'properties');
        expect(jsonMember(object(type, 'type'), 'enum')).toEqual(['text', 'decimal', 'double', 'integer', 'date', 'boolean']);
    });

    it('tells the AI a source\'s shape as the declaration a Board writes, without its rows', () => {
        const shape: DataShape = { columns: trades().columns.map(({ name, kind }) => ({ name, kind })) };
        const declaration = boardDeclarationOf(shape);

        expect(declaration).toBe('{"columns":[{"name":"desk","type":"text"},{"name":"day","type":"date"},{"name":"pnl","type":"decimal"}]}');
        expect(BoardInput.read('trades', readJsonText(declaration), 'inputs.trades').columns).toEqual(shape.columns);
    });

    it('says an accepted Board in words, view by view in the layout\'s order, then its links and outputs', () => {
        const answer = answerBoardSpec(call(), handles, { locale: 'en-US' });

        if (!answer.accepted) throw new Error(answer.text);
        expect(answer.text.startsWith('Board "Desk P&L": 3 views in 2 rows. blotter ("Trades") is a grid of 3 rows over trades, showing Day, desk and P&L. '
            + 'byDesk is a pivot of 3 rows of trades: rows desk; values Sum of pnl. daily is a chart. ')).toBe(true);
        expect(answer.text.endsWith(' A selection in blotter narrows byDesk and daily on desk. "Send to review" hands on day of the rows selected in blotter, as output review.'))
            .toBe(true);
        expect(answer.board.inputs.map(input => input.name)).toEqual(['trades']);
        expect(answer.bound.data.get('trades')?.rowCount).toBe(3);
    });

    it.each([
        [call(undefined, board.replace('"format": "#,##0"', '"width": 90')), 'board.views[0].grid.columns[2].format: '],
        [call('{"trades": "h-9"}'), 'data.trades: "h-9" is not known: name a Data Handle a capability returned.'],
        [call('{}'), 'data: Input \'trades\' is bound to no Data Handle.'],
        [call('{"trades": "h-1", "quotes": "h-1"}'), 'data.quotes: The call binds input \'quotes\', which the Board does not declare.'],
        [call('{"trades": "h-2"}'), 'data.trades: Input \'trades\', column \'pnl\': declared decimal, and its source holds double.'],
        [`{"board": ${board}}`, 'data: The call binds each input to a Data Handle in \'data\'.'],
        ['{"board": {"version": 1}, "data": {}, "when": 1}', 'when: The call has no member \'when\'.'],
        ['[]', 'The call is a JSON object of \'board\' and \'data\'.'],
    ])('refuses a Board that cannot be drawn by path, in one sentence (%#)', (text, said) => {
        const answer = answerBoardSpec(text, handles);

        expect(answer.accepted).toBe(false);
        expect(answer.text.startsWith(said)).toBe(true);
        expect(answer.board).toBeUndefined();
    });
});

const spec = `{"version": 1, "title": "P&L", "axis": {"values": "dates"},
 "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "h-trades", "x": "day", "y": "pnl"}]}`;

function held(): Snapshot {
    return createSnapshot({
        version: 3,
        columns: [
            { name: 'day', kind: 'date', values: days },
            { name: 'pnl', kind: 'decimal', values: ['100', '-50', '75'].map(decimalFromText) },
            { name: 'desk', kind: 'text', values: ['Rates', 'FX', 'Rates'] },
        ],
    });
}

const limits: DataShape = { columns: [{ name: 'limit', kind: 'decimal' }] };
const tables = (snapshot: Snapshot) => (handle: string): DataShape | undefined => handle === 'h-trades' ? snapshot : handle === 'h-limits' ? limits : undefined;

function refusedBy(run: () => unknown): BoardRefusedError {
    try {
        run();
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- the refusal is what the test reads
        if (error instanceof BoardRefusedError) return error;
        throw error;
    }
    throw new Error('Nothing was refused.');
}

describe('a Chart Spec as a Board of one view (ADR-0320 as rewritten by ADR-0340, Q4)', () => {
    it('is drawn as the tool draws it, its only inputs the handles the spec names', () => {
        const snapshot = held();
        const reading = boardOfChart(spec, tables(snapshot));

        expect(reading.views.map(view => [view.kind, view.id])).toEqual([['chart', boardChartViewId]]);
        expect(reading.inputs.map(input => input.name)).toEqual(['h-trades']);
        expect(boardInput(reading, 'h-trades')?.columns.map(column => column.name)).toEqual(['day', 'pnl', 'desk']);

        const chart = bindBoard(reading, { 'h-trades': snapshot }).view(boardChartViewId);
        if (chart.kind !== 'chart') throw new Error('The one view is not a chart.');
        const input = boardInput(reading, 'h-trades');
        if (input === undefined) throw new Error('No input.');
        const answered = answerChartSpec(spec, handle => handle === 'h-trades' ? declaredChartData(snapshot, input, undefined) : undefined);
        if (!answered.accepted) throw new Error(answered.text);
        expect(chart.reading.definition.series[0]?.points).toEqual(answered.reading.definition.series[0]?.points);
    });

    it.each([
        ['"data": "h-trades"', '"data": "h-quotes"', 'series[0].data', 'series[0].data "h-quotes" is not known'],
        ['"y": "pnl"', '"y": "book"', 'series[0].y', 'series[0].y: "h-trades" has no column "book"'],
        ['"version": 1,', '"version": 1, "version": 1,', '', ''],
    ])('is refused as the tool refuses, with the spec\'s own path (%#)', (from, to, path, message) => {
        const refused = refusedBy(() => boardOfChart(spec.replace(from, to), tables(held())));

        expect(refused.path).toBe(path);
        expect(refused.message.startsWith(message)).toBe(true);
    });

    it('refuses a spec of a 3D surface by its path, rather than drawing it as no chart', () => {
        const surface = '{"version": 1, "surface": {"data": "h-trades", "x": "desk", "y": "day", "z": "pnl"}}';
        const refused = refusedBy(() => boardOfChart(surface, tables(held())));
        expect(refused.path).toBe('surface');
        expect(refused.message).toContain('ExSurface');
    });
});
