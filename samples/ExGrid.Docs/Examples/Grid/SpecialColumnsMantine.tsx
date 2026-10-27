import { useCallback, useEffect, useState, useSyncExternalStore, type ReactElement } from 'react';
import { Text } from '@mantine/core';
import type { GridActionEvent, GridRefusal } from '@ex-grid/core';
import { MantineExGrid } from '@ex-grid/mantine';
import type { Position } from './Positions';
import { columns, markCounts, markRefusalWords, source, subscribeToMarks } from './SpecialColumnsParts';

export default function SpecialColumnsMantine(): ReactElement {
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
            <MantineExGrid withBorder source={source} columns={columns} onAction={onAction} onRefused={onRefused} pinnedColumnCount={2}
                viewportHeight={300} viewportWidth="stretch" />
            <Text size="sm" mt="sm">{shown} {lastAction}</Text>
            <Text role="status" size="sm" mt={4}>{markRefusal}</Text>
        </>
    );
}
