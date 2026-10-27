// The curve history the design-system Chart Dock pages chart from (/mantine-chart and /mui-chart): eight dates, then
// 1Y, 2Y, 5Y and 10Y under "Rates", as /chart-bridge holds them, and the bridge a page makes over it.
import type { RefObject } from 'react';
import { Temporal } from 'temporal-polyfill';
import { GridSource, type GridColumn, type GridHeaderGroup, type InMemoryGridSource } from '@ex-grid/core';
import { GridChartBridge } from '@ex-grid/chart';
import type { ChartDockBridgeOptions } from '@ex-grid/chart-react';
import type { ExGridApi } from '@ex-grid/react';

/** One date's rates. */
export interface CurveRow {
    readonly date: Temporal.PlainDate;
    readonly y1: number;
    readonly y2: number;
    readonly y5: number;
    readonly y10: number;
}

const start = Temporal.PlainDate.from('2026-10-01');

function threePlaces(value: unknown): string {
    if (typeof value !== 'number') throw new TypeError(`A rate is a number, not ${typeof value}.`);
    return value.toFixed(3);
}

const rows: readonly CurveRow[] = Object.freeze(Array.from({ length: 8 }, (_, i) => Object.freeze({
    date: start.add({ days: i }),
    y1: 3.0 + i * 0.01,
    y2: 3.1 + i * 0.01,
    y5: 3.3 - i * 0.02,
    y10: 3.6 - i * 0.01,
})));

/** The grid's columns, held once, never rebuilt per render (ADR-0003). */
export const curveColumns: readonly GridColumn<CurveRow>[] = Object.freeze([
    { name: 'date', type: 'date', dateType: 'dateOnly', value: (r: CurveRow) => r.date, header: 'Date', width: 140 },
    { name: 'y1', type: 'number', value: (r: CurveRow) => r.y1, header: '1Y', format: threePlaces, width: 80 },
    { name: 'y2', type: 'number', value: (r: CurveRow) => r.y2, header: '2Y', format: threePlaces, width: 80 },
    { name: 'y5', type: 'number', value: (r: CurveRow) => r.y5, header: '5Y', format: threePlaces, width: 80 },
    { name: 'y10', type: 'number', value: (r: CurveRow) => r.y10, header: '10Y', format: threePlaces, width: 80 },
]);

/** The tenors' band. */
export const curveGroups: readonly GridHeaderGroup[] = Object.freeze([{ label: 'Rates', columns: ['y1', 'y2', 'y5', 'y10'] }]);

const tenors: ReadonlyMap<string, number> = new Map([['y1', 1], ['y2', 2], ['y5', 5], ['y10', 10]]);
const keyOf = (r: CurveRow): string => r.date.toString();

/** A page's grid source and bridge over the curves, the bridge's events the dock's and a point's click placed through
 * the grid's handle. Made once per page. */
export function curveBridge(dock: ChartDockBridgeOptions, grid: RefObject<ExGridApi<CurveRow> | null>): {
    readonly source: InMemoryGridSource<CurveRow>;
    readonly bridge: GridChartBridge<CurveRow>;
} {
    const source = GridSource.from(rows, { rowKey: keyOf });
    const bridge = new GridChartBridge<CurveRow>({
        ...dock,
        declarations: {
            rowKey: keyOf,
            rowOf: key => rows.find(r => keyOf(r) === key),
            kindOf: () => 'level',
            positionOf: column => tenors.get(column),
            positionTitle: 'Tenor (years)',
            rowName: r => r.date.toString(),
        },
        grid: () => ({ columns: curveColumns, headerGroups: curveGroups, source }),
        placeSelection: ({ range, focus, rowSequenceVersion }) => {
            if (grid.current === null) throw new Error('Internal: the grid is not mounted.');
            return grid.current.placeSelection(range, focus, rowSequenceVersion);
        },
    });
    return { source, bridge };
}
