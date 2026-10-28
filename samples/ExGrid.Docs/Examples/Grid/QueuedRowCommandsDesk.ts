import { useCallback, useEffect, useRef, useState } from 'react';
import { actionColumn, GridSource, markColumn, type GridActionEvent, type GridColumn, type GridRefusal, type InMemoryGridSource } from '@ex-grid/core';

/** A row the commands act on, named by its id. */
export interface CommandRow {
    readonly id: number;
}

const everyRow: readonly CommandRow[] = Array.from({ length: 80 }, (_, id) => ({ id }));
const firstTwo = everyRow.slice(0, 2);

export const columns: readonly GridColumn<CommandRow>[] = [
    { name: 'Id', type: 'number', value: r => r.id, width: 100 },
    actionColumn('Action', [{ name: 'open', label: 'Open' }], { width: 120 }),
    markColumn({ width: 60 }),
];

// Control+B (Command+B on macOS) reaches this page, which answers it later.
export const declaredKeys: readonly string[] = ['Control+b'];

// Keyed by id, so a removed row that comes back is the same row. It comes back after the others, where the source puts
// a row it is handed.
const commandRows = (): InMemoryGridSource<CommandRow> => GridSource.from(everyRow, { rowKey: r => r.id });

/** A Consumer operation the grid waits for, and the row commands made behind it. The handlers are made once, since a
 * handler made again on every render would be handed to the grid again each time. */
export function useQueuedRowCommands() {
    const [source] = useState(commandRows);
    const [trace, setTrace] = useState<readonly string[]>([]);
    const release = useRef<(() => void) | undefined>(undefined);
    const removed = useRef(false);
    const record = useCallback((entry: string) => { setTrace(said => [...said, entry]); }, []);

    // The marks are the source's: each mark the grid applies is told here, in the order it lands.
    useEffect(() => {
        const { marks } = source;
        let marked = new Set<number>();
        return marks.subscribe(() => {
            const now = new Set(everyRow.filter(row => marks.isMarked(row)).map(row => row.id));
            for (const id of now) if (!marked.has(id)) record(`mark:${String(id)}`);
            for (const id of marked) if (!now.has(id)) record(`unmark:${String(id)}`);
            marked = now;
        });
    }, [source, record]);

    // The operation answers later: while its promise is out, the grid holds every input made after the key, the
    // Action and Mark clicks among them, and takes them in their order once it settles.
    const onDeclaredKey = useCallback(() => new Promise<void>(resolve => {
        record('started');
        release.current = () => {
            release.current = undefined;
            record('finished');
            resolve();
        };
    }), [record]);
    // A page that goes ends what it started.
    useEffect(() => () => { release.current?.(); }, []);

    const onRelease = useCallback(() => { release.current?.(); }, []);
    const onRemove = useCallback(() => {
        if (removed.current) return;
        removed.current = true;
        source.apply({ removedKeys: firstTwo.map(row => row.id) });
    }, [source]);
    const onRestore = useCallback(() => {
        if (!removed.current) return;
        removed.current = false;
        source.apply({ added: firstTwo });
    }, [source]);

    const onAction = useCallback((action: GridActionEvent<CommandRow>) => { record(`action:${String(action.row.id)}`); }, [record]);
    // A command whose row left the Window while it waited is refused at its turn, though the row came back.
    const onRefused = useCallback((refusal: GridRefusal<CommandRow>) => {
        if (refusal.kind === 'action' || refusal.kind === 'mark') record(`${refusal.kind}:${refusal.reason}`);
    }, [record]);

    return { source, trace: trace.join(' | '), onDeclaredKey, onRelease, onRemove, onRestore, onAction, onRefused };
}
