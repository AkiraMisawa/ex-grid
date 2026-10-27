import { useCallback, useEffect, useState, useSyncExternalStore, type ReactElement } from 'react';
import { Typography } from '@mui/material';
import type { GridActionEvent, GridRefusal } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
import type { Position } from './Positions';
import { columns, markCounts, markRefusalWords, source, subscribeToMarks } from './SpecialColumnsParts';

export default function SpecialColumnsMui(): ReactElement {
    // The source keeps the marks, by row; this page only shows how many of the rows shown are marked.
    const counts = useSyncExternalStore(subscribeToMarks, markCounts);
    const [lastAction, setLastAction] = useState('');
    const [markRefusal, setMarkRefusal] = useState('');
    // A mark that lands answers the refusal before it.
    useEffect(() => source.marks.subscribe(() => { setMarkRefusal(''); }), []);
    // Made once: a handler made again on every render would be handed to the grid again each time.
    const onAction = useCallback((action: GridActionEvent<Position>) => {
        setLastAction(`${action.action} asked for position ${String(action.row.id)} (${action.row.instrument}).`);
    }, []);
    const onRefused = useCallback((refusal: GridRefusal<Position>) => {
        if (refusal.kind === 'mark') setMarkRefusal(markRefusalWords[refusal.reason]);
    }, []);
    const shown = `${String(counts.markedInResult)} of ${String(counts.rowsInResult)} marked`
        + (counts.markedOutsideResult === 0 ? '.' : ` (${String(counts.markedOutsideResult)} more outside the filter).`);
    return (
        <>
            <MuiExGrid source={source} columns={columns} onAction={onAction} onRefused={onRefused} pinnedColumnCount={2}
                viewportHeight={300} viewportWidth="stretch" />
            <Typography variant="body2" sx={{ mt: 1.5 }}>{shown} {lastAction}</Typography>
            <Typography role="status" variant="body2" sx={{ mt: 0.5 }}>{markRefusal}</Typography>
        </>
    );
}
