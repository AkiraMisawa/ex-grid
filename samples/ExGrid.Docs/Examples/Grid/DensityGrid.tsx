import { useState, type ReactElement } from 'react';
import { GridSource, type GridColumn, type GridDensity } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { samplePositions, type Position } from './Positions';

const source = GridSource.from(samplePositions());
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const columns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id },
    { name: 'Desk', type: 'text', value: p => p.desk },
    { name: 'Trader', type: 'text', value: p => p.trader },
    { name: 'Instrument', type: 'text', value: p => p.instrument },
    { name: 'Quantity', type: 'number', value: p => p.quantity, format: v => whole.format(v as number) },
    { name: 'Price', type: 'number', value: p => p.price, format: v => money.format(v as number) },
    { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', format: v => money.format(v as number) },
];

const densities: readonly GridDensity[] = ['compact', 'excel', 'standard', 'comfortable'];

export default function DensityGrid(): ReactElement {
    const [density, setDensity] = useState<GridDensity>('standard');
    const [stripes, setStripes] = useState(true);
    const [focusRow, setFocusRow] = useState(true);
    return (
        <>
            <div className="bar">
                <label>
                    Density{' '}
                    <select value={density} onChange={event => { setDensity(event.target.value as GridDensity); }}>
                        {densities.map(name => <option key={name} value={name}>{name}</option>)}
                    </select>
                </label>
                <label><input type="checkbox" checked={stripes} onChange={event => { setStripes(event.target.checked); }} /> Row stripes</label>
                <label><input type="checkbox" checked={focusRow} onChange={event => { setFocusRow(event.target.checked); }} /> Focus row</label>
            </div>
            <ExGrid source={source} columns={columns} density={density} stripeRows={stripes} highlightFocusRow={focusRow}
                viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
