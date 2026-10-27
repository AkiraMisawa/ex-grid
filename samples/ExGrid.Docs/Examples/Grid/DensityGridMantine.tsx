import { useState, type ReactElement } from 'react';
import { Group, SegmentedControl, Switch } from '@mantine/core';
import { GridSource, type GridColumn, type GridDensity } from '@ex-grid/core';
import { MantineExGrid } from '@ex-grid/mantine';
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

// Mantine's Table words: striped for Row Stripes, highlightOnHover for the hover band. The Density also picks the
// size the Wrapper's menus and panels are drawn at.
export default function DensityGridMantine(): ReactElement {
    const [density, setDensity] = useState<GridDensity>('standard');
    const [striped, setStriped] = useState(true);
    const [focusRow, setFocusRow] = useState(true);
    return (
        <>
            <Group mb="sm">
                <SegmentedControl size="xs" data={[...densities]} value={density} onChange={value => { setDensity(value as GridDensity); }} />
                <Switch label="Striped" checked={striped} onChange={event => { setStriped(event.currentTarget.checked); }} />
                <Switch label="Focus row" checked={focusRow} onChange={event => { setFocusRow(event.currentTarget.checked); }} />
            </Group>
            <MantineExGrid withBorder striped={striped} highlightOnHover source={source} columns={columns} density={density}
                highlightFocusRow={focusRow} viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
