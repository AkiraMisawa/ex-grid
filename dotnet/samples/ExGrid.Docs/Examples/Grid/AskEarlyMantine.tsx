import type { ReactElement } from 'react';
import { Switch, Text } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { columns } from './MillionTrades';
import { margin, useAskEarly } from './ReadAheadTrades';

export default function AskEarlyMantine(): ReactElement {
    const { source, early, toggle, rangeRequestMargin, asked } = useAskEarly();
    return (
        <>
            <Switch mb="sm" checked={early} onChange={event => { toggle(event.currentTarget.checked); }}
                label={`Ask ${String(margin)} rows before the Window runs out`} />
            <MantineExGrid withBorder source={source} columns={columns} rangeRequestMargin={rangeRequestMargin} viewportHeight={320}
                viewportWidth="stretch" />
            <Text size="sm" mt="sm">{asked}</Text>
        </>
    );
}
