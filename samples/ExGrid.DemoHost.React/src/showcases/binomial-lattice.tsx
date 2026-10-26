// A binomial lattice (ADR-0280): ExChart.Showcases' BinomialLatticeShowcase under the built-in Chrome. An option
// priced on a Cox–Ross–Rubinstein lattice, each node a row of the grid and a mark of the chart beside it, coloured
// by the option's value, an American option's early exercise ringed; and the lattice price for every N against
// Black–Scholes. The page prices, as BinomialLattice.cs does; the grid and the charts only show what it priced
// (spine rule 3). How the screen builds its colour bands, ring and edges from series is said in the Razor
// component, whose stylesheet this page takes.
import { useMemo, useState, type ReactElement } from 'react';
import { GridSource, type GridColumn } from '@ex-grid/core';
import { ChartDefinition, type ChartPoint, type ChartSeriesOptions, type ChartValue } from '@ex-grid/chart';
import { ExChart } from '@ex-grid/chart-react';
import { ExGrid } from '@ex-grid/react';
import latticeSource from '../../../ExChart.Showcases/BinomialLatticeShowcase.razor?raw';
import { DemoNav } from '../demo-nav';
import { navigateTo, useQuery } from '../router';
import { fixed, numberOf } from './format';
import { razorStyle, ShowcaseShell } from './shell';

const latticeStyle = razorStyle(latticeSource, 'BinomialLatticeShowcase.razor');

/** One node: step i, up-moves j, the underlying there, the option's value, what exercise pays, and whether an American option is exercised there early. */
interface LatticeNode {
    readonly step: number;
    readonly up: number;
    readonly spot: number;
    readonly value: number;
    readonly exercise: number;
    readonly early: boolean;
}

const net = (x: LatticeNode): number => 2 * x.up - x.step;
const keyOf = (x: LatticeNode): string => `${String(x.step)}:${String(x.up)}`;

interface OptionTerms {
    readonly spot: number;
    readonly strike: number;
    readonly rate: number;
    readonly volatility: number;
    readonly maturity: number;
    readonly put: boolean;
    readonly american: boolean;
}

/** Prices `o` on an `n`-step lattice, returning the price and every node. */
function price(o: OptionTerms, n: number): { readonly price: number; readonly nodes: readonly LatticeNode[] } {
    const dt = o.maturity / n;
    const u = Math.exp(o.volatility * Math.sqrt(dt));
    const d = 1 / u;
    const p = (Math.exp(o.rate * dt) - d) / (u - d);
    const disc = Math.exp(-o.rate * dt);
    const payoff = (s: number): number => Math.max(o.put ? o.strike - s : s - o.strike, 0);
    const spotAt = (i: number, j: number): number => o.spot * (u ** j) * (d ** (i - j));
    const nodes: LatticeNode[] = new Array<LatticeNode>((n + 1) * (n + 2) / 2);
    const index = (i: number, j: number): number => i * (i + 1) / 2 + j;
    const v: number[] = new Array<number>(n + 1).fill(0);
    const held = (j: number): number => {
        const value = v[j];
        if (value === undefined) throw new Error(`Internal: the lattice holds no value at ${String(j)}.`);
        return value;
    };
    for (let j = 0; j <= n; j++) {
        const value = payoff(spotAt(n, j));
        v[j] = value;
        nodes[index(n, j)] = { step: n, up: j, spot: spotAt(n, j), value, exercise: value, early: false };
    }
    for (let i = n - 1; i >= 0; i--) {
        for (let j = 0; j <= i; j++) {
            const hold = disc * (p * held(j + 1) + (1 - p) * held(j));
            const exercise = payoff(spotAt(i, j));
            const early = o.american && exercise > hold;
            const value = early ? exercise : hold;
            v[j] = value;
            nodes[index(i, j)] = { step: i, up: j, spot: spotAt(i, j), value, exercise, early };
        }
    }
    const first = held(0);
    return { price: first, nodes };
}

// Abramowitz–Stegun 26.2.17, |error| < 7.5e-8.
function cdf(x: number): number {
    const t = 1 / (1 + 0.2316419 * Math.abs(x));
    const poly = t * (0.319381530 + t * (-0.356563782 + t * (1.781477937 + t * (-1.821255978 + t * 1.330274429))));
    const tail = Math.exp(-x * x / 2) / Math.sqrt(2 * Math.PI) * poly;
    return x >= 0 ? 1 - tail : tail;
}

/** The Black–Scholes price of the European option on the same terms. */
function blackScholes(o: OptionTerms): number {
    const sd = o.volatility * Math.sqrt(o.maturity);
    const d1 = (Math.log(o.spot / o.strike) + (o.rate + o.volatility * o.volatility / 2) * o.maturity) / sd;
    const d2 = d1 - sd;
    const df = Math.exp(-o.rate * o.maturity);
    return o.put ? o.strike * df * cdf(-d2) - o.spot * cdf(-d1) : o.spot * cdf(d1) - o.strike * df * cdf(d2);
}

interface Greeks { readonly delta: number; readonly gamma: number; readonly vega: number; readonly theta: number }

/** Delta, gamma, vega per volatility point and theta per day, read from an `n`-step lattice. */
function greeks(o: OptionTerms, n: number): Greeks {
    const { nodes } = price(o, n);
    const at = (i: number, j: number): LatticeNode => {
        const node = nodes[i * (i + 1) / 2 + j];
        if (node === undefined) throw new Error(`Internal: the lattice has no node ${String(i)}:${String(j)}.`);
        return node;
    };
    const slope = (a: LatticeNode, b: LatticeNode): number => (b.value - a.value) / (b.spot - a.spot);
    const delta = slope(at(1, 0), at(1, 1));
    const gamma = (slope(at(2, 1), at(2, 2)) - slope(at(2, 0), at(2, 1))) / ((at(2, 2).spot - at(2, 0).spot) / 2);
    const theta = (at(2, 1).value - at(0, 0).value) / (2 * o.maturity / n) / 365;
    const vega = (price({ ...o, volatility: o.volatility + 0.01 }, n).price - price({ ...o, volatility: o.volatility - 0.01 }, n).price) / 2;
    return { delta, gamma, vega, theta };
}

const maxDrawn = 10, minConvergence = 2, maxConvergence = 500, bands = 8, greekSteps = 200;
const chartWidth = 900, latticeHeight = 480, convergenceHeight = 320;

/** `ToString("0.0000")`. */
const money = (v: unknown): string => fixed(numberOf(v), 4);
const chartMoney = (v: ChartValue): string => fixed(Number(v), 4);

const columns: readonly GridColumn<LatticeNode>[] = Object.freeze([
    { name: 'Step', type: 'number', value: (x: LatticeNode) => x.step, header: 'Step i' },
    { name: 'Up', type: 'number', value: (x: LatticeNode) => x.up, header: 'Up-moves j' },
    { name: 'Net', type: 'number', value: (x: LatticeNode) => net(x), header: '2j − i' },
    { name: 'Spot', type: 'number', value: (x: LatticeNode) => x.spot, header: 'Underlying', format: money },
    { name: 'Value', type: 'number', value: (x: LatticeNode) => x.value, header: 'Option value', format: money },
    { name: 'Exercise', type: 'number', value: (x: LatticeNode) => x.exercise, header: 'Exercise pays', format: money },
    { name: 'Early', type: 'text', value: (x: LatticeNode) => (x.early ? 'Exercise' : ''), header: 'Early' },
]);

const position = (x: LatticeNode): ChartPoint => ({ x: x.step, y: net(x) });
const mark = (x: LatticeNode): ChartPoint => ({
    ...position(x),
    name: `i ${String(x.step)}, j ${String(x.up)}: S ${fixed(x.spot, 2)}, V ${fixed(x.value, 4)}${x.early ? ', exercised' : ''}`,
    row: keyOf(x),
});
const band = (v: number, max: number): number => Math.min(bands - 1, Math.trunc(v / max * bands));

function lattice(nodes: readonly LatticeNode[], n: number): ChartDefinition {
    const series: ChartSeriesOptions[] = [];
    // Slot order is the order keys are first named: the edges take slot 0, the ring 1, the bands 2 to 9.
    const edge = { colour: 'edge' };
    const byStep = (a: LatticeNode, b: LatticeNode): number => a.step - b.step;
    // Every edge lies on a diagonal: an up-diagonal holds the nodes with k down-moves, a down-diagonal those with k up-moves.
    for (let k = 0; k <= n; k++) {
        series.push({
            id: `edge-u${String(k)}`, name: `Edges, ${String(k)} down`, kind: 'level', keys: edge,
            points: nodes.filter(x => x.step - x.up === k).toSorted(byStep).map(position),
        }, {
            id: `edge-d${String(k)}`, name: `Edges, ${String(k)} up`, kind: 'level', keys: edge,
            points: nodes.filter(x => x.up === k).toSorted(byStep).map(position),
        });
    }
    series.push({ id: 'early', name: 'Early exercise', kind: 'observation', keys: { colour: 'early' }, points: nodes.filter(x => x.early).map(mark) });
    const max = Math.max(...nodes.map(x => x.value), 1e-9);
    for (let b = 0; b < bands; b++) {
        series.push({
            id: `band${String(b)}`, name: `Value ${fixed(max * b / bands, 1)}–${fixed(max * (b + 1) / bands, 1)}`, kind: 'observation',
            keys: { colour: `band${String(b)}` }, points: nodes.filter(x => band(x.value, max) === b).map(mark),
        });
    }
    return new ChartDefinition({
        axis: { values: 'numbers', title: 'Step i', format: x => fixed(x, 0) },
        series,
        notes: ['Colour is the option value, in eight bands; a ring marks early exercise.'],
    });
}

function convergence(terms: OptionTerms, nMax: number, bs: number): ChartDefinition {
    const points = Array.from({ length: nMax }, (_, k) => ({ x: k + 1, y: price(terms, k + 1).price }));
    return new ChartDefinition({
        axis: { values: 'numbers', title: 'Steps N', format: x => fixed(x, 0) },
        series: [
            { id: 'lattice', name: 'Lattice price', kind: 'level', keys: { colour: 'lattice', lineStyle: 'solid' }, fromZero: false, format: chartMoney, points },
            {
                id: 'black-scholes', name: `Black–Scholes ${fixed(bs, 4)}`, kind: 'level', keys: { colour: 'black-scholes', lineStyle: 'dashed' },
                fromZero: false, format: chartMoney, points: [{ x: 1, y: bs }, { x: nMax, y: bs }],
            },
        ],
    });
}

/** A switch of the screen's, as the built-in Chrome draws one. */
function Switch({ label, options, value, onChange }: {
    readonly label: string; readonly options: readonly string[]; readonly value: string; readonly onChange: (option: string) => void;
}): ReactElement {
    return (
        <nav className="xs-views" aria-label={label}>
            {options.map(option => (
                <button key={option} type="button" className={option === value ? 'xs-view xs-view-on' : 'xs-view'}
                    aria-pressed={option === value} onClick={() => { onChange(option); }}>{option}</button>
            ))}
        </nav>
    );
}

/** A number the user types, as the built-in Chrome draws one; a text that is no number changes nothing. */
function Field({ label, id, value, onChange }: {
    readonly label: string; readonly id: string; readonly value: number; readonly onChange: (value: number) => void;
}): ReactElement {
    const [text, setText] = useState(String(value));
    return (
        <label className={`xs-bin-field xs-bin-${id}`}>
            <span>{label}</span>
            <input type="number" step="any" value={text} onChange={event => {
                setText(event.target.value);
                const typed = event.target.valueAsNumber;
                if (Number.isFinite(typed)) onChange(typed);
            }} />
        </label>
    );
}

function Card({ label, value, className }: { readonly label: string; readonly value: string; readonly className: string }): ReactElement {
    return (
        <div className={`xs-bin-card ${className}`} role="group" aria-label={label}>
            <div className="xs-bin-label">{label}</div>
            <div className="xs-bin-value">{value}</div>
        </div>
    );
}

/** The page /chart-showcase/binomial-lattice of the Blazor hosts, under the built-in Chrome; ?scheme=dark in the dark scheme. */
export function BinomialLatticeShowcase(): ReactElement {
    const dark = useQuery('scheme') === 'dark';
    const [terms, setTerms] = useState<OptionTerms>({ spot: 100, strike: 100, rate: 0.05, volatility: 0.2, maturity: 1, put: false, american: false });
    const [n, setN] = useState(5);
    const [nMax, setNMax] = useState(200);
    const priced = useMemo(() => {
        const { price: p, nodes } = price(terms, n);
        const bs = blackScholes(terms);
        return {
            price: p, nodes, bs, maxValue: Math.max(...nodes.map(x => x.value)), greeks: greeks(terms, greekSteps),
            lattice: lattice(nodes, n), convergence: convergence(terms, nMax, bs),
            source: GridSource.from(nodes, { rowKey: keyOf }),
        };
    }, [terms, n, nMax]);
    const kind = `${terms.american ? 'American ' : 'European '}${terms.put ? 'put' : 'call'}`;
    const set = (change: Partial<OptionTerms>): void => { setTerms({ ...terms, ...change }); };
    // The scheme lives in the address, as every page's does: the switch asks for the other one.
    const scheme = (option: string): void => {
        const url = new URL(window.location.href);
        if (option === 'Dark') url.searchParams.set('scheme', 'dark');
        else url.searchParams.delete('scheme');
        navigateTo(url.href);
    };

    return (
        <>
            <DemoNav route="chart-showcase/binomial-lattice" />
            {dark ? <style>{':root { color-scheme: dark; }'}</style> : null}
            <style>{latticeStyle}</style>
            <div className="xs-bin-app">
            <ShowcaseShell app="Option Lattice Pricer" crumb="Derivatives · Binomial lattice"
                tools={<Switch label="Scheme" options={['Light', 'Dark']} value={dark ? 'Dark' : 'Light'} onChange={scheme} />}>
                <p className="xs-note">
                    An option priced on a recombining lattice. Each node is a row of the grid: the underlying after j up-moves in i steps,
                    and the option&apos;s value there. Change a term and the lattice, its rows and the convergence are priced again.
                </p>
                <div className="xs-bin">
                    <div className="xs-panel xs-bin-inputs">
                        <div className="xs-bin-heading">Contract</div>
                        <Switch label="Option type" options={['Call', 'Put']} value={terms.put ? 'Put' : 'Call'} onChange={s => { set({ put: s === 'Put' }); }} />
                        <Switch label="Exercise" options={['European', 'American']} value={terms.american ? 'American' : 'European'}
                            onChange={s => { set({ american: s === 'American' }); }} />
                        <Field label="Spot S₀" id="spot" value={terms.spot} onChange={v => { set({ spot: v }); }} />
                        <Field label="Strike K" id="strike" value={terms.strike} onChange={v => { set({ strike: v }); }} />
                        <Field label="Risk-free rate r" id="rate" value={terms.rate} onChange={v => { set({ rate: v }); }} />
                        <Field label="Volatility σ" id="vol" value={terms.volatility} onChange={v => { set({ volatility: v }); }} />
                        <Field label="Maturity T (years)" id="maturity" value={terms.maturity} onChange={v => { set({ maturity: v }); }} />
                        <div className="xs-bin-heading">Lattice</div>
                        <Field label={`Steps drawn N (1–${String(maxDrawn)})`} id="steps" value={n}
                            onChange={v => { setN(Math.min(maxDrawn, Math.max(1, Math.trunc(v)))); }} />
                        <Field label={`Convergence up to N (${String(minConvergence)}–${String(maxConvergence)})`} id="convergence" value={nMax}
                            onChange={v => { setNMax(Math.min(maxConvergence, Math.max(minConvergence, Math.trunc(v)))); }} />
                    </div>
                    <div className="xs-bin-main">
                        <div className="xs-bin-cards">
                            <Card label={`Lattice price (N = ${String(n)})`} value={fixed(priced.price, 4)} className="xs-bin-primary" />
                            <Card label="Black–Scholes, European" value={fixed(priced.bs, 4)} className="xs-bin-secondary" />
                            <Card label="Lattice − Black–Scholes" value={fixed(priced.price - priced.bs, 4)} className="" />
                        </div>
                        <div className="xs-bin-cards xs-bin-greeks">
                            <Card label="Delta Δ" value={fixed(priced.greeks.delta, 4)} className="" />
                            <Card label="Gamma Γ" value={fixed(priced.greeks.gamma, 5)} className="" />
                            <Card label="Vega ν, per vol point" value={fixed(priced.greeks.vega, 4)} className="" />
                            <Card label="Theta Θ, per day" value={fixed(priced.greeks.theta, 4)} className="" />
                        </div>
                        <div className="xs-panel xs-bin-lattice">
                            <ExChart definition={priced.lattice} title={`CRR lattice, ${kind}, N = ${String(n)}: net up-moves 2j − i by step i`}
                                width={chartWidth} height={latticeHeight} legend={{ position: 'right' }} />
                            <div className="xs-bin-bar" aria-hidden="true">
                                <div>Option value</div>
                                <div className="xs-bin-ramp" />
                                {[0, 1, 2, 3, 4].map(q => (
                                    <div key={q} className="xs-bin-tick" style={{ bottom: `calc(34px + (100% - 56px) * ${fixed(q / 4, 2)})` }}>
                                        {fixed(priced.maxValue * q / 4, 1)}
                                    </div>
                                ))}
                                {terms.american ? <div className="xs-bin-ring">early exercise</div> : null}
                            </div>
                        </div>
                        <div className="xs-panel">
                            <ExChart definition={priced.convergence} title={`Lattice price by N against Black–Scholes, ${kind}`}
                                width={chartWidth} height={convergenceHeight} />
                        </div>
                        <div className="xs-panel xs-bin-grid">
                            <ExGrid hideColumnMenu columns={columns} source={priced.source} viewportHeight={320} viewportWidth="stretch" />
                        </div>
                    </div>
                </div>
            </ShowcaseShell>
            </div>
        </>
    );
}
