import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { Alert, Box, Button, Stack, TextField } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import { useShapedTrades } from './ShapedTrades';

const mono = { htmlInput: { style: { fontFamily: 'monospace' } } } as const;

export default function ShapedColumnsMui(): ReactElement {
    const { texts, edit, draw, drawn, result } = useShapedTrades();
    return (
        <>
            <Box sx={{ display: 'grid', gridTemplateColumns: { xs: '1fr', md: 'repeat(3, 1fr)' }, gap: 2 }}>
                <TextField label="Input" multiline rows={9} slotProps={mono} value={texts.input} onChange={event => { edit('input')(event.target.value); }} />
                <TextField label="Data" multiline rows={9} slotProps={mono} value={texts.data} onChange={event => { edit('data')(event.target.value); }} />
                <TextField label="Grid" multiline rows={9} slotProps={mono} value={texts.spec} onChange={event => { edit('spec')(event.target.value); }} />
            </Box>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center', my: 1 }}>
                <Button variant="contained" onClick={draw}>Draw</Button>
                {'refusal' in result && <Alert severity="warning" role="status">{result.refusal}</Alert>}
            </Stack>
            {'grid' in result && (
                <MuiExGrid key={drawn} source={result.grid.source} columns={result.grid.columns}
                    pinnedColumnCount={result.grid.pinnedColumnCount} temporal={Temporal} viewportHeight={180} viewportWidth="stretch" />
            )}
        </>
    );
}
