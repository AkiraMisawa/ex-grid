import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { GridSource, type CellTone, type GridColumn } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { samplePositions, type Position } from './Positions';

const source = GridSource.from(samplePositions());
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const months = ['Jan', 'Feb', 'Mar', 'Apr', 'May', 'Jun', 'Jul', 'Aug', 'Sep', 'Oct', 'Nov', 'Dec'];

// A loss in brackets, as an accountant writes it, and the sign as a tone: the theme says what a gain and a loss look like.
const pnl = (value: number): string => (value < 0 ? `(${money.format(-value)})` : money.format(value));
const sign = (value: unknown): CellTone => ((value as number) > 0 ? 'positive' : (value as number) < 0 ? 'negative' : 'none');
const day = (value: unknown): string => {
    const date = value as Temporal.PlainDate;
    return `${String(date.day).padStart(2, '0')} ${months[date.month - 1] as string} ${String(date.year)}`;
};

const columns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id, width: 70, align: 'left' },
    { name: 'Instrument', type: 'text', value: p => p.instrument, width: { auto: true, minPx: 120 } },
    { name: 'Quantity', type: 'number', value: p => p.quantity, format: v => whole.format(v as number) },
    { name: 'Price', type: 'number', value: p => p.price, format: v => money.format(v as number) },
    { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', width: 120, format: v => pnl(v as number), tone: sign },
    // Too narrow for most of its values on purpose: a number that does not fit is ####, never cut short.
    { name: 'MarketValue', type: 'number', value: p => p.price * p.quantity, header: 'Market value', width: 84,
        format: v => whole.format(v as number) },
    { name: 'TradeDate', type: 'date', value: p => p.tradeDate, header: 'Trade date', dateType: 'dateOnly', format: day },
    { name: 'Hedged', type: 'boolean', value: p => p.hedged, align: 'center', headerAlign: 'center',
        format: v => ((v as boolean) ? 'Yes' : 'No') },
];

// The Wrapper paints a gain in the theme's success colour and a loss in its error colour.
export default function ColumnTypesMui(): ReactElement {
    return <MuiExGrid source={source} columns={columns} temporal={Temporal} viewportHeight={320} viewportWidth="stretch" />;
}
