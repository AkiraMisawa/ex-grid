import { useRef, useState, type ReactElement } from 'react';
import { SurfaceDefinition, type SurfaceNode, type SurfaceSlice, type SurfaceTriangle, type SurfaceView } from '@ex-grid/chart';
import { ExSurface, type ExSurfaceApi } from '@ex-grid/chart-react';
import { DemoNav } from '../demo-nav';

const deltas = ['10P', '25P', 'ATM', '25C', '10C'] as const;
const tenors = ['1W', '1M', '3M', '6M', '1Y', '2Y', '5Y', '10Y'] as const;
const smile = [1.6, 0.5, 0, 0.3, 1.1] as const;

const round = (v: number, digits: number): number => Math.round(v * 10 ** digits) / 10 ** digits;
const twoPlaces = (v: number): string => v.toFixed(2);

// A smile over a term structure, deterministic, and moved a little by each run; no value at 25C 5Y.
function vol(run: number): SurfaceDefinition {
    const term = (t: number): number => 9.5 + 1.8 * Math.exp(-t / 2.5) + 0.12 * t;
    return SurfaceDefinition.grid({
        x: { title: 'Delta', values: deltas },
        y: { title: 'Tenor', values: tenors },
        z: tenors.map((tenor, t) => smile.map((wing, i) => (deltas[i] === '25C' && tenor === '5Y'
            ? null : round(term(t) + wing * (1 + 0.1 * t) + run * 0.2, 2)))),
        zTitle: 'Volatility',
        zFormat: v => `${twoPlaces(Number(v))}%`,
    });
}

// A book short volatility and long a little spot: P&L in thousands over spot and volatility shifts.
const spotShifts = [-10, -5, -2, 0, 2, 5, 10];
const volShifts = [-4, -2, 0, 2, 4];
const pnl = SurfaceDefinition.grid({
    x: { title: 'Spot shift %', values: spotShifts, scale: 'proportional' },
    y: { title: 'Vol shift', values: volShifts, scale: 'proportional' },
    z: volShifts.map(v => spotShifts.map(s => round(12 * s - 3 * s * s - 40 * v, 1))),
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
            const x = round(i / 4 + (inner ? 0.04 * (((i + 2 * j) % 3) - 1) : 0), 4);
            const y = round(j / 4 + (inner ? 0.04 * (((2 * i + j) % 3) - 1) : 0), 4);
            nodes.push({ x, y, z: round(Math.sin(Math.PI * x) * Math.cos(Math.PI * y), 4) });
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
 * event raised; then a surface coloured from zero and a mesh of the page's own triangles. */
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
        </>
    );
}
