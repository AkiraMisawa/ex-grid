import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { GridSource, type GridColumn } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { sampleTrades, type Trade } from './Trades';

const source = GridSource.from(sampleTrades(2_000));
const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

// A value list suits a column with few distinct values; a condition suits every type.
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book, filterUi: 'both' },
    { name: 'Trader', type: 'text', value: t => t.trader, filterUi: 'valueList' },
    { name: 'Instrument', type: 'text', value: t => t.instrument, filterUi: 'both' },
    { name: 'Currency', type: 'text', value: t => t.currency, filterUi: 'valueList' },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number) },
    { name: 'Price', type: 'number', value: t => t.price },
    { name: 'TradeDate', type: 'date', value: t => t.tradeDate, header: 'Trade date', dateType: 'dateOnly' },
    { name: 'Confirmed', type: 'boolean', value: t => t.confirmed, filterUi: 'valueList' },
];

// A date typed into Trade date's filter is made with Temporal, so the grid is handed one: the browser's, or a
// polyfill's where the browser has none.
//
// The filter and find panels are drawn in MUI's components; what they do is the grid's.
export default function SortFilterFindMui(): ReactElement {
    return <MuiExGrid source={source} columns={columns} temporal={Temporal} viewportHeight={320} viewportWidth="stretch" />;
}
