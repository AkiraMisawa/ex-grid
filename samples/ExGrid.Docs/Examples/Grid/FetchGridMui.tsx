import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { Stack, Typography } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import { columns, tradeDesk, useServerCounts } from './TradeServer';

// Made once, outside the component: a new source or column list on every render would render every row again.
const desk = tradeDesk();

export default function FetchGridMui(): ReactElement {
    const { total, answered } = useServerCounts(desk);
    return (
        <>
            <Stack direction="row" sx={{ justifyContent: 'space-between', mb: 1.5 }}>
                <Typography variant="subtitle2">{total} trades on the server</Typography>
                <Typography variant="body2">{answered} pages answered</Typography>
            </Stack>
            <MuiExGrid source={desk.source} columns={columns} pinnedColumnCount={1} temporal={Temporal} viewportHeight={320} viewportWidth="stretch" />
        </>
    );
}
