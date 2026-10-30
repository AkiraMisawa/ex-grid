import type { ReactElement } from 'react';
import { Button, Group, Text } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { columns, declaredKeys, useQueuedRowCommands } from './QueuedRowCommandsDesk';

export default function QueuedRowCommandsMantine(): ReactElement {
    const { source, trace, onDeclaredKey, onRelease, onRemove, onRestore, onAction, onRefused } = useQueuedRowCommands();
    return (
        <>
            <Text size="sm" mb="sm">Control+B starts a Consumer operation. Release completes it. Replace the Window while commands wait.</Text>
            <Group gap="sm" mb="sm">
                <Button size="xs" variant="outline" onClick={onRelease}>Release</Button>
                <Button size="xs" variant="outline" onClick={onRemove}>Remove first two rows</Button>
                <Button size="xs" variant="outline" onClick={onRestore}>Restore rows</Button>
            </Group>
            <MantineExGrid withBorder source={source} columns={columns} rowHeight={24} viewportHeight={220} viewportWidth="stretch"
                declaredKeys={declaredKeys} onDeclaredKey={onDeclaredKey} onAction={onAction} onRefused={onRefused} />
            <Text size="sm" mt="sm" role="status">{trace}</Text>
        </>
    );
}
