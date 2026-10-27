import { Temporal } from 'temporal-polyfill';
import { GridSource, type GridColumn } from '@ex-grid/core';
import { GridChartBridge, type GridChartBridgeOptions } from '@ex-grid/chart';

/** One date's zero rates, in percent. */
export interface CurveRow {
    readonly date: Temporal.PlainDate;
    readonly y1: number;
    readonly y2: number;
    readonly y5: number;
    readonly y10: number;
}

const start = Temporal.PlainDate.from('2026-10-01');

/** Ten business days of fixed sample rates. */
const rows: readonly CurveRow[] = Array.from({ length: 10 }, (_, i) => ({
    date: start.add({ days: i }), y1: 3.0 + i * 0.01, y2: 3.1 + i * 0.012, y5: 3.3 - i * 0.02, y10: 3.6 - i * 0.01,
}));

const rate = (v: unknown): string => (v as number).toFixed(3);

/** The grid's columns, made once: a new list on every render would render every row again. */
export const curveColumns: readonly GridColumn<CurveRow>[] = [
    { name: 'date', type: 'date', dateType: 'dateOnly', value: r => r.date, header: 'Date', width: 120 },
    { name: 'y1', type: 'number', value: r => r.y1, header: '1Y', format: rate },
    { name: 'y2', type: 'number', value: r => r.y2, header: '2Y', format: rate },
    { name: 'y5', type: 'number', value: r => r.y5, header: '5Y', format: rate },
    { name: 'y10', type: 'number', value: r => r.y10, header: '10Y', format: rate },
];

const tenors = new Map([['y1', 1], ['y2', 2], ['y5', 5], ['y10', 10]]);
const keyOf = (r: CurveRow): string => r.date.toString();

/** The grid's rows, keyed by date. */
export const curveSource = GridSource.from(rows, { rowKey: keyOf });

/**
 * The bridge from the grid's selection to a chart: how to find a row again by its key, what each column is (a level,
 * read away from zero so rates between 3.0% and 3.6% are not drawn flat), and where a tenor column stands along a
 * curve. `events` are where the charts it makes go; a chart follows the rows it was made from.
 */
export function curveBridge(events: Pick<GridChartBridgeOptions<CurveRow>, 'targets' | 'onChart' | 'onAdded' | 'onRefused' | 'placeSelection'>): GridChartBridge<CurveRow> {
    return new GridChartBridge<CurveRow>({
        ...events,
        declarations: {
            rowKey: keyOf,
            rowOf: key => rows.find(r => keyOf(r) === key),
            kindOf: () => 'level',
            fromZeroOf: () => false,
            positionOf: column => tenors.get(column),
            positionTitle: 'Tenor (years)',
            rowName: r => r.date.toString(),
        },
        grid: () => ({ columns: curveColumns, source: curveSource }),
    });
}
