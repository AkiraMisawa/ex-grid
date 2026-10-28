import { useEffect, useState, type ReactElement } from 'react';
import { Badge, Button, Group } from '@mantine/core';
import { createChangeBatch } from '@ex-grid/data';
import { createPivotLayout, SnapshotPivotSource } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
import { mulberry32 } from './Seeded';
import { sampleSales } from './SalesData';
import { amendSales, salesSnapshot, salesSnapshotFields } from './SalesSnapshot';

const firstLayout = createPivotLayout({
    rows: ['Region', 'Country'],
    columns: ['Channel'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function LiveBatchesMantine(): ReactElement {
    // The sales and their source, made once for the component: the Record Key (Id) is what a Change Batch finds a sale by.
    const [{ sales, source }] = useState(() => {
        const made = [...sampleSales()];
        return { sales: made, source: new SnapshotPivotSource(salesSnapshot(made), salesSnapshotFields) };
    });
    const [layout, setLayout] = useState(firstLayout);
    const [paused, setPaused] = useState(false);
    const [version, setVersion] = useState(source.snapshot.version);
    const [random] = useState(() => mulberry32(1));

    // A few sales amended, as one Change Batch, every 400 ms. The source makes the next Snapshot, brings its answer up
    // to date from what the batch removed and added, and tells the pivot: it asks again, and marks every value whose
    // text changed.
    useEffect(() => {
        if (paused) return undefined;
        const timer = setInterval(() => {
            const change = source.apply(createChangeBatch({ changed: salesSnapshot(amendSales(sales, random, 5)) }));
            setVersion(change.after.version);
        }, 400);
        return () => { clearInterval(timer); };
    }, [paused, random, sales, source]);

    return (
        <>
            <Group mb="sm">
                <Button size="xs" variant="default" onClick={() => { setPaused(!paused); }}>{paused ? 'Resume the changes' : 'Pause the changes'}</Button>
                <Badge variant="light">Snapshot version {version.toString()}</Badge>
            </Group>
            <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />
        </>
    );
}
