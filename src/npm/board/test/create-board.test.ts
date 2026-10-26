// @vitest-environment happy-dom
// A Board drawn into an element (layer 2, ADR-0350 section 13). What a grid, a pivot and a chart draw is theirs, tested in
// their packages: here each is a stand-in that records what the Board hands it, and the tests judge what the Board
// decides — what it lays out, what a selection narrows, what a press hands on, which Diff it raises, what it refuses.
import { afterEach, describe, expect, it } from 'vitest';
import type { ChartHandle, ChartOptions } from '@ex-grid/chart';
import type { GridHandle, GridOptions, GridSelection } from '@ex-grid/core';
import type { Snapshot } from '@ex-grid/data';
import type { PivotHandle, PivotViewOptions } from '@ex-grid/pivot';
import { readPivotLayoutJson } from '@ex-grid/pivot-engine';
import { BoardView, type BoardMounts, type BoardOptions } from '../src/create-board';
import { boardInput, BoardDiff, readBoard, type BoardOutput, type BoardReading, type ShapedRow } from '../src/index';

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

class StandIns implements BoardMounts {
    readonly grids: Mounted<GridOptions<ShapedRow>>[] = [];
    readonly pivots: Mounted<PivotViewOptions>[] = [];
    readonly charts: Mounted<ChartOptions>[] = [];

    readonly grid = (element: HTMLElement, options: GridOptions<ShapedRow>): GridHandle<ShapedRow> => {
        const made = new Mounted(element, options);
        this.grids.push(made);
        return made.handle();
    };

    readonly pivot = (element: HTMLElement, options: PivotViewOptions): PivotHandle => {
        const made = new Mounted(element, options);
        this.pivots.push(made);
        return made.handle();
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

const drawn: BoardView[] = [];

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
    const view = new BoardView(element, full, mounts);
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

    it('redraws only its charts for another width, and empties its element when destroyed', () => {
        const { view, mounts, element, options } = draw({ board: board(), width: 1000 });
        view.update({ ...options, width: 500 });
        expect(only(mounts.charts).options.width).toBe(500);
        expect(mounts.grids).toHaveLength(1);

        view.destroy();
        expect(element.children).toHaveLength(0);
        expect(element.classList.contains('ex-board')).toBe(false);
        expect([mounts.grids[0]?.destroyed, mounts.pivots[0]?.destroyed, mounts.charts[0]?.destroyed]).toEqual([true, true, true]);
    });
});
