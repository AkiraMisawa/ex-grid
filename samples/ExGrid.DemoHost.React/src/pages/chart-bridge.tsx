import { useRef, useState, type ReactElement } from 'react';
import { GridSource, type GridColumn, type GridHeaderGroup, type InMemoryGridSource } from '@ex-grid/core';
import { GridChartBridge, gridChartCommandLabel, type ChartTarget, type GridChart } from '@ex-grid/chart';
import { ExChart } from '@ex-grid/chart-react';
import { ExGrid, type ExGridApi } from '@ex-grid/react';
import { Temporal } from 'temporal-polyfill';
import { DemoNav } from '../demo-nav';

/** One date's rates, and a note that is not charted. */
interface CurveRow {
    readonly date: Temporal.PlainDate;
    readonly note: string;
    readonly y1: number;
    readonly y2: number;
    readonly y5: number;
    readonly y10: number;
    readonly spread: number;
}

const start = Temporal.PlainDate.from('2026-10-01');

/** The Blazor page's `ToString("0.000")` of a rate. */
function threePlaces(value: unknown): string {
    if (typeof value !== 'number') throw new TypeError(`A rate is a number, not ${typeof value}.`);
    return value.toFixed(3);
}

const columns: readonly GridColumn<CurveRow>[] = Object.freeze([
    { name: 'date', type: 'date', dateType: 'dateOnly', value: (r: CurveRow) => r.date, header: 'Date' },
    { name: 'note', type: 'text', value: (r: CurveRow) => r.note, header: 'Note' },
    { name: 'y1', type: 'number', value: (r: CurveRow) => r.y1, header: '1Y', format: threePlaces },
    { name: 'y2', type: 'number', value: (r: CurveRow) => r.y2, header: '2Y', format: threePlaces },
    { name: 'y5', type: 'number', value: (r: CurveRow) => r.y5, header: '5Y', format: threePlaces },
    { name: 'y10', type: 'number', value: (r: CurveRow) => r.y10, header: '10Y', format: threePlaces },
    { name: 'spread', type: 'number', value: (r: CurveRow) => r.spread, header: 'Spread' },
]);

const groups: readonly GridHeaderGroup[] = Object.freeze([{ label: 'Rates', columns: ['y1', 'y2', 'y5', 'y10'] }]);

const tenors: ReadonlyMap<string, number> = new Map([['y1', 1], ['y2', 2], ['y5', 5], ['y10', 10]]);

/** A row's Row Key: its date, as the Blazor page keys by `r.Date`. */
const keyOf = (r: CurveRow): string => r.date.toString();

function row(i: number, recalculation: number): CurveRow {
    return Object.freeze({
        date: start.add({ days: i }),
        note: i % 3 === 0 ? 'month end' : '',
        y1: 3.0 + i * 0.01 + recalculation * 0.1,
        y2: 3.1 + i * 0.01 + recalculation * 0.1,
        y5: 3.3 - i * 0.02 + recalculation * 0.1,
        y10: 3.6 - i * 0.01 + recalculation * 0.1,
        spread: 40 + i,
    });
}

interface NamedChart {
    readonly name: string;
    readonly chart: GridChart;
}

/** What the page holds beside React's state, made once: the rows and charts the bridge reads, and the bridge. */
interface Held {
    rows: readonly CurveRow[];
    charts: readonly NamedChart[];
    readonly source: InMemoryGridSource<CurveRow>;
    readonly bridge: GridChartBridge<CurveRow>;
}

/**
 * The page /chart-bridge of the Blazor hosts (ticket 20, ADR-0258): a curve history, dates down and tenors
 * across, charted from the grid's context menu. The page is the Consumer: it adds the bridge's commands to the
 * grid in one line, shows each chart it is handed, and registers every open chart as a target of "Add to chart".
 * "Recalculate" moves every rate; "Delete the first row" removes a row the charts were made from.
 * A click on a point moves the grid's Focus to its cell, through the grid's own placement (ADR-0050 item 4),
 * which refuses a request written under an order the grid no longer shows.
 */
export function ChartBridge(): ReactElement {
    const [charts, setCharts] = useState<readonly NamedChart[]>([]);
    const [refusal, setRefusal] = useState('');
    const [, setRows] = useState<readonly CurveRow[]>([]);
    const recalculations = useRef(0);
    const held = useRef<Held | undefined>(undefined);
    const grid = useRef<ExGridApi<CurveRow>>(null);
    if (held.current === undefined) {
        const rows: readonly CurveRow[] = Object.freeze(Array.from({ length: 8 }, (_, i) => row(i, 0)));
        const source = GridSource.from(rows, { rowKey: keyOf });
        const show = (next: readonly NamedChart[]): void => {
            holder.charts = next;
            setRefusal('');
            setCharts(next);
        };
        const bridge = new GridChartBridge<CurveRow>({
            declarations: {
                rowKey: keyOf,
                rowOf: key => holder.rows.find(r => keyOf(r) === key),
                kindOf: () => 'level',
                positionOf: column => tenors.get(column),
                positionTitle: 'Tenor (years)',
                rowName: r => r.date.toString(),
            },
            grid: () => ({ columns, headerGroups: groups, source }),
            targets: () => holder.charts.map((c): ChartTarget => ({ name: c.name, chart: c.chart })),
            onChart: chart => { show([...holder.charts, { name: `Chart ${holder.charts.length + 1}`, chart }]); },
            onAdded: (target, chart) => { show(holder.charts.map(c => (c.name === target.name ? { name: c.name, chart } : c))); },
            onRefused: setRefusal,
            placeSelection: ({ range, focus, rowSequenceVersion }) => {
                if (grid.current === null) throw new Error('Internal: the grid is not mounted.');
                return grid.current.placeSelection(range, focus, rowSequenceVersion);
            },
        });
        const holder: Held = { rows, charts: [], source, bridge };
        held.current = holder;
    }
    const { source, bridge } = held.current;

    // The rows the bridge reads are the page's own, by Row Key (ADR-0258): the source the grid shows is handed
    // the same list, so a recalculation keeps the order and a deleted row moves the Row Sequence Version.
    const replaceRows = (next: readonly CurveRow[]): void => {
        const holder = held.current;
        if (holder === undefined) throw new Error('Internal: the page has not made its bridge.');
        holder.rows = next;
        source.replaceAll(next);
        setRows(next);
    };
    const recalculate = (): void => {
        recalculations.current++;
        const holder = held.current;
        if (holder === undefined) throw new Error('Internal: the page has not made its bridge.');
        replaceRows(Object.freeze(holder.rows.map(r => row(start.until(r.date).days, recalculations.current))));
    };
    const dropRow = (): void => {
        const holder = held.current;
        if (holder === undefined) throw new Error('Internal: the page has not made its bridge.');
        if (holder.rows.length === 0) return;
        replaceRows(Object.freeze(holder.rows.slice(1)));
    };

    return (
        <>
            <DemoNav route="chart-bridge" />
            <h1>Charting a selection</h1>
            <p>
                Select cells and open the context menu (ADR-0258). A selection with the Date column charts each
                column as a series along the dates; one of tenor columns charts each row as a curve across the tenors;
                one holding both offers both. A chart keeps the rows it was made from: a recalculated rate is redrawn,
                a deleted row becomes a gap with a note, and a new row is never added. Click a point to move the grid
                to its cell.
            </p>
            <ExGrid
                ref={grid}
                columns={columns}
                source={source}
                headerGroups={groups}
                contextCommands={bridge.commands}
                commandLabel={gridChartCommandLabel}
                pinnedColumnCount={1}
                viewportHeight={260}
                viewportWidth={760}
                // The polyfill this page's dates are, which a date typed into a filter is made with.
                temporal={Temporal} />
            <p>
                <button type="button" id="recalculate" onClick={recalculate}>Recalculate</button>
                {' '}
                <button type="button" id="drop-row" onClick={dropRow}>Delete the first row</button>
            </p>
            <p id="bridge-refusal" role="status">{refusal}</p>
            {charts.map(({ name, chart }) => (
                <section key={name} className="bridge-chart" data-chart={name}>
                    <h2>{name}</h2>
                    <ExChart definition={bridge.draw(chart)} title={name} width={760} height={280}
                        onPointClick={click => { void bridge.showInGrid(chart, click); }} />
                </section>
            ))}
        </>
    );
}
