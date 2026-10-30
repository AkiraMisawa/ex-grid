// Equity index daily bars (ADR-0300 to ADR-0304): ExChart.Showcases' IndexShowcase under the built-in Chrome.
// Ten years of an index's sessions as candles with their volume and a 25-day moving average, beside the grid of
// its rows, and the figures that carry intervals in a second view, switched in the application bar. Every figure
// is a fixed value (data.ts); the one the page computes is the moving average, as a Consumer's page would.
import { useRef, useState, type ReactElement } from 'react';
import { GridSource, type GridColumn } from '@ex-grid/core';
import {
    ChartDefinition, GridChartBridge, type ChartPointClick, type ChartRange, type ChartSeriesOptions, type ChartValue,
} from '@ex-grid/chart';
import { ExChart } from '@ex-grid/chart-react';
import { ExGrid, type ExGridApi } from '@ex-grid/react';
import { Temporal } from 'temporal-polyfill';
import { DemoNav } from '../demo-nav';
import { indexDataBars, indexDataName, intervalDataCurve, intervalDataForecasts, intervalDataStrategies, type IndexBar } from './data';
import { dayNumberOf, fixed, grouped, numberOf } from './format';
import barsSource from '../../../../dotnet/samples/ExChart.Showcases/IndexBarsView.razor?raw';
import { razorStyle, ShowcaseShell, Views } from './shell';

const name = indexDataName;
const averageDays = 25;

/** One session as the grid shows it: the bar, its change from the previous close, and the page's average. */
interface IndexRow {
    readonly bar: IndexBar;
    readonly changePct: number | null;
    readonly average: number | null;
}

/** A price in whole cents, as the decimal it was written as holds it exactly. */
const cents = (v: number): number => Math.round(v * 100);

/** `n` ÷ `d` rounded to a whole number, a tie to the even one, as C#'s `Math.Round` rounds a decimal; `d` > 0. */
function roundHalfEven(n: number, d: number): number {
    const q = Math.floor(n / d);
    const twice = 2 * (n - q * d);
    return twice > d || (twice === d && q % 2 !== 0) ? q + 1 : q;
}

// The page's own calculation, as a Consumer's would be: each close's change, and the mean of the last 25 closes,
// in whole cents so they come out as the Blazor Showcase's decimals do.
const rows: readonly IndexRow[] = ((): readonly IndexRow[] => {
    const made: IndexRow[] = [];
    let sum = 0;
    indexDataBars.forEach((bar, i) => {
        sum += cents(bar.close);
        const old = indexDataBars[i - averageDays];
        if (old !== undefined) sum -= cents(old.close);
        const previous = indexDataBars[i - 1];
        const changePct = previous === undefined
            ? null : roundHalfEven((cents(bar.close) - cents(previous.close)) * 10_000, cents(previous.close)) / 100;
        const average = i + 1 < averageDays ? null : roundHalfEven(sum, averageDays) / 100;
        made.push(Object.freeze({ bar, changePct, average }));
    });
    return Object.freeze(made);
})();

/** `ToString("N2")`, `ToString("N0")` and `ToString("+0.00'%';−0.00'%';0.00'%'")`. */
const price = (v: unknown): string => grouped(numberOf(v), 2);
const count = (v: unknown): string => grouped(numberOf(v), 0);
function percent(v: unknown): string {
    const value = numberOf(v);
    return value > 0 ? `+${fixed(value, 2)}%` : value < 0 ? `−${fixed(-value, 2)}%` : `${fixed(0, 2)}%`;
}

const columns: readonly GridColumn<IndexRow>[] = Object.freeze([
    { name: 'Date', type: 'date', dateType: 'dateOnly', value: (r: IndexRow) => r.bar.date, header: 'Date' },
    { name: 'Open', type: 'number', value: (r: IndexRow) => r.bar.open, header: 'Open', format: price },
    { name: 'High', type: 'number', value: (r: IndexRow) => r.bar.high, header: 'High', format: price },
    { name: 'Low', type: 'number', value: (r: IndexRow) => r.bar.low, header: 'Low', format: price },
    { name: 'Close', type: 'number', value: (r: IndexRow) => r.bar.close, header: 'Close', format: price },
    { name: 'Change', type: 'number', value: (r: IndexRow) => r.changePct, header: 'Chg', format: percent },
    { name: 'Volume', type: 'number', value: (r: IndexRow) => r.bar.volume, header: 'Volume', format: count },
    { name: 'Average', type: 'number', value: (r: IndexRow) => r.average, header: '25-day avg', format: price },
]);

const keyOf = (r: IndexRow): string => r.bar.date.toString();
const source = GridSource.from(rows, { rowKey: keyOf });
const byDate: ReadonlyMap<string, IndexRow> = new Map(rows.map(r => [keyOf(r), r]));

const chartPrice = (v: ChartValue): string => price(Number(v));

const average: ChartSeriesOptions = {
    id: 'average', name: `${String(averageDays)}-day average`, kind: 'level', fromZero: false, format: chartPrice,
    points: rows.map(r => ({ x: dayNumberOf(r.bar.date), y: r.average })),
};

const make = (packed: boolean): ChartDefinition => new ChartDefinition({
    axis: { values: 'dates', title: 'Date', packed },
    series: [
        {
            id: 'index', name, kind: 'priceRange', format: chartPrice,
            bars: rows.map(({ bar }) => ({ x: dayNumberOf(bar.date), open: bar.open, high: bar.high, low: bar.low, close: bar.close, volume: bar.volume })),
        },
        average,
    ],
});

const calendar = make(false);
const packedSessions = make(true);

const firstDay = Temporal.PlainDate.from('0001-01-01');
const day = (d: number | undefined): string => (d === undefined ? '…' : firstDay.add({ days: Math.trunc(d) }).toString());

// The page's rise and fall colours, IndexBarsView.razor's own rules.
const barsStyle = razorStyle(barsSource, 'IndexBarsView.razor');

const colourChoices = [['shape', 'Shape only'], ['japan', 'Red rises, blue falls'], ['west', 'Green rises, red falls']] as const;
type Colours = (typeof colourChoices)[number][0];

/** Daily bars: the sessions in a grid beside their candles, the volume below and the page's average. */
function BarsView(): ReactElement {
    const [packed, setPacked] = useState(false);
    const [colours, setColours] = useState<Colours>('shape');
    // The chart holds no View State (ADR-0272): a narrowing comes back as an event, and the page hands it back.
    const [range, setRange] = useState<ChartRange | null>(null);
    const [said, setSaid] = useState('');
    const grid = useRef<ExGridApi<IndexRow>>(null);
    const bridge = useRef<GridChartBridge<IndexRow> | undefined>(undefined);
    bridge.current ??= new GridChartBridge<IndexRow>({
        declarations: { rowKey: keyOf, rowOf: key => (typeof key === 'string' ? byDate.get(key) : undefined) },
        grid: () => ({ columns, source }),
        placeSelection: ({ range: placed, focus, rowSequenceVersion }) => {
            if (grid.current === null) throw new Error('Internal: the grid is not mounted.');
            return grid.current.placeSelection(placed, focus, rowSequenceVersion);
        },
    });
    const rangeChanged = (next: ChartRange | null): void => {
        setRange(next);
        setSaid(next === null ? '' : `${day(next.from)} to ${day(next.to)}`);
    };
    // A click on a day's candle, or on the average at a day, goes to that row (ADR-0258).
    const clicked = (click: ChartPointClick): void => {
        setSaid(`Went to ${day(click.position)}`);
        void bridge.current?.showCellInGrid({ rowKey: day(click.position), column: 'Close' });
    };
    return (
        <>
            <style>{barsStyle}</style>
            <div className="xs-tools">
                <label className="xs-packed">
                    <input type="checkbox" checked={packed} onChange={event => { setPacked(event.target.checked); }} /> Packed sessions
                </label>
                <nav className="xs-views xs-colours" aria-label="Colours">
                    {colourChoices.map(([value, label]) => (
                        <button key={value} type="button" className={value === colours ? 'xs-view xs-view-on' : 'xs-view'}
                            aria-pressed={value === colours} onClick={() => { setColours(value); }}>{label}</button>
                    ))}
                </nav>
                <button type="button" className="xs-button xs-reset" disabled={range === null} onClick={() => { rangeChanged(null); }}>Whole range</button>
                <span className="xs-spacer" />
                <span className="xs-events" role="status">{said}</span>
            </div>
            <div className="xs-side">
                <div className="xs-panel xs-side-grid">
                    <ExGrid ref={grid} hideColumnMenu columns={columns} source={source} pinnedColumnCount={1}
                        viewportHeight={520} viewportWidth="stretch" temporal={Temporal} />
                </div>
                <div className={`xs-panel xs-candles-${colours}`}>
                    <ExChart definition={packed ? packedSessions : calendar} title={name} fileName={name} width={760} height={480}
                        range={range} onRangeChanged={rangeChanged} onPointClick={clicked} />
                </div>
            </div>
        </>
    );
}

/** `ToString("0.00") + "%"` of an interval's figure. */
const intervalPercent = (v: ChartValue): string => `${fixed(Number(v), 2)}%`;

const curve = new ChartDefinition({
    axis: { values: 'numbers', title: 'Tenor (years)' },
    series: [{
        id: 'mid', name: 'Mid', kind: 'level', fromZero: false, format: intervalPercent,
        // The mid of two decimals, exactly: half their sum in cents.
        points: intervalDataCurve.map(q => ({ x: q.years, y: (cents(q.bid) + cents(q.ask)) / 200 })),
        interval: { meaning: 'Bid–Ask', bounds: intervalDataCurve.map(q => ({ x: q.years, lower: q.bid, upper: q.ask })), style: 'band' },
    }],
});

const forecasts = new ChartDefinition({
    axis: { values: 'labels', title: 'Quarter', labels: intervalDataForecasts.map(f => f.quarter) },
    series: [{
        id: 'median', name: 'Median forecast', kind: 'level', fromZero: false, format: intervalPercent,
        keys: { colour: 'Median', marker: 'circle' },
        points: intervalDataForecasts.map((f, k) => ({ x: k, y: f.median })),
        interval: { meaning: '95% CI', bounds: intervalDataForecasts.map((f, k) => ({ x: k, lower: f.low, upper: f.high })) },
    }],
});

const estimates = new ChartDefinition({
    axis: { values: 'numbers', title: 'Volatility (% a year)', format: x => `${fixed(x, 1)}%` },
    series: [{
        id: 'strategies', name: 'Strategies', kind: 'observation', format: intervalPercent,
        points: intervalDataStrategies.map(s => ({ x: s.vol, y: s.return, name: s.name })),
        interval: { meaning: '95% CI', bounds: intervalDataStrategies.map(s => ({ x: s.vol, lower: s.returnLow, upper: s.returnHigh })) },
        xInterval: { meaning: '95% CI', bounds: intervalDataStrategies.map(s => ({ x: s.vol, lower: s.volLow, upper: s.volHigh })) },
    }],
});

/** Intervals: the figures beside the index that carry an interval, each saying what it means (ADR-0303, ADR-0304). */
function IntervalsView(): ReactElement {
    const chart = (definition: ChartDefinition, title: string, width: number, height: number): ReactElement => (
        <div className="xs-panel"><ExChart definition={definition} title={title} fileName={title} width={width} height={height} /></div>
    );
    return (
        <div className="xs-side">
            {chart(curve, 'Yield curve', 560, 300)}
            {chart(forecasts, 'Policy rate forecasts', 560, 300)}
            {chart(estimates, 'Strategies\' risk and return', 560, 340)}
        </div>
    );
}

const views = ['Daily bars', 'Intervals'] as const;
type View = (typeof views)[number];

/** The page /chart-showcase/equity-index of the Blazor hosts, under the built-in Chrome. */
export function EquityIndexShowcase(): ReactElement {
    const [view, setView] = useState<View>('Daily bars');
    return (
        <>
            <DemoNav route="chart-showcase/equity-index" />
            <ShowcaseShell app="Equity Charts" crumb={`Equities · ${name} · ${view}`}
                tools={<Views label="View" views={views} value={view} onChange={setView} />}>
                {view === 'Daily bars' ? <BarsView /> : <IntervalsView />}
            </ShowcaseShell>
        </>
    );
}
