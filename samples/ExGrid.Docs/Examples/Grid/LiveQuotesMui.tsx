import { useState, type ReactElement } from 'react';
import { FormControlLabel, Stack, Switch, Typography } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import { columns, quoteBoard, useQuoteFeed } from './Quotes';

// Made once, outside the component: a new source or column list on every render would render every row again.
const { source, cellChangedAt, tick } = quoteBoard();

// A gain and a loss take the theme's success and error colours.
export default function LiveQuotesMui(): ReactElement {
    const [live, setLive] = useState(true);
    // Stands in for a price feed.
    useQuoteFeed(tick, live);
    return (
        <>
            <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1.5 }}>
                <Typography variant="subtitle2">Quotes</Typography>
                <FormControlLabel label="Live" control={<Switch color="success" checked={live} onChange={event => { setLive(event.target.checked); }} />} />
            </Stack>
            <MuiExGrid source={source} columns={columns} cellChangedAt={cellChangedAt} viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
