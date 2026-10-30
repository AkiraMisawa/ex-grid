import { useCallback, useMemo, useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { Button, Stack, Typography } from '@mui/material';
import { GridSource, type GridColumn, type GridColumnWidthChange, type GridHeaderGroup } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import { reordered } from './ColumnOrder';
import { samplePositions, type Position } from './Positions';

const source = GridSource.from(samplePositions());
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });
const share = new Intl.NumberFormat('en-US', { style: 'percent', maximumFractionDigits: 0 });

const all: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id, width: 70, align: 'left' },
    { name: 'Instrument', type: 'text', value: p => p.instrument, width: 120 },
    { name: 'Desk', type: 'text', value: p => p.desk, width: 100 },
    { name: 'Trader', type: 'text', value: p => p.trader, width: 110 },
    { name: 'Currency', type: 'text', value: p => p.currency, header: 'Ccy', width: 72 },
    { name: 'Quantity', type: 'number', value: p => p.quantity, width: 110, format: v => whole.format(v as number) },
    { name: 'Cost', type: 'number', value: p => p.cost, width: 90, format: v => money.format(v as number) },
    { name: 'Price', type: 'number', value: p => p.price, width: 90, format: v => money.format(v as number) },
    { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', width: 132, format: v => money.format(v as number) },
    { name: 'LimitUsed', type: 'number', value: p => p.limitUsed, header: 'Limit used', width: 100, format: v => share.format(v as number) },
    { name: 'TradeDate', type: 'date', value: p => p.tradeDate, header: 'Trade date', width: 110, dateType: 'dateOnly' },
    { name: 'Hedged', type: 'boolean', value: p => p.hedged, width: 80 },
];

// Every group names its member columns. A tier-2 group sits above the tier-1 groups.
const allGroups: readonly GridHeaderGroup[] = [
    { label: 'Position', columns: ['Id', 'Instrument'] },
    { label: 'Owner', columns: ['Desk', 'Trader'] },
    { label: 'Economics', columns: ['Currency', 'Quantity', 'Cost', 'Price'] },
    { label: 'Risk', columns: ['Pnl', 'LimitUsed'] },
    { label: 'Booking', columns: ['TradeDate', 'Hedged'] },
    { label: 'Valuation', columns: ['Currency', 'Quantity', 'Cost', 'Price', 'Pnl', 'LimitUsed'], tier: 2 },
];

function declared(name: string): GridColumn<Position> {
    const column = all.find(each => each.name === name);
    if (column === undefined) throw new Error(`The grid reported a column '${name}' this page never declared.`);
    return column;
}

// The order, the widths and what is hidden are yours to hold; the grid only reports the gestures, and a new
// column list is what tells it.
export default function ColumnLayoutMui(): ReactElement {
    const [order, setOrder] = useState<readonly string[]>(() => all.map(column => column.name));
    const [widths, setWidths] = useState<Readonly<Record<string, number>>>({});
    const [hidden, setHidden] = useState<ReadonlySet<string>>(() => new Set());
    const columns = useMemo(() => order.filter(name => !hidden.has(name)).map(name => {
        const column = declared(name);
        const width = widths[name];
        return width === undefined ? column : { ...column, width };
    }), [order, widths, hidden]);
    // A group may only name columns that are shown.
    const groups = useMemo(() => allGroups.flatMap(group => {
        const shown = group.columns.filter(name => !hidden.has(name));
        return shown.length > 0 ? [{ ...group, columns: shown }] : [];
    }), [hidden]);
    const pinned = columns.filter(column => column.name === 'Id' || column.name === 'Instrument').length;
    // Made once: a handler made again on every render would be handed to the grid again each time.
    const onColumnWidthChanged = useCallback((change: GridColumnWidthChange) => {
        setWidths(was => ({ ...was, [change.column]: change.widthPx }));
    }, []);
    // The grid reports the columns it shows; each hidden one keeps its place inside its groups.
    const onColumnOrderChanged = useCallback((shown: readonly string[]) => {
        setOrder(was => reordered(was, shown, allGroups));
    }, []);
    const onColumnHidden = useCallback((column: string) => { setHidden(was => new Set([...was, column])); }, []);
    return (
        <>
            <MuiExGrid source={source} columns={columns} headerGroups={groups} pinnedColumnCount={pinned} temporal={Temporal}
                onColumnWidthChanged={onColumnWidthChanged} onColumnOrderChanged={onColumnOrderChanged} onColumnHidden={onColumnHidden}
                viewportHeight={320} viewportWidth="stretch" />
            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mt: 1.5 }}>
                <Typography variant="body2">{hidden.size} hidden</Typography>
                <Button size="small" variant="outlined" disabled={hidden.size === 0} onClick={() => { setHidden(new Set()); }}>Show every column</Button>
            </Stack>
        </>
    );
}
