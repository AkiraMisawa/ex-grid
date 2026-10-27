import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { Button, Code, Group, ScrollArea, Text } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { indented, useSavedView } from './SavedViewParts';

export default function SavedViewMantine(): ReactElement {
    const view = useSavedView();
    return (
        <>
            <MantineExGrid withBorder source={view.source} columns={view.columns} pinnedColumnCount={view.pinned} temporal={Temporal}
                onColumnWidthChanged={view.onColumnWidthChanged} onColumnOrderChanged={view.onColumnOrderChanged}
                onPinnedCountChanged={view.onPinnedCountChanged} viewportHeight={260} viewportWidth="stretch" />
            <Group gap="sm" mt="sm">
                <Button size="xs" variant="outline" onClick={view.save}>Save the view</Button>
                <Button size="xs" variant="subtle" onClick={view.reset}>Reset</Button>
                <Button size="xs" variant="outline" disabled={view.saved === undefined} onClick={view.restore}>Restore the saved view</Button>
                <Text size="sm" role="status">{view.said}</Text>
            </Group>
            {view.saved !== undefined && (
                <ScrollArea.Autosize mah={220} mt="xs">
                    <Code block>{indented(view.saved)}</Code>
                </ScrollArea.Autosize>
            )}
        </>
    );
}
