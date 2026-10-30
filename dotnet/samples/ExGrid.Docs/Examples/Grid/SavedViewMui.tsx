import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { Box, Button, Stack, Typography } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import { indented, useSavedView } from './SavedViewParts';

export default function SavedViewMui(): ReactElement {
    const view = useSavedView();
    return (
        <>
            <MuiExGrid source={view.source} columns={view.columns} pinnedColumnCount={view.pinned} temporal={Temporal}
                onColumnWidthChanged={view.onColumnWidthChanged} onColumnOrderChanged={view.onColumnOrderChanged}
                onPinnedCountChanged={view.onPinnedCountChanged} viewportHeight={260} viewportWidth="stretch" />
            <Stack direction="row" spacing={1} useFlexGap sx={{ alignItems: 'center', flexWrap: 'wrap', mt: 1.5 }}>
                <Button size="small" variant="outlined" onClick={view.save}>Save the view</Button>
                <Button size="small" onClick={view.reset}>Reset</Button>
                <Button size="small" variant="outlined" disabled={view.saved === undefined} onClick={view.restore}>Restore the saved view</Button>
                <Typography variant="body2" role="status">{view.said}</Typography>
            </Stack>
            {view.saved !== undefined && (
                <Box component="pre" sx={{ m: 0, mt: 1, maxHeight: 220, overflow: 'auto', typography: 'caption', fontFamily: 'monospace' }}>
                    {indented(view.saved)}
                </Box>
            )}
        </>
    );
}
