import { GridSource, type CellState, type GridColumn } from '@ex-grid/core';
import { samplePositions, type Position } from './Positions';

export const source = GridSource.from(samplePositions());
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

export const columns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id },
    { name: 'Desk', type: 'text', value: p => p.desk },
    { name: 'Instrument', type: 'text', value: p => p.instrument },
    { name: 'Quantity', type: 'number', value: p => p.quantity, format: v => whole.format(v as number) },
    { name: 'Price', type: 'number', value: p => p.price, format: v => money.format(v as number) },
    {
        name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', format: v => money.format(v as number),
        tone: v => ((v as number) > 0 ? 'positive' : (v as number) < 0 ? 'negative' : 'none'),
    },
];

/** What the value alone cannot say: Credit prices are yesterday's close, so they are stale. */
export const cellState = (position: Position, column: GridColumn<Position>): CellState =>
    (column.name === 'Price' || column.name === 'Pnl') && position.desk === 'Credit' ? 'stale' : 'normal';
