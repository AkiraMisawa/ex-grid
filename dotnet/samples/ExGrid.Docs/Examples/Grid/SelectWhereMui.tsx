import { useRef, useState, type ReactElement } from 'react';
import { Button, ButtonGroup, Stack, Typography } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import type { ExGridApi } from '@ex-grid/react';
import { columns, currencies, selectWhere, source } from './SelectWhereParts';
import type { Trade } from './Trades';

export default function SelectWhereMui(): ReactElement {
    const grid = useRef<ExGridApi<Trade>>(null);
    const [status, setStatus] = useState('');
    return (
        <>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center', mb: 1.5 }}>
                <Typography variant="body2">Select where Currency is</Typography>
                <ButtonGroup size="small" variant="outlined">
                    {currencies.map(currency => (
                        <Button key={currency} onClick={() => { setStatus(selectWhere(grid.current, currency)); }}>{currency}</Button>
                    ))}
                </ButtonGroup>
                <Typography variant="body2">{status}</Typography>
            </Stack>
            <MuiExGrid ref={grid} highlightHoverRow source={source} columns={columns} viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
