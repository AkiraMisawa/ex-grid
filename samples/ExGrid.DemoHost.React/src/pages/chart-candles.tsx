import { useState, type ReactElement } from 'react';
import { ChartDefinition, type ChartPointClick, type ChartRange, type ChartSeriesOptions, type ChartValue, type PriceBar } from '@ex-grid/chart';
import { ExChart } from '@ex-grid/chart-react';
import { DemoNav } from '../demo-nav';
import { dotNetRound } from '../dotnet-math';
import { DotNetRandom } from '../dotnet-random';

// The day number of 1970-01-01, counted from 0001-01-01 as a C# DateOnly counts it.
const epochDay = 719_162;
const msPerDay = 86_400_000;

const dayOf = (year: number, month: number, day: number): number => Date.UTC(year, month - 1, day) / msPerDay + epochDay;
const isoDay = (day: number): string => new Date((day - epochDay) * msPerDay).toISOString().slice(0, 10);
const round2 = (v: number): number => dotNetRound(v, 2);

const twoPlaces = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const price = (v: ChartValue): string => twoPlaces.format(v);

// Weekdays from 2016-01-04, a geometric walk from a fixed seed; 2020-03-16 falls 12%.
function index(): PriceBar[] {
    const random = new DotNetRandom(20260920);
    const bars: PriceBar[] = [];
    const crash = dayOf(2020, 3, 16);
    let close = 20_000;
    for (let day = dayOf(2016, 1, 4); day < dayOf(2026, 1, 1); day++) {
        // Day number 0 was a Monday.
        const weekday = day % 7;
        if (weekday === 5 || weekday === 6) continue;
        const open = close * (1 + (random.nextDouble() - 0.5) * 0.006);
        const move = day === crash ? -0.12 : (random.nextDouble() - 0.48) * 0.025;
        const next = open * (1 + move);
        const high = Math.max(open, next) * (1 + random.nextDouble() * 0.008);
        const low = Math.min(open, next) * (1 - random.nextDouble() * 0.008);
        const volume = dotNetRound(1_000_000 * (0.6 + random.nextDouble()) * (Math.abs(move) > 0.05 ? 4 : 1), 0);
        bars.push({ x: day, open: round2(open), high: round2(high), low: round2(low), close: round2(next), volume });
        close = next;
    }
    return bars;
}

const bars = index();

// The page's own calculation, as a Consumer's would be: the mean of the last n closes.
function movingAverage(n: number): ChartSeriesOptions {
    const closes = bars.map(b => Number(b.close));
    return {
        id: 'ma25', name: '25-day average', kind: 'level', fromZero: false, format: price,
        points: bars.map((b, i) => ({
            x: b.x,
            y: i + 1 < n ? null : round2(closes.slice(i + 1 - n, i + 1).reduce((a, c) => a + c, 0) / n),
        })),
    };
}

const average = movingAverage(25);

const make = (packed: boolean): ChartDefinition => new ChartDefinition({
    axis: { values: 'dates', title: 'Date', packed },
    series: [{ id: 'index', name: 'Index', kind: 'priceRange', bars, format: price }, average],
});

const calendar = make(false);
const packedSessions = make(true);

const quotes = [[0.25, 3.71, 3.75], [0.5, 3.62, 3.65], [1, 3.48, 3.51], [2, 3.30, 3.34], [3, 3.27, 3.30], [5, 3.31, 3.35],
    [7, 3.42, 3.47], [10, 3.55, 3.61], [20, 3.81, 3.90], [30, 3.86, 3.98]] as const;

const curve = new ChartDefinition({
    axis: { values: 'numbers', title: 'Tenor (years)' },
    series: [{
        id: 'mid', name: 'Mid', kind: 'level', fromZero: false,
        points: quotes.map(([x, bid, ask]) => ({ x, y: Math.round((bid + ask) * 500) / 1000 })),
        interval: { meaning: 'Bid–Ask', bounds: quotes.map(([x, lower, upper]) => ({ x, lower, upper })), style: 'band' },
    }],
});

const survey = [[0, 4.25, 4.25, 4.25], [1, 4.00, 3.75, 4.25], [2, 3.75, 3.25, 4.25], [3, 3.50, 2.90, 4.10], [4, 3.50, 2.60, 4.20],
    [5, 3.25, 2.30, 4.20]] as const;

const forecasts = new ChartDefinition({
    axis: { values: 'labels', title: 'Quarter', labels: ['Now', 'Q1', 'Q2', 'Q3', 'Q4', 'Q5'] },
    series: [{
        id: 'median', name: 'Median forecast', kind: 'level', fromZero: false, keys: { colour: 'Median', marker: 'circle' },
        points: survey.map(([x, y]) => ({ x, y })),
        interval: { meaning: '95% CI', bounds: survey.map(([x, , lower, upper]) => ({ x, lower, upper })) },
    }],
});

// Each strategy's annualised volatility and return, both estimated, with bootstrap intervals (ADR-0304).
const strategies = [
    ['Carry', 6.1, 5.4, 6.9, 4.2, 1.1, 7.3], ['Momentum', 11.8, 10.5, 13.4, 6.0, 0.2, 11.9],
    ['Value', 9.4, 8.3, 10.6, 2.9, -2.0, 7.8], ['Low vol', 5.2, 4.7, 5.8, 3.8, 1.3, 6.4],
    ['Trend', 14.6, 12.9, 16.5, 7.1, -0.1, 14.3], ['Short vol', 8.7, 6.2, 12.9, 5.5, 0.9, 10.0],
] as const;

const estimates = new ChartDefinition({
    axis: { values: 'numbers', title: 'Volatility (% a year)' },
    series: [{
        id: 'strategies', name: 'Strategies', kind: 'observation',
        points: strategies.map(([name, x, , , y]) => ({ x, y, name })),
        interval: { meaning: '95% CI', bounds: strategies.map(([, x, , , , lower, upper]) => ({ x, lower, upper })) },
        xInterval: { meaning: '95% CI', bounds: strategies.map(([, x, lower, upper]) => ({ x, lower, upper })) },
    }],
});

const write = (day: number | undefined): string => (day === undefined ? '…' : isoDay(Math.trunc(day)));

/** The page /chart-candles of the Blazor hosts (tickets 60 to 66): ten years of a synthetic daily index as
 * candles with volume and a 25-day average, a yield curve with its Bid–Ask band, forecasts with whiskers,
 * and strategies' intervals both ways. The page keeps the range and writes the last event raised. */
export function ChartCandles(): ReactElement {
    const [packed, setPacked] = useState(false);
    const [colours, setColours] = useState('shape');
    const [range, setRange] = useState<ChartRange | null>(null);
    const [events, setEvents] = useState('');

    const rangeChanged = (next: ChartRange | null): void => {
        setRange(next);
        setEvents(next === null ? 'range: all' : `range: ${write(next.from)} to ${write(next.to)}`);
    };
    const pointClicked = (click: ChartPointClick): void => { setEvents(`clicked ${click.seriesId} at ${write(click.position)}`); };

    return (
        <>
            <DemoNav route="chart-candles" />
            <h1>Candles and intervals</h1>
            <p>
                A Price Range is one candle a row: rising hollow, falling filled, and a row whose values disagree is
                refused (ADR-0300). Ten years do not fit as days, so the candles merge into the market&apos;s own months,
                and a click on one narrows to it (ADR-0302). By default the dates are calendar time; packed, the
                days nobody traded are left out (ADR-0301). An interval names what it means (ADR-0303).
            </p>
            <section id="index">
                <h2>Equity index daily bars</h2>
                <p>
                    <label>
                        <input type="checkbox" id="packed" checked={packed} onChange={e => { setPacked(e.target.checked); }} /> Packed sessions
                    </label>
                    <label>
                        Colours{' '}
                        <select id="colours" value={colours} onChange={e => { setColours(e.target.value); }}>
                            <option value="shape">Shape only</option>
                            <option value="japan">Japan: red rises, blue falls</option>
                            <option value="west">West: green rises, red falls</option>
                        </select>
                    </label>
                </p>
                <div className={`candles-${colours}`}>
                    <ExChart
                        definition={packed ? packedSessions : calendar}
                        title="Equity index"
                        width={760}
                        height={460}
                        range={range}
                        onRangeChanged={rangeChanged}
                        onPointClick={pointClicked} />
                </div>
                <p id="index-events">{events}</p>
            </section>
            <section id="curve">
                <h2>Yield curve with its Bid–Ask</h2>
                <ExChart definition={curve} title="Yield curve" width={760} height={260} />
            </section>
            <section id="forecasts">
                <h2>Rate forecasts</h2>
                <ExChart definition={forecasts} title="Policy rate forecasts" width={760} height={260} />
            </section>
            <section id="estimates">
                <h2>Strategies&apos; estimated risk and return</h2>
                <ExChart definition={estimates} title="Volatility against return, each a 95% CI" width={760} height={300} />
            </section>
            <style>{`
                .candles-japan .ex-chart { --ex-chart-rise: #d32f2f; --ex-chart-fall: #1565c0; }
                .candles-west .ex-chart { --ex-chart-rise: #2e7d32; --ex-chart-fall: #c62828; }
            `}</style>
        </>
    );
}
