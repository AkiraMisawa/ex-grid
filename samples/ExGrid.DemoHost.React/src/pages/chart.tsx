import { useState, type ReactElement } from 'react';
import { ChartDefinition, type ChartRange, type ChartSeriesOptions, type SeriesVisibility } from '@ex-grid/chart';
import { ExChart } from '@ex-grid/chart-react';
import { DemoNav } from '../demo-nav';

// The day number of 1970-01-01, counted from 0001-01-01 as a C# DateOnly counts it.
const epochDay = 719_162;
const msPerDay = 86_400_000;

/** The day number of the date `months` months after 2026-10-30, as DateOnly.AddMonths finds it. */
function monthDay(months: number): number {
    const year = 2026 + Math.floor((9 + months) / 12);
    const month = (9 + months) % 12;
    // A month shorter than the 30th takes its last day.
    const last = new Date(Date.UTC(year, month + 1, 0)).getUTCDate();
    return Date.UTC(year, month, Math.min(30, last)) / msPerDay + epochDay;
}

function isoDay(day: number): string {
    return new Date((day - epochDay) * msPerDay).toISOString().slice(0, 10);
}

const twoPlaces = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

function exposure(run: number): ChartDefinition {
    const days = Array.from({ length: 121 }, (_, m) => monthDay(m));
    // A hump that peaks near year three, deterministic, and moved a little by each run.
    const hump = (m: number, scale: number): number =>
        Math.round(100 * (scale * Math.sin(Math.PI * Math.min(m, 120) / 120) * (1 + 0.6 * Math.exp(-(((m - 36) / 18) ** 2))) + run * 0.25)) / 100;
    const series = (id: string, name: string, panel: string, key: string, value: (m: number) => number): ChartSeriesOptions => ({
        id, name, kind: 'level', panel, keys: { colour: key },
        points: days.map((x, m) => ({ x, y: value(m) })),
        format: v => twoPlaces.format(v),
    });
    return new ChartDefinition({
        axis: { values: 'dates', title: 'Date' },
        series: [
            series('epe-before', 'EPE · Before', 'EPE', 'Before', m => hump(m, 10)),
            series('epe-after', 'EPE · After', 'EPE', 'After', m => hump(m, 7)),
            series('pfe-after', 'PFE95 · After', 'EPE', 'PFE', m => hump(m, 12)),
            series('ene-before', 'ENE · Before', 'ENE', 'Before', m => -hump(m, 6)),
            series('ene-after', 'ENE · After', 'ENE', 'After', m => -hump(m, 4)),
        ],
    });
}

const hazard = new ChartDefinition({
    axis: { values: 'numbers', title: 'Tenor', named: [[0.5, '6M'], [1, '1Y'], [2, '2Y'], [3, '3Y'], [5, '5Y'], [7, '7Y'], [10, '10Y']] },
    series: [{
        id: 'hazard', name: 'Hazard rate', kind: 'rateOverInterval', keys: { colour: 'Hazard', marker: 'square' },
        points: [
            { x: 0, y: null }, { x: 0.5, y: 0.0110 }, { x: 1, y: 0.0125 }, { x: 2, y: 0.0150 }, { x: 3, y: 0.0170 },
            { x: 5, y: 0.0190 }, { x: 7, y: 0.0205 }, { x: 10, y: 0.0210 },
        ],
    }],
});

const hazardLegend = { position: 'right', align: 'centre' } as const;

const write = (day: number | undefined): string => (day === undefined ? '…' : isoDay(Math.trunc(day)));

/** The page /chart of the Blazor hosts (ticket 14): the exposure profile, two panels of levels on dates, and a
 * hazard curve drawn as steps. The page keeps what the user changed, as a Consumer would (ADR-0272), and
 * writes the last event raised under the exposure chart; "Next run" replaces the exposure data. */
export function Chart(): ReactElement {
    const [run, setRun] = useState(0);
    const [definition, setDefinition] = useState(() => exposure(0));
    const [hidden, setHidden] = useState<ReadonlySet<string>>(new Set());
    const [range, setRange] = useState<ChartRange | null>(null);
    const [events, setEvents] = useState('');

    const seriesChanged = (change: SeriesVisibility): void => {
        const next = new Set(hidden);
        if (change.hidden) next.add(change.key);
        else next.delete(change.key);
        setHidden(next);
        setEvents(`${change.key} ${change.hidden ? 'hidden' : 'shown'}`);
    };
    const rangeChanged = (next: ChartRange | null): void => {
        setRange(next);
        setEvents(next === null ? 'range: all' : `range: ${write(next.from)} to ${write(next.to)}`);
    };
    const nextRun = (): void => {
        setRun(run + 1);
        setDefinition(exposure(run + 1));
    };

    return (
        <>
            <DemoNav route="chart" />
            <h1>Charts</h1>
            <p>
                Each series declares its Series Kind and is drawn as what it is (ADR-0253): exposures are levels
                joined only to their neighbours, a hazard rate holds over each interval and is drawn as steps with
                a filled and an open end. Move over a plot to read every series of its panel at that date
                (ADR-0270); click a legend entry to hide it (ADR-0271); drag across a plot to narrow the range,
                and double-click to return it (ADR-0273).
            </p>
            <section id="exposure">
                <h2>Exposure profile</h2>
                <button type="button" id="next-run" onClick={nextRun}>Next run</button>
                <ExChart
                    definition={definition}
                    title="Exposure profile"
                    width={760}
                    height={420}
                    hiddenSeries={hidden}
                    onSeriesVisibilityChanged={seriesChanged}
                    range={range}
                    onRangeChanged={rangeChanged} />
                <p id="exposure-events">{events}</p>
            </section>
            <section id="hazard">
                <h2>Hazard curve</h2>
                <ExChart definition={hazard} title="Hazard rates" width={760} height={260} legend={hazardLegend} />
            </section>
        </>
    );
}
