import { useState, type ReactElement } from 'react';
import { GridSource, type GridColumn } from '@ex-grid/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { sampleTrades, type Trade } from './Trades';

const trades = sampleTrades(2_000);
const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

// GridSource.from answers Find All for you: every match over its rows, a page at a time, with the exact count. The
// list names a row by its position in the order shown, "Row 12, Trader", or by your own words for it.
const byPosition = GridSource.from(trades);
const byTrade = GridSource.from(trades, { findAllRowLabel: t => `Trade ${String(t.id)}` });

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book },
    { name: 'Trader', type: 'text', value: t => t.trader },
    { name: 'Instrument', type: 'text', value: t => t.instrument },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number) },
];

// The find panel is drawn in Mantine's components, Find All among them; the list under it is the grid's.
export default function FindAllMantine(): ReactElement {
    const [labelled, setLabelled] = useState(false);
    return (
        <>
            <div className="bar">
                <label>
                    <input type="checkbox" checked={labelled} onChange={event => { setLabelled(event.target.checked); }} />
                    {' '}Name each row by its trade
                </label>
                <span>Press Ctrl+F in the grid, type &quot;novak&quot; and press Find All</span>
            </div>
            <MantineExGrid withBorder source={labelled ? byTrade : byPosition} columns={columns} viewportHeight={320} viewportWidth="stretch" />
        </>
    );
}
