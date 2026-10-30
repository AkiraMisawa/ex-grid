import { useCallback, useMemo, useState } from 'react';
import {
    GridSource, GridViewState, GridViewStateJson, GridViewStateJsonError, type GridColumn, type GridColumnWidth, type GridColumnWidthChange,
} from '@ex-grid/core';
import { samplePositions, type Position } from './Positions';

const quantity = new Intl.NumberFormat('en-US');
const pnl = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

const all: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id, width: 70, align: 'left' },
    { name: 'Instrument', type: 'text', value: p => p.instrument },
    { name: 'Desk', type: 'text', value: p => p.desk, width: 100 },
    { name: 'Trader', type: 'text', value: p => p.trader },
    { name: 'Quantity', type: 'number', value: p => p.quantity, width: 100, format: v => quantity.format(v as number) },
    { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', width: 120, format: v => pnl.format(v as number) },
    { name: 'TradeDate', type: 'date', value: p => p.tradeDate, header: 'Trade date', width: 130, dateType: 'dateOnly' },
    { name: 'Hedged', type: 'boolean', value: p => p.hedged, width: 96 },
];
const names = all.map(column => column.name);

/** The View State on screen and the gestures that change it. The View State is the page's: the order, the widths
 * and the pinned count here, the sort and the filter in the source. The grid only reports the gestures. */
export function useSavedView() {
    const [source] = useState(() => GridSource.from(samplePositions()));
    const [order, setOrder] = useState<readonly string[]>(names);
    const [widths, setWidths] = useState<ReadonlyMap<string, GridColumnWidth>>(() => new Map());
    const [pinned, setPinned] = useState(1);
    // What your application keeps for the user: in local storage, or in a settings table.
    const [saved, setSaved] = useState<string>();
    const [said, setSaid] = useState('');

    // A new list each time something changed: the list instance is what tells the grid. A column takes the width the
    // user gave it, fixed after a drag or as the stored view holds it, where auto stays auto and measures itself again.
    const columns = useMemo(() => order.map(name => {
        const column = all.find(c => c.name === name);
        if (column === undefined) throw new RangeError(`There is no column ${name}.`);
        const width = widths.get(name);
        return width === undefined ? column : { ...column, width };
    }), [order, widths]);

    // The view on screen now.
    const shown = useCallback(() => {
        const { sorts, filter } = source.snapshot();
        return GridViewState.of(columns, pinned, sorts, filter);
    }, [source, columns, pinned]);

    const save = useCallback(() => {
        const now = shown();
        // A View State compares by what it holds, so the stored one, read back, equals what it was saved from until
        // the user changes something.
        if (saved !== undefined && GridViewStateJson.read(saved, names).equals(now)) {
            setSaid('Nothing has changed since the view was saved.');
            return;
        }
        setSaved(GridViewStateJson.write(now));
        setSaid('Saved.');
    }, [shown, saved]);

    const restore = useCallback(() => {
        if (saved === undefined) return;
        let view: GridViewState;
        try {
            // Told every column the grid can show: a stored view that names one it no longer has, or holds anything
            // else it cannot read, is refused by name.
            view = GridViewStateJson.read(saved, names);
        } catch (refused) {
            if (!(refused instanceof GridViewStateJsonError)) throw refused;
            setSaid(refused.message);
            return;
        }
        if (view.equals(shown())) {
            setSaid('The grid already shows the saved view.');
            return;
        }
        setOrder(view.columns.map(c => c.column));
        setWidths(new Map(view.columns.map(c => [c.column, 'px' in c.width ? c.width.px : { auto: true }])));
        setPinned(view.pinnedColumnCount);
        source.setSorts(view.sorts);
        source.setFilter(view.filter);
        setSaid('Restored.');
    }, [source, saved, shown]);

    const reset = useCallback(() => {
        // What is already as it was stays the same instance, so an unchanged view hands the grid nothing new.
        setOrder(before => (before.every((name, at) => name === names[at]) ? before : names));
        setWidths(before => (before.size === 0 ? before : new Map()));
        setPinned(1);
        source.setSorts([]);
        source.setFilter(undefined);
        setSaid('');
    }, [source]);

    const onColumnWidthChanged = useCallback((change: GridColumnWidthChange) => {
        setWidths(before => new Map(before).set(change.column, change.widthPx));
    }, []);
    // Every column's name, in the new order.
    const onColumnOrderChanged = useCallback((order: readonly string[]) => { setOrder(order); }, []);

    return { source, columns, pinned, saved, said, save, restore, reset, onColumnWidthChanged, onColumnOrderChanged, onPinnedCountChanged: setPinned };
}

/** The stored text, laid out to read. */
export const indented = (json: string): string => JSON.stringify(JSON.parse(json), undefined, 2);
