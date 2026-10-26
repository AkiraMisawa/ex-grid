import { useRef, useState, type ReactElement } from 'react';
import { interpolateBilinear, SurfaceDefinition, tenor, type SurfaceNode, type SurfaceSlice, type SurfaceTriangle, type SurfaceView } from '@ex-grid/chart';
import { ExSurface, type ExSurfaceApi } from '@ex-grid/chart-react';
import { DemoNav } from '../demo-nav';
import { dotNetRound } from '../dotnet-math';

const deltas = ['10P', '25P', 'ATM', '25C', '10C'] as const;
const tenors = ['1W', '1M', '3M', '6M', '1Y', '2Y', '5Y', '10Y'] as const;
const smile = [1.6, 0.5, 0, 0.3, 1.1] as const;

const twoPlaces = (v: number): string => v.toFixed(2);

// A smile over a term structure, deterministic, and moved a little by each run; no value at 25C 5Y.
function vol(run: number): SurfaceDefinition {
    const term = (t: number): number => 9.5 + 1.8 * Math.exp(-t / 2.5) + 0.12 * t;
    return SurfaceDefinition.grid({
        x: { title: 'Delta', values: deltas },
        y: { title: 'Tenor', values: tenors },
        z: tenors.map((tenor, t) => smile.map((wing, i) => (deltas[i] === '25C' && tenor === '5Y'
            ? null : dotNetRound(term(t) + wing * (1 + 0.1 * t) + run * 0.2, 2)))),
        zTitle: 'Volatility',
        zFormat: v => `${twoPlaces(Number(v))}%`,
    });
}

const deltaValues = [10, 25, 50, 75, 90];
const tenorYears = [7 / 365, 1 / 12, 0.25, 0.5, 1, 2, 5, 10];
const quotes = vol(0).z;

// The same quotes on years and deltas, interpolated bilinearly by the page and handed in (ADR-0257).
const interpolated = interpolateBilinear(deltaValues, tenorYears, quotes.map(row => row.map(v => (v === null ? null : Number(v)))))
    .surface({ xTitle: 'Delta', yTitle: 'Years', zTitle: 'Volatility', samples: 13 });

// Total variance on years placed by their logarithm, on a logarithmic vertical scale (ADR-0312).
const logVol = SurfaceDefinition.grid({
    x: { title: 'Delta', values: deltas },
    y: { title: 'Years', values: tenorYears, format: y => tenor(Number(y)), scale: 'logarithmic' },
    z: tenorYears.map((years, t) => {
        const row = quotes[t];
        if (row === undefined) throw new Error(`The quotes have no row for tenor ${String(t)}.`);
        return row.map(v => (v === null ? null : dotNetRound(Number(v) * Number(v) * years / 100, 4)));
    }),
    zTitle: 'Total variance',
    zScale: 'logarithmic',
});

// A book short volatility and long a little spot: P&L in thousands over spot and volatility shifts.
const spotShifts = [-10, -5, -2, 0, 2, 5, 10];
const volShifts = [-4, -2, 0, 2, 4];
const pnl = SurfaceDefinition.grid({
    x: { title: 'Spot shift %', values: spotShifts, scale: 'proportional' },
    y: { title: 'Vol shift', values: volShifts, scale: 'proportional' },
    z: volShifts.map(v => spotShifts.map(s => dotNetRound(12 * s - 3 * s * s - 40 * v, 1))),
    zTitle: 'P&L (k)',
    centre: 0,
});

// A five-by-five lattice on the unit square, its inner nodes nudged off the lattice, cut into triangles whose
// diagonal alternates, without the cell at (0.25, 0.25): a mesh, not a grid.
function lattice(): SurfaceDefinition {
    const nodes: SurfaceNode[] = [];
    for (let j = 0; j <= 4; j++) {
        for (let i = 0; i <= 4; i++) {
            const inner = i > 0 && i < 4 && j > 0 && j < 4;
            const x = dotNetRound(i / 4 + (inner ? 0.04 * (((i + 2 * j) % 3) - 1) : 0), 4);
            const y = dotNetRound(j / 4 + (inner ? 0.04 * (((2 * i + j) % 3) - 1) : 0), 4);
            nodes.push({ x, y, z: dotNetRound(Math.sin(Math.PI * x) * Math.cos(Math.PI * y), 4) });
        }
    }
    const triangles: SurfaceTriangle[] = [];
    for (let j = 0; j < 4; j++) {
        for (let i = 0; i < 4; i++) {
            if (i === 1 && j === 1) continue;
            const a = j * 5 + i;
            const [b, c, d] = [a + 1, a + 6, a + 5];
            if ((i + j) % 2 === 0) triangles.push({ a, b, c }, { a, b: c, c: d });
            else triangles.push({ a, b, c: d }, { a: b, b: c, c: d });
        }
    }
    return SurfaceDefinition.fromTriangles({ nodes, triangles, xTitle: 'x', yTitle: 'y', zTitle: 'u' });
}

const mesh = lattice();

const writeView = (v: SurfaceView): string =>
    `View: yaw ${String(v.yaw)}, pitch ${String(v.pitch)}, zoom ${String(v.zoom)}, pan ${String(v.panX)} ${String(v.panY)}`;

/** The page /surface of the Blazor hosts (tickets 30 to 34): an FX volatility surface with one hole, which the
 * page turns and slices as a Consumer would, keeping the view and the slice (ADR-0272) and writing the last
 * event raised; then the same quotes interpolated by the page, a surface coloured from zero, a mesh of the
 * page's own triangles, and logarithmic axes. */
export function Surface(): ReactElement {
    const [run, setRun] = useState(0);
    const [definition, setDefinition] = useState(() => vol(0));
    const [view, setView] = useState<SurfaceView | null>(null);
    const [slice, setSlice] = useState<SurfaceSlice | null>(null);
    const [events, setEvents] = useState('');
    const [svg, setSvg] = useState('');
    const surface = useRef<ExSurfaceApi>(null);

    const viewChanged = (next: SurfaceView): void => {
        setView(next);
        setEvents(writeView(next));
    };
    const sliceChanged = (next: SurfaceSlice): void => {
        setSlice(next);
        setEvents(`Slice: ${next.axis.toUpperCase()} ${String(next.value)}`);
    };
    const nextRun = (): void => {
        setRun(run + 1);
        setDefinition(vol(run + 1));
    };
    const svgText = (): void => {
        const text = surface.current?.svgText();
        setSvg(text === undefined ? 'No drawing' : `SVG: ${String(text.length)} characters, ${text.includes('ex-chart-heat') ? 'with' : 'without'} classes`);
    };

    return (
        <>
            <DemoNav route="surface" />
            <h1>3D surface</h1>
            <p>
                Drag to turn the surface, or use the arrow keys; Shift pans, the wheel or + and - zoom (ADR-0274).
                Move over it to read the grid point nearest the pointer, and click a tenor or a delta to draw that
                slice beside it. The surface has no value at 25C 5Y, and is drawn with a hole there.
            </p>
            <section id="vol">
                <button type="button" id="next-run" onClick={nextRun}>Next run</button>
                <button type="button" id="svg-text" onClick={svgText}>Copy SVG</button>
                <ExSurface
                    ref={surface}
                    definition={definition}
                    title="EURUSD volatility"
                    width={640}
                    height={420}
                    view={view}
                    onViewChanged={viewChanged}
                    slice={slice}
                    onSliceChanged={sliceChanged} />
                <p id="surface-events">{events}</p>
                <p id="surface-svg">{svg}</p>
            </section>
            <section id="interpolated">
                <h2>Interpolated by the page</h2>
                <p>
                    The same quotes on years and deltas, interpolated bilinearly by the page with the chart&apos;s helper
                    and handed in as a function (ADR-0257). The chart samples it, says the method under the surface and
                    in the tooltip, draws no dot at an interpolated value, and leaves a hole wherever a cell has a
                    missing corner: the helper does not reach past the quotes.
                </p>
                <ExSurface definition={interpolated} title="EURUSD volatility, bilinear" width={640} height={420} />
            </section>
            <section id="pnl">
                <h2>Coloured from zero</h2>
                <p>
                    A book&apos;s P&amp;L over spot and volatility shifts, coloured from zero (ADR-0310): losses red, gains
                    blue, each as deep as its distance from zero, so a small loss beside a large gain stays pale. The
                    colour bar beside it says which value each colour is.
                </p>
                <ExSurface definition={pnl} title="Scenario P&L" width={640} height={420} />
            </section>
            <section id="mesh">
                <h2>The page&apos;s own triangles</h2>
                <p>
                    A field on the page&apos;s own triangle mesh, drawn as its triangles (ADR-0311), with one cell of the
                    domain left out. The tooltip reads a node; a click on one slices through its position, cutting the
                    triangles.
                </p>
                <ExSurface definition={mesh} title="Field" width={640} height={420} />
            </section>
            <section id="log">
                <h2>Logarithmic axes</h2>
                <p>Tenors placed by their logarithm, and the values on a logarithmic scale (ADR-0312).</p>
                <ExSurface definition={logVol} title="Variance on years" width={640} height={420} />
            </section>
        </>
    );
}
