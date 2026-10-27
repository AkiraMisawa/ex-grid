import { useCallback, useState, type ReactElement } from 'react';
import { GridSource, type CellState, type GridColumn, type GridHeaderGroup, type HeaderGroupToggle } from '@ex-grid/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { samplePositions, type Position } from './Positions';

const positions = samplePositions();
const source = GridSource.from(positions);
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

// Every seventh position's price was set by hand: its cell says so, and so does the group while the price is hidden.
const setByHand = (position: Position): boolean => position.id % 7 === 0;
const handSetPrices = positions.filter(setByHand).length;
const cellState = (position: Position, column: GridColumn<Position>): CellState =>
    (column.name === 'Price' && setByHand(position) ? 'modified' : 'normal');

const all: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id, width: 70, align: 'left' },
    { name: 'Instrument', type: 'text', value: p => p.instrument, width: 120 },
    { name: 'Quantity', type: 'number', value: p => p.quantity, width: 110, format: v => whole.format(v as number) },
    { name: 'Cost', type: 'number', value: p => p.cost, width: 90, format: v => money.format(v as number) },
    { name: 'Price', type: 'number', value: p => p.price, width: 90, format: v => money.format(v as number) },
    { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', width: 132, format: v => money.format(v as number) },
];
// Collapsed, the group keeps one column shown: its press opens the group again.
const collapsed = all.filter(column => column.name !== 'Quantity' && column.name !== 'Cost' && column.name !== 'Price');

// Whether the group is open is yours to hold, as the column order is: the grid draws the toggle and tells you.
function groupsFor(expanded: boolean): readonly GridHeaderGroup[] {
    return [
        { label: 'Position', columns: ['Id', 'Instrument'] },
        expanded
            ? { label: 'Valuation', columns: ['Quantity', 'Cost', 'Price', 'Pnl'], id: 'valuation', expanded }
            : { label: 'Valuation', columns: ['Pnl'], id: 'valuation', expanded, hiddenMark: {
                state: 'modified', reason: `${String(handSetPrices)} prices set by hand in hidden columns` } },
    ];
}

// The Wrapper draws the toggle with Mantine's Accordion chevron, and the Hidden Mark in the theme's primary.
export default function ExpandableGroupsMantine(): ReactElement {
    const [expanded, setExpanded] = useState(true);
    const onHeaderGroupToggled = useCallback((toggle: HeaderGroupToggle) => { setExpanded(toggle.expanded); }, []);
    return <MantineExGrid withBorder source={source} columns={expanded ? all : collapsed} headerGroups={groupsFor(expanded)} cellState={cellState}
        onHeaderGroupToggled={onHeaderGroupToggled} viewportHeight={320} viewportWidth="stretch" />;
}
