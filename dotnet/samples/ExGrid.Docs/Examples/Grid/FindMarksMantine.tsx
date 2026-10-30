import { useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { Group, Switch, Text } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { columns, match, source } from './FindMarksParts';

export default function FindMarksMantine(): ReactElement {
    const [marked, setMarked] = useState(true);
    return (
        <>
            <Group justify="space-between" mb="sm">
                <Switch checked={marked} onChange={event => { setMarked(event.currentTarget.checked); }} label="Pass the match function" />
                <Text size="sm">Press Ctrl+F in the grid and type "novak"</Text>
            </Group>
            <MantineExGrid withBorder source={source} columns={columns} {...(marked ? { findMatch: match } : {})} temporal={Temporal} viewportHeight={320}
                viewportWidth="stretch" />
        </>
    );
}
