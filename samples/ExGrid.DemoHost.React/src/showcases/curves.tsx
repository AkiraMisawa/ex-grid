// Curves (ADR-0280): ExChart.Showcases' CurvesShowcase under the built-in Chrome. Three views of credit and rate
// curves, switched in the application bar: one name's CDS term structure, CDS curves by reference entity with
// their tenors across, and an interest-rate curve three ways. Each view keeps its own grid, controls and chart;
// only the selected one is on the screen. Every figure is a fixed value (data.ts); nothing here bootstraps or
// interpolates a curve.
import { useEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { GridSource, type GridColumn, type GridHeaderGroup, type InMemoryGridSource } from '@ex-grid/core';
import {
    ChartDefinition, GridChartBridge, gridChartCommandLabel, type ChartSeriesOptions, type ChartValue, type GridChart,
    type GridChartDeclarations, type SeriesKind,
} from '@ex-grid/chart';
import { ExChart } from '@ex-grid/chart-react';
import { ExGrid, type ExGridApi } from '@ex-grid/react';
import { Temporal } from 'temporal-polyfill';
import { DemoNav } from '../demo-nav';
import { ChartTabs, ChartTabsModel } from './chart-tabs';
import {
    cdsCurveDataCurves, cdsCurveDataShifts, cdsCurveDataTenorNames, cdsCurveDataTenorYears, cdsDataCurves, cdsDataShifts, rateDataBuilt,
    rateDataCurves, rateDataInterpolations, rateDataQuotes, rateDataTenors, rateDataYears, type BuiltCurve, type CdsCurveRow, type CdsRow,
    type RateQuotes,
    cdsDataRatePct, cdsDataRecoveryPct, rateDataDeposits, rateDataEarlier, rateDataToday,
} from './data';
import { dayNumberOf, fixed, grouped, numberOf } from './format';
import { ShowcaseShell, Views } from './shell';

function at<T>(items: readonly T[], k: number, what: string): T {
    const item = items[k];
    if (item === undefined) throw new Error(`There is no ${what} ${String(k)}.`);
    return item;
}

/** `ToString("+0'bp';−0'bp';'0bp'")`. */
const shiftText = (bp: number): string => (bp > 0 ? `+${String(bp)}bp` : bp < 0 ? `−${String(-bp)}bp` : '0bp');

/** `ToString("N…")`. */
const digits = (places: number) => (v: unknown): string => grouped(numberOf(v), places);

/** A grid with a bridge and the tabs of the charts it makes, as CdsCurveView and EntityCurvesView hold them. */
interface Charted<Row> {
    rows: readonly Row[];
    readonly source: InMemoryGridSource<Row>;
    readonly tabs: ChartTabsModel;
    readonly bridge: GridChartBridge<Row>;
}

function useCharted<Row>(first: readonly Row[], keyOf: (row: Row) => string, columns: readonly GridColumn<Row>[], groups: readonly GridHeaderGroup[],
    declarations: Omit<GridChartDeclarations<Row>, 'rowKey' | 'rowOf'>, initial: () => readonly GridChart[],
): { readonly held: Charted<Row>; readonly grid: React.RefObject<ExGridApi<Row> | null> } {
    const grid = useRef<ExGridApi<Row>>(null);
    const held = useRef<Charted<Row> | undefined>(undefined);
    if (held.current === undefined) {
        const heldNow = (): Charted<Row> => {
            if (held.current === undefined) throw new Error('Internal: the page asked for its rows before it held them.');
            return held.current;
        };
        const source = GridSource.from(first, { rowKey: keyOf });
        const tabs = new ChartTabsModel();
        const bridge = new GridChartBridge<Row>({
            declarations: { ...declarations, rowKey: keyOf, rowOf: key => heldNow().rows.find(r => keyOf(r) === key) },
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
        held.current = { rows: first, source, tabs, bridge };
    }
    const charted = held.current;
    useEffect(() => {
        if (charted.tabs.snapshot().charts.length === 0) for (const chart of initial()) charted.tabs.open(chart);
    }, [charted, initial]);
    return { held: charted, grid };
}

function replace<Row>(charted: Charted<Row>, rows: readonly Row[]): void {
    charted.rows = rows;
    charted.source.replaceAll(rows);
}

// Curves › CDS curve: one name's par spreads by tenor and the piecewise-constant hazard rates bootstrapped from
// them, each holding over (tᵢ₋₁, tᵢ]. The quotes shift in steps of 10bp; each step is a curve kept in the data.

const cdsColumns: readonly GridColumn<CdsRow>[] = Object.freeze([
    { name: 'Tenor', type: 'text', value: (r: CdsRow) => r.tenor, header: 'Tenor' },
    { name: 'Maturity', type: 'date', dateType: 'dateOnly', value: (r: CdsRow) => r.maturity, header: 'Maturity' },
    { name: 'Spread', type: 'number', value: (r: CdsRow) => r.spreadBp, header: 'Par spread (bp)', format: digits(2) },
    { name: 'Hazard', type: 'number', value: (r: CdsRow) => r.hazardPct, header: 'Hazard rate (%)', format: digits(4) },
    { name: 'Survival', type: 'number', value: (r: CdsRow) => r.survivalPct, header: 'Survival (%)', format: digits(4) },
    { name: 'Default', type: 'number', value: (r: CdsRow) => r.defaultPct, header: 'Default prob. (%)', format: digits(4) },
]);
const cdsGroups: readonly GridHeaderGroup[] = Object.freeze([
    { label: 'CDS quote', columns: ['Tenor', 'Maturity', 'Spread'] },
    { label: 'Bootstrapped', columns: ['Hazard', 'Survival', 'Default'] },
]);
const cdsKey = (r: CdsRow): string => r.tenor;
const cdsUnshifted = cdsDataShifts.indexOf(0);
// A hazard rate holds over (tᵢ₋₁, tᵢ], which is how the bootstrap defined it: the page says so.
const cdsDeclarations = { kindOf: (column: string): SeriesKind => (column === 'Hazard' ? 'rateOverInterval' : 'level') };

// Shown from the start: the spread and the hazard rate at every pillar, the curve's date included.
const cdsInitial = (): readonly GridChart[] => {
    const rows = at(cdsDataCurves, cdsUnshifted, 'curve').map(r => ({ key: cdsKey(r), x: dayNumberOf(r.maturity) }));
    return [{
        orientation: 'seriesInColumns', axisColumn: 'Maturity',
        series: [
            { type: 'column', id: 'Spread', panel: 'CDS quote', column: 'Spread', rows },
            { type: 'column', id: 'Hazard', panel: 'Bootstrapped', column: 'Hazard', rows },
        ],
    }];
};

function CdsCurveView(): ReactElement {
    const [shift, setShift] = useState(cdsUnshifted);
    const { held, grid } = useCharted(at(cdsDataCurves, cdsUnshifted, 'curve'), cdsKey, cdsColumns, cdsGroups, cdsDeclarations, cdsInitial);
    const move = (step: number): void => {
        const next = Math.min(cdsDataShifts.length - 1, Math.max(0, shift + step));
        replace(held, at(cdsDataCurves, next, 'curve'));
        setShift(next);
    };
    return (
        <>
            <div className="xs-tools">
                <span>Recovery {cdsDataRecoveryPct}%, a flat {cdsDataRatePct}% rate, quarterly premiums on the IMM dates.</span>
                <span className="xs-spacer" />
                <span>Quotes shifted {shiftText(at(cdsDataShifts, shift, 'shift'))}</span>
                <button type="button" className="xs-button xs-shift-down" disabled={shift === 0} onClick={() => { move(-1); }}>−10bp</button>
                <button type="button" className="xs-button xs-shift-up" disabled={shift === cdsDataShifts.length - 1} onClick={() => { move(1); }}>+10bp</button>
            </div>
            <div className="xs-panel">
                <ExGrid ref={grid} hideColumnMenu columns={cdsColumns} source={held.source} headerGroups={cdsGroups}
                    contextCommands={held.bridge.commands} commandLabel={gridChartCommandLabel}
                    viewportHeight={330} viewportWidth="stretch" temporal={Temporal} />
            </div>
            <ChartTabs tabs={held.tabs} bridge={held.bridge} chartWidth={760} chartHeight={380} fileName="cds-curve" />
        </>
    );
}

// Curves › By reference entity: one row per CDS curve, its tenors across, Excel's "series in rows". Each row
// draws its par spreads or its hazard rates along the tenors' own years, which the page declares per column.

const spreadGroup = 'Par spread (bp)', hazardGroup = 'Hazard rate (%)';
const spreadColumns = cdsCurveDataTenorNames.map(t => `S_${t}`);
const hazardColumns = cdsCurveDataTenorNames.map(t => `H_${t}`);
const entityKey = (r: CdsCurveRow): string => `${r.ticker} ${r.seniority} ${r.ccy} ${r.docClause}`;

const entityColumns: readonly GridColumn<CdsCurveRow>[] = Object.freeze([
    { name: 'Ticker', type: 'text', value: (r: CdsCurveRow) => r.ticker, header: 'Ticker', width: 84 },
    { name: 'Entity', type: 'text', value: (r: CdsCurveRow) => r.entity, header: 'Ref entity name', width: 190 },
    { name: 'Ccy', type: 'text', value: (r: CdsCurveRow) => r.ccy, header: 'Ccy', width: 64 },
    { name: 'Seniority', type: 'text', value: (r: CdsCurveRow) => r.seniority, header: 'Seniority', width: 96 },
    { name: 'Doc', type: 'text', value: (r: CdsCurveRow) => r.docClause, header: 'Doc clause', width: 96 },
    { name: 'Recovery', type: 'number', value: (r: CdsCurveRow) => r.recoveryPct, header: 'Recovery (%)', width: 112, format: digits(0) },
    ...cdsCurveDataTenorNames.map((t, i): GridColumn<CdsCurveRow> =>
        ({ name: `S_${t}`, type: 'number', value: (r: CdsCurveRow) => at(r.spreads, i, 'spread'), header: t, width: 72, format: digits(1) })),
    ...cdsCurveDataTenorNames.map((t, i): GridColumn<CdsCurveRow> =>
        ({ name: `H_${t}`, type: 'number', value: (r: CdsCurveRow) => at(r.hazards, i, 'hazard'), header: t, width: 84, format: digits(4) })),
]);
const entityGroups: readonly GridHeaderGroup[] = Object.freeze([{ label: spreadGroup, columns: spreadColumns }, { label: hazardGroup, columns: hazardColumns }]);

// Each tenor column's position on the axis, in years: declared here, never read off "6m".
const positions: ReadonlyMap<string, number> = new Map(cdsCurveDataTenorNames.flatMap((t, i) => {
    const years = at(cdsCurveDataTenorYears, i, 'tenor');
    return [[`S_${t}`, years], [`H_${t}`, years]] as const;
}));
const entityUnshifted = cdsCurveDataShifts.indexOf(0);
const entityDeclarations = {
    kindOf: (column: string): SeriesKind => (column.startsWith('H_') ? 'rateOverInterval' : 'level'),
    positionOf: (column: string) => positions.get(column),
    positionTitle: 'Tenor (years)',
    rowName: entityKey,
    orientation: 'seriesInRows' as const,
};

// From the start: an investment-grade name, and one high-yield name senior and subordinated, their spreads in one
// chart and their hazard rates in another, as basis points and percent a year read on no common axis.
const entityInitial = (): readonly GridChart[] => {
    const shown = ['ACMEC SNRFOR USD XR14', 'CYGNUS SNRFOR USD XR14', 'CYGNUS SUBLT2 USD XR14'];
    const of = (group: string, columns: readonly string[]): GridChart => ({
        orientation: 'seriesInRows',
        series: shown.map(k => ({ type: 'row', id: `${k} · ${group}`, panel: '', rowKey: k, name: k, group, columns })),
    });
    return [of(spreadGroup, spreadColumns), of(hazardGroup, hazardColumns)];
};

function EntityCurvesView(): ReactElement {
    const [shift, setShift] = useState(entityUnshifted);
    const { held, grid } = useCharted(at(cdsCurveDataCurves, entityUnshifted, 'curves'), entityKey, entityColumns, entityGroups,
        entityDeclarations, entityInitial);
    const move = (step: number): void => {
        const next = Math.min(cdsCurveDataShifts.length - 1, Math.max(0, shift + step));
        replace(held, at(cdsCurveDataCurves, next, 'curves'));
        setShift(next);
    };
    return (
        <>
            <div className="xs-tools">
                <span>Par spreads in bp, hazard rates in % a year, piecewise constant over (tᵢ₋₁, tᵢ].</span>
                <span className="xs-spacer" />
                <span>Quotes shifted {shiftText(at(cdsCurveDataShifts, shift, 'shift'))}</span>
                <button type="button" className="xs-button xs-shift-down" disabled={shift === 0} onClick={() => { move(-1); }}>−25bp</button>
                <button type="button" className="xs-button xs-shift-up" disabled={shift === cdsCurveDataShifts.length - 1} onClick={() => { move(1); }}>+25bp</button>
            </div>
            <div className="xs-panel">
                <ExGrid ref={grid} hideColumnMenu columns={entityColumns} source={held.source} headerGroups={entityGroups} pinnedColumnCount={1}
                    contextCommands={held.bridge.commands} commandLabel={gridChartCommandLabel}
                    viewportHeight={250} viewportWidth="stretch" />
            </div>
            <ChartTabs tabs={held.tabs} bridge={held.bridge} chartWidth={1000} chartHeight={360} fileName="cds-curves" />
        </>
    );
}

// Curves › Rates: an interest-rate curve three ways, its market quotes, its zero rates, its discount factors, and
// its forwards. Each curve, on each of two dates, was built under each interpolation and kept in the data; the
// page only picks which to show. The pillars are the quotes, the curve between them is declared Interpolated
// with the interpolation's name (ADR-0257), and a forward under log-linear discount factors is a rate over each
// interval (ADR-0253).

const today = rateDataToday.toString(), earlier = rateDataEarlier.toString(), deposits = rateDataDeposits;
const ratePanel = 'Rate (%)', discountPanel = 'Discount factor';

/** One pillar as the table shows it: the instrument, its quotes, and what the curve made of it. */
interface PillarRow {
    readonly term: string;
    readonly type: string;
    readonly maturity: Temporal.PlainDate;
    readonly bid: number;
    readonly ask: number;
    readonly mid: number;
    readonly zero: number;
    readonly discount: number;
    readonly forward1Y: number;
    readonly implied: number;
}

const pillarColumns: readonly GridColumn<PillarRow>[] = Object.freeze([
    { name: 'Term', type: 'text', value: (r: PillarRow) => r.term, header: 'Term' },
    { name: 'Type', type: 'text', value: (r: PillarRow) => r.type, header: 'Type' },
    { name: 'Maturity', type: 'date', dateType: 'dateOnly', value: (r: PillarRow) => r.maturity, header: 'Maturity' },
    { name: 'Bid', type: 'number', value: (r: PillarRow) => r.bid, header: 'Bid (%)', format: digits(4) },
    { name: 'Ask', type: 'number', value: (r: PillarRow) => r.ask, header: 'Ask (%)', format: digits(4) },
    { name: 'Mid', type: 'number', value: (r: PillarRow) => r.mid, header: 'Mid (%)', format: digits(4) },
    { name: 'Zero', type: 'number', value: (r: PillarRow) => r.zero, header: 'Zero rate (%)', format: digits(4) },
    { name: 'Discount', type: 'number', value: (r: PillarRow) => r.discount, header: 'Discount factor', format: digits(6) },
    { name: 'Forward', type: 'number', value: (r: PillarRow) => r.forward1Y, header: '1Y forward (%)', format: digits(4) },
    { name: 'Implied', type: 'number', value: (r: PillarRow) => r.implied, header: 'Implied (%)', format: digits(4) },
]);
const pillarGroups: readonly GridHeaderGroup[] = Object.freeze([
    { label: 'Instrument', columns: ['Type', 'Maturity'] },
    { label: 'Market', columns: ['Bid', 'Ask', 'Mid'] },
    { label: 'Built curve', columns: ['Zero', 'Discount', 'Forward', 'Implied'] },
]);

// The market's tenors on a proportional axis, where the pillars bunch at the short end, as on the screens.
const ticked = ['1Y', '2Y', '3Y', '5Y', '7Y', '10Y', '15Y', '20Y', '30Y', '40Y', '50Y'];
const tenorAxis = {
    values: 'numbers' as const, title: 'Tenor',
    named: ticked.map(t => [at(rateDataYears, rateDataTenors.indexOf(t), 'tenor'), t] as const),
    format: (position: number): string => {
        const i = rateDataYears.indexOf(position);
        // `ToString("0.####") + "Y"`: four places at most, no trailing zero.
        return i >= 0 ? at(rateDataTenors, i, 'tenor') : `${String(Number(position.toFixed(4)))}Y`;
    },
};

function quotesOf(curve: string, date: string): RateQuotes {
    const quotes = rateDataQuotes.find(q => q.curve === curve && q.date.toString() === date);
    if (quotes === undefined) throw new Error(`There are no quotes of ${curve} on ${date}.`);
    return quotes;
}

function builtOf(curve: string, date: string, interpolation: string): BuiltCurve {
    const built = rateDataBuilt.find(b => b.curve === curve && b.date.toString() === date && b.interpolation === interpolation);
    if (built === undefined) throw new Error(`No ${curve} was built on ${date} under ${interpolation}.`);
    return built;
}

const mid = (q: RateQuotes, i: number): number => (at(q.bid, i, 'bid') + at(q.ask, i, 'ask')) / 2;

const chartViews = ['Zero rates', 'Discount factors', 'Market quotes', 'Forwards', 'Quotes, zeros and forwards'] as const;
type ChartView = (typeof chartViews)[number];

interface RateChoice {
    readonly curve: string;
    readonly interpolation: string;
    readonly annual: boolean;
    readonly compare: boolean;
    readonly chart: ChartView;
}

// One date's series for the chosen view: the date is the line-style key, the measure the colour key, so the
// earlier date draws the same colours dashed.
function seriesOf(c: RateChoice, date: string): ChartSeriesOptions[] {
    const series: ChartSeriesOptions[] = [];
    const built = builtOf(c.curve, date, c.interpolation);
    const quotes = quotesOf(c.curve, date);
    const logLinear = c.interpolation.startsWith('Log');
    const curveSegment = { from: at(built.curveYears, 0, 'year'), to: at(built.curveYears, built.curveYears.length - 1, 'year'), method: c.interpolation };
    const make = (id: string, measure: string, panel: string, points: readonly (readonly [number, number | null])[], kind: SeriesKind,
        places: number, fromZero: boolean): ChartSeriesOptions => ({
        id: `${id}|${date}`, name: `${measure} · ${date}`, kind, panel, keys: { colour: measure, lineStyle: date }, fromZero,
        points: points.map(([x, y]) => ({ x, y })), format: (v: ChartValue) => grouped(Number(v), places),
    });
    const pillars = (values: readonly number[]) => rateDataYears.map((t, i) => [t, at(values, i, 'value')] as const);
    const curve = (values: readonly number[]) => built.curveYears.map((t, i) => [t, at(values, i, 'value')] as const);
    const zero = (): void => {
        series.push(
            make('zero', 'Zero rate', ratePanel, pillars(c.annual ? built.zeroAnnual : built.zeroContinuous), 'level', 4, false),
            { ...make('zero-curve', 'Zero rate', ratePanel, curve(c.annual ? built.curveZeroAnnual : built.curveZeroContinuous), 'level', 4, false),
                interpolated: true, segments: [curveSegment] },
        );
    };
    const quoted = (): void => {
        series.push({
            ...make('quote', 'Market rate', ratePanel, rateDataYears.map((t, i) => [t, mid(quotes, i)] as const), 'level', 4, false),
            keys: { colour: 'Market rate', lineStyle: date, marker: 'Market rate' },
        });
    };
    const forward = (): void => {
        const points = built.forwardYears.map((t, i) => [t, at(built.forward, i, 'forward')] as const);
        // Under log-linear discount factors the forward is flat over each (tᵢ₋₁, tᵢ]: a rate over the interval,
        // whose first interval starts at today. Under linear zero rates it moves between pillars.
        if (logLinear) series.push(make('forward', 'Instantaneous forward', ratePanel, [[0, null], ...points], 'rateOverInterval', 4, false));
        else {
            series.push({
                ...make('forward', 'Instantaneous forward', ratePanel, points, 'level', 4, false), interpolated: true,
                segments: [{ from: at(built.forwardYears, 0, 'year'), to: at(built.forwardYears, built.forwardYears.length - 1, 'year'), method: c.interpolation }],
            });
        }
    };
    switch (c.chart) {
        case 'Zero rates': zero(); break;
        case 'Discount factors':
            series.push(
                make('df', 'Discount factor', discountPanel, pillars(built.discount), 'level', 6, true),
                { ...make('df-curve', 'Discount factor', discountPanel, curve(built.curveDiscount), 'level', 6, true), interpolated: true, segments: [curveSegment] },
            );
            break;
        case 'Market quotes': quoted(); break;
        case 'Forwards': forward(); break;
        case 'Quotes, zeros and forwards': quoted(); zero(); forward(); break;
    }
    return series;
}

function viewNote(c: RateChoice): string {
    switch (c.chart) {
        case 'Zero rates':
            return `Zero rates, ${c.annual ? 'annually' : 'continuously'} compounded: a marker at each pillar, the line between them as the ${c.interpolation.toLowerCase()} reads the curve.`;
        case 'Discount factors': return 'Discount factors: the value today of one unit paid at each tenor; the line between pillars is the interpolation\'s.';
        case 'Market quotes': return 'The mid quotes the curve is built from — the deposits\' and the par swaps\' rates.';
        case 'Forwards':
            return c.interpolation.startsWith('Log')
                ? 'Instantaneous forwards: with log-linear discount factors they are flat over each (tᵢ₋₁, tᵢ] — filled where the rate holds, open where it does not.'
                : 'Instantaneous forwards: with linear zero rates they move between pillars, and jump at each.';
        case 'Quotes, zeros and forwards': return 'The mid quotes, the zero rates and the forwards on one axis, as a forward-curve analysis overlays them.';
    }
}

function RateCurveView(): ReactElement {
    const [choice, setChoice] = useState<RateChoice>({
        curve: at(rateDataCurves, 0, 'curve'), interpolation: at(rateDataInterpolations, 0, 'interpolation'), annual: false, compare: false, chart: 'Zero rates',
    });
    const change = (next: Partial<RateChoice>): void => { setChoice({ ...choice, ...next }); };
    const quotes = quotesOf(choice.curve, today);
    // One source while the rows stand: a switch of the chart alone leaves the grid's Selection and scroll as they were.
    const { rows, source } = useMemo(() => {
        const built = builtOf(choice.curve, today, choice.interpolation);
        const pillars: readonly PillarRow[] = rateDataTenors.map((t, i) => ({
            term: t, type: i < deposits ? 'Deposit' : 'Swap', maturity: at(quotes.maturities, i, 'maturity'),
            bid: at(quotes.bid, i, 'bid'), ask: at(quotes.ask, i, 'ask'), mid: mid(quotes, i),
            zero: at(choice.annual ? built.zeroAnnual : built.zeroContinuous, i, 'zero'), discount: at(built.discount, i, 'discount'),
            forward1Y: at(built.forward1Y, i, 'forward'), implied: at(built.implied, i, 'implied'),
        }));
        return { rows: pillars, source: GridSource.from(pillars, { rowKey: (r: PillarRow) => r.term }) };
    }, [choice.curve, choice.interpolation, choice.annual, quotes]);
    const fit = fixed(Math.max(...rows.map(r => Math.abs(r.implied - r.mid) * 100)), 6);
    const definition = new ChartDefinition({
        axis: tenorAxis,
        series: [...seriesOf(choice, today), ...(choice.compare ? seriesOf(choice, earlier) : [])],
    });
    return (
        <>
            <div className="xs-tools">
                <label>Curve
                    <select className="xs-curve" value={choice.curve} onChange={event => { change({ curve: event.target.value }); }}>
                        {rateDataCurves.map(c => <option key={c} value={c}>{c}</option>)}
                    </select>
                </label>
                <label>Interpolation
                    <select className="xs-interpolation" value={choice.interpolation} onChange={event => { change({ interpolation: event.target.value }); }}>
                        {rateDataInterpolations.map(i => <option key={i} value={i}>{i}</option>)}
                    </select>
                </label>
                <label>Zero rates
                    <select className="xs-compounding" value={choice.annual ? 'Annual' : 'Continuous'} onChange={event => { change({ annual: event.target.value === 'Annual' }); }}>
                        <option value="Continuous">continuously compounded</option>
                        <option value="Annual">annually compounded</option>
                    </select>
                </label>
                <label>
                    <input type="checkbox" className="xs-compare" checked={choice.compare} onChange={event => { change({ compare: event.target.checked }); }} /> Compare with {earlier}
                </label>
            </div>
            <p className="xs-note">
                Curve date {today}. Deposits, then par swaps paying a fixed rate once a year, built from the mid quotes;
                instruments accrue ACT/360, the curve is in ACT/365 years. <b>Implied</b> is each instrument repriced off the built curve.
                Fit: the largest gap between a mid quote and its repriced rate is {fit} bp.
            </p>
            <div className="xs-panel">
                <ExGrid hideColumnMenu columns={pillarColumns} source={source} headerGroups={pillarGroups} pinnedColumnCount={1}
                    viewportHeight={300} viewportWidth="stretch" temporal={Temporal} />
            </div>
            <div className="xs-tools">
                <b>Chart</b>
                <Views label="Chart" className="xs-views xs-chart-views" views={chartViews} value={choice.chart} onChange={chart => { change({ chart }); }} />
            </div>
            <div className="xs-panel">
                <ExChart definition={definition} title={`${choice.curve} · ${choice.chart}`} width={1000} height={380}
                    axisStyle={{ placement: 'named' }} fileName={`${choice.curve.replaceAll(' ', '-')}-curve`} />
                <p className="xs-note">{viewNote(choice)}</p>
            </div>
        </>
    );
}

const views = ['CDS curve', 'By reference entity', 'Rates'] as const;
type View = (typeof views)[number];

/** The page /chart-showcase/curves of the Blazor hosts, under the built-in Chrome. */
export function CurvesShowcase(): ReactElement {
    const [view, setView] = useState<View>('CDS curve');
    return (
        <>
            <DemoNav route="chart-showcase/curves" />
            <ShowcaseShell app="Curve Viewer" crumb={`Market data · ${view}`} tools={<Views label="View" views={views} value={view} onChange={setView} />}>
                {view === 'CDS curve' ? <CdsCurveView /> : view === 'By reference entity' ? <EntityCurvesView /> : <RateCurveView />}
            </ShowcaseShell>
        </>
    );
}
