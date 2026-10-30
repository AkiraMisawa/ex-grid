import type { ReactElement } from 'react';
import { Button, Stack, Typography } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import { useEditingDesk } from './EditingDesk';

export default function EditingGridMui(): ReactElement {
    const desk = useEditingDesk();
    return (
        <>
            <Stack direction="row" spacing={0.5} sx={{ alignItems: 'center', mb: 1.5 }}>
                <Button size="small" disabled={!desk.canUndo} onClick={desk.undo}>Undo</Button>
                <Button size="small" disabled={!desk.canRedo} onClick={desk.redo}>Redo</Button>
                <Typography variant="body2" sx={{ ml: 1 }}>{desk.message}</Typography>
            </Stack>
            <MuiExGrid source={desk.source} columns={desk.columns} pinnedColumnCount={1} showFillHandle cellState={desk.paint.cellState}
                cellMessage={desk.paint.cellMessage} onEdit={desk.onEdit} onPaste={desk.onPaste} onFill={desk.onFill} onClear={desk.onClear}
                onRefused={desk.onRefused} onUndo={desk.undo} onRedo={desk.redo} viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
