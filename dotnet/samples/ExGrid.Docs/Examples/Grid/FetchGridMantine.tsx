import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { Group, Text } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { columns, tradeDesk, useServerCounts } from './TradeServer';

// Made once, outside the component: a new source or column list on every render would render every row again.
const desk = tradeDesk();

export default function FetchGridMantine(): ReactElement {
    const { total, answered } = useServerCounts(desk);
    return (
        <>
            <Group justify="space-between" mb="sm">
                <Text size="sm" fw={500}>{total} trades on the server</Text>
                <Text size="sm">{answered} pages answered</Text>
            </Group>
            <MantineExGrid withBorder source={desk.source} columns={columns} pinnedColumnCount={1} temporal={Temporal} viewportHeight={320} viewportWidth="stretch" />
        </>
    );
}
