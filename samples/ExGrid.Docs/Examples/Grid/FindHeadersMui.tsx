import { useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { FormControlLabel, Stack, Switch, Typography } from '@mui/material';
import { GridSource, type GridColumn, type GridHeaderGroup } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { sampleTrades, type Trade } from './Trades';

const source = GridSource.from(sampleTrades(2_000));
const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

// No cell says "price" or "economics": a step finds the column header or the Header Group label, marks it as the
// current match and says so in the panel, and the Focus stays on the cell it was on.
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book },
    { name: 'Trader', type: 'text', value: t => t.trader },
    { name: 'Instrument', type: 'text', value: t => t.instrument },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number) },
    { name: 'Price', type: 'number', value: t => t.price },
    { name: 'TradeDate', type: 'date', value: t => t.tradeDate, header: 'Trade date', dateType: 'dateOnly' },
];

const groups: readonly GridHeaderGroup[] = [
    { label: 'Who', columns: ['Book', 'Trader'] },
    { label: 'Economics', columns: ['Notional', 'Price'] },
];

export default function FindHeadersMui(): ReactElement {
    const [findsHeaders, setFindsHeaders] = useState(true);
    return (
        <>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
                <FormControlLabel label="Search the headers" control={<Switch checked={findsHeaders} onChange={event => { setFindsHeaders(event.target.checked); }} />} />
                <Typography variant="body2">Press Ctrl+F in the grid and type "price", then "economics"</Typography>
            </Stack>
            <MuiExGrid source={source} columns={columns} headerGroups={groups} findsHeaders={findsHeaders} temporal={Temporal}
                viewportHeight={320} viewportWidth="stretch" />
        </>
    );
}
