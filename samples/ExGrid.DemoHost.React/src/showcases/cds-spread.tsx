// CDS spread vs leverage (ADR-0290, ADR-0291): ExChart.Showcases' CreditScatterShowcase under the built-in
// Chrome. Two hundred reference names, each a row of the grid and a point of the scatter beside it, with the
// least-squares and the Theil–Sen line through them. One name, DSTR, is distressed: it pulls the least-squares
// line up and leaves Theil–Sen where the others put it. The page fits both, which is the page computing, and
// each line puts its method and coefficients in the Notes. The names are fixed values (data.ts).
import { useCallback, useRef, useState, type ReactElement } from 'react';
import { GridSource, type GridColumn } from '@ex-grid/core';
import {
    ChartDefinition, fitLeastSquares, fitTheilSen, GridChartBridge, type AxisScale, type ChartPointClick, type ChartRange, type ChartValue,
} from '@ex-grid/chart';
import { ExChart } from '@ex-grid/chart-react';
import { ExGrid, type ExGridApi } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { creditDataAsOf, creditDataNames, type CreditName } from './data';
import { fixed, grouped, numberOf } from './format';
import { ShowcaseShell } from './shell';

const title = 'CDS spread vs leverage';
const asOf = creditDataAsOf.toString();

/** `ToString("0.00") + "x"` and `ToString("#,##0") + "bp"`. */
const times = (v: number): string => `${fixed(v, 2)}x`;
const bp = (v: ChartValue): string => `${grouped(Number(v), 0)}bp`;

const columns: readonly GridColumn<CreditName>[] = Object.freeze([
    { name: 'Ticker', type: 'text', value: (n: CreditName) => n.ticker, header: 'Name' },
    { name: 'Leverage', type: 'number', value: (n: CreditName) => n.leverage, header: 'Leverage', format: (v: unknown) => times(numberOf(v)) },
    { name: 'Spread', type: 'number', value: (n: CreditName) => n.spreadBp, header: '5Y spread', format: (v: unknown) => bp(numberOf(v)) },
]);

const keyOf = (n: CreditName): string => n.ticker;
const source = GridSource.from(creditDataNames, { rowKey: keyOf });
const byTicker: ReadonlyMap<string, CreditName> = new Map(creditDataNames.map(n => [n.ticker, n]));

// The page fits both lines once, over every name: the chart draws them and names their method (ADR-0291).
const definition = ((): ChartDefinition => {
    const xs = creditDataNames.map(n => n.leverage);
    const ys = creditDataNames.map(n => n.spreadBp);
    return new ChartDefinition({
        axis: { values: 'numbers', title: 'Leverage', format: times },
        series: [
            {
                id: 'names', name: 'Names', kind: 'observation', format: bp,
                points: creditDataNames.map(n => ({ x: n.leverage, y: n.spreadBp, name: n.ticker, row: n.ticker })),
            },
            { ...fitLeastSquares(xs, ys).series('ols', 'Least squares'), format: bp },
            { ...fitTheilSen(xs, ys).series('theil-sen', 'Theil–Sen'), format: bp },
        ],
        notes: ['Synthetic names; DSTR is distressed.'],
    });
})();

function rangeText(r: ChartRange | null): string {
    if (r === null) return `${String(creditDataNames.length)} names`;
    const two = (v: number | undefined): string => (v === undefined ? '' : fixed(v, 2));
    const whole = (v: number | undefined): string => (v === undefined ? '' : grouped(v, 0));
    return `Leverage ${two(r.from)}x–${two(r.to)}x, spread ${whole(r.yFrom)}–${whole(r.yTo)}bp`;
}

/** The page /chart-showcase/cds-spread of the Blazor hosts, under the built-in Chrome. */
export function CdsSpreadShowcase(): ReactElement {
    const [logSpread, setLogSpread] = useState(false);
    // The chart holds no View State (ADR-0272): the rectangle comes back as an event, and the page hands it back.
    const [range, setRange] = useState<ChartRange | null>(null);
    const [refusal, setRefusal] = useState<string | undefined>(undefined);
    const grid = useRef<ExGridApi<CreditName>>(null);
    const bridge = useRef<GridChartBridge<CreditName> | undefined>(undefined);
    bridge.current ??= new GridChartBridge<CreditName>({
        declarations: { rowKey: keyOf, rowOf: key => (typeof key === 'string' ? byTicker.get(key) : undefined) },
        grid: () => ({ columns, source }),
        onRefused: setRefusal,
        placeSelection: ({ range: placed, focus, rowSequenceVersion }) => {
            if (grid.current === null) throw new Error('Internal: the grid is not mounted.');
            return grid.current.placeSelection(placed, focus, rowSequenceVersion);
        },
    });
    const clicked = useCallback((click: ChartPointClick): void => {
        setRefusal(undefined);
        if (typeof click.row === 'string') void bridge.current?.showCellInGrid({ rowKey: click.row, column: 'Spread' });
    }, []);
    const yScale: AxisScale | undefined = logSpread ? 'logarithmic' : undefined;

    return (
        <>
            <DemoNav route="chart-showcase/cds-spread" />
            <ShowcaseShell app="Credit Relative Value" crumb="Credit · CDS spread vs leverage" tools={
                <>
                    <span className="xs-crumb xs-range" role="status">{rangeText(range)}</span>
                    <span className="xs-tools">
                        <button type="button" className="xs-button xs-reset" disabled={range === null} onClick={() => { setRange(null); }}>Whole range</button>
                        <label className="xs-log">
                            <input type="checkbox" checked={logSpread} onChange={event => { setLogSpread(event.target.checked); setRange(null); }} /> Log spread
                        </label>
                    </span>
                </>
            }>
                <p className="xs-note">
                    Five-year CDS spreads against leverage (debt over EBITDA) on {asOf}. Hover a point to read it,
                    click it to go to its row, drag a rectangle to narrow both axes. The fitted lines' methods and coefficients are
                    under the chart.
                </p>
                {refusal === undefined ? null : <p className="xs-refusal" role="alert">{refusal}</p>}
                <div className="xs-side">
                    <div className="xs-panel xs-side-grid">
                        <ExGrid ref={grid} hideColumnMenu columns={columns} source={source} viewportHeight={470} viewportWidth="stretch" />
                    </div>
                    <div className="xs-panel">
                        <ExChart definition={definition} title={title} fileName={title} width={680} height={440}
                            {...(yScale === undefined ? {} : { yScale })}
                            range={range} onRangeChanged={setRange} onPointClick={clicked} />
                    </div>
                </div>
            </ShowcaseShell>
        </>
    );
}
