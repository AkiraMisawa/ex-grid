import { useEffect, useRef, useState, type ReactElement } from 'react';
import { Button, Group } from '@mantine/core';
import { createPivotLayout } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
import { fetchFrom, SalesServer } from './SalesServer';

const firstLayout = createPivotLayout({
    rows: ['Category'],
    columns: ['Region'],
    values: [{ field: 'Units', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function LiveServerMantine(): ReactElement {
    const [cutOff, setCutOff] = useState(false);
    // Read by the source when it asks, so the source is made once.
    const reachable = useRef(true);
    // Cut off, the next question fails, as an unreachable server's does: ExPivot keeps the report it has as a Stale
    // Report, says so, and mutes its numbers until Retry.
    const [{ server, source }] = useState(() => {
        const made = new SalesServer();
        return { server: made, source: fetchFrom(made, made.fields, () => reachable.current) };
    });
    const [layout, setLayout] = useState(firstLayout);

    // The server amends a few sales every second and pushes each new Source Version: the source hands the notice on,
    // and ExPivot asks the report on screen again.
    useEffect(() => {
        server.startTrading(1000);
        return () => { server.dispose(); };
    }, [server]);

    const toggle = (): void => {
        reachable.current = cutOff;
        setCutOff(!cutOff);
    };

    return (
        <>
            <Group mb="sm">
                <Button size="xs" variant="default" onClick={toggle}>{cutOff ? 'Reconnect the server' : 'Cut the server off'}</Button>
            </Group>
            <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />
        </>
    );
}
