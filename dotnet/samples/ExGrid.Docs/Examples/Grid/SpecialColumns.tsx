import { useCallback, useEffect, useState, useSyncExternalStore, type ReactElement } from 'react';
import type { GridActionEvent, GridRefusal } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import type { Position } from './Positions';
import { columns, markCounts, markRefusalWords, source, subscribeToMarks } from './SpecialColumnsParts';

export default function SpecialColumns(): ReactElement {
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
            <ExGrid source={source} columns={columns} onAction={onAction} onRefused={onRefused} pinnedColumnCount={2}
                viewportHeight={300} viewportWidth="stretch" />
            <p style={{ margin: '12px 0 0', fontSize: 14 }}>{shown} {lastAction}</p>
            <p role="status" style={{ margin: '4px 0 0', fontSize: 14 }}>{markRefusal}</p>
        </>
    );
}
