import { useState, type ReactElement } from 'react';
import { Group, Switch, Text } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { columns, quoteBoard, useQuoteFeed } from './Quotes';

// Made once, outside the component: a new source or column list on every render would render every row again.
const { source, cellChangedAt, tick } = quoteBoard();

// A gain and a loss take the theme's success and error colours.
export default function LiveQuotesMantine(): ReactElement {
    const [live, setLive] = useState(true);
    // Stands in for a price feed.
    useQuoteFeed(tick, live);
    return (
        <>
            <Group justify="space-between" mb="sm">
                <Text size="sm" fw={500}>Quotes</Text>
                <Switch color="green" label="Live" checked={live} onChange={event => { setLive(event.currentTarget.checked); }} />
            </Group>
            <MantineExGrid withBorder source={source} columns={columns} cellChangedAt={cellChangedAt} viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
