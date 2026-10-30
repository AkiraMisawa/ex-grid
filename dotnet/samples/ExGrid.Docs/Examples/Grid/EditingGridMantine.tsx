import type { ReactElement } from 'react';
import { Button, Group, Text } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { useEditingDesk } from './EditingDesk';

export default function EditingGridMantine(): ReactElement {
    const desk = useEditingDesk();
    return (
        <>
            <Group gap="xs" mb="sm">
                <Button size="xs" variant="subtle" disabled={!desk.canUndo} onClick={desk.undo}>Undo</Button>
                <Button size="xs" variant="subtle" disabled={!desk.canRedo} onClick={desk.redo}>Redo</Button>
                <Text size="sm">{desk.message}</Text>
            </Group>
            <MantineExGrid withBorder source={desk.source} columns={desk.columns} pinnedColumnCount={1} showFillHandle cellState={desk.paint.cellState}
                cellMessage={desk.paint.cellMessage} onEdit={desk.onEdit} onPaste={desk.onPaste} onFill={desk.onFill} onClear={desk.onClear}
                onRefused={desk.onRefused} onUndo={desk.undo} onRedo={desk.redo} viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
