import { useState, type ReactElement } from 'react';
import { Button, Group, Text } from '@mantine/core';
import { readCsvSnapshot, SnapshotError, type CsvSchema } from '@ex-grid/data';
import { createPivotLayout, SnapshotPivotSource } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
import { writeSalesCsv } from './SalesCsv';

// The export this application knows, declared: nothing is guessed. A column the Schema does not declare is skipped; a
// declared one missing from the header is refused by name.
const exported: CsvSchema = {
    recordKey: 'Order',
    columns: [
        { name: 'Order', kind: 'integer' },
        { name: 'Region', kind: 'text' },
        { name: 'Country', kind: 'text' },
        { name: 'Category', kind: 'text' },
        { name: 'Channel', kind: 'text' },
        { name: 'Date', kind: 'date', header: 'Order date', dateFormats: ['yyyy-MM-dd'] },
        { name: 'Units', kind: 'integer' },
        { name: 'Revenue', kind: 'decimal', thousandsSeparator: ',' },
    ],
};

const firstLayout = createPivotLayout({
    rows: ['Region', 'Country'],
    columns: ['Channel'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function FromCsvMantine(): ReactElement {
    const [source, setSource] = useState<SnapshotPivotSource>();
    const [layout, setLayout] = useState(firstLayout);
    const [reading, setReading] = useState(false);
    const [status, setStatus] = useState('');

    const read = async (malformed: boolean): Promise<void> => {
        setReading(true);
        setStatus('Reading…');
        // Paint the status and the disabled buttons before the file is made.
        await new Promise(resolve => { setTimeout(resolve, 0); });
        try {
            // A file the user brings is an <input type="file">'s File, which reads the same way.
            const snapshot = await readCsvSnapshot(exported, writeSalesCsv(50_000, malformed));
            // One field per column, captioned and typed by it.
            setSource(new SnapshotPivotSource(snapshot));
            setStatus(`${snapshot.rowCount.toLocaleString('en-US')} rows read.`);
        } catch (refused) {
            if (!(refused instanceof SnapshotError)) throw refused;
            // The whole file is refused, naming the row and the column: a report over the rows that could be read
            // would be quietly short.
            setSource(undefined);
            setStatus(refused.message);
        } finally {
            setReading(false);
        }
    };

    return (
        <>
            <Group mb="sm">
                <Button size="xs" variant="default" disabled={reading} onClick={() => { void read(false); }}>Read 50,000 sales from a CSV</Button>
                <Button size="xs" variant="default" disabled={reading} onClick={() => { void read(true); }}>Read a file with a bad row</Button>
                <Text size="sm">{status}</Text>
            </Group>
            {source !== undefined && (
                <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={320} />
            )}
        </>
    );
}
