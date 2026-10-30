import type { ReactElement } from 'react';
import { FormControlLabel, Switch, Typography } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import { columns } from './MillionTrades';
import { margin, useAskEarly } from './ReadAheadTrades';

export default function AskEarlyMui(): ReactElement {
    const { source, early, toggle, rangeRequestMargin, asked } = useAskEarly();
    return (
        <>
            <FormControlLabel sx={{ mb: 1 }} label={`Ask ${String(margin)} rows before the Window runs out`}
                control={<Switch checked={early} onChange={event => { toggle(event.target.checked); }} />} />
            <MuiExGrid source={source} columns={columns} rangeRequestMargin={rangeRequestMargin} viewportHeight={320} viewportWidth="stretch" />
            <Typography variant="body2" sx={{ mt: 1.5 }}>{asked}</Typography>
        </>
    );
}
