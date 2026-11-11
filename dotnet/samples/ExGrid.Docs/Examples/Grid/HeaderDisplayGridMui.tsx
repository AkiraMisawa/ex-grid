import { useState, type ReactElement } from 'react';
import { Button, FormControlLabel, Stack, Switch, ToggleButton, ToggleButtonGroup } from '@mui/material';
import { GridSource, type GridColumn, type GridFilter, type GridHeaderDisplay } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { samplePositions, type Position } from './Positions';

const source = GridSource.from(samplePositions());
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

// No width is declared, so every column is Auto and is measured under the display its header has.
const columns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id, header: 'ID' },
    { name: 'Currency', type: 'text', value: p => p.currency, header: 'Ccy', filterUi: 'both' },
    { name: 'Quantity', type: 'number', value: p => p.quantity, header: 'Qty', format: v => whole.format(v as number) },
    { name: 'Hedged', type: 'boolean', value: p => p.hedged },
    { name: 'Price', type: 'number', value: p => p.price, format: v => money.format(v as number) },
    { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', format: v => money.format(v as number) },
    { name: 'Desk', type: 'text', value: p => p.desk, filterUi: 'both' },
    { name: 'Instrument', type: 'text', value: p => p.instrument },
];

const displays: readonly GridHeaderDisplay[] = ['standard', 'compact'];

// A filter from the page's own controls: the only way to filter a grid that hides its column menu. The header of a
// filtered column shows the funnel either way; without the ▾ a standard header takes a box for it meanwhile.
const usdOnly: GridFilter = { columns: { Currency: { clauses: [{ operator: 'equals', value: 'USD' }] } } };
const onlyUsd = (): void => { source.setFilter(usdOnly); };
const clearFilter = (): void => { source.setFilter(undefined); };

// The Wrapper passes headerDisplay to the grid with its other props; the header's box is the core's under every Wrapper.
export default function HeaderDisplayGridMui(): ReactElement {
    const [display, setDisplay] = useState<GridHeaderDisplay>('compact');
    const [hideColumnMenu, setHideColumnMenu] = useState(false);
    return (
        <>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center', mb: 1.5 }}>
                <ToggleButtonGroup size="small" exclusive value={display}
                    onChange={(_, value: GridHeaderDisplay | null) => { if (value !== null) setDisplay(value); }}>
                    {displays.map(name => <ToggleButton key={name} value={name}>{name}</ToggleButton>)}
                </ToggleButtonGroup>
                <FormControlLabel label="Hide the column menu"
                    control={<Switch size="small" checked={hideColumnMenu} onChange={event => { setHideColumnMenu(event.target.checked); }} />} />
                <Button size="small" variant="outlined" onClick={onlyUsd}>Filter Ccy to USD</Button>
                <Button size="small" onClick={clearFilter}>Clear the filter</Button>
            </Stack>
            <MuiExGrid highlightHoverRow source={source} columns={columns} headerDisplay={display} hideColumnMenu={hideColumnMenu}
                viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
