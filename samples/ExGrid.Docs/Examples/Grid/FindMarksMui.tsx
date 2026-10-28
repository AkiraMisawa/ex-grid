import { useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { FormControlLabel, Stack, Switch, Typography } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import { columns, match, source } from './FindMarksParts';

export default function FindMarksMui(): ReactElement {
    const [marked, setMarked] = useState(true);
    return (
        <>
            <Stack direction="row" sx={{ alignItems: 'center', justifyContent: 'space-between', mb: 1 }}>
                <FormControlLabel label="Pass the match function"
                    control={<Switch checked={marked} onChange={event => { setMarked(event.target.checked); }} />} />
                <Typography variant="body2">Press Ctrl+F in the grid and type "novak"</Typography>
            </Stack>
            <MuiExGrid source={source} columns={columns} {...(marked ? { findMatch: match } : {})} temporal={Temporal} viewportHeight={320} viewportWidth="stretch" />
        </>
    );
}
