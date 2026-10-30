import { useState, type ReactElement } from 'react';
import { FormControlLabel, Stack, Switch, ToggleButton, ToggleButtonGroup } from '@mui/material';
import { GridSource, type GridColumn, type GridDensity } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
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

// MUI's Table has no stripes, so Row Stripes and the hover band are the grid's own words. The Density also picks
// the size the Wrapper's menus and panels are drawn at.
export default function DensityGridMui(): ReactElement {
    const [density, setDensity] = useState<GridDensity>('standard');
    const [stripes, setStripes] = useState(true);
    const [focusRow, setFocusRow] = useState(true);
    return (
        <>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center', mb: 1.5 }}>
                <ToggleButtonGroup size="small" exclusive value={density}
                    onChange={(_, value: GridDensity | null) => { if (value !== null) setDensity(value); }}>
                    {densities.map(name => <ToggleButton key={name} value={name}>{name}</ToggleButton>)}
                </ToggleButtonGroup>
                <FormControlLabel label="Striped" control={<Switch checked={stripes} onChange={event => { setStripes(event.target.checked); }} />} />
                <FormControlLabel label="Focus row" control={<Switch checked={focusRow} onChange={event => { setFocusRow(event.target.checked); }} />} />
            </Stack>
            <MuiExGrid stripeRows={stripes} highlightHoverRow highlightFocusRow={focusRow} source={source} columns={columns}
                density={density} viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
