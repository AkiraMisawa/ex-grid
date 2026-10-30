import { useCallback, useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { GridSource, type GridColumn, type GridSelection } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { sampleTrades, type Trade } from './Trades';

const source = GridSource.from(sampleTrades());
const notional = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const count = new Intl.NumberFormat('en-US');

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Id', type: 'number', value: t => t.id },
    { name: 'Book', type: 'text', value: t => t.book },
    { name: 'Trader', type: 'text', value: t => t.trader },
    { name: 'Instrument', type: 'text', value: t => t.instrument },
    { name: 'Currency', type: 'text', value: t => t.currency },
    { name: 'Notional', type: 'number', value: t => t.notional, format: v => notional.format(v as number) },
    { name: 'Price', type: 'number', value: t => t.price },
    { name: 'TradeDate', type: 'date', value: t => t.tradeDate, header: 'Trade date', dateType: 'dateOnly' },
];

// Positions are in the current order: the Focus's row is the row the source holds there now.
function describe(selection: GridSelection): readonly [string, string] {
    const focus = selection.focus;
    if (focus === undefined) return ['Nothing selected', ''];
    const { window, windowStart } = source.snapshot();
    const trade = window[focus.row - windowStart];
    const column = columns[focus.column];
    if (trade === undefined || column === undefined) throw new Error(`The Focus at ${String(focus.row)}, ${String(focus.column)} is not a cell the source holds.`);
    return [`Focus: trade ${String(trade.id)}, ${column.header ?? column.name}`, `${String(selection.ranges.length)} range(s), ${count.format(selection.cellCount)} cells`];
}

// A date typed into Trade date's filter is made with Temporal, so the grid is handed one: the browser's, or a
// polyfill's where the browser has none.
export default function SelectionStatusMui(): ReactElement {
    const [[where, cells], setStatus] = useState<readonly [string, string]>(['Nothing selected', '']);
    // Made once: a handler made again on every render would be handed to the grid again each time.
    const onSelectionChanged = useCallback((selection: GridSelection) => { setStatus(describe(selection)); }, []);
    return (
        <>
            <MuiExGrid source={source} columns={columns} temporal={Temporal} onSelectionChanged={onSelectionChanged} highlightFocusRow highlightHoverRow
                viewportHeight={300} viewportWidth="stretch" />
            <p className="bar"><span>{where}</span><span>{cells}</span></p>
        </>
    );
}
