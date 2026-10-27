import { useCallback, useState, type ReactElement } from 'react';
import { Text } from '@mantine/core';
import { GridSource, type CellPosition, type GridColumn, type GridEditIntent } from '@ex-grid/core';
import { MantineExGrid } from '@ex-grid/mantine';

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

export default function OrderedFieldTextMantine(): ReactElement {
    const [message, setMessage] = useState('Edit a note or type a name and press Enter.');
    // Made once: a handler made again on every render would be handed to the grid again each time.
    const onEdit = useCallback((edit: GridEditIntent<Note>) => {
        source.replaceRow(edit.row, { ...edit.row, text: edit.value });
        setMessage(`Committed: ${edit.value}`);
    }, []);
    const onNameBoxEntered = useCallback((typed: string) => { setMessage(`Name received: ${typed}`); }, []);
    return (
        <>
            <MantineExGrid withBorder source={source} columns={columns} showFormulaBar nameBoxLabel={name} onNameBoxEntered={onNameBoxEntered}
                onEdit={onEdit} viewportHeight={180} viewportWidth="stretch" />
            <Text role="status" size="sm" mt="sm">{message}</Text>
        </>
    );
}
