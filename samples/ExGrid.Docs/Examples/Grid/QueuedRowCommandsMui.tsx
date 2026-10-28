import type { ReactElement } from 'react';
import { Button, Stack, Typography } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import { columns, declaredKeys, useQueuedRowCommands } from './QueuedRowCommandsDesk';

export default function QueuedRowCommandsMui(): ReactElement {
    const { source, trace, onDeclaredKey, onRelease, onRemove, onRestore, onAction, onRefused } = useQueuedRowCommands();
    return (
        <>
            <Typography variant="body2" sx={{ mb: 1.5 }}>Control+B starts a Consumer operation. Release completes it. Replace the Window while commands wait.</Typography>
            <Stack direction="row" spacing={1.5} sx={{ mb: 1.5 }}>
                <Button size="small" variant="outlined" onClick={onRelease}>Release</Button>
                <Button size="small" variant="outlined" onClick={onRemove}>Remove first two rows</Button>
                <Button size="small" variant="outlined" onClick={onRestore}>Restore rows</Button>
            </Stack>
            <MuiExGrid source={source} columns={columns} rowHeight={24} viewportHeight={220} viewportWidth="stretch"
                declaredKeys={declaredKeys} onDeclaredKey={onDeclaredKey} onAction={onAction} onRefused={onRefused} />
            <Typography variant="body2" role="status" sx={{ mt: 1.5 }}>{trace}</Typography>
        </>
    );
}
