import { useState, type ReactElement } from 'react';
import { Button, Text } from '@mantine/core';
import { GridSource, type GridColumn } from '@ex-grid/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { sampleTrades, type Trade } from './Trades';

const source = GridSource.from(sampleTrades());

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: t => t.book, width: 120 },
    { name: 'Trader', type: 'text', value: t => t.trader, width: 140 },
    { name: 'Notional', type: 'number', value: t => t.notional, width: 180 },
    { name: 'Price', type: 'number', value: t => t.price, width: 180 },
];

// The Paper takes the room the box leaves it and follows the box's width, so the grid inside it does too.
export default function ResponsiveBoxMantine(): ReactElement {
    const [narrow, setNarrow] = useState(false);
    return (
        <>
            <Button size="xs" variant="outline" onClick={() => { setNarrow(!narrow); }}>{narrow ? 'Widen container' : 'Narrow container'}</Button>
            <Text size="sm" my="sm">Two columns are requested as pinned. Narrowing suspends their pinning; widening restores it.</Text>
            <div style={{ display: 'flex', width: narrow ? '240px' : '100%', maxWidth: '100%' }}>
                <aside style={{ width: '40px', flex: 'none' }}>Box</aside>
                <MantineExGrid withBorder radius={0} style={{ flex: '1 1 auto' }} source={source} columns={columns} pinnedColumnCount={2}
                    viewportHeight={240} viewportWidth="stretch" />
            </div>
        </>
    );
}
