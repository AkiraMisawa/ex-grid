import { useState, type ReactElement } from 'react';
import { readArrowSnapshot, writeArrowSnapshot, type SnapshotProgress } from '@ex-grid/data';
import { createPivotLayout, SnapshotPivotSource } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
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

export default function LoadInSlices(): ReactElement {
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
            <p>
                <button type="button" disabled={progress !== undefined} onClick={() => { void load(); }}>Load 200,000 sales</button>{' '}
                {progress !== undefined && (
                    <>
                        <progress max={progress.totalRows} value={progress.rows} />{' '}
                        <span>{progress.rows.toLocaleString('en-US')} sales read</span>
                    </>
                )}
            </p>
            {source !== undefined && (
                <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={320} />
            )}
        </>
    );
}
