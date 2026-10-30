import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { GridSource, type GridColumn } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { sampleTrades, type Trade } from './Trades';

// Made once, outside the component: a new source or column list on every render would render every row again.
const source = GridSource.from(sampleTrades());
const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book },
    { name: 'Trader', type: 'text', value: t => t.trader },
    { name: 'Instrument', type: 'text', value: t => t.instrument },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number) },
    { name: 'Price', type: 'number', value: t => t.price },
    { name: 'TradeDate', type: 'date', value: t => t.tradeDate, header: 'Trade date', dateType: 'dateOnly' },
    { name: 'Confirmed', type: 'boolean', value: t => t.confirmed },
];

// Trade date's filter makes the dates typed into it with Temporal, so the grid is handed one: the browser's, or a
// polyfill's where the browser has none.
//
// Under a ThemeProvider whose theme is made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// loaded: the grid in MUI's Paper, its menus and panels MUI's components, its colours the theme's.
export default function FirstGridMui(): ReactElement {
    return <MuiExGrid stripeRows highlightHoverRow source={source} columns={columns} temporal={Temporal} viewportHeight={320} viewportWidth="stretch" />;
}
