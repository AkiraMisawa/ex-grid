import { useRef, useState, type ReactElement } from 'react';
import { Button, Group, Text } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import type { ExGridApi } from '@ex-grid/react';
import { columns, currencies, selectWhere, source } from './SelectWhereParts';
import type { Trade } from './Trades';

export default function SelectWhereMantine(): ReactElement {
    const grid = useRef<ExGridApi<Trade>>(null);
    const [status, setStatus] = useState('');
    return (
        <>
            <Group gap="sm" mb="sm">
                <Text size="sm">Select where Currency is</Text>
                <Button.Group>
                    {currencies.map(currency => (
                        <Button key={currency} size="xs" variant="default" onClick={() => { setStatus(selectWhere(grid.current, currency)); }}>{currency}</Button>
                    ))}
                </Button.Group>
                <Text size="sm">{status}</Text>
            </Group>
            <MantineExGrid ref={grid} withBorder highlightOnHover source={source} columns={columns} viewportHeight={300} viewportWidth="stretch" />
        </>
    );
}
