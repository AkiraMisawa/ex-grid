import type { ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { Alert, Button, Group, SimpleGrid, Textarea } from '@mantine/core';
import { MantineExGrid } from '@ex-grid/mantine';
import { useShapedTrades } from './ShapedTrades';

const mono = { input: { fontFamily: 'monospace' } } as const;

export default function ShapedColumnsMantine(): ReactElement {
    const { texts, edit, draw, drawn, result } = useShapedTrades();
    return (
        <>
            <SimpleGrid cols={{ base: 1, md: 3 }} spacing="sm">
                <Textarea label="Input" rows={9} styles={mono} value={texts.input} onChange={event => { edit('input')(event.currentTarget.value); }} />
                <Textarea label="Data" rows={9} styles={mono} value={texts.data} onChange={event => { edit('data')(event.currentTarget.value); }} />
                <Textarea label="Grid" rows={9} styles={mono} value={texts.spec} onChange={event => { edit('spec')(event.currentTarget.value); }} />
            </SimpleGrid>
            <Group my="sm">
                <Button onClick={draw}>Draw</Button>
                {'refusal' in result && <Alert color="yellow" py={6} role="status">{result.refusal}</Alert>}
            </Group>
            {'grid' in result && (
                <MantineExGrid key={drawn} withBorder source={result.grid.source} columns={result.grid.columns}
                    pinnedColumnCount={result.grid.pinnedColumnCount} temporal={Temporal} viewportHeight={180} viewportWidth="stretch" />
            )}
        </>
    );
}
