// Layer 2 (ADR-0370 section 1): the selection bridge (ADR-0251, ADR-0258), read against a grid handed in as
// its state. The facts are tests/ExChart.Components/SelectionBridgeTests.cs's, moved as they stand: the bridge
// reads the grid's columns and the Consumer's functions, which a shared case cannot hold, and its C# side lives
// in ExChart, which the chart cases' C# runner (ExChart.Engine.Tests) does not reference.
import { describe, expect, it } from 'vitest';
import { GridSource, type GridColumn, type GridContextMenuContext, type GridHeaderGroup, type SelectionRange } from '@ex-grid/core';
import { Temporal } from 'temporal-polyfill';
import { dayNumber } from '../src/day-numbers';
import { ChartRefusedError } from '../src/values';
import {
    GridChartBridge, gridChartCommandIds, gridChartCommandLabel, type GridSelectionPlacement,
} from '../src/bridge/grid-chart-bridge';
import { gridChartRowKeys, type ChartTarget, type ColumnSeries, type GridChart } from '../src/bridge/grid-chart';
import type { GridChartDeclarations } from '../src/bridge/grid-chart-declarations';
import { drawGridChart, readSelection, type BridgeGridState, type SelectionReading } from '../src/bridge/selection-reader';

// A curve history: a date down the side, tenors across, and an exposure profile beside them.
interface Day {
    readonly date: Temporal.PlainDate;
    readonly note: string;
    readonly epe: number;
    readonly ene: number;
    readonly diff: number;
    readonly y1: number;
    readonly y5: number;
    readonly recovery: number;
}

const d0 = Temporal.PlainDate.from('2026-01-02');
const day = (offset: number): Temporal.PlainDate => d0.add({ days: offset });
const keyOf = (date: Temporal.PlainDate): string => date.toString();
const dayNumberOf = (date: Temporal.PlainDate): number => dayNumber(date.year, date.month, date.day);

const columns: readonly GridColumn<Day>[] = [
    { name: 'date', type: 'date', dateType: 'dateOnly', value: r => r.date, header: 'Date' },
    { name: 'note', type: 'text', value: r => r.note, header: 'Note' },
    { name: 'epe', type: 'number', value: r => r.epe, header: 'EPE' },
    { name: 'ene', type: 'number', value: r => r.ene, header: 'ENE' },
    { name: 'diff', type: 'number', value: r => r.diff, header: 'Diff' },
    { name: 'y1', type: 'number', value: r => r.y1, header: '1Y' },
    { name: 'y5', type: 'number', value: r => r.y5, header: '5Y' },
    { name: 'recovery', type: 'number', value: r => r.recovery, header: 'Recovery' },
];

const groups: readonly GridHeaderGroup[] = [
    { label: 'Exposure', columns: ['epe', 'ene', 'diff'] },
    { label: 'Rates', columns: ['y1', 'y5'] },
];

/** The rows, the version and the declarations of one test, as the C# test class holds them per instance. */
class Fixture {
    rows: Day[] = Array.from({ length: 4 }, (_, i) => ({
        date: day(i), note: `n${i}`, epe: 10 + i, ene: -(5 + i), diff: i % 2 === 0 ? 1 : -1, y1: 1 + i / 10, y5: 2 + i / 10, recovery: 0.4,
    }));

    readonly version = 7;

    declarations(more: Partial<GridChartDeclarations<Day>> = {}): GridChartDeclarations<Day> {
        return {
            rowKey: r => keyOf(r.date),
            rowOf: k => this.rows.find(r => keyOf(r.date) === k),
            kindOf: () => 'level',
            positionOf: c => (c === 'y1' ? 1 : c === 'y5' ? 5 : undefined),
            positionTitle: 'Tenor (years)',
            rowName: r => r.date.toString(),
            ...more,
        };
    }

    state(): BridgeGridState<Day> {
        return { columns, groups, rowSequenceVersion: this.version, window: this.rows, windowStart: 0, versionNow: () => this.version };
    }

    read(ranges: readonly SelectionRange[], options: {
        readonly orientation?: 'seriesInColumns' | 'seriesInRows'; readonly target?: ChartTarget; readonly version?: number;
        readonly declarations?: GridChartDeclarations<Day>;
    } = {}): Promise<SelectionReading> {
        const { version = this.version, declarations = this.declarations() } = options;
        return readSelection(this.state(), ranges, version, declarations, options.orientation, options.target);
    }

    draw(chart: GridChart, declarations = this.declarations()): ReturnType<typeof drawGridChart> {
        return drawGridChart(columns, groups, chart, declarations);
    }
}

const r = (topRow: number, leftColumn: number, rowCount: number, columnCount: number): SelectionRange =>
    ({ topRow, leftColumn, rowCount, columnCount });

function chartOf(reading: SelectionReading): GridChart {
    if (reading.chart === undefined) throw new Error(`The selection was refused: ${reading.refusal}`);
    return reading.chart;
}

function refusalOf(reading: SelectionReading): string {
    if (reading.refusal === undefined) throw new Error('The selection was charted, and a refusal was expected.');
    return reading.refusal;
}

function columnSeries(chart: GridChart, index: number): ColumnSeries {
    const s = chart.series[index];
    if (s?.type !== 'column') throw new Error(`Series ${index} is not a column.`);
    return s;
}

/** A bridge over the fixture's rows, held by an in-memory source as a grid holds them. */
function bridgeOver(f: Fixture, more: { readonly targets?: readonly ChartTarget[]; readonly placed?: GridSelectionPlacement[]; readonly refused?: string[] } = {}): GridChartBridge<Day> {
    const source = GridSource.from(f.rows);
    const { targets = [], placed = [], refused = [] } = more;
    return new GridChartBridge<Day>({
        declarations: f.declarations(),
        grid: () => ({ columns, headerGroups: groups, source }),
        targets: () => targets,
        onRefused: why => { refused.push(why); },
        placeSelection: placement => { placed.push(placement); return true; },
    });
}

function context(f: Fixture, selection: readonly SelectionRange[], version = 0): GridContextMenuContext<Day> {
    const row = f.rows[0];
    if (row === undefined) throw new Error('The fixture holds no rows.');
    return { row, column: 'date', columnType: 'date', selection, rowSequenceVersion: version, commands: [] };
}

describe('the selection bridge (ADR-0251, ADR-0258)', () => {
    it('ADR-0258: an axis column charts series in columns, each a column at the Row Keys it was made from', async () => {
        const f = new Fixture();
        const chart = chartOf(await f.read([r(0, 0, 3, 1), r(0, 2, 3, 2)]));
        expect(chart.orientation).toBe('seriesInColumns');
        expect(chart.axisColumn).toBe('date');
        const epe = columnSeries(chart, 0);
        expect(epe.column).toBe('epe');
        expect(epe.panel).toBe('Exposure');
        expect(epe.rows.map(p => p.key)).toEqual([keyOf(day(0)), keyOf(day(1)), keyOf(day(2))]);
        const definition = f.draw(chart);
        expect(definition.axis.values).toBe('dates');
        expect(definition.series[0]?.name).toBe('Exposure · EPE');
        expect(definition.series[0]?.points[0]?.y).toBe(10);
        expect(definition.series[0]?.points[0]?.x).toBe(dayNumberOf(d0));
    });

    it('ADR-0251, spine rule 6: a selection taken under another Row Sequence Version is refused', async () => {
        const f = new Fixture();
        const reading = await f.read([r(0, 0, 3, 3)], { version: 6 });
        expect(reading.chart).toBeUndefined();
        expect(refusalOf(reading)).toBe('The rows were reordered after the selection was made. Select again.');
    });

    it('ADR-0258: a selection that declares neither orientation is refused by name', async () => {
        const f = new Fixture();
        expect(refusalOf(await f.read([r(0, 2, 3, 2)]))).toContain('no axis column');
    });

    it('ADR-0251: two axis columns, or a chosen text column, are refused by name', async () => {
        const f = new Fixture();
        const two: GridChartDeclarations<Day> = { rowKey: row => keyOf(row.date), rowOf: () => undefined, axisColumns: ['epe'] };
        const axes = await f.read([r(0, 0, 3, 3)], { declarations: two, orientation: 'seriesInColumns' });
        expect(refusalOf(axes)).toContain('2 axis columns (Date, EPE)');
        expect(refusalOf(await f.read([r(0, 0, 3, 3)]))).toBe('Note cannot be plotted: not a number column.');
    });

    it('ADR-0251: one cell charts the Window, leaving out the text column nobody chose', async () => {
        const f = new Fixture();
        const chart = chartOf(await f.read([r(1, 2, 1, 1)], { orientation: 'seriesInColumns' }));
        expect(chart.series.map(s => s.id)).toEqual(['epe', 'ene', 'diff', 'y1', 'y5', 'recovery']);
        chart.series.forEach((_, i) => { expect(columnSeries(chart, i).rows).toHaveLength(4); });
    });

    it('ADR-0258: the menu offers two items only when a selection declares both orientations', () => {
        const f = new Fixture();
        const targets: ChartTarget[] = [];
        const bridge = bridgeOver(f, { targets });
        const ids = (...ranges: SelectionRange[]): string[] => bridge.commands(context(f, ranges)).map(c => c.id);
        expect(ids(r(0, 0, 3, 3))).toEqual([gridChartCommandIds.chart]);
        expect(ids(r(0, 5, 3, 2))).toEqual([gridChartCommandIds.chart]);
        expect(ids(r(0, 0, 3, 7))).toEqual([gridChartCommandIds.inColumns, gridChartCommandIds.inRows]);
        targets.push({ name: 'Exposure', chart: { orientation: 'seriesInColumns', axisColumn: 'date', series: [] } });
        expect(ids(r(0, 0, 3, 3)).map(gridChartCommandLabel)).toEqual(['Chart', 'Add to chart: Exposure']);
        expect(gridChartCommandLabel(gridChartCommandIds.inRows)).toBe('Series in rows');
        expect(gridChartCommandLabel('copy')).toBeUndefined();
    });

    it('ADR-0253, ADR-0258: a column declared away from zero is read away from zero, in both orientations', async () => {
        const f = new Fixture();
        const declarations = f.declarations({ fromZeroOf: c => (c === 'y1' || c === 'y5' ? false : undefined) });
        const inColumns = chartOf(await f.read([r(0, 0, 3, 1), r(0, 5, 3, 1), r(0, 2, 3, 1)], { declarations, orientation: 'seriesInColumns' }));
        const byId = new Map(f.draw(inColumns, declarations).series.map(s => [s.id, s]));
        expect(byId.get('y1')?.fromZero).toBe(false);
        expect(byId.get('y1')?.reachesZero).toBe(false);
        expect(byId.get('epe')?.fromZero).toBeUndefined();
        // EPE keeps the Series Kind's default.
        expect(byId.get('epe')?.reachesZero).toBe(true);

        const inRows = chartOf(await f.read([r(1, 5, 2, 2)], { declarations }));
        expect(f.draw(inRows, declarations).series.map(s => s.reachesZero)).toEqual([false, false]);
    });

    it('ADR-0258, spine rule 1: a curve\'s columns declared both from zero and away from it are refused by name', async () => {
        const f = new Fixture();
        const reading = await f.read([r(1, 5, 2, 2)], { declarations: f.declarations({ fromZeroOf: c => (c === 'y1' ? false : undefined) }) });
        expect(reading.chart).toBeUndefined();
        expect(refusalOf(reading)).toBe('Rates are declared both from zero (5Y) and away from it (1Y), so they are not one curve.');
    });

    it('ADR-0253, ADR-0258: declarations are compared as they are drawn: absent beside true is alike for a Level', async () => {
        const f = new Fixture();
        const declarations = f.declarations({ fromZeroOf: c => (c === 'y1' ? true : undefined) });
        const chart = chartOf(await f.read([r(1, 5, 2, 2)], { declarations }));
        expect(f.draw(chart, declarations).series.map(s => s.reachesZero)).toEqual([true, true]);
    });

    it('ADR-0258, spine rule 1: a curve whose declarations changed after it was made is refused when drawn, not drawn by one column\'s', async () => {
        const f = new Fixture();
        const chart = chartOf(await f.read([r(1, 5, 2, 2)], { declarations: f.declarations({ fromZeroOf: () => false }) }));
        const changed = f.declarations({ fromZeroOf: c => (c === 'y1' ? false : undefined) });
        expect(() => f.draw(chart, changed)).toThrow(ChartRefusedError);
        expect(() => f.draw(chart, changed)).toThrow('now declared both from zero and away from it');
    });

    it('ADR-0253: an amount per period declared away from zero is refused by name when the selection is read', async () => {
        const f = new Fixture();
        const declarations = f.declarations({ kindOf: () => 'amountPerPeriod', fromZeroOf: () => false });
        const reading = await f.read([r(0, 0, 3, 1), r(0, 2, 3, 1)], { declarations, orientation: 'seriesInColumns' });
        expect(reading.chart).toBeUndefined();
        expect(refusalOf(reading).startsWith('\'Exposure · EPE\' is an Amount per period')).toBe(true);
    });

    it('ADR-0253: a column with no Series Kind declared is refused by name when the selection is read', async () => {
        const f = new Fixture();
        const declarations = f.declarations({ kindOf: c => (c === 'ene' ? undefined : 'level') });
        const reading = await f.read([r(0, 0, 3, 1), r(0, 2, 3, 2)], { declarations });
        expect(refusalOf(reading)).toBe('\'Exposure · ENE\' declares no Series Kind: say whether it is a Level, a Rate over an interval or an Amount per period.');
    });

    it('ADR-0258: series in rows, a row across the columns at declared positions', async () => {
        const f = new Fixture();
        const chart = chartOf(await f.read([r(1, 5, 2, 2)]));
        expect(chart.orientation).toBe('seriesInRows');
        const definition = f.draw(chart);
        expect(definition.axis.title).toBe('Tenor (years)');
        expect(definition.axis.named).toEqual([[1, '1Y'], [5, '5Y']]);
        expect(definition.series[0]?.name).toBe('2026-01-03 · Rates');
        expect(definition.series[0]?.points.map(p => p.x)).toEqual([1, 5]);
        expect(chart.series[0]?.id).toBe('2026-01-03 · Rates');
    });

    it('ADR-0251: a number column with no declared position is refused by name in rows', async () => {
        const f = new Fixture();
        expect(refusalOf(await f.read([r(0, 5, 2, 3)]))).toBe('Recovery has no declared position on the Tenor (years) axis, so it cannot be drawn here.');
    });

    it('ADR-0258: a recalculated value is redrawn; a row gone is a gap with a Note; a new row is not added', async () => {
        const f = new Fixture();
        const chart = chartOf(await f.read([r(0, 0, 3, 1), r(0, 2, 3, 1)]));
        const [first] = f.rows;
        if (first === undefined) throw new Error('The fixture holds no rows.');
        f.rows = [{ ...first, epe: 99 }, { ...first, date: day(-1) }, ...f.rows.slice(2)];
        const definition = f.draw(chart);
        const points = definition.series[0]?.points;
        expect(points).toHaveLength(3);
        expect(points?.[0]?.y).toBe(99);
        expect(points?.[1]?.y).toBeNull();
        expect(points?.[1]?.x).toBe(dayNumberOf(day(1)));
        expect(definition.notes).toEqual(['1 of the selected rows no longer exists.']);
        expect(gridChartRowKeys(chart)).toEqual(new Set([keyOf(day(0)), keyOf(day(1)), keyOf(day(2))]));
    });

    it('ADR-0258, M3-Q7: values are read by Row Key from the Consumer\'s data, not from the Window', async () => {
        const f = new Fixture();
        const chart = chartOf(await f.read([r(0, 0, 3, 1), r(0, 2, 3, 1)]));
        // The grid narrowed to one row: the chart reads the Consumer's data, which still holds all three.
        const definition = drawGridChart(columns, groups, chart, f.declarations());
        expect(definition.notes).toEqual([]);
        expect(definition.series[0]?.points.every(p => p.y !== null)).toBe(true);
    });

    it('ADR-0251: a Diff, negatives beside positives, goes to a panel of its own', async () => {
        const f = new Fixture();
        const chart = chartOf(await f.read([r(0, 0, 4, 1), r(0, 2, 4, 3)]));
        expect(chart.series.map(s => s.panel)).toEqual(['Exposure', 'Exposure', 'Exposure · Diff']);
    });

    it('ADR-0258, U7, U8: an added series keeps its own rows; one already in the chart, or of another axis, is refused', async () => {
        const f = new Fixture();
        const made = chartOf(await f.read([r(0, 0, 2, 1), r(0, 2, 2, 1)]));
        const target: ChartTarget = { name: 'Exposure', chart: made };
        const added = chartOf(await f.read([r(2, 3, 2, 1)], { target }));
        expect(added.series.map(s => s.id)).toEqual(['epe', 'ene']);
        expect(columnSeries(added, 1).rows).toHaveLength(2);
        expect(columnSeries(added, 1).rows[0]?.key).toBe(keyOf(day(2)));

        expect(refusalOf(await f.read([r(2, 2, 2, 1)], { target }))).toBe('EPE is already in \'Exposure\'.');
        expect(refusalOf(await f.read([r(0, 5, 2, 2)], { target })))
            .toBe('The selection charts curves across declared positions, but \'Exposure\' is drawn along Date.');
    });

    it('ADR-0252: while nothing it reads changed, the bridge hands back the same definition, so the chart skips its render', async () => {
        const f = new Fixture();
        const bridge = bridgeOver(f);
        const chart = chartOf(await f.read([r(0, 0, 3, 1), r(0, 2, 3, 1)]));
        const first = bridge.draw(chart);
        expect(bridge.draw(chart)).toBe(first);
        const [row] = f.rows;
        if (row === undefined) throw new Error('The fixture holds no rows.');
        f.rows = [{ ...row, epe: 1 }, ...f.rows.slice(1)];
        expect(bridge.draw(chart)).not.toBe(first);
    });

    it('ADR-0258, M3-Q6: a click on a point names its row and column', async () => {
        const f = new Fixture();
        const bridge = bridgeOver(f);
        const history = chartOf(await f.read([r(0, 0, 3, 1), r(0, 3, 3, 1)]));
        expect(bridge.cellOf(history, { seriesId: 'ene', position: dayNumberOf(day(1)) })).toEqual({ rowKey: keyOf(day(1)), column: 'ene' });
        const curves = chartOf(await f.read([r(2, 5, 1, 2)]));
        expect(curves.series.map(s => s.id)).toEqual(['2026-01-04 · Rates']);
        expect(bridge.cellOf(curves, { seriesId: '2026-01-04 · Rates', position: 5 })).toEqual({ rowKey: keyOf(day(2)), column: 'y5' });
    });

    it('ADR-0258, M3-Q6: a point is shown in the grid through the Consumer\'s placement, under the version it was found in', async () => {
        const f = new Fixture();
        const placed: GridSelectionPlacement[] = [];
        const refused: string[] = [];
        const bridge = bridgeOver(f, { placed, refused });
        const history = chartOf(await f.read([r(0, 0, 3, 1), r(0, 3, 3, 1)]));
        expect(await bridge.showInGrid(history, { seriesId: 'ene', position: dayNumberOf(day(1)) })).toBe(true);
        expect(placed).toEqual([{ range: r(1, 3, 1, 1), focus: { row: 1, column: 3 }, rowSequenceVersion: 0 }]);
        expect(await bridge.showCellInGrid({ rowKey: keyOf(day(1)), column: 'gone' })).toBe(false);
        expect(refused).toEqual(['gone is not among the grid\'s columns.']);
    });

    it('ADR-0258: the menu\'s command charts the selection and hands the chart over, or says why it did not', async () => {
        const f = new Fixture();
        const charts: GridChart[] = [];
        const refused: string[] = [];
        const source = GridSource.from(f.rows, { rowKey: row => keyOf(row.date) });
        const bridge = new GridChartBridge<Day>({
            declarations: f.declarations(),
            grid: () => ({ columns, headerGroups: groups, source }),
            onChart: chart => { charts.push(chart); },
            onRefused: why => { refused.push(why); },
        });
        const [chart] = bridge.commands(context(f, [r(0, 5, 2, 2)]));
        chart?.invoke();
        await expect.poll(() => charts.length).toBe(1);
        expect(charts[0]?.orientation).toBe('seriesInRows');
        // A command run after the order moved under the menu is refused: its Selection names other rows now.
        const [stale] = bridge.commands(context(f, [r(0, 5, 2, 2)]));
        source.replaceAll(f.rows.slice(1));
        stale?.invoke();
        await expect.poll(() => refused.length).toBe(1);
        expect([charts.length, refused]).toEqual([1, ['The rows were reordered after the selection was made. Select again.']]);
    });

    it('ADR-0251, spine rule 6: an order that moves while rows outside the Window are fetched is refused, not read under two orders', async () => {
        const f = new Fixture();
        const held = { version: f.version };
        const state: BridgeGridState<Day> = {
            ...f.state(), window: f.rows.slice(0, 1),
            fetch: range => { held.version += 1; return Promise.resolve(f.rows.slice(range.start, range.start + range.count)); },
            versionNow: () => held.version,
        };
        const reading = await readSelection(state, [r(0, 0, 3, 3)], f.version, f.declarations(), undefined, undefined);
        expect(refusalOf(reading)).toBe('The rows were reordered after the selection was made. Select again.');
    });

    it('ADR-0258: two targets of one name are thrown on, since a target is told apart by its name', async () => {
        const f = new Fixture();
        const target: ChartTarget = { name: 'Exposure', chart: chartOf(await f.read([r(0, 0, 3, 1), r(0, 3, 3, 1)])) };
        expect(() => bridgeOver(f, { targets: [target, { ...target }] }).commands(context(f, [r(0, 0, 1, 1)]))).toThrow(/Two chart targets are named 'Exposure'/);
    });

    it('ADR-0258, M3-Q6: a point whose row has gone from the chart is refused by name, not quietly dropped', async () => {
        const f = new Fixture();
        const refused: string[] = [];
        const bridge = bridgeOver(f, { refused });
        const history = chartOf(await f.read([r(0, 0, 3, 1), r(0, 3, 3, 1)]));
        expect(await bridge.showInGrid(history, { seriesId: 'ene', position: dayNumberOf(day(40)) })).toBe(false);
        expect(refused).toEqual(['That point\'s row no longer exists, so it has no cell in the grid.']);
    });
});
