// Exposure profile (ADR-0280): ExChart.Showcases' ExposureShowcase under the built-in Chrome. A netting set's
// EPE, ENE, CVA and PFE by date, before and after a proposed trade, as a risk system reported them. Select the
// Date and the figures and choose Chart from the grid's context menu; each chart opens in a tab of its own.
// "Recalculate" swaps in the profile at a larger hedge, and every open chart follows its rows. The figures are
// fixed values (data.ts); nothing here computes one.
import { useEffect, useRef, useState, type ReactElement } from 'react';
import { GridSource, type GridColumn, type GridHeaderGroup, type InMemoryGridSource } from '@ex-grid/core';
import { GridChartBridge, gridChartCommandLabel, type GridChart } from '@ex-grid/chart';
import { ExGrid, type ExGridApi } from '@ex-grid/react';
import { Temporal } from 'temporal-polyfill';
import { DemoNav } from '../demo-nav';
import { ChartTabs, ChartTabsModel } from './chart-tabs';
import { exposureDataHedges, exposureDataProfiles, type ExposureRow } from './data';
import { dayNumberOf, fixed, grouped, numberOf } from './format';
import { ShowcaseShell } from './shell';

const chartWidth = 1000, chartHeight = 380;

/** `ToString("#,##0.00;-#,##0.00")`; a CVA the as-of date has none of is an empty cell. */
const money = (v: unknown): string => grouped(numberOf(v), 2);

type Figure = Exclude<keyof ExposureRow, 'date'>;
const figure = (name: string, field: Figure, header: string): GridColumn<ExposureRow> =>
    ({ name, type: 'number', value: (r: ExposureRow) => r[field], header, format: money });

const columns: readonly GridColumn<ExposureRow>[] = Object.freeze([
    { name: 'Date', type: 'date', dateType: 'dateOnly', value: (r: ExposureRow) => r.date, header: 'Date' },
    figure('EpeBefore', 'epeBefore', 'Before'), figure('EpeAfter', 'epeAfter', 'After'), figure('EpeDiff', 'epeDiff', 'Diff'),
    figure('EneBefore', 'eneBefore', 'Before'), figure('EneAfter', 'eneAfter', 'After'), figure('EneDiff', 'eneDiff', 'Diff'),
    figure('CvaBefore', 'cvaBefore', 'Before'), figure('CvaAfter', 'cvaAfter', 'After'), figure('CvaDiff', 'cvaDiff', 'Diff'),
    figure('PfeBefore', 'pfeBefore', 'Before'), figure('PfeAfter', 'pfeAfter', 'After'), figure('PfeDiff', 'pfeDiff', 'Diff'),
]);

const groups: readonly GridHeaderGroup[] = Object.freeze([
    { label: 'EPE(0)', columns: ['EpeBefore', 'EpeAfter', 'EpeDiff'] },
    { label: 'ENE(0)', columns: ['EneBefore', 'EneAfter', 'EneDiff'] },
    { label: 'CVA', columns: ['CvaBefore', 'CvaAfter', 'CvaDiff'] },
    { label: 'PFE(97.5%)', columns: ['PfeBefore', 'PfeAfter', 'PfeDiff'] },
]);

/** A row's Row Key: its date, as the Blazor Showcase keys by `r.Date`. */
const keyOf = (r: ExposureRow): string => r.date.toString();

function profile(hedge: number): readonly ExposureRow[] {
    const rows = exposureDataProfiles[hedge];
    if (rows === undefined) throw new Error(`There is no profile at hedge ${String(hedge)}.`);
    return rows;
}

/** What the page holds beside React's state, made once. */
interface Held {
    rows: readonly ExposureRow[];
    readonly source: InMemoryGridSource<ExposureRow>;
    readonly tabs: ChartTabsModel;
    readonly bridge: GridChartBridge<ExposureRow>;
}

/** The screen opens on EPE before and after the trade, at every date: the chart a user makes first. */
function initial(rows: readonly ExposureRow[]): GridChart {
    const positions = rows.map(r => ({ key: keyOf(r), x: dayNumberOf(r.date) }));
    return {
        orientation: 'seriesInColumns', axisColumn: 'Date',
        series: [
            { type: 'column', id: 'EpeBefore', panel: 'EPE(0)', column: 'EpeBefore', rows: positions },
            { type: 'column', id: 'EpeAfter', panel: 'EPE(0)', column: 'EpeAfter', rows: positions },
        ],
    };
}

/** The page /chart-showcase/exposure of the Blazor hosts, under the built-in Chrome. */
export function ExposureShowcase(): ReactElement {
    const [hedge, setHedge] = useState(0);
    const grid = useRef<ExGridApi<ExposureRow>>(null);
    const held = useRef<Held | undefined>(undefined);
    if (held.current === undefined) {
        const heldNow = (): Held => {
            if (held.current === undefined) throw new Error('Internal: the page asked for its rows before it held them.');
            return held.current;
        };
        const rows = profile(0);
        const source = GridSource.from(rows, { rowKey: keyOf });
        const tabs = new ChartTabsModel();
        const bridge = new GridChartBridge<ExposureRow>({
            declarations: {
                rowKey: keyOf,
                rowOf: key => heldNow().rows.find(r => keyOf(r) === key),
                // The page says what each figure is (ADR-0253): CVA is an amount per bucket, the rest are levels.
                kindOf: column => (column.startsWith('Cva') ? 'amountPerPeriod' : 'level'),
            },
            grid: () => ({ columns, headerGroups: groups, source }),
            targets: tabs.targets,
            onChart: tabs.open,
            onAdded: tabs.added,
            onRefused: tabs.refused,
            placeSelection: ({ range, focus, rowSequenceVersion }) => {
                if (grid.current === null) throw new Error('Internal: the grid is not mounted.');
                return grid.current.placeSelection(range, focus, rowSequenceVersion);
            },
        });
        held.current = { rows, source, tabs, bridge };
    }
    const { source, tabs, bridge } = held.current;
    useEffect(() => {
        if (tabs.snapshot().charts.length === 0) tabs.open(initial(profile(0)));
    }, [tabs]);

    const recalculate = (): void => {
        const next = (hedge + 1) % exposureDataHedges.length;
        const rows = profile(next);
        const now = held.current;
        if (now === undefined) throw new Error('Internal: the page has not made its bridge.');
        now.rows = rows;
        source.replaceAll(rows);
        setHedge(next);
    };
    const hedgeValue = exposureDataHedges[hedge];
    if (hedgeValue === undefined) throw new Error(`There is no hedge ${String(hedge)}.`);

    return (
        <>
            <DemoNav route="chart-showcase/exposure" />
            <ShowcaseShell app="Counterparty Exposure" crumb="Counterparty risk · Exposure profile" tools={
                <>
                    <span className="xs-crumb">Hedge ×{fixed(hedgeValue, 1)}</span>
                    <button type="button" className="xs-button xs-recalculate" onClick={recalculate}>Recalculate</button>
                </>
            }>
                <p className="xs-note">
                    Select the Date and the figures to plot, then right-click and choose Chart. CVA is the amount of
                    each bucket, drawn per year over its bucket; a Diff that crosses zero gets a panel of its own.
                </p>
                <div className="xs-panel">
                    <ExGrid ref={grid} hideColumnMenu columns={columns} source={source} headerGroups={groups} pinnedColumnCount={1}
                        contextCommands={bridge.commands} commandLabel={gridChartCommandLabel}
                        viewportHeight={260} viewportWidth="stretch" temporal={Temporal} />
                </div>
                <ChartTabs tabs={tabs} bridge={bridge} chartWidth={chartWidth} chartHeight={chartHeight} fileName="exposure-profile" />
            </ShowcaseShell>
        </>
    );
}
