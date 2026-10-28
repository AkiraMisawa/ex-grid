import { useState, type ReactElement } from 'react';
import { readCsvSnapshot, SnapshotError, type CsvSchema } from '@ex-grid/data';
import { createPivotLayout, SnapshotPivotSource } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
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

export default function FromCsv(): ReactElement {
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
            <p>
                <button type="button" disabled={reading} onClick={() => { void read(false); }}>Read 50,000 sales from a CSV</button>{' '}
                <button type="button" disabled={reading} onClick={() => { void read(true); }}>Read a file with a bad row</button>{' '}
                <span>{status}</span>
            </p>
            {source !== undefined && (
                <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={320} />
            )}
        </>
    );
}
