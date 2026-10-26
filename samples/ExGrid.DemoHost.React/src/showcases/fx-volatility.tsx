// FX volatility (ADR-0280): ExChart.Showcases' FxVolShowcase under the built-in Chrome. An EURUSD
// implied-volatility surface four ways, switched in the application bar: the quotes beside the 3D surface, the
// term structure at a delta, the smile at an expiry, and the deposits and forwards behind them. Every figure is
// a fixed value (data.ts); the only one the page computes is a missing quote, filled by the page's own
// interpolation and named as such, as FxVolSurface.cs fills it.
import { useState, type ReactElement } from 'react';
import { GridSource, type GridColumn, type GridHeaderGroup } from '@ex-grid/core';
import {
    ChartDefinition, interpolateNaturalCubicSpline, SurfaceDefinition, type AppearanceKeys, type ChartValue, type SurfaceSlice,
    type SurfaceSliceAxis, type SurfaceView,
} from '@ex-grid/chart';
import { ExChart, ExSurface } from '@ex-grid/chart-react';
import { ExGrid } from '@ex-grid/react';
import { Temporal } from 'temporal-polyfill';
import { DemoNav } from '../demo-nav';
import { dotNetRound } from '../dotnet-math';
import { fxVolDataAsOf, fxVolDataDeltas, fxVolDataDeltaValues, fxVolDataForwards, fxVolDataPair, fxVolDataQuotes, fxVolDataSpot, fxVolDataSpotDate, type FxForwardRow, type FxVolRow } from './data';
import { fixed, grouped, numberOf } from './format';
import { ShowcaseShell, Views } from './shell';

const pair = fxVolDataPair;
const asOf = fxVolDataAsOf.toString(), spotDate = fxVolDataSpotDate.toString(), spot = fxVolDataSpot;

/** The method the filled quotes were made with, as the surface names it. */
const method = 'natural cubic spline across delta, per expiry';

/** A volatility, as the grid and the charts write it: `ToString("0.00") + "%"`. */
const percent = (v: unknown): string => `${fixed(Number(v), 2)}%`;

const tenors: readonly string[] = fxVolDataQuotes.map(r => r.tenor);

function at<T>(items: readonly T[], k: number, what: string): T {
    const item = items[k];
    if (item === undefined) throw new Error(`There is no ${what} ${String(k)}.`);
    return item;
}

// Each expiry's quotes, its missing quotes filled where they lie between quotes. A filled value is rounded to
// the quotes' precision, two decimals of a percent: a chart's "View as table" writes each value with the digits
// it was handed in with (ADR-0254), and a spline's full double there is a figure nobody can read.
function fill(row: FxVolRow): readonly (number | null)[] {
    const quoted = row.vols.flatMap((v, i) => (v === null ? [] : [{ delta: at(fxVolDataDeltaValues, i, 'delta'), vol: v }]));
    const spline = interpolateNaturalCubicSpline(quoted.map(p => p.delta), quoted.map(p => p.vol));
    return row.vols.map((v, i) => {
        if (v !== null) return v;
        const x = spline.evaluate(at(fxVolDataDeltaValues, i, 'delta'));
        return x === null ? null : dotNetRound(x, 2);
    });
}

const filled = fxVolDataQuotes.map(fill);

function holes(): string[] {
    const notes: string[] = [];
    filled.forEach((row, j) => {
        row.forEach((v, i) => {
            if (v === null) {
                notes.push(`${at(fxVolDataDeltas, i, 'delta')} at ${at(tenors, j, 'tenor')} has no quote and lies outside its expiry's quotes, so it is a hole: nothing is extrapolated.`);
            }
        });
    });
    return notes;
}

/** The surface: deltas across, tenors along, the filled values marked. */
const surface = SurfaceDefinition.grid({
    x: { title: 'Delta', values: fxVolDataDeltas },
    y: { title: 'Tenor', values: tenors },
    z: filled,
    zTitle: 'Volatility',
    zFormat: percent,
    method,
    interpolated: fxVolDataQuotes.map((r, j) => r.vols.map((v, i) => v === null && at(filled, j, 'row')[i] !== null)),
    notes: holes(),
});

/**
 * A slice of the surface as a 2D chart on category axes (ADR-0274): the quotes, and where the slice holds a
 * filled value, the slice itself declared Interpolated under the same colour, so the legend shows one entry and
 * the tooltip says which values are quoted (ADR-0257).
 */
function sliceOf(slice: SurfaceSlice, name: string): ChartDefinition {
    const chart = surface.sliceChart(slice);
    if (chart === undefined) throw new RangeError('The slice is not on the surface.');
    const keys: AppearanceKeys = { colour: name };
    const own = { ...at(chart.series, 0, 'series'), name, keys };
    if (own.interpolated !== true) return new ChartDefinition({ axis: chart.axis, series: [own], notes: chart.notes });
    const quotes: readonly (number | null)[] = slice.axis === 'x'
        ? fxVolDataQuotes.map(r => at(r.vols, fxVolDataDeltas.indexOf(String(slice.value)), 'delta'))
        : at(fxVolDataQuotes, tenors.indexOf(String(slice.value)), 'tenor').vols;
    const quoted = {
        id: 'quoted', name, kind: 'level' as const, fromZero: false, format: percent, keys,
        points: quotes.map((v, k) => ({ x: k, y: v })),
    };
    return new ChartDefinition({ axis: chart.axis, series: [quoted, { ...own, id: 'interpolated' }], notes: chart.notes });
}

const quoteColumns: readonly GridColumn<FxVolRow>[] = Object.freeze([
    { name: 'Tenor', type: 'text', value: (r: FxVolRow) => r.tenor, header: 'Tenor' },
    { name: 'Expiry', type: 'date', dateType: 'dateOnly', value: (r: FxVolRow) => r.expiry, header: 'Expiry' },
    ...fxVolDataDeltas.map((d, i): GridColumn<FxVolRow> => ({ name: d, type: 'number', value: (r: FxVolRow) => at(r.vols, i, 'vol'), header: d, format: percent })),
]);
const quoteGroups: readonly GridHeaderGroup[] = Object.freeze([{ label: 'Implied volatility (%)', columns: fxVolDataDeltas }]);
const quoteSource = GridSource.from(fxVolDataQuotes, { rowKey: (r: FxVolRow) => r.tenor });

/** Surface: the quotes as the market sends them beside the surface made from them. */
function SurfaceView(): ReactElement {
    const title = `${pair} volatility`;
    // The surface holds no View State: the angle it was turned to and the slice clicked come back as events, for
    // the page to keep.
    const [view, setView] = useState<SurfaceView | null>(null);
    const [slice, setSlice] = useState<SurfaceSlice | null>(null);
    return (
        <>
            <p className="xs-note">
                Implied volatilities in %, by delta and expiry, as of {asOf}. A blank cell has no quote. Click a
                delta or a tenor on the surface to slice it; drag or use the arrow keys to turn it.
            </p>
            <div className="xs-fx-row">
                <div className="xs-panel xs-fx-quotes">
                    <ExGrid hideColumnMenu columns={quoteColumns} source={quoteSource} headerGroups={quoteGroups} pinnedColumnCount={1}
                        viewportHeight={344} viewportWidth="stretch" temporal={Temporal} />
                </div>
                <div className="xs-panel">
                    <ExSurface definition={surface} title={title} fileName={title} width={480} height={360} sliceWidth={320}
                        view={view} onViewChanged={setView} slice={slice} onSliceChanged={setSlice} />
                </div>
            </div>
        </>
    );
}

/** Term structure and Smile: one slice of the surface on a category axis, picked from the page's own list. */
function SliceView({ axis }: { readonly axis: SurfaceSliceAxis }): ReactElement {
    const term = axis === 'x';
    // The term structure opens at the money; the smile opens at 1Y, whose 25C has no quote.
    const [chosen, setChosen] = useState(term ? 'ATM' : '1Y');
    const choices = term ? fxVolDataDeltas : tenors;
    const name = term ? `${chosen} volatility` : `${chosen} smile`;
    const title = term ? `${pair} ${chosen} volatility by tenor` : `${pair} ${chosen} smile`;
    const fileName = term ? `${pair} term structure ${chosen}` : `${pair} smile ${chosen}`;
    return (
        <>
            <div className="xs-tools">
                <label>{term ? 'Delta' : 'Expiry'}
                    <select className="xs-slice-at" value={chosen} onChange={event => { setChosen(event.target.value); }}>
                        {choices.map(choice => <option key={choice} value={choice}>{choice}</option>)}
                    </select>
                </label>
                <span>{term
                    ? 'Tenors are evenly spaced, as quoted, not by their length.'
                    : 'Puts on the left, calls on the right, at the money in the middle.'}</span>
            </div>
            <div className="xs-panel">
                <ExChart definition={sliceOf({ axis, value: chosen }, name)} title={title} width={900} height={380} fileName={fileName} />
            </div>
        </>
    );
}

/** `ToString("N…")` of a forward's figure. */
const digits = (places: number) => (v: unknown): string => grouped(numberOf(v), places);

const forwardColumns: readonly GridColumn<FxForwardRow>[] = Object.freeze([
    { name: 'Tenor', type: 'text', value: (r: FxForwardRow) => r.tenor, header: 'Tenor' },
    { name: 'ValueDate', type: 'date', dateType: 'dateOnly', value: (r: FxForwardRow) => r.valueDate, header: 'Value date' },
    { name: 'Days', type: 'number', value: (r: FxForwardRow) => r.days, header: 'Days', format: digits(0) },
    { name: 'Eur', type: 'number', value: (r: FxForwardRow) => r.eurPct, header: 'EUR (%)', format: digits(2) },
    { name: 'Usd', type: 'number', value: (r: FxForwardRow) => r.usdPct, header: 'USD (%)', format: digits(2) },
    { name: 'Points', type: 'number', value: (r: FxForwardRow) => r.points, header: 'Points', format: digits(2) },
    { name: 'Outright', type: 'number', value: (r: FxForwardRow) => r.outright, header: 'Outright', format: digits(5) },
]);
const forwardGroups: readonly GridHeaderGroup[] = Object.freeze([
    { label: 'Deposit rate', columns: ['Eur', 'Usd'] },
    { label: 'Forward', columns: ['Points', 'Outright'] },
]);
const forwardSource = GridSource.from(fxVolDataForwards, { rowKey: (r: FxForwardRow) => r.tenor });

const forwardsTitle = `${pair} deposits and forwards`;
const ratePanel = 'Deposit rate (%)', pointsPanel = 'Forward points';

// The tenors on a category axis, as quoted: a rate at each, the forward points at each.
const forwards = ((): ChartDefinition => {
    const series = (id: string, name: string, panel: string, value: (r: FxForwardRow) => number, fromZero: boolean) => ({
        id, name, kind: 'level' as const, panel, fromZero, format: (v: ChartValue) => grouped(Number(v), 2),
        points: fxVolDataForwards.map((r, k) => ({ x: k, y: value(r) })),
    });
    return new ChartDefinition({
        axis: { values: 'labels', labels: tenors, title: 'Tenor' },
        series: [
            series('eur', 'EUR deposit', ratePanel, r => r.eurPct, false),
            series('usd', 'USD deposit', ratePanel, r => r.usdPct, false),
            series('points', 'Forward points', pointsPanel, r => r.points, true),
        ],
    });
})();

/** Deposits and forwards: the two currencies' deposit rates and the forwards they imply at each tenor. */
function ForwardsView(): ReactElement {
    return (
        <>
            <p className="xs-note">
                Spot {fixed(spot, 4)} on {asOf}, for value {spotDate}. Deposits accrue ACT/360; forward points are
                pips, the outright less spot.
            </p>
            <div className="xs-panel">
                <ExGrid hideColumnMenu columns={forwardColumns} source={forwardSource} headerGroups={forwardGroups} pinnedColumnCount={1}
                    viewportHeight={300} viewportWidth="stretch" temporal={Temporal} />
            </div>
            <div className="xs-panel">
                <ExChart definition={forwards} title={forwardsTitle} width={1000} height={420} fileName={forwardsTitle} />
            </div>
        </>
    );
}

const views = ['Surface', 'Term structure', 'Smile', 'Deposits and forwards'] as const;
type View = (typeof views)[number];

/** The page /chart-showcase/fx-volatility of the Blazor hosts, under the built-in Chrome. */
export function FxVolatilityShowcase(): ReactElement {
    const [view, setView] = useState<View>('Surface');
    return (
        <>
            <DemoNav route="chart-showcase/fx-volatility" />
            <ShowcaseShell app="FX Vol Surface" crumb={`FX options · ${pair} volatility · ${view}`}
                tools={<Views label="View" views={views} value={view} onChange={setView} />}>
                {view === 'Surface' ? <SurfaceView />
                    : view === 'Term structure' ? <SliceView key="term" axis="x" />
                        : view === 'Smile' ? <SliceView key="smile" axis="y" />
                            : <ForwardsView />}
            </ShowcaseShell>
        </>
    );
}
