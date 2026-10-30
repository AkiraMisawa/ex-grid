import type { ReactElement } from 'react';
import { Button, Group, Text } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { columns, feedKeys, useLiveWrites } from './LiveWritesDesk';

export default function LiveWritesMantine(): ReactElement {
    const { source, cellChangedAt, feed, result, advancePrices, onEdit, onOverwriteNotice, onAction } = useLiveWrites();
    return (
        <>
            <Group gap="sm" mb="sm">
                <Button size="xs" variant="outline" onClick={advancePrices}>Advance prices (F9)</Button>
                <Text size="sm">{feed}</Text>
            </Group>
            <MantineExGrid withBorder source={source} columns={columns} cellChangedAt={cellChangedAt} onEdit={onEdit}
                onOverwriteNotice={onOverwriteNotice} onAction={onAction} declaredKeys={feedKeys} onDeclaredKey={advancePrices}
                viewportHeight={150} viewportWidth="stretch" />
            <Text size="sm" mt="sm" role="status">{result}</Text>
        </>
    );
}
