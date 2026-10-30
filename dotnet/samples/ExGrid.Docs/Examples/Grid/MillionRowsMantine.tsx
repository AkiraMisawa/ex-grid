import type { ReactElement } from 'react';
import { Text } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { columns, millionTrades, useHolding } from './MillionTrades';

// Made once, outside the component: a new source or column list on every render would render every row again.
const source = millionTrades();

// The DOM does not grow with the row count: a million rows paint the same few dozen elements as a hundred.
export default function MillionRowsMantine(): ReactElement {
    const holding = useHolding(source);
    return (
        <>
            <MantineExGrid withBorder source={source} columns={columns} viewportHeight={320} viewportWidth="stretch" />
            <Text size="sm" mt="sm">{holding}</Text>
        </>
    );
}
