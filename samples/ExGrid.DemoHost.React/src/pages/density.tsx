import { useState, type ReactElement } from 'react';
import type { GridColumn, GridDensity, GridHeaderGroup } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';

/** One row of this page. */
interface DensityRow {
    readonly book: string;
    readonly trader: string;
    readonly notional: number;
}

/** Each preset's name as the Blazor page writes it: every preset has one, so the status never shows none. */
const labels: Readonly<Record<GridDensity, string>> = Object.freeze({
    compact: 'Compact', standard: 'Standard', comfortable: 'Comfortable', excel: 'Excel',
});

/** The presets, in the order the Blazor page offers them. */
const presets: readonly GridDensity[] = Object.freeze(['compact', 'standard', 'comfortable', 'excel']);

// Held once, never rebuilt per render (ADR-0003).
const rows: readonly DensityRow[] = Object.freeze(Array.from({ length: 200 }, (_, i): DensityRow => ({
    book: `Book ${i % 7}`,
    trader: `Trader ${String(i).padStart(3, '0')}`,
    notional: 1_000 * (i + 1),
})));

const columns: readonly GridColumn<DensityRow>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (r: DensityRow) => r.book, width: 140 },
    { name: 'Trader', type: 'text', value: (r: DensityRow) => r.trader, width: 140 },
    { name: 'Notional', type: 'number', value: (r: DensityRow) => r.notional, width: 160 },
]);

// One tier above the leaves: the band is two header tiers tall at every preset.
const groups: readonly GridHeaderGroup[] = Object.freeze([{ label: 'Who', columns: ['Book', 'Trader'] }]);

/** The page /density of the Blazor hosts: one grid whose density preset changes while it is on screen (ADR-0028). */
export function Density(): ReactElement {
    const [density, setDensity] = useState<GridDensity>('compact');
    return (
        <>
            <DemoNav route="density" />
            <h1>Density</h1>
            <p>
                One grid whose density preset changes while it is on screen (ADR-0028). Each preset resolves to Grid
                Metrics — the row height, the header tier&apos;s height, the cell padding and the font size — and the
                grid paints exactly those numbers and does its row arithmetic with the same ones (ADR-0027, P6). Select
                a range, then switch: the rows, the header band and the Selection&apos;s outline follow together.
            </p>
            <p className="demo-status">
                {presets.map(preset => (
                    <button key={preset} id={`density-${preset}`} type="button" onClick={() => { setDensity(preset); }}>{labels[preset]}</button>
                ))}{' '}
                <span id="density-status">Density: {labels[density]}</span>
            </p>
            <ExGrid columns={columns} rows={rows} headerGroups={groups} density={density} viewportHeight={320}
                viewportWidth="stretch" />
        </>
    );
}
