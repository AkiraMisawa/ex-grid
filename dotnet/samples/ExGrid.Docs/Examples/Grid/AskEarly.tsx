import type { ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { columns } from './MillionTrades';
import { margin, useAskEarly } from './ReadAheadTrades';

export default function AskEarly(): ReactElement {
    const { source, early, toggle, rangeRequestMargin, asked } = useAskEarly();
    return (
        <>
            <label style={{ display: 'inline-flex', gap: 6, alignItems: 'center', marginBottom: 12, fontSize: 14 }}>
                <input type="checkbox" checked={early} onChange={event => { toggle(event.target.checked); }} />
                Ask {margin} rows before the Window runs out
            </label>
            <ExGrid source={source} columns={columns} rangeRequestMargin={rangeRequestMargin} viewportHeight={320} viewportWidth="stretch" />
            <p style={{ margin: '12px 0 0', fontSize: 14 }}>{asked}</p>
        </>
    );
}
