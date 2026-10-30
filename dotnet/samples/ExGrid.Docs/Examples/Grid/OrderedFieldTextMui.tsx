import { useCallback, useState, type ReactElement } from 'react';
import { Typography } from '@mui/material';
import { GridSource, type CellPosition, type GridColumn, type GridEditIntent } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';

interface Note {
    readonly id: number;
    readonly text: string;
}

const source = GridSource.from<Note>([
    { id: 1, text: 'Edit this note' }, { id: 2, text: 'Then press another row' }, { id: 3, text: 'The whole text is kept' },
]);
const columns: readonly GridColumn<Note>[] = [
    { name: 'Id', type: 'number', value: row => row.id, width: 90 },
    { name: 'Text', type: 'text', value: row => row.text, editable: true, width: 300 },
];
const name = (cell: CellPosition): string => `Row ${String(cell.row + 1)}`;

export default function OrderedFieldTextMui(): ReactElement {
    const [message, setMessage] = useState('Edit a note or type a name and press Enter.');
    // Made once: a handler made again on every render would be handed to the grid again each time.
    const onEdit = useCallback((edit: GridEditIntent<Note>) => {
        source.replaceRow(edit.row, { ...edit.row, text: edit.value });
        setMessage(`Committed: ${edit.value}`);
    }, []);
    const onNameBoxEntered = useCallback((typed: string) => { setMessage(`Name received: ${typed}`); }, []);
    return (
        <>
            <MuiExGrid source={source} columns={columns} showFormulaBar nameBoxLabel={name} onNameBoxEntered={onNameBoxEntered}
                onEdit={onEdit} viewportHeight={180} viewportWidth="stretch" />
            <Typography role="status" variant="body2" sx={{ mt: 1.5 }}>{message}</Typography>
        </>
    );
}
