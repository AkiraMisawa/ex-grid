import { useState, type ReactElement } from 'react';
import { Group, SegmentedControl } from '@mantine/core';
import { GridSource, type GridColumn, type GridHeaderDisplay } from '@ex-grid/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { samplePositions, type Position } from './Positions';

const source = GridSource.from(samplePositions());
const whole = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });
const money = new Intl.NumberFormat('en-US', { minimumFractionDigits: 2, maximumFractionDigits: 2 });

// No width is declared, so every column is Auto and is measured under the display its header has.
const columns: readonly GridColumn<Position>[] = [
    { name: 'Id', type: 'number', value: p => p.id, header: 'ID' },
    { name: 'Currency', type: 'text', value: p => p.currency, header: 'Ccy', filterUi: 'both' },
    { name: 'Quantity', type: 'number', value: p => p.quantity, header: 'Qty', format: v => whole.format(v as number) },
    { name: 'Hedged', type: 'boolean', value: p => p.hedged },
    { name: 'Price', type: 'number', value: p => p.price, format: v => money.format(v as number) },
    { name: 'Pnl', type: 'number', value: p => p.pnl, header: 'P&L', format: v => money.format(v as number) },
    { name: 'Desk', type: 'text', value: p => p.desk, filterUi: 'both' },
    { name: 'Instrument', type: 'text', value: p => p.instrument },
];

const displays: readonly GridHeaderDisplay[] = ['standard', 'compact'];

// The Wrapper passes headerDisplay to the grid with its other props; the header's box is the core's under every Wrapper.
export default function HeaderDisplayGridMantine(): ReactElement {
    const [display, setDisplay] = useState<GridHeaderDisplay>('compact');
    return (
        <>
            <Group mb="sm">
                <SegmentedControl size="xs" data={[...displays]} value={display} onChange={value => { setDisplay(value as GridHeaderDisplay); }} />
            </Group>
            <MantineExGrid withBorder highlightOnHover source={source} columns={columns} headerDisplay={display} viewportHeight={300}
                viewportWidth="stretch" />
        </>
    );
}
