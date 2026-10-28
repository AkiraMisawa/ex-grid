// @vitest-environment happy-dom
// A Board drawn into an element (layer 2, ADR-0350 section 13). What a grid, a pivot and a chart draw is theirs, tested in
// their packages: here each is a stand-in that records what the Board hands it, and the tests judge what the Board
// decides — what it lays out, what a selection narrows, what a press hands on, which Diff it raises, what it refuses.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { thinChartData, type ChartHandle, type ChartOptions } from '@ex-grid/chart';
import type { GridHandle, GridOptions, GridSelection } from '@ex-grid/core';
import type { Snapshot } from '@ex-grid/data';
import type { PivotHandle, PivotViewOptions } from '@ex-grid/pivot';
import { readPivotLayoutJson, SnapshotPivotSource, textItemKey } from '@ex-grid/pivot-engine';
import { BoardDrawing, type BoardMounts, type BoardOptions } from '../src/create-board';
import {
    boardInput, BoardDiff, narrowedPositions, narrowedSnapshot, readBoard, valuesUnder, type BoardOutput, type BoardReading, type BoardSource, type LinkCondition,
    type ShapedRow,
} from '../src/index';
import { declaredChartData } from '../src/snapshot-values';

const views = [
    '{"id": "blotter", "title": "Blotter", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "pnl", "format": "#,##0", "width": 90}]}}',
    '{"id": "byDesk", "pivot": {"data": "trades", "layout": {"version": 1, "rows": [{"field": "desk"}], "values": [{"field": "pnl", "aggregation": "sum"}]}}}',
    '{"id": "curve", "chart": {"version": 1, "axis": {"values": "dates"}, "series": [{"id": "pnl", "name": "P&L", "kind": "level", "data": "trades", "x": "day", "y": "pnl"}]}}',
];

function board(extra = ''): BoardReading {
    return readBoard(`{"version": 1, "title": "Desk review",
      "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "pnl", "type": "decimal"}, {"name": "day", "type": "date"}]}},
      "views": [${views.join(', ')}], "layout": [{"views": ["blotter", "byDesk"], "height": 300}, {"views": ["curve"], "height": 200}]${extra}}`);
}

const linked = board(', "links": [{"from": "blotter", "to": ["curve"], "on": ["desk"]}], "outputs": {"chosen": {"from": "blotter", "on": ["desk"], "confirm": "Use these"}}');

function data(reading: BoardReading): Record<string, Snapshot> {
    const trades = boardInput(reading, 'trades');
    if (trades === undefined) throw new Error('No input trades.');
    return {
        trades: trades.fromJson(`[{"desk": "Rates", "pnl": 100, "day": "2026-10-19"}, {"desk": "FX", "pnl": -50, "day": "2026-10-20"},
            {"desk": "Rates", "pnl": 75, "day": "2026-10-21"}]`),
    };
}

/** Each view's stand-in: the options it was last handed, how often, and whether it was destroyed. */
class Mounted<O> {
    options: O;
    updates = 0;
    destroyed = false;
    readonly element: HTMLElement;

    constructor(element: HTMLElement, options: O) {
        this.element = element;
        this.options = options;
    }

    handle() {
        return {
            update: (next: O) => { this.options = next; this.updates++; },
            destroy: () => { this.destroyed = true; },
        };
    }
}

/** A member of a handle the Board never calls. */
const unused = (what: string) => () => { throw new Error(`The Board does not ${what}.`); };

class StandIns implements BoardMounts {
    readonly grids: Mounted<GridOptions<ShapedRow>>[] = [];
    readonly pivots: Mounted<PivotViewOptions>[] = [];
    readonly charts: Mounted<ChartOptions>[] = [];

    readonly grid = (element: HTMLElement, options: GridOptions<ShapedRow>): GridHandle<ShapedRow> => {
        const made = new Mounted(element, options);
        this.grids.push(made);
        return {
            ...made.handle(),
            placeSelection: unused('place a selection'),
            readView: unused('read a grid\'s view'),
            writePointedText: unused('write a grid\'s pointed text'),
            takeBackPointedText: unused('take back a grid\'s pointed text'),
            showViewWith: unused('hand a grid a view'),
            focus: unused('hand a grid the keyboard'),
            keyName: unused('name a grid\'s keys'),
            openPopover: unused('open a grid\'s popover'),
            closePopover: unused('close a grid\'s popover'),
            canSummarize: false,
            refreshSummary: unused('refresh a grid\'s summary'),
            returnKeyboard: unused('return a grid\'s keyboard'),
            handKeyboardTo: unused('hand a grid\'s keyboard on'),
            inOrder: unused('place work in a grid\'s order'),
            cancellableInOrder: unused('place cancellable work in a grid\'s order'),
        };
    };

    readonly pivot = (element: HTMLElement, options: PivotViewOptions): PivotHandle => {
        const made = new Mounted(element, options);
        this.pivots.push(made);
        return { ...made.handle(), openDetails: unused('open the records of a cell') };
    };

    readonly chart = (element: HTMLElement, options: ChartOptions): ChartHandle => {
        const made = new Mounted(element, options);
        this.charts.push(made);
        return { ...made.handle(), download: () => Promise.resolve(false) };
    };
}

function only<T>(items: readonly T[]): T {
    expect(items).toHaveLength(1);
    const [item] = items;
    if (item === undefined) throw new Error('None.');
    return item;
}

const drawn: BoardDrawing[] = [];

afterEach(() => {
    for (const view of drawn.splice(0)) view.destroy();
    document.body.replaceChildren();
});

function draw(options: Partial<BoardOptions> & Pick<BoardOptions, 'board'>, mounts = new StandIns()) {
    const element = document.createElement('div');
    document.body.append(element);
    const diffs: BoardDiff[] = [];
    const outputs: BoardOutput[] = [];
    const full: BoardOptions = { data: data(options.board), onDiff: diff => { diffs.push(diff); }, onOutput: output => { outputs.push(output); }, ...options };
    const view = new BoardDrawing(element, full, mounts);
    drawn.push(view);
    return { element, view, mounts, diffs, outputs, options: full };
}

/** Whole rows selected, in the order the grid shows them. */
function rows(...shown: number[]): GridSelection {
    return { ranges: shown.map(topRow => ({ topRow, leftColumn: 0, rowCount: 1, columnCount: 1 })) } as unknown as GridSelection;
}

function call<A extends unknown[]>(handler: ((...args: A) => void) | undefined, ...args: A): void {
    if (handler === undefined) throw new Error('The Board handed the view no such handler.');
    handler(...args);
}

describe('createBoard (ADR-0340)', () => {
    it('lays out each row\'s views at the row\'s height, a chart at its share of the Board\'s width', () => {
        const { element, mounts } = draw({ board: board(), width: 1000 });

        expect(element.querySelector('.ex-board-title')?.textContent).toBe('Desk review');
        expect([...element.querySelectorAll('.ex-board-row')].map(row => [...row.querySelectorAll('section')].map(s => s.dataset['exView'])))
            .toEqual([['blotter', 'byDesk'], ['curve']]);
        expect(element.querySelector('[data-ex-view="blotter"] .ex-board-view-title')?.textContent).toBe('Blotter');
        expect(element.querySelector('[data-ex-view="byDesk"]')?.getAttribute('aria-label')).toBe('byDesk');
        expect(only(mounts.grids).options.viewportHeight).toBe(300);
        expect(only(mounts.pivots).options).toMatchObject({ viewportHeight: 300, showFieldList: false, locale: 'invariant' });
        expect(only(mounts.charts).options).toMatchObject({ width: 1000, height: 200, title: '' });
    });

    it('draws nothing of a Board that does not bind but the refusal, naming its path (spine rule 1)', () => {
        const reading = board();
        const { element, mounts, view } = draw({ board: reading, data: {} });

        expect(element.querySelector('[role="alert"]')?.textContent).toMatch(/^inputs\.trades: /u);
        expect(element.querySelectorAll('section')).toHaveLength(0);
        expect(mounts.grids.length + mounts.pivots.length + mounts.charts.length).toBe(0);
        expect(view.bound).toBeUndefined();
    });

    it('narrows a linked chart by the grid\'s selection, and lets go when it is dropped (ADR-0340 Q5, ADR-0011)', () => {
        const { mounts } = draw({ board: linked });
        const grid = only(mounts.grids);
        const chart = only(mounts.charts);
        const points = () => chart.options.definition.series.map(series => series.points.length);

        expect(points()).toEqual([3]);
        call(grid.options.onSelectionChanged, rows(1));
        expect(points()).toEqual([1]);
        call(grid.options.onSelectionChanged, rows());
        expect(points()).toEqual([3]);
    });

    it('hands on what is selected only when the user confirms, and says so with nothing selected (ADR-0340 Q6)', () => {
        const { element, mounts, outputs } = draw({ board: linked });
        const button = element.querySelector<HTMLButtonElement>('button[data-ex-output="chosen"]');
        const status = element.querySelector('.ex-board-confirm-status');
        if (button === null) throw new Error('No confirm button.');

        button.click();
        expect(status?.textContent).toBe('Select the rows to hand on first.');
        expect(button.disabled).toBe(false);
        call(only(mounts.grids).options.onSelectionChanged, rows(0, 2));
        expect(status?.textContent).toBe('');
        expect(outputs).toEqual([]);
        button.click();
        expect(only(outputs).name).toBe('chosen');
        expect(only(outputs).condition.values).toEqual([['Rates']]);
    });

    it('raises the whole Diff on each change the user makes, and nothing for a gesture refused (ADR-0340 Q7)', () => {
        const { mounts, diffs } = draw({ board: board() });
        const grid = only(mounts.grids);
        expect(diffs).toEqual([]);

        call(grid.options.onColumnWidthChanged, { column: 'desk', widthPx: 140 });
        expect(grid.options.columns.map(column => column.width)).toEqual([{ px: 140 }, { px: 90 }]);
        call(grid.options.onColumnHidden, 'desk');
        call(grid.options.onColumnHidden, 'desk');
        call(grid.options.onColumnWidthChanged, { column: 'desk', widthPx: 200 });
        call(only(mounts.charts).options.onSeriesVisibilityChanged, { key: 'pnl', hidden: true });
        call(only(mounts.pivots).options.onLayoutChanged, { layout: readPivotLayoutJson('{"version": 1, "rows": [{"field": "day"}], "values": [{"field": "pnl", "aggregation": "sum"}]}') });

        expect(diffs).toHaveLength(4);
        const last = diffs.at(-1);
        expect(last?.grids.get('blotter')?.hidden).toEqual(['desk']);
        expect(last?.charts.get('curve')?.hiddenSeries).toEqual(['pnl']);
        expect(last?.pivots.get('byDesk')?.layout.rows.map(field => field.field)).toEqual(['day']);
        expect(only(mounts.charts).options.hiddenSeries).toEqual(['pnl']);
    });

    it('raises a Diff for a sort set on a grid\'s source, which the grid sets itself (ADR-0001)', () => {
        const { mounts, diffs } = draw({ board: board() });
        const source = only(mounts.grids).options.source;
        if (source === undefined) throw new Error('No source.');

        source.setSorts([{ column: 'pnl', direction: 'descending' }]);
        source.publishGathered?.();

        expect(only(diffs).grids.get('blotter')?.viewState.sorts).toEqual([{ column: 'pnl', direction: 'descending' }]);
    });

    it('says what of a Diff no longer fits, and raises the Diff that did apply', () => {
        const json = '{"version":1,"views":{"gone":{"chart":{"hidden":["pnl"]}},"curve":{"chart":{"hidden":["pnl"]}}}}';
        const { element, diffs, mounts } = draw({ board: board(), diff: BoardDiff.read(json) });

        expect(element.querySelector('.ex-board-dropped')?.getAttribute('role')).toBe('status');
        expect(element.querySelectorAll('.ex-board-dropped-part')).toHaveLength(1);
        expect(only(diffs).toJson()).toBe('{"version":1,"views":{"curve":{"chart":{"hidden":["pnl"]}}}}');
        expect(only(mounts.charts).options.hiddenSeries).toEqual(['pnl']);
    });

    it('does not bind again for the Diff it raised, and keeps the user\'s changes when bound again for new data', () => {
        const { view, mounts, diffs, options } = draw({ board: board() });
        call(only(mounts.charts).options.onSeriesVisibilityChanged, { key: 'pnl', hidden: true });
        view.update({ ...options, diff: only(diffs) });
        expect(mounts.charts).toHaveLength(1);

        view.update({ ...options, diff: only(diffs), data: data(options.board) });
        expect(mounts.charts).toHaveLength(2);
        expect(mounts.charts[0]?.destroyed).toBe(true);
        expect(mounts.charts[1]?.options.hiddenSeries).toEqual(['pnl']);
        expect(diffs).toHaveLength(1);

        view.update({ ...options, diff: BoardDiff.empty, data: data(options.board) });
        expect(mounts.charts[2]?.options.hiddenSeries).toEqual([]);
    });

    it('keeps the user\'s changes through a refusal between two bindings, and raises no Diff for them again', () => {
        const { element, view, mounts, diffs, options } = draw({ board: board() });
        call(only(mounts.charts).options.onSeriesVisibilityChanged, { key: 'pnl', hidden: true });
        view.update({ ...options, data: {} });
        expect(element.querySelector('[role="alert"]')?.textContent).toMatch(/trades/u);

        view.update({ ...options, data: data(options.board) });
        expect(mounts.charts.at(-1)?.options.hiddenSeries).toEqual(['pnl']);
        expect(diffs).toHaveLength(1);
    });

    it('binds again for the same options after a binding threw, rather than keep the emptied Board', () => {
        const mounts = new StandIns();
        let failing = false;
        const flaky: BoardMounts = {
            grid: mounts.grid,
            pivot: mounts.pivot,
            chart: (element, chartOptions) => { if (failing) throw new RangeError('No chart.'); return mounts.chart(element, chartOptions); },
        };
        const element = document.createElement('div');
        const reading = board();
        const view = new BoardDrawing(element, { board: reading, data: data(reading) }, flaky);
        drawn.push(view);
        const again = { board: reading, data: data(reading) };
        failing = true;
        expect(() => { view.update(again); }).toThrow('No chart.');
        expect(element.querySelectorAll('section')).toHaveLength(0);

        failing = false;
        view.update(again);
        expect(element.querySelectorAll('section')).toHaveLength(3);
        expect(view.bound).toBeDefined();
    });

    it('takes no gesture from a view of an earlier binding', () => {
        const { view, mounts, diffs, options } = draw({ board: linked });
        const earlier = only(mounts.grids).options;
        const earlierChart = only(mounts.charts).options;
        view.update({ ...options, data: data(options.board) });

        call(earlier.onSelectionChanged, rows(1));
        call(earlierChart.onSeriesVisibilityChanged, { key: 'pnl', hidden: true });
        expect(mounts.charts.at(-1)?.options.definition.series.map(series => series.points.length)).toEqual([3]);
        expect(diffs).toHaveLength(0);
        expect(view.bound?.diff.isEmpty).toBe(true);
    });

    it('keeps what it says of a pivot\'s selection while a link leaves the pivot as it was', () => {
        const fromPivot = board(', "links": [{"from": "byDesk", "to": ["curve"], "on": ["desk"]}, {"from": "blotter", "to": ["byDesk"], "on": ["desk"]}]');
        const { element, mounts } = draw({ board: fromPivot });
        const status = element.querySelector('[data-ex-view="byDesk"] .ex-board-link-status');
        call(only(mounts.grids).options.onSelectionChanged, rows(0));
        call(only(mounts.pivots).options.onSelectionChanged, { selection: rows(0), items: undefined });

        // Row 2 is Rates too: the pivot is not narrowed again, and keeps its selection.
        call(only(mounts.grids).options.onSelectionChanged, rows(2));
        expect(status?.textContent).toBe('The selection cannot be read from the report as drawn, so it narrows nothing.');
    });

    it('refuses, before drawing any view, a grid showing dates on a Board given no Temporal (ADR-0350 section 16)', () => {
        const dated = readBoard(`{"version": 1, "inputs": {"trades": {"columns": [{"name": "desk", "type": "text"}, {"name": "day", "type": "date"}]}},
          "views": [{"id": "desks", "grid": {"data": "trades", "columns": [{"column": "desk"}]}},
                    {"id": "days", "grid": {"data": "trades", "columns": [{"column": "desk"}, {"column": "day"}]}}],
          "layout": [{"views": ["desks", "days"]}]}`);
        const { element, mounts, view } = draw({ board: dated });

        expect(element.querySelector('[role="alert"]')?.textContent).toMatch(/^views\[1\]\.grid: Column 'day' holds dates/u);
        expect(mounts.grids).toHaveLength(0);
        expect(view.bound).toBeUndefined();
    });

    it('leaves no view running when one cannot be mounted', () => {
        const mounts = new StandIns();
        const failing: BoardMounts = { grid: mounts.grid, pivot: mounts.pivot, chart: () => { throw new RangeError('No chart.'); } };
        const element = document.createElement('div');

        expect(() => new BoardDrawing(element, { board: board(), data: data(board()) }, failing)).toThrow('No chart.');
        expect([only(mounts.grids).destroyed, only(mounts.pivots).destroyed]).toEqual([true, true]);
        expect(element.children).toHaveLength(0);
    });

    it('narrows a chart by a pivot\'s selected cells, says when it cannot read them, and lets go of a dropped selection (ADR-0340 Q5)', () => {
        const fromPivot = board(', "links": [{"from": "byDesk", "to": ["curve"], "on": ["desk"]}, {"from": "blotter", "to": ["byDesk"], "on": ["desk"]}]');
        const { element, mounts } = draw({ board: fromPivot });
        const pivot = only(mounts.pivots);
        const chart = only(mounts.charts);
        const status = element.querySelector('[data-ex-view="byDesk"] .ex-board-link-status');
        const points = () => chart.options.definition.series.map(series => series.points.length);
        const rates = { row: [{ field: 'desk', item: textItemKey('Rates') }], column: [] };

        call(pivot.options.onSelectionChanged, { selection: rows(0), items: [rates] });
        expect(points()).toEqual([2]);
        call(pivot.options.onSelectionChanged, { selection: rows(0), items: undefined });
        expect(status?.textContent).toBe('The selection cannot be read from the report as drawn, so it narrows nothing.');
        expect(points()).toEqual([3]);

        // The blotter narrows the pivot again, which lets go of its selection, and of what was said of it.
        call(only(mounts.grids).options.onSelectionChanged, rows(1));
        expect(status?.textContent).toBe('');
        call(pivot.options.onSelectionChanged, { selection: rows(0), items: [{ row: [{ field: 'desk', item: textItemKey('FX') }], column: [] }] });
        expect(points()).toEqual([1]);
        call(pivot.options.onSelectionChanged, { selection: rows(), items: [] });
        expect(points()).toEqual([3]);
    });

    it('hears no selection from a pivot no link leaves', () => {
        expect(only(draw({ board: board() }).mounts.pivots).options.onSelectionChanged).toBeUndefined();
    });

    it('binds again for an earlier Diff handed back, as a host undoing the user\'s changes does', () => {
        const first = BoardDiff.read('{"version":1,"views":{"curve":{"chart":{"hidden":["pnl"]}}}}');
        const { view, mounts, diffs, options } = draw({ board: board(), diff: first });
        call(mounts.charts[0]?.options.onSeriesVisibilityChanged, { key: 'pnl', hidden: false });
        view.update({ ...options, diff: only(diffs) });
        expect(mounts.charts).toHaveLength(1);

        view.update({ ...options, diff: first });
        expect(mounts.charts).toHaveLength(2);
        expect(mounts.charts[1]?.options.hiddenSeries).toEqual(['pnl']);
    });

    it('redraws only its charts for another width, and empties its element when destroyed', () => {
        const { view, mounts, element, options } = draw({ board: board(), width: 1000 });
        element.style.margin = '0 auto';
        view.update({ ...options, width: 500 });
        expect(only(mounts.charts).options.width).toBe(500);
        expect(mounts.grids).toHaveLength(1);

        view.destroy();
        expect(element.children).toHaveLength(0);
        expect(element.classList.contains('ex-board')).toBe(false);
        expect([element.style.display, element.style.gap, element.style.margin]).toEqual(['', '', '0px auto']);
        expect([mounts.grids[0]?.destroyed, mounts.pivots[0]?.destroyed, mounts.charts[0]?.destroyed]).toEqual([true, true, true]);
    });
});

/** A server holding the trades, answering as a host's own code would, its shape question held until the test lets it go. */
function served(reading: BoardReading, failCells = false) {
    const trades = data(reading)['trades'];
    if (trades === undefined) throw new Error('No trades.');
    let release: (() => void) | undefined;
    const shapeAnswered = new Promise<void>(resolve => { release = resolve; });
    const counts = { shape: 0, shapeAbandoned: 0, cells: 0 };
    const input = boardInput(reading, 'trades');
    if (input === undefined || release === undefined) throw new Error('No input trades.');
    const through = (narrowing: readonly LinkCondition[]) => narrowedSnapshot(trades, narrowedPositions(trades, narrowing));
    const source: BoardSource = {
        shape: async signal => {
            counts.shape++;
            signal?.addEventListener('abort', () => { counts.shapeAbandoned++; });
            await shapeAnswered;
            return input.shape;
        },
        fetch: query => {
            const kept = through(query.narrowing);
            const start = Math.min(query.grid.range.start, kept.rowCount);
            const count = Math.min(query.grid.range.count, kept.rowCount - start);
            return Promise.resolve({ rows: narrowedSnapshot(kept, Array.from({ length: count }, (_, i) => start + i)), start, totalCount: kept.rowCount });
        },
        pivot: narrowing => new SnapshotPivotSource(through(narrowing)),
        thin: query => Promise.resolve(thinChartData(declaredChartData(through(query.narrowing), input, undefined), query.chart, trades.version)),
        under: query => {
            counts.cells++;
            return failCells ? Promise.reject(new Error('the server is down')) : Promise.resolve(valuesUnder(trades, query));
        },
    };
    return { sources: { trades: source }, counts, release };
}

describe('createBoard over sources held on a server (ADR-0343)', () => {
    it('says it is asking the sources, then draws its views over them', async () => {
        const reading = board();
        const server = served(reading);
        const { element, mounts, view } = draw({ board: reading, data: {}, sources: server.sources });

        expect(element.querySelector('.ex-board-binding[role="status"]')?.textContent).toBe('Asking the sources for their shapes.');
        expect(element.querySelectorAll('section')).toHaveLength(0);
        server.release();

        await vi.waitFor(() => { expect(view.bound).toBeDefined(); });
        expect(element.querySelector('.ex-board-binding')).toBeNull();
        expect(element.querySelectorAll('section')).toHaveLength(3);
        expect(only(mounts.grids).options.source).toBe(gridView(view, 'blotter').fetched);
        expect(only(mounts.charts).options.definition.series.map(series => series.points.length)).toEqual([3]);
        expect(server.counts.shape).toBe(1);
    });

    it('drops a binding over sources the options changed under, and lets go of its questions (spine rule 6)', () => {
        const reading = board();
        const server = served(reading);
        const { view, mounts, options } = draw({ board: reading, data: {}, sources: server.sources });

        view.update({ ...options, data: data(reading), sources: undefined });

        expect(server.counts.shapeAbandoned).toBe(1);
        expect(only(mounts.grids).options.source).toBe(gridView(view, 'blotter').source);
        expect(gridView(view, 'blotter').fetched).toBeUndefined();
    });

    it('says under a pivot over a server that its source did not answer for the cells selected (ADR-0345)', async () => {
        const reading = board(', "links": [{"from": "byDesk", "to": ["curve"], "on": ["desk"]}]');
        const server = served(reading, true);
        const { element, mounts, view } = draw({ board: reading, data: {}, sources: server.sources });
        server.release();
        await vi.waitFor(() => { expect(view.bound).toBeDefined(); });
        const status = element.querySelector('[data-ex-view="byDesk"] .ex-board-link-status');

        call(only(mounts.pivots).options.onSelectionChanged, { selection: rows(0), items: [{ row: [{ field: 'desk', item: textItemKey('Rates') }], column: [] }] });

        await vi.waitFor(() => { expect(status?.textContent).toBe('The source did not answer for the cells selected, so they narrow nothing: the server is down'); });
        expect(server.counts.cells).toBe(1);
    });

    it('says under a fetched grid that a selection reaching rows not fetched hands nothing on (ADR-0343)', async () => {
        const server = served(linked);
        const { element, mounts, view } = draw({ board: linked, data: {}, sources: server.sources });
        server.release();
        await vi.waitFor(() => { expect(view.bound).toBeDefined(); });
        const unread = 'The selection reaches rows not fetched from the source, so it narrows nothing and hands nothing on.';
        const button = element.querySelector<HTMLButtonElement>('button[data-ex-output="chosen"]');
        await vi.waitFor(() => { expect(gridView(view, 'blotter').source.snapshot().window).toHaveLength(3); });

        call(only(mounts.grids).options.onSelectionChanged, rows(1, 900));
        button?.click();

        expect(element.querySelector('[data-ex-view="blotter"] .ex-board-link-status')?.textContent).toBe(unread);
        expect(element.querySelector('.ex-board-confirm-status')?.textContent).toBe(unread);
    });
});

function gridView(view: BoardDrawing, id: string) {
    const found = view.bound?.view(id);
    if (found?.kind !== 'grid') throw new Error(`'${id}' is not a grid.`);
    return found;
}
