// A Board over sources held where the data lives (ADR-0343 to ADR-0345), as ExBoard.Tests' BoardSourceTests hold the
// C# BoundBoard to them.
import { describe, expect, it } from 'vitest';
import { thinChartData, type ChartSourceAnswer } from '@ex-grid/chart';
import { BoardRefusedError, createSnapshot, decimalFromText, ticksFromText, type DataShape, type Snapshot } from '@ex-grid/data';
import { readPivotLayoutJson, SnapshotPivotSource, textItemKey, type PivotSource } from '@ex-grid/pivot-engine';
import {
    BoardChartChange, bindBoardThrough, boardChartPixels, boardInput, narrowedPositions, narrowedSnapshot, readBoard, valuesUnder, type BoardCellsQuery,
    type BoardChartQuery, type BoardLink, type BoardQuery, type BoardReading, type BoardSource, type BoundBoard, type LinkCondition, type PivotCellItems, type SelectedRows,
} from '../src/index';
import { declaredChartData } from '../src/snapshot-values';

const inputs = '"inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]}}';
const blotter = '{"id": "blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "pnl", "format": "#,##0"}]}}';
const other = '{"id": "other", "grid": {"data": "trades", "columns": [{"column": "desk"}]}}';
const pivot = '{"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}';
const chart = '{"id": "curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}';

function board(views: string, links = '', outputs = ''): BoardReading {
    const ids = [...views.matchAll(/"id": ("[A-Za-z]+"), "(?:grid|pivot|chart)"/g)].map(m => m[1]);
    return readBoard(`{"version": 1, ${inputs}, "views": [${views}], "layout": [{"views": [${ids.join(', ')}]}], "links": [${links}]`
        + `${outputs === '' ? '' : `, "outputs": {${outputs}}`}}`);
}

/** A hold a test releases: the answer it gates comes only once released, with a value or a failure. */
interface Gate {
    readonly passed: Promise<void>;
    readonly pass: () => void;
    readonly fail: (error: Error) => void;
}

function gate(): Gate {
    let pass: (() => void) | undefined;
    let fail: ((error: Error) => void) | undefined;
    const passed = new Promise<void>((resolve, reject) => {
        pass = resolve;
        fail = reject;
    });
    if (pass === undefined || fail === undefined) throw new Error('A promise runs its executor at once.');
    return { passed, pass, fail };
}

/** A server holding the trades: it answers its shape, and each query under the narrowing it is handed. */
class Server {
    readonly held: Snapshot = createSnapshot({
        version: 7,
        columns: [
            { name: 'desk', kind: 'text', values: ['Rates', 'FX', 'Rates', 'Credit'] },
            { name: 'pnl', kind: 'decimal', values: ['100', '200', '300', '400'].map(decimalFromText) },
            { name: 'day', kind: 'date', values: ['2026-10-19', '2026-10-20', '2026-10-21', '2026-10-22'].map(ticksFromText) },
        ],
    });
    shape: DataShape = { columns: [{ name: 'desk', kind: 'text' }, { name: 'pnl', kind: 'decimal' }, { name: 'day', kind: 'date' }] };
    shapeAsked = 0;
    readonly asked: BoardQuery[] = [];
    readonly pivots: (readonly LinkCondition[])[] = [];
    readonly charted: BoardChartQuery[] = [];
    readonly cells: BoardCellsQuery[] = [];
    // Each chart or cells query answered once its gate passes; undefined answers at once.
    holdChart: ((query: BoardChartQuery) => Promise<void>) | undefined;
    holdCells: ((query: BoardCellsQuery) => Promise<void>) | undefined;
    // The version a chart's answer names; undefined names the held data's own.
    chartVersion: bigint | undefined;

    source(answers: { readonly pivot?: boolean; readonly thin?: boolean; readonly under?: boolean } = {}): BoardSource {
        const { pivot: pivots = true, thin = true, under = true } = answers;
        const through = (narrowing: readonly LinkCondition[]) => narrowedSnapshot(this.held, narrowedPositions(this.held, narrowing));
        return {
            shape: () => {
                this.shapeAsked++;
                return Promise.resolve(this.shape);
            },
            fetch: query => {
                this.asked.push(query);
                const kept = narrowedPositions(this.held, query.narrowing);
                const start = Math.min(query.grid.range.start, kept.length);
                const count = Math.min(query.grid.range.count, kept.length - start);
                return Promise.resolve({ rows: narrowedSnapshot(this.held, kept.slice(start, start + count)), start, totalCount: kept.length });
            },
            ...(pivots ? {
                pivot: (narrowing: readonly LinkCondition[]): PivotSource => {
                    this.pivots.push(narrowing);
                    return new SnapshotPivotSource(through(narrowing));
                },
            } : {}),
            ...(thin ? {
                thin: async (query: BoardChartQuery): Promise<ChartSourceAnswer> => {
                    this.charted.push(query);
                    await this.holdChart?.(query);
                    const input = boardInput(board(chart), 'trades');
                    if (input === undefined) throw new Error('No input.');
                    return thinChartData(declaredChartData(through(query.narrowing), input, undefined), query.chart, this.chartVersion ?? this.held.version);
                },
            } : {}),
            ...(under ? {
                under: async (query: BoardCellsQuery): Promise<LinkCondition> => {
                    this.cells.push(query);
                    await this.holdCells?.(query);
                    return valuesUnder(this.held, query);
                },
            } : {}),
        };
    }
}

function bind(reading: BoardReading, server: Server, answers?: Parameters<Server['source']>[0]): Promise<BoundBoard> {
    return bindBoardThrough(reading, {}, { trades: server.source(answers) });
}

function rows(top: number, bottom: number): SelectedRows {
    return { ranges: [{ topRow: top, leftColumn: 0, rowCount: bottom - top + 1, columnCount: 1 }] };
}

const none: SelectedRows = { ranges: [] };

function grid(bound: BoundBoard, id: string) {
    const view = bound.view(id);
    if (view.kind !== 'grid') throw new Error(`'${id}' is not a grid.`);
    return view;
}

function chartOf(bound: BoundBoard, id = 'curve') {
    const view = bound.view(id);
    if (view.kind !== 'chart') throw new Error(`'${id}' is not a chart.`);
    return view;
}

function pivotOf(bound: BoundBoard, id = 'byDesk') {
    const view = bound.view(id);
    if (view.kind !== 'pivot') throw new Error(`'${id}' is not a pivot.`);
    return view;
}

// The grid's rows once its source has answered the question in flight: a reading taken when it has settled.
async function desks(bound: BoundBoard, id: string): Promise<(string | null)[]> {
    const { source } = grid(bound, id);
    await new Promise<void>(resolve => {
        const stop = source.subscribe(() => {
            if (source.snapshot().loading) return;
            stop();
            resolve();
        });
        if (!source.snapshot().loading) {
            stop();
            resolve();
        }
    });
    return source.snapshot().window.map(row => {
        const desk = row.snapshot.column('desk');
        return desk.kind === 'text' ? desk.valueAt(row.at) : null;
    });
}

// The next time the Board tells its listeners, as a chart's source answers.
function told(bound: BoundBoard): Promise<void> {
    return new Promise(resolve => {
        const stop = bound.subscribe(() => {
            stop();
            resolve();
        });
    });
}

function points(bound: BoundBoard): number[] | undefined {
    return chartOf(bound).reading.definition.series[0]?.points.map(p => Number(p.y));
}

function firstLink(reading: BoardReading): BoardLink {
    const [link] = reading.links;
    if (link === undefined) throw new Error('The Board has no link.');
    return link;
}

const desk = (name: string): PivotCellItems => ({ row: [{ field: 'desk', item: textItemKey(name) }], column: [] });

function refusalOf(promise: Promise<unknown>): Promise<BoardRefusedError> {
    return promise.then(() => { throw new Error('Not refused.'); }, (error: unknown) => { // oxlint-disable-line ex-grid/no-catch -- the refusal is what the test reads, and anything else is rethrown
        if (!(error instanceof BoardRefusedError)) throw error;
        return error;
    });
}

function linked(): BoardReading {
    return board(`${pivot}, ${blotter}`, '{"from": "byDesk", "to": ["blotter"], "on": ["desk"]}');
}

function dayNumber(text: string): number {
    return Number(ticksFromText(text) / 864_000_000_000n);
}

describe('a Board over a source held on a server (ADR-0343)', () => {
    it('refuses a source of another shape before any row is fetched (ADR-0341)', async () => {
        const server = new Server();
        server.shape = { columns: [{ name: 'desk', kind: 'text' }, { name: 'pnl', kind: 'double' }, { name: 'day', kind: 'date' }] };

        const refused = await refusalOf(bind(board(blotter), server));

        expect(refused.path).toBe('inputs.trades');
        expect(refused.message).toContain("'pnl'");
        expect(server.shapeAsked).toBe(1);
        expect(server.asked).toHaveLength(0);
    });

    it('refuses an input bound both to a Snapshot and to a source, never choosing one', async () => {
        const server = new Server();
        const refused = await refusalOf(bindBoardThrough(board(blotter), { trades: server.held }, { trades: server.source() }));
        expect(refused.path).toBe('inputs.trades');
    });

    it('fetches a grid\'s rows, and hands a link\'s condition to the source to execute', async () => {
        const server = new Server();
        const bound = await bind(board(`${blotter}, ${other}, ${pivot}`, '{"from": "blotter", "to": ["other", "byDesk"], "on": ["desk"]}'), server);
        expect(grid(bound, 'blotter').snapshot).toBeUndefined();
        expect(await desks(bound, 'blotter')).toEqual(['Rates', 'FX', 'Rates', 'Credit']);
        expect(server.asked.every(query => query.narrowing.length === 0)).toBe(true);
        expect(server.pivots).toEqual([[]]);

        expect(bound.select('blotter', rows(0, 0))).toBe(true);

        expect(await desks(bound, 'other')).toEqual(['Rates', 'Rates']);
        const asked = server.asked.at(-1);
        expect(asked?.view).toBe('other');
        expect(asked?.narrowing.map(condition => condition.values)).toEqual([[['Rates']]]);
        expect(server.pivots.at(-1)?.map(condition => condition.values)).toEqual([[['Rates']]]);
        expect(pivotOf(bound).source).toBeInstanceOf(SnapshotPivotSource);

        // The selection let go, the narrowing goes with it, and the views are asked for the whole again.
        expect(bound.select('blotter', none)).toBe(true);
        expect(await desks(bound, 'other')).toHaveLength(4);
        expect(server.asked.at(-1)?.narrowing).toEqual([]);
        expect(server.pivots.at(-1)).toEqual([]);
    });

    it('narrows nothing and hands nothing on by a selection past the rows fetched, and says so (spine rule 1)', async () => {
        const server = new Server();
        const bound = await bind(board(`${blotter}, ${other}`, '{"from": "blotter", "to": ["other"], "on": ["desk"]}',
            '"review": {"from": "blotter", "on": ["desk"], "confirm": "Send"}'), server);
        await desks(bound, 'blotter');
        await desks(bound, 'other');
        const asked = server.asked.length;

        expect(bound.select('blotter', rows(2, 900))).toBe(false);

        expect(server.asked).toHaveLength(asked);
        expect(bound.canConfirm('review')).toBe(false);
        expect(bound.confirm('review')).toBeUndefined();
        expect(grid(bound, 'blotter').unread).toBe('The selection reaches rows not fetched from the source, so it narrows nothing and hands nothing on.');

        // A selection of rows fetched is read again, and what was said of the last one goes.
        expect(bound.select('blotter', rows(1, 1))).toBe(true);
        expect(grid(bound, 'blotter').unread).toBeUndefined();
    });

    it('hands on from a fetched grid the version of the page its rows were read from (ADR-0340, Q6)', async () => {
        const server = new Server();
        const bound = await bind(board(blotter, '', '"review": {"from": "blotter", "on": ["desk"], "confirm": "Send"}'), server);
        await desks(bound, 'blotter');

        bound.select('blotter', rows(1, 2));
        const output = bound.confirm('review');

        expect(output?.sourceVersion).toBe(7n);
        expect(output?.condition.values).toEqual([['FX'], ['Rates']]);
        expect(output?.take(server.held).rowCount).toBe(3);
    });

    it('refuses by path a pivot over a source that makes no pivot source', async () => {
        const refused = await refusalOf(bind(board(pivot), new Server(), { pivot: false }));
        expect(refused.path).toBe('views[0].pivot');
    });
});

describe('a pivot over a source held on a server (ADR-0345)', () => {
    it('refuses by path a link from a pivot whose source answers nothing under cells', async () => {
        const refused = await refusalOf(bind(linked(), new Server(), { under: false }));
        expect(refused.path).toBe('links[0].from');
        expect(refused.message).toBe("Pivot 'byDesk' is over input 'trades', held on a server, where the rows under a cell are, "
            + 'and its source answers no values under cells (BoardSource.under).');
    });

    it('asks the source for the values under the cells selected, and narrows by its answer', async () => {
        const server = new Server();
        const reading = linked();
        const bound = await bind(reading, server);
        const link = firstLink(reading);

        expect(await bound.selectCellsThrough('byDesk', [desk('Rates')])).toBe(true);

        expect(server.cells).toHaveLength(1);
        const asked = server.cells[0];
        expect(asked?.view).toBe('byDesk');
        expect(asked?.on).toEqual(['desk']);
        expect(asked?.layout).toBe(pivotOf(bound).layout);
        expect(asked?.narrowing).toEqual([]);
        expect(bound.conditionOf(link)?.values).toEqual([['Rates']]);
        expect(await desks(bound, 'blotter')).toEqual(['Rates', 'Rates']);
        expect(server.asked.at(-1)?.narrowing.map(condition => condition.values)).toEqual([[['Rates']]]);

        // A selection let go asks nothing, and takes the narrowing away.
        expect(await bound.selectCellsThrough('byDesk', [])).toBe(true);
        expect(server.cells).toHaveLength(1);
        expect(bound.conditionOf(link)).toBeUndefined();
    });

    it('refuses the synchronous selection, which cannot read a server pivot at once', async () => {
        const bound = await bind(linked(), new Server());
        expect(() => bound.selectCells('byDesk', [desk('FX')])).toThrow(Error);
        expect(() => bound.cellCondition(firstLink(bound.board), [desk('FX')])).toThrow(Error);
    });

    it('narrows nothing by the answer for a selection a later one replaced, whichever comes first (spine rule 6)', async () => {
        const server = new Server();
        const reading = linked();
        const bound = await bind(reading, server);
        const held: Gate[] = [];
        server.holdCells = () => {
            const one = gate();
            held.push(one);
            return one.passed;
        };

        const first = bound.selectCellsThrough('byDesk', [desk('Rates')]);
        const second = bound.selectCellsThrough('byDesk', [desk('FX')]);
        held[1]?.pass();
        expect(await second).toBe(true);
        held[0]?.pass();
        expect(await first).toBe(false);

        expect(bound.conditionOf(firstLink(reading))?.values).toEqual([['FX']]);
    });

    it('lets its links go when the source fails the newest selection (spine rule 1)', async () => {
        const server = new Server();
        const reading = linked();
        const bound = await bind(reading, server);
        expect(await bound.selectCellsThrough('byDesk', [desk('Rates')])).toBe(true);
        server.holdCells = () => Promise.reject(new Error('the server is down'));

        await expect(bound.selectCellsThrough('byDesk', [desk('FX')])).rejects.toThrow('the server is down');

        expect(bound.conditionOf(firstLink(reading))).toBeUndefined();
        expect(await desks(bound, 'blotter')).toHaveLength(4);
        expect(server.asked.at(-1)?.narrowing).toEqual([]);
    });

    it('drops a failure for a selection a later one replaced, and keeps the later one\'s narrowing (spine rule 6)', async () => {
        const server = new Server();
        const reading = linked();
        const bound = await bind(reading, server);
        const held: Gate[] = [];
        server.holdCells = () => {
            const one = gate();
            held.push(one);
            return one.passed;
        };

        const first = bound.selectCellsThrough('byDesk', [desk('Rates')]);
        const second = bound.selectCellsThrough('byDesk', [desk('FX')]);
        held[1]?.pass();
        expect(await second).toBe(true);
        held[0]?.fail(new Error('the server is down'));
        expect(await first).toBe(false);

        expect(bound.conditionOf(firstLink(reading))?.values).toEqual([['FX']]);
    });

    it('reads a total under the reference, and leaves out the Items the layout hides', () => {
        const server = new Server();
        const layout = readPivotLayoutJson('{"version": 1, "rows": [{"field": "desk", "hiddenItems": [{"kind": "text", "value": "Credit"}]}], '
            + '"values": [{"field": "pnl", "aggregation": "sum"}]}');

        const under = valuesUnder(server.held, { view: 'byDesk', layout, cells: [{ row: [], column: [] }], on: ['desk'], narrowing: [] });

        expect(under.values).toEqual([['Rates'], ['FX']]);
    });

    it('refuses by a TypeError a cell naming a field the data does not hold', () => {
        const server = new Server();
        const layout = readPivotLayoutJson('{"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}');
        const cells = [{ row: [{ field: 'book', item: textItemKey('A') }], column: [] }];
        expect(() => valuesUnder(server.held, { view: 'byDesk', layout, cells, on: ['desk'], narrowing: [] }))
            .toThrow(new TypeError("A cell names field 'book', which pivot 'byDesk' does not read from its data."));
    });
});

describe('a chart over a source held on a server (ADR-0344)', () => {
    it('is read from the points its source thinned, asked at the Board\'s pixels', async () => {
        const server = new Server();
        const bound = await bind(board(chart), server);

        const curve = chartOf(bound);
        expect(curve.thinned).toBe(true);
        expect(curve.refusal).toBeUndefined();
        expect(server.charted).toHaveLength(1);
        expect(server.charted[0]?.chart).toEqual({ data: 'trades', x: 'day', y: 'pnl', values: 'dates', from: null, to: null, pixels: boardChartPixels });
        expect(server.charted[0]?.view).toBe('curve');
        expect(boardChartPixels).toBe(2048);
        expect(points(bound)).toEqual([100, 200, 300, 400]);
    });

    it('is refused by path over a source that thins nothing', async () => {
        const refused = await refusalOf(bind(board(chart), new Server(), { thin: false }));
        expect(refused.path).toBe('views[0].chart');
    });

    it('refuses at binding what a thinned point cannot stand for, before any source is asked', async () => {
        const server = new Server();
        const refused = await refusalOf(bind(board(chart.replace('"kind": "level"', '"kind": "observation"')), server));
        expect(refused.path).toBe('views[0].chart.series[0].kind');
        expect(server.charted).toHaveLength(0);
    });

    it('hands a link\'s narrowing to the source, and is drawn from its answer', async () => {
        const server = new Server();
        const bound = await bind(board(`${blotter}, ${chart}`, '{"from": "blotter", "to": ["curve"], "on": ["desk"]}'), server);
        await desks(bound, 'blotter');

        expect(bound.select('blotter', rows(0, 0))).toBe(true);

        // Told once as the link narrows, and again as the source answers.
        await told(bound);
        expect(server.charted.at(-1)?.narrowing.map(condition => condition.values)).toEqual([[['Rates']]]);
        expect(points(bound)).toEqual([100, 300]);
    });

    it('asks the source for a range narrowed, drawn at full detail, and asks nothing for the same range again', async () => {
        const server = new Server();
        const bound = await bind(board(chart), server);
        const from = dayNumber('2026-10-20');
        const answered = told(bound);

        chartOf(bound).changeRange({ from });

        await answered;
        expect(server.charted.at(-1)?.chart.from).toBe(from);
        expect(points(bound)).toHaveLength(3);
        chartOf(bound).changeRange({ from });
        expect(server.charted).toHaveLength(2);
    });

    it('never draws an answer to an ask a later one replaced, whichever comes first (spine rule 6)', async () => {
        const server = new Server();
        const bound = await bind(board(chart), server);
        const curve = chartOf(bound);
        const held: Gate[] = [];
        server.holdChart = () => {
            const one = gate();
            held.push(one);
            return one.passed;
        };
        let answered = 0;
        bound.subscribe(() => { answered++; });

        // The first ask is from the 20th, the second from the 21st; the second is answered first.
        curve.layOver(new BoardChartChange([], { from: dayNumber('2026-10-20') }));
        const first = bound.askChart('curve');
        curve.layOver(new BoardChartChange([], { from: dayNumber('2026-10-21') }));
        const second = bound.askChart('curve');
        held[1]?.pass();
        await second;
        held[0]?.pass();
        await first;

        expect(answered).toBe(1);
        expect(points(bound)).toEqual([300, 400]);
    });

    it('says in its place that its source failed', async () => {
        const server = new Server();
        const bound = await bind(board(`${blotter}, ${chart}`, '{"from": "blotter", "to": ["curve"], "on": ["desk"]}'), server);
        await desks(bound, 'blotter');
        server.holdChart = () => Promise.reject(new Error('the server is down'));

        bound.select('blotter', rows(0, 0));

        await told(bound);
        expect(chartOf(bound).refusal).toBe('The source did not answer the chart: the server is down');
    });

    it('says, and does not draw, an answer from older data than the chart shows (spine rule 1)', async () => {
        const server = new Server();
        const bound = await bind(board(chart), server);
        server.chartVersion = 6n;

        await bound.askChart('curve');

        expect(chartOf(bound).refusal).toBe("The source answered the chart from version 6 of 'trades', older than version 7 the chart was drawn from, so it is not drawn.");
    });
});
