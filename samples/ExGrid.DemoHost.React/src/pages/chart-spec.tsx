// The Chart Spec's page (/chart-spec), as ChartSpecPage.razor draws it (ADR-0320, ADR-0321): an American put's
// 4-step lattice priced by the page, as two tables a capability would return, drawn from the spec in the field,
// coloured by value, sized by the chance of reaching each node, its branches as edges. The tool's answer is
// written under the field. A second button loads a 3D surface's spec, and a third a spec with numbers in it,
// which is refused.
import { useState, type ReactElement } from 'react';
import { answerChartSpec, chartDataOf, type ChartData, type ChartSpecAnswer } from '@ex-grid/chart';
import { ExChart, ExSurface } from '@ex-grid/chart-react';
import { DemoNav } from '../demo-nav';
import { dotNetRound } from '../dotnet-math';
import { keyOf, price, type OptionTerms } from '../showcases/binomial-lattice-pricing';

const steps = 4;
const size = { width: 620, height: 380 };

function makeNodes(): ChartData {
    const terms: OptionTerms = { spot: 100, strike: 100, rate: 0.05, volatility: 0.3, maturity: 1, put: true, american: true };
    const { nodes } = price(terms, steps);
    const u = Math.exp(terms.volatility * Math.sqrt(terms.maturity / steps));
    const p = (Math.exp(terms.rate * terms.maturity / steps) - 1 / u) / (u - 1 / u);
    // C#'s Enumerable.Range(1, k).Aggregate(1.0, (c, i) => c * (n - k + i) / i), in its order.
    const choose = (n: number, k: number): number => {
        let c = 1;
        for (let i = 1; i <= k; i++) c = c * (n - k + i) / i;
        return c;
    };
    return chartDataOf([
        ['node', nodes.map(keyOf)],
        ['step', nodes.map(n => n.step)],
        ['spot', nodes.map(n => dotNetRound(n.spot, 2))],
        ['value', nodes.map(n => dotNetRound(n.value, 2))],
        ['reach', nodes.map(n => dotNetRound(choose(n.step, n.up) * (p ** n.up) * ((1 - p) ** (n.step - n.up)), 4))],
        ['exercised', nodes.map(n => (n.early ? 'early' : 'no'))],
        ['spare', nodes.map(() => 'not named')],
    ]);
}

function makeBranches(): ChartData {
    const pairs: (readonly [string, string])[] = [];
    for (let i = 0; i < steps; i++) {
        for (let j = 0; j <= i; j++) {
            pairs.push([`${String(i)}:${String(j)}`, `${String(i + 1)}:${String(j)}`], [`${String(i)}:${String(j)}`, `${String(i + 1)}:${String(j + 1)}`]);
        }
    }
    return chartDataOf([['from', pairs.map(e => e[0])], ['to', pairs.map(e => e[1])]]);
}

// A third table, for the surface spec: three tenors by three deltas, a row a point, the 1M 25C quote missing.
function makeSmile(): ChartData {
    return chartDataOf([
        ['tenor', ['1W', '1W', '1W', '1M', '1M', '1M', '1Y', '1Y', '1Y']],
        ['delta', ['25P', 'ATM', '25C', '25P', 'ATM', '25C', '25P', 'ATM', '25C']],
        ['vol', [11.8, 11.3, 11.6, 11.2, 10.7, null, 10.1, 9.5, 9.9]],
    ]);
}

// The tables the page's capabilities return, made when the page first reads one, not as the host loads.
let tables: ReadonlyMap<string, ChartData> | undefined;

const lookup = (handle: string): ChartData | undefined => {
    tables ??= new Map([['lattice', makeNodes()], ['branches', makeBranches()], ['smile', makeSmile()]]);
    return tables.get(handle);
};

const latticeSpec = `{
  "version": 1,
  "title": "American put on a 4-step lattice",
  "axis": { "values": "numbers", "title": "Step" },
  "series": [
    {
      "id": "nodes", "name": "Nodes", "kind": "observation",
      "data": "lattice", "x": "step", "y": "spot", "key": "node", "label": "node",
      "colour": { "column": "value", "scale": "sequential", "title": "Value" },
      "size": { "column": "reach", "title": "Chance of reaching" },
      "tooltip": ["exercised"],
      "edges": { "data": "branches", "from": "from", "to": "to" }
    }
  ]
}`;

const surfaceSpec = `{
  "version": 1,
  "title": "A small smile",
  "surface": { "data": "smile", "x": "delta", "y": "tenor", "z": "vol", "zTitle": "Volatility" }
}`;

const numbersSpec = `{
  "version": 1,
  "axis": { "values": "numbers" },
  "series": [
    { "id": "nodes", "name": "Nodes", "kind": "observation", "data": "lattice", "x": "step", "y": "spot",
      "values": [100, 109.1, 91.7] }
  ]
}`;

const answer = (spec: string): ChartSpecAnswer => answerChartSpec(spec, lookup, size);

function Drawn({ told }: { readonly told: ChartSpecAnswer }): ReactElement | null {
    if (told.reading === undefined) return null;
    const surface = told.reading.surface;
    return surface !== undefined
        ? <ExSurface definition={surface} title={told.reading.title} width={size.width} height={size.height} sliceWidth={360} />
        : <ExChart definition={told.reading.definition} title={told.reading.title} width={size.width} height={size.height} />;
}

export function ChartSpecPage(): ReactElement {
    const [spec, setSpec] = useState(latticeSpec);
    // The text last read, with what the tool answered for it.
    const [read, setRead] = useState(() => ({ text: latticeSpec, told: answer(latticeSpec) }));
    const take = (text: string): void => {
        setSpec(text);
        setRead({ text, told: answer(text) });
    };
    const told = read.told;

    return (
        <>
            <DemoNav route="chart-spec" />
            <h1>Charts from a Chart Spec</h1>
            <p>
                <button type="button" id="load-lattice" onClick={() => { take(latticeSpec); }}>The lattice</button>{' '}
                <button type="button" id="load-surface" onClick={() => { take(surfaceSpec); }}>A 3D surface</button>{' '}
                <button type="button" id="load-numbers" onClick={() => { take(numbersSpec); }}>A spec with numbers in it</button>
            </p>
            {/* Read again when the field is left changed, as Blazor's @bind reads it on change: a field left as it was
                keeps the chart, and what the user narrowed or hid on it. */}
            <textarea id="spec" value={spec} onChange={e => { setSpec(e.target.value); }}
                onBlur={e => { if (e.target.value !== read.text) take(e.target.value); }}
                rows={18} cols={70} spellCheck={false} style={{ fontFamily: 'monospace' }} />
            <p id="answer" role="status">{told.text}</p>
            <Drawn told={told} />
        </>
    );
}
