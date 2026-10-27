import { useState, type ReactElement } from 'react';
import { GridSource, type GridColumn } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { sampleTrades, type Trade } from './Trades';

const source = GridSource.from(sampleTrades());

const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: t => t.book, width: 120 },
    { name: 'Trader', type: 'text', value: t => t.trader, width: 140 },
    { name: 'Notional', type: 'number', value: t => t.notional, width: 180 },
    { name: 'Price', type: 'number', value: t => t.price, width: 180 },
];

export default function ResponsiveBox(): ReactElement {
    const [narrow, setNarrow] = useState(false);
    return (
        <>
            <button type="button" onClick={() => { setNarrow(!narrow); }}>{narrow ? 'Widen container' : 'Narrow container'}</button>
            <p style={{ margin: '12px 0' }}>Two columns are requested as pinned. Narrowing suspends their pinning; widening restores it.</p>
            <div style={{ display: 'flex', width: narrow ? '240px' : '100%', maxWidth: '100%' }}>
                <aside style={{ width: '40px', flex: 'none' }}>Box</aside>
                <ExGrid source={source} columns={columns} pinnedColumnCount={2} viewportHeight={240} viewportWidth="stretch" />
            </div>
        </>
    );
}
