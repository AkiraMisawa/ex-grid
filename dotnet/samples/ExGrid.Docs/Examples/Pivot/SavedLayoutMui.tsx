import { useState, type ReactElement } from 'react';
import { Box, Button, Stack } from '@mui/material';
import { RecordPivotSource, createPivotLayout, readPivotLayoutJson, writePivotLayoutJson, type PivotLayout } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Category'],
    columns: ['Channel'],
    values: [{ field: 'Units', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function SavedLayoutMui(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    // What you would store as the user's setting: versioned JSON, holding no value.
    const [saved, setSaved] = useState<string>();
    return (
        <>
            <Stack direction="row" spacing={1} sx={{ mb: 1.5 }}>
                <Button size="small" variant="contained" onClick={() => { setSaved(writePivotLayoutJson(layout)); }}>Save the layout</Button>
                <Button size="small" variant="outlined" disabled={saved === undefined}
                    onClick={() => { if (saved !== undefined) setLayout(readPivotLayoutJson(saved)); }}>Restore the saved layout</Button>
            </Stack>
            <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={320} />
            {saved !== undefined && (
                <Box component="pre" sx={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', fontSize: 12 }}>{saved}</Box>
            )}
        </>
    );
}
