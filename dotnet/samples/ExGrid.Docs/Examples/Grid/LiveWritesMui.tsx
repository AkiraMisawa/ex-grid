import type { ReactElement } from 'react';
import { Button, Stack, Typography } from '@mui/material';
import { MuiExGrid } from '@ex-grid/mui';
import { columns, feedKeys, useLiveWrites } from './LiveWritesDesk';

export default function LiveWritesMui(): ReactElement {
    const { source, cellChangedAt, feed, result, advancePrices, onEdit, onOverwriteNotice, onAction } = useLiveWrites();
    return (
        <>
            <Stack direction="row" spacing={1.5} sx={{ alignItems: 'center', mb: 1.5 }}>
                <Button size="small" variant="outlined" onClick={advancePrices}>Advance prices (F9)</Button>
                <Typography variant="body2">{feed}</Typography>
            </Stack>
            <MuiExGrid source={source} columns={columns} cellChangedAt={cellChangedAt} onEdit={onEdit}
                onOverwriteNotice={onOverwriteNotice} onAction={onAction} declaredKeys={feedKeys} onDeclaredKey={advancePrices}
                viewportHeight={150} viewportWidth="stretch" />
            <Typography variant="body2" role="status" sx={{ mt: 1.5 }}>{result}</Typography>
        </>
    );
}
