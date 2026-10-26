import { useCallback, useRef, useState, type ReactElement } from 'react';
import { GridSource, type GridColumn } from '@ex-grid/core';
import {
    ChartDefinition, fitLeastSquares, fitPolynomial, fitTheilSen, GridChartBridge, type AxisScale, type ChartPointClick, type ChartRange,
    type ChartValue,
} from '@ex-grid/chart';
import { ExChart } from '@ex-grid/chart-react';
import { ExGrid, type ExGridApi } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { dotNetRound } from '../dotnet-math';
import { DotNetRandom } from '../dotnet-random';

/** One reference name: its leverage and five-year spread. */
interface Name {
    readonly ticker: string;
    readonly leverage: number;
    readonly spread: number;
}

function gaussian(rng: DotNetRandom): number {
    return Math.sqrt(-2 * Math.log(1 - rng.nextDouble())) * Math.cos(2 * Math.PI * rng.nextDouble());
}

// Two hundred synthetic names from the Blazor page's seed, drawn in its order, so all three hosts show the
// same names; DSTR, distressed, stands at row 57.
function makeNames(): readonly Name[] {
    const rng = new DotNetRandom(20261019);
    const names: Name[] = [];
    for (let i = 0; i < 199; i++) {
        const leverage = dotNetRound(1 + 5 * rng.nextDouble() * rng.nextDouble() + rng.nextDouble(), 2);
        const spread = dotNetRound(Math.exp(Math.log(35) + 0.42 * leverage + 0.35 * gaussian(rng)), 0);
        names.push(Object.freeze({ ticker: `N${String(i + 1).padStart(3, '0')}`, leverage, spread }));
    }
    names.splice(57, 0, Object.freeze({ ticker: 'DSTR', leverage: 5.6, spread: 2400 }));
    return Object.freeze(names);
}

const cdsNames = makeNames();

const keyOf = (n: Name): string => n.ticker;

/** The Blazor page's `ToString("0.00") + "x"` and `ToString("0") + "bp"`. */
const leverageText = (v: number): string => `${v.toFixed(2)}x`;
const spreadText = (v: ChartValue): string => `${Number(v).toFixed(0)}bp`;

const cdsColumns: readonly GridColumn<Name>[] = Object.freeze([
    { name: 'ticker', type: 'text', value: (n: Name) => n.ticker, header: 'Name' },
    { name: 'leverage', type: 'number', value: (n: Name) => n.leverage, header: 'Leverage', format: (v: unknown) => leverageText(Number(v)) },
    { name: 'spread', type: 'number', value: (n: Name) => n.spread, header: '5Y spread', format: (v: unknown) => spreadText(Number(v)) },
]);

const cdsSource = GridSource.from(cdsNames, { rowKey: keyOf });

function makeCds(): ChartDefinition {
    const xs = cdsNames.map(n => n.leverage);
    const ys = cdsNames.map(n => n.spread);
    return new ChartDefinition({
        axis: { values: 'numbers', title: 'Leverage', format: leverageText },
        series: [
            {
                id: 'names', name: 'Names', kind: 'observation', format: spreadText,
                points: cdsNames.map(n => ({ x: n.leverage, y: n.spread, name: n.ticker, row: n.ticker })),
            },
            { ...fitLeastSquares(xs, ys).series('ols', 'Least squares'), format: spreadText },
            { ...fitTheilSen(xs, ys).series('theil-sen', 'Theil–Sen'), format: spreadText },
        ],
        notes: ['Synthetic names from a fixed seed; DSTR is distressed.'],
    });
}

const cds = makeCds();

const faithfulData: readonly (readonly [number, number])[] = [
    [3.6, 79], [1.8, 54], [3.333, 74], [2.283, 62], [4.533, 85], [2.883, 55], [4.7, 88], [3.6, 85], [1.95, 51], [4.35, 85],
    [1.833, 54], [3.917, 84], [4.2, 78], [1.75, 47], [4.7, 83], [2.167, 52], [1.75, 62], [4.8, 84], [1.6, 52], [4.25, 79],
    [1.8, 51], [1.75, 47], [3.45, 78], [3.067, 69], [4.533, 74], [3.6, 83], [1.967, 55], [4.083, 76], [3.85, 78], [4.433, 79],
    [4.3, 73], [4.467, 77], [3.367, 66], [4.033, 80], [3.833, 74], [2.017, 52], [1.867, 48], [4.833, 80], [1.833, 59], [4.783, 90],
    [4.35, 80], [1.883, 58], [4.567, 84], [1.75, 58], [4.533, 73], [3.317, 83], [3.833, 64], [2.1, 53], [4.633, 82], [2, 59],
    [4.8, 75], [4.716, 90], [1.833, 54], [4.833, 80], [1.733, 54], [4.883, 83], [3.717, 71], [1.667, 64], [4.567, 77], [4.317, 81],
    [2.233, 59], [4.5, 84], [1.75, 48], [4.8, 82], [1.817, 60], [4.4, 92], [4.167, 78], [4.7, 78], [2.067, 65], [4.7, 73],
    [4.033, 82], [1.967, 56], [4.5, 79], [4, 71], [1.983, 62], [5.067, 76], [2.017, 60], [4.567, 78], [3.883, 76], [3.6, 83],
    [4.133, 75], [4.333, 82], [4.1, 70], [2.633, 65], [4.067, 73], [4.933, 88], [3.95, 76], [4.517, 80], [2.167, 48], [4, 86],
    [2.2, 60], [4.333, 90], [1.867, 50], [4.817, 78], [1.833, 63], [4.3, 72], [4.667, 84], [3.75, 75], [1.867, 51], [4.9, 82],
    [2.483, 62], [4.367, 88], [2.1, 49], [4.5, 83], [4.05, 81], [1.867, 47], [4.7, 84], [1.783, 52], [4.85, 86], [3.683, 81],
    [4.733, 75], [2.3, 59], [4.9, 89], [4.417, 79], [1.7, 59], [4.633, 81], [2.317, 50], [4.6, 85], [1.817, 59], [4.417, 87],
    [2.617, 53], [4.067, 69], [4.25, 77], [1.967, 56], [4.6, 88], [3.767, 81], [1.917, 45], [4.5, 82], [2.267, 55], [4.65, 90],
    [1.867, 45], [4.167, 83], [2.8, 56], [4.333, 89], [1.833, 46], [4.383, 82], [1.883, 51], [4.933, 86], [2.033, 53], [3.733, 79],
    [4.233, 81], [2.233, 60], [4.533, 82], [4.817, 77], [4.333, 76], [1.983, 59], [4.633, 80], [2.017, 49], [5.1, 96], [1.8, 53],
    [5.033, 77], [4, 77], [2.4, 65], [4.6, 81], [3.567, 71], [4, 70], [4.5, 81], [4.083, 93], [1.8, 53], [3.967, 89],
    [2.2, 45], [4.15, 86], [2, 58], [3.833, 78], [3.5, 66], [4.583, 76], [2.367, 63], [5, 88], [1.933, 52], [4.617, 93],
    [1.917, 49], [2.083, 57], [4.583, 77], [3.333, 68], [4.167, 81], [4.333, 81], [4.5, 73], [2.417, 50], [4, 85], [4.167, 74],
    [1.883, 55], [4.583, 77], [4.25, 83], [3.767, 83], [2.033, 51], [4.433, 78], [4.083, 84], [1.833, 46], [4.417, 83], [2.183, 55],
    [4.8, 81], [1.833, 57], [4.8, 76], [4.1, 84], [3.966, 77], [4.233, 81], [3.5, 87], [4.366, 77], [2.25, 51], [4.667, 78],
    [2.1, 60], [4.35, 82], [4.133, 91], [1.867, 53], [4.6, 78], [1.783, 46], [4.367, 77], [3.85, 84], [1.933, 49], [4.5, 83],
    [2.383, 71], [4.7, 80], [1.867, 49], [3.833, 75], [3.417, 64], [4.233, 76], [2.4, 53], [4.8, 94], [2, 55], [4.15, 76],
    [1.867, 50], [4.267, 82], [1.75, 54], [4.483, 75], [4, 78], [4.117, 79], [4.083, 78], [4.267, 78], [3.917, 70], [4.55, 79],
    [4.083, 70], [2.417, 54], [4.183, 86], [2.217, 50], [4.45, 90], [1.883, 54], [1.85, 54], [4.283, 77], [3.95, 79], [2.333, 64],
    [4.15, 75], [2.35, 47], [4.933, 86], [2.9, 63], [4.583, 85], [3.833, 82], [2.083, 57], [4.367, 82], [2.133, 67], [4.35, 74],
    [2.2, 54], [4.45, 83], [3.567, 73], [4.5, 73], [4.15, 88], [3.817, 80], [3.917, 71], [4.45, 83], [2, 56], [4.283, 79],
    [4.767, 78], [4.533, 84], [1.85, 58], [4.25, 83], [1.983, 43], [2.25, 60], [4.75, 75], [4.117, 81], [2.15, 46], [4.417, 90],
    [1.817, 46], [4.467, 74],
];

const faithful = new ChartDefinition({
    axis: { values: 'numbers', title: 'Eruption (minutes)' },
    series: [{
        id: 'eruptions', name: 'Eruptions', kind: 'observation',
        points: faithfulData.map(([eruption, waiting], i) => ({ x: eruption, y: waiting, name: `Eruption ${String(i + 1)}: waited ${String(waiting)} minutes` })),
    }],
});

const degrees: readonly number[] = [0, 1, 3, 9];

// PRML §1.1: ten points evenly across [0, 1], sin 2πx plus Gaussian noise of standard deviation 0.3.
const curveX = Array.from({ length: 10 }, (_, i) => i / 9);
const curveT = ((): readonly number[] => {
    const rng = new DotNetRandom(1);
    return curveX.map(x => Math.sin(2 * Math.PI * x) + 0.3 * gaussian(rng));
})();

const curves: ReadonlyMap<number, ChartDefinition> = new Map(degrees.map(m => [m, new ChartDefinition({
    axis: { values: 'numbers', title: 'x' },
    series: [
        { id: 'points', name: 'Training points', kind: 'observation', points: curveT.map((y, i) => ({ x: i / 9, y })) },
        { id: 'truth', name: 'sin 2πx', kind: 'level', fromZero: false, sampler: { from: 0, to: 1, valueAt: x => Math.sin(2 * Math.PI * x) } },
        fitPolynomial(curveX, curveT, m).series('fit', `M = ${String(m)}`),
    ],
})]));

function curveOf(degree: number): ChartDefinition {
    const curve = curves.get(degree);
    if (curve === undefined) throw new Error(`Internal: no curve of degree ${String(degree)} is made.`);
    return curve;
}

// PRML's frame, held still as M changes, so M = 9's swings are cut at its edge rather than stretching the axis.
const curveRange: ChartRange = Object.freeze({ from: 0, to: 1, yFrom: -1.5, yTo: 1.5 });

function rangeText(r: ChartRange | null): string {
    if (r === null) return 'Not narrowed';
    const two = (v: number | undefined): string => (v === undefined ? '' : v.toFixed(2));
    const whole = (v: number | undefined): string => (v === undefined ? '' : v.toFixed(0));
    return `Leverage ${two(r.from)}–${two(r.to)}, spread ${whole(r.yFrom)}–${whole(r.yTo)}bp`;
}

/**
 * The page /scatter of the Blazor hosts (tickets 54 to 59, ADR-0290, ADR-0291): CDS spreads against leverage
 * beside their grid, with the least-squares and the Theil–Sen line; Old Faithful's eruptions against the wait
 * before them; and PRML's curve fitting, polynomials of degree 0, 1, 3 and 9 through ten noisy points of sin
 * 2πx. A click on a point moves the grid's Focus to its row, through the grid's own placement (ADR-0050).
 */
export function Scatter(): ReactElement {
    const [degree, setDegree] = useState(3);
    const [logSpread, setLogSpread] = useState(false);
    const [range, setRange] = useState<ChartRange | null>(null);
    const [refusal, setRefusal] = useState('');
    const grid = useRef<ExGridApi<Name>>(null);
    const bridge = useRef<GridChartBridge<Name> | undefined>(undefined);
    bridge.current ??= new GridChartBridge<Name>({
        declarations: { rowKey: keyOf, rowOf: key => cdsNames.find(n => n.ticker === key) },
        grid: () => ({ columns: cdsColumns, source: cdsSource }),
        onRefused: setRefusal,
        placeSelection: ({ range: placed, focus, rowSequenceVersion }) => {
            if (grid.current === null) throw new Error('Internal: the grid is not mounted.');
            return grid.current.placeSelection(placed, focus, rowSequenceVersion);
        },
    });
    const clicked = useCallback((click: ChartPointClick): void => {
        setRefusal('');
        if (typeof click.row === 'string') void bridge.current?.showCellInGrid({ rowKey: click.row, column: 'spread' });
    }, []);
    const yScale: AxisScale | undefined = logSpread ? 'logarithmic' : undefined;

    return (
        <>
            <DemoNav route="scatter" />
            <h1>Scatter and fitted lines</h1>

            <h2>CDS spread vs leverage</h2>
            <p>
                Two hundred synthetic names (a fixed seed), one of them distressed. The least-squares line is pulled
                up by it; the Theil–Sen line is not. Each line is drawn over the leverage it was fitted on, and its
                method and coefficients are in the notes under the chart. Hover a point to read it, click it to move
                the grid to its row, and drag a rectangle to narrow both axes.
            </p>
            <p>
                <label>
                    <input type="checkbox" id="log-spread" checked={logSpread}
                        onChange={event => { setLogSpread(event.target.checked); setRange(null); }} />
                    {' '}Spread on a logarithmic axis
                </label>
                {' '}
                <span id="cds-range" role="status">{rangeText(range)}</span>
            </p>
            <div className="scatter-side-by-side">
                <ExGrid ref={grid} columns={cdsColumns} source={cdsSource} viewportHeight={360} viewportWidth={380} />
                <ExChart definition={cds} title="CDS spread vs leverage" width={620} height={360}
                    {...(yScale === undefined ? {} : { yScale })}
                    range={range} onRangeChanged={setRange} onPointClick={clicked} />
            </div>
            <p id="cds-refusal" role="status">{refusal}</p>

            <h2>Old Faithful</h2>
            <p>
                272 eruptions of the Old Faithful geyser: how long each lasted, against the wait before it. The two
                clusters are the classic first look at a scatter (Bishop, <i>Pattern Recognition and Machine
                Learning</i>, figure 9.1).
            </p>
            <ExChart definition={faithful} title="Old Faithful" width={620} height={360} />

            <h2>Polynomial curve fitting</h2>
            <p>
                Ten points of sin 2πx with noise, and the least-squares polynomial of degree M through them (PRML
                §1.1). M = 9 passes through every point and swings between them: it has learnt the noise.
            </p>
            <p>
                {degrees.map(m => (
                    <button key={m} type="button" className="degree" data-degree={m} aria-pressed={m === degree ? 'true' : 'false'}
                        onClick={() => { setDegree(m); }}>M = {m}</button>
                ))}
            </p>
            <ExChart definition={curveOf(degree)} title="Polynomial curve fitting" width={620} height={360} range={curveRange} />
        </>
    );
}
