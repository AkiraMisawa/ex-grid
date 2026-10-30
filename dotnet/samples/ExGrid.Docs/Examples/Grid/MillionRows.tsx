import type { ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { columns, millionTrades, useHolding } from './MillionTrades';

// Made once, outside the component: a new source or column list on every render would render every row again.
const source = millionTrades();

// The DOM does not grow with the row count: a million rows paint the same few dozen elements as a hundred.
export default function MillionRows(): ReactElement {
    const holding = useHolding(source);
    return (
        <>
            <ExGrid source={source} columns={columns} viewportHeight={320} viewportWidth="stretch" />
            <p style={{ margin: '12px 0 0', fontSize: 14 }}>{holding}</p>
        </>
    );
}
