import { useState, type ReactElement } from 'react';
import { Button, Group, Progress, Text } from '@mantine/core';
import { readArrowSnapshot, writeArrowSnapshot, type SnapshotProgress } from '@ex-grid/data';
import { createPivotLayout, SnapshotPivotSource } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
import { sampleSales } from './SalesData';
import { salesSnapshot, salesSnapshotFields } from './SalesSnapshot';

const firstLayout = createPivotLayout({
    rows: ['Country'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Stands for the body of a fetch from your server: 200,000 sales as an Arrow stream.
function download(): Uint8Array {
    return writeArrowSnapshot(salesSnapshot(sampleSales(200_000)));
}

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function LoadInSlicesMantine(): ReactElement {
    const [source, setSource] = useState<SnapshotPivotSource>();
    const [layout, setLayout] = useState(firstLayout);
    const [progress, setProgress] = useState<SnapshotProgress>();

    const load = async (): Promise<void> => {
        setProgress({ rows: 0, totalRows: undefined, bytes: undefined, totalBytes: undefined });
        // Paint the disabled button before the sales are made.
        await new Promise(resolve => { setTimeout(resolve, 0); });
        try {
            // Read in slices of about 30 ms, yielding between them: the page paints the progress.
            const snapshot = await readArrowSnapshot(download(), { sliceBudgetMs: 30, onProgress: setProgress });
            // The pivot computes its report in slices of the same size.
            setSource(new SnapshotPivotSource(snapshot, salesSnapshotFields, { sliceBudgetMs: 30 }));
        } finally {
            setProgress(undefined);
        }
    };

    return (
        <>
            <Group mb="sm">
                <Button size="xs" disabled={progress !== undefined} onClick={() => { void load(); }}>Load 200,000 sales</Button>
                {progress !== undefined && (
                    <>
                        {progress.totalRows === undefined
                            ? <Progress w={240} value={100} striped animated />
                            : <Progress w={240} value={(100 * progress.rows) / progress.totalRows} />}
                        <Text size="sm">{progress.rows.toLocaleString('en-US')} sales read</Text>
                    </>
                )}
            </Group>
            {source !== undefined && (
                <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={320} />
            )}
        </>
    );
}
