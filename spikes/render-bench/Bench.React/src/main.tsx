// The core comparison's React host (ADR-0350 section 15): /core/grid and /core/template, the pages
// CoreGrid.razor draws on the Blazor hosts, over the Script Core under React. The same ids, the same rows
// from CoreData's formula, the same columns and geometry (README, "Where the Script Core plugs in"), so
// core-bench.js measures it unchanged. A template cell is a React component in the slot the core gives it.
import { useCallback, useMemo, useState, type ReactElement } from 'react';
import { createRoot } from 'react-dom/client';
import type { GridColumn, GridTemplateCell } from '@ex-grid/core';
import { ExGrid, templateColumn } from '@ex-grid/react';
import '../../Bench.Client/wwwroot/css/app.css';
import '../../../../src/ExGrid/wwwroot/ex-grid.min.css';
import '../../Bench.Live/wwwroot/core-bench.css';
import '../../Bench.Live/wwwroot/core-bench.js';

// CoreData's formula, written out again (CoreModel.cs is the contract, not a port).
const measures = 20;
const sizes = [1_000, 10_000, 100_000] as const;
const ticks = [1, 5, 25] as const;

interface CoreRow {
    readonly id: number;
    readonly gen: number;
    readonly book: string;
    readonly values: readonly number[];
}

const value = (id: number, column: number, gen: number): number =>
    ((id * 7919 + column * 104729 + gen * 15485863) % 2_000_000 - 1_000_000) / 100;

const coreRow = (id: number, gen: number): CoreRow => ({
    id,
    gen,
    book: `G${gen}-${id}`,
    values: Array.from({ length: measures }, (_, c) => value(id, c, gen)),
});

const windowOf = (size: number, gen: number): readonly CoreRow[] => Array.from({ length: size }, (_, i) => coreRow(i, gen));

const replace = (rows: readonly CoreRow[], count: number, gen: number): readonly CoreRow[] => {
    const next = rows.slice();
    for (let i = 0; i < count && i < next.length; i++) next[i] = coreRow(i, gen);
    return next;
};

// N2 in the invariant culture: grouped, two decimals.
const n2 = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const format = (v: unknown): string => n2.format(v as number);

const book: GridColumn<CoreRow> = { name: 'Book', type: 'text', value: r => r.book, width: { px: 120 } };
const measure = (index: number): GridColumn<CoreRow> =>
    ({ name: `M${index}`, type: 'number', value: r => r.values[index], width: { px: 120 }, format });

function TemplateValue({ value: v }: { readonly value: number }): ReactElement {
    return <span className={v < 0 ? 'core-neg' : 'core-pos'}>{format(v)}</span>;
}

// Held once, never rebuilt per render: a new columns array would paint every row again (ADR-0003).
const plainColumns: readonly GridColumn<CoreRow>[] = Object.freeze([book, ...Array.from({ length: measures }, (_, i) => measure(i))]);
const templateColumns: readonly GridColumn<CoreRow>[] = Object.freeze([
    book,
    ...Array.from({ length: measures - 4 }, (_, i) => measure(i)),
    // Two of markup, a bar as wide as the value's share of its range with the value beside it, and two of a component.
    ...[measures - 4, measures - 3].map(index => templateColumn<CoreRow>(
        { ...measure(index) },
        (cell: GridTemplateCell<CoreRow>) => (
            <div className="core-bar">
                <div className="core-bar-fill" style={{ width: `${Math.trunc((cell.row.values[index] + 10_000) / 200)}%` }} />
                <span>{format(cell.row.values[index])}</span>
            </div>
        ))),
    ...[measures - 2, measures - 1].map(index => templateColumn<CoreRow>(
        { ...measure(index) },
        (cell: GridTemplateCell<CoreRow>) => <TemplateValue value={cell.row.values[index]} />)),
]);

function CoreGrid({ templates }: { readonly templates: boolean }): ReactElement {
    const [state, setState] = useState(() => ({
        window: windowOf(sizes[sizes.length - 1], 0),
        gen: 0,
        prepared: undefined as readonly CoreRow[] | undefined,
        preparedGen: 0,
    }));
    const [mounted, setMounted] = useState(true);

    const prepare = useCallback((build: (s: typeof state) => readonly CoreRow[]) =>
        setState(s => ({ ...s, prepared: build(s), preparedGen: s.gen + 1 })), []);
    const push = useCallback(() => setState(s => {
        if (s.prepared === undefined) throw new Error('Push before Prepare: the driver pushes only a Window it prepared.');
        return { window: s.prepared, gen: s.preparedGen, prepared: undefined, preparedGen: 0 };
    }), []);
    const tick = useMemo(() => new Map(ticks.map(k => [k, () => setState(s => ({
        // A Window prepared before the tick is at a generation the tick now takes.
        window: replace(s.window, k, s.gen + 1), gen: s.gen + 1, prepared: undefined, preparedGen: 0,
    }))])), []);

    return (
        <div id="core-bench" style={{ width: 1200 }}>
            <div className="core-controls">
                {sizes.map(size => (
                    <button key={size} type="button" id={`core-prepare-all-${size}`} onClick={() => prepare(s => windowOf(size, s.gen + 1))}>
                        Prepare {size.toLocaleString('en-US')} rows
                    </button>
                ))}
                <button type="button" id="core-prepare-one" onClick={() => prepare(s => replace(s.window, 1, s.gen + 1))}>Prepare one row</button>
                <button type="button" id="core-push" onClick={push}>Push</button>
                {ticks.map(k => <button key={k} type="button" id={`core-tick-${k}`} onClick={tick.get(k)}>Tick {k}</button>)}
                <button type="button" id="core-unmount" onClick={() => setMounted(false)}>Unmount</button>
                <button type="button" id="core-mount" onClick={() => setMounted(true)}>Mount</button>
                <span id="core-state" data-size={state.window.length} data-gen={state.gen}
                    data-prepared={state.prepared === undefined ? '' : String(state.preparedGen)}>
                    {state.window.length.toLocaleString('en-US')} rows at generation {state.gen}
                </span>
            </div>
            {mounted && (
                <ExGrid<CoreRow> rows={state.window} columns={templates ? templateColumns : plainColumns}
                    rowHeight={22} viewportHeight={600} viewportWidth={1200} />
            )}
        </div>
    );
}

const scenario = location.pathname.replace(/\/$/, '').split('/').pop();
const app = document.getElementById('app');
if (app === null) throw new Error('index.html has no #app.');
if (scenario !== 'grid' && scenario !== 'template') {
    app.textContent = 'This host serves /core/grid and /core/template.';
} else {
    createRoot(app).render(<div className="content"><CoreGrid templates={scenario === 'template'} /></div>);
}
