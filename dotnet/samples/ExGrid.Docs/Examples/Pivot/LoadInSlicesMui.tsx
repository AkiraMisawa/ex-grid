import { useState, type ReactElement } from 'react';
import { Button, LinearProgress, Stack, Typography } from '@mui/material';
import { readArrowSnapshot, writeArrowSnapshot, type SnapshotProgress } from '@ex-grid/data';
import { createPivotLayout, SnapshotPivotSource } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
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

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function LoadInSlicesMui(): ReactElement {
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
            <Stack direction="row" spacing={1.5} sx={{ mb: 1.5, alignItems: 'center' }}>
                <Button size="small" variant="contained" disabled={progress !== undefined} onClick={() => { void load(); }}>Load 200,000 sales</Button>
                {progress !== undefined && (
                    <>
                        {progress.totalRows === undefined
                            ? <LinearProgress sx={{ width: 240 }} />
                            : <LinearProgress variant="determinate" sx={{ width: 240 }} value={(100 * progress.rows) / progress.totalRows} />}
                        <Typography variant="body2">{progress.rows.toLocaleString('en-US')} sales read</Typography>
                    </>
                )}
            </Stack>
            {source !== undefined && (
                <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={320} />
            )}
        </>
    );
}
