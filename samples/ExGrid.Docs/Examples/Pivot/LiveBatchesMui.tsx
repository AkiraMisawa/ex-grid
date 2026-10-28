import { useEffect, useState, type ReactElement } from 'react';
import { Button, Chip, Stack } from '@mui/material';
import { createChangeBatch } from '@ex-grid/data';
import { createPivotLayout, SnapshotPivotSource } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { mulberry32 } from './Seeded';
import { sampleSales } from './SalesData';
import { amendSales, salesSnapshot, salesSnapshotFields } from './SalesSnapshot';

const firstLayout = createPivotLayout({
    rows: ['Region', 'Country'],
    columns: ['Channel'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function LiveBatchesMui(): ReactElement {
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
            <Stack direction="row" spacing={1} sx={{ mb: 1.5, alignItems: 'center' }}>
                <Button size="small" variant="outlined" onClick={() => { setPaused(was => !was); }}>{paused ? 'Resume the changes' : 'Pause the changes'}</Button>
                <Chip size="small" label={`Snapshot version ${version.toString()}`} />
            </Stack>
            <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />
        </>
    );
}
