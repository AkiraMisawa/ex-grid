import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { ExGrid } from '@ex-grid/react';
import { columns, tradeDesk, useServerCounts } from './TradeServer';

// Made once, outside the component: a new source or column list on every render would render every row again.
const desk = tradeDesk();

export default function FetchGrid(): ReactElement {
    const { total, answered } = useServerCounts(desk);
    return (
        <>
            <ExGrid source={desk.source} columns={columns} pinnedColumnCount={1} temporal={Temporal} viewportHeight={320} viewportWidth="stretch" />
            <p style={{ margin: '12px 0 0', fontSize: 14 }}>{total} trades on the server. It has answered {answered} pages.</p>
        </>
    );
}
