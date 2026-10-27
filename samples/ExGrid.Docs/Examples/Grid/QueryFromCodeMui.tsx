import { useSyncExternalStore, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { Button, Stack, Typography } from '@mui/material';
import { GridSource, type GridColumn, type GridFilter, type GridSourceState } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { sampleTrades, type Trade } from './Trades';

const source = GridSource.from(sampleTrades(2_000));
const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const count = new Intl.NumberFormat('en-US');

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book },
    { name: 'Trader', type: 'text', value: t => t.trader },
    { name: 'Instrument', type: 'text', value: t => t.instrument },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number) },
    { name: 'TradeDate', type: 'date', value: t => t.tradeDate, header: 'Trade date', dateType: 'dateOnly' },
];

// The same Filter the panel builds: per column, clauses; across columns, AND.
const ratesFromTwentyFiveMillion: GridFilter = {
    columns: {
        Book: { clauses: [{ operator: 'equals', value: 'Rates' }] },
        Notional: { clauses: [{ operator: 'greaterThanOrEqual', value: 25_000_000 }] },
    },
};

const largeRatesTrades = (): void => { source.setFilter(ratesFromTwentyFiveMillion); };
const largestFirst = (): void => { source.setSorts([{ column: 'Notional', direction: 'descending' }]); };
const clear = (): void => {
    source.setFilter(undefined);
    source.setSorts([]);
};

function describe(state: GridSourceState<Trade>): string {
    const filtered = state.filter === undefined ? 'nothing' : Object.keys(state.filter.columns).join(', ');
    const sorted = state.sorts.length > 0 ? state.sorts.map(sort => `${sort.column} ${sort.direction}`).join(', ') : 'nothing';
    // The size of the result, not the rows in hand: a fetching source holds a page of it.
    const trades = state.totalCount === undefined ? 'An unknown number of' : count.format(state.totalCount);
    return `${trades} trades. Filtered on ${filtered}; sorted by ${sorted}.`;
}

const subscribe = (listener: () => void): (() => void) => source.subscribe(listener);
const snapshot = (): GridSourceState<Trade> => source.snapshot();

// The header, the column menu and the filter panel change the source too, so the line below follows the source,
// not the buttons.
export default function QueryFromCodeMui(): ReactElement {
    const state = useSyncExternalStore(subscribe, snapshot);
    return (
        <>
            <Stack direction="row" spacing={1} sx={{ mb: 1.5 }}>
                <Button size="small" variant="outlined" onClick={largeRatesTrades}>Rates, 25m and over</Button>
                <Button size="small" variant="outlined" onClick={largestFirst}>Largest notional first</Button>
                <Button size="small" onClick={clear}>Clear</Button>
            </Stack>
            <MuiExGrid source={source} columns={columns} temporal={Temporal} viewportHeight={280} viewportWidth="stretch" />
            <Typography variant="body2" sx={{ mt: 1.5 }}>{describe(state)}</Typography>
        </>
    );
}
