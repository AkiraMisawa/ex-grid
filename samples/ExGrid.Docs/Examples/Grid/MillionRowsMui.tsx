import type { ReactElement } from 'react';
import { Typography } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import { columns, millionTrades, useHolding } from './MillionTrades';

// Made once, outside the component: a new source or column list on every render would render every row again.
const source = millionTrades();

// The DOM does not grow with the row count: a million rows paint the same few dozen elements as a hundred.
export default function MillionRowsMui(): ReactElement {
    const holding = useHolding(source);
    return (
        <>
            <MuiExGrid source={source} columns={columns} viewportHeight={320} viewportWidth="stretch" />
            <Typography variant="body2" sx={{ mt: 1.5 }}>{holding}</Typography>
        </>
    );
}
