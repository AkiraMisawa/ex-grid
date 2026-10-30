import { useState, type ReactElement } from 'react';
import { FormControl, FormControlLabel, FormLabel, Radio, RadioGroup } from '@mui/material';
import type { PivotDetailsView } from '@ex-grid/pivot';
import { RecordPivotSource, createPivotLayout, type PivotLayout } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Category'],
    columns: ['Channel'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function ShowDetailsMui(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    const [detailsView, setDetailsView] = useState<PivotDetailsView>('tab');
    return (
        <>
            <FormControl sx={{ mb: 1.5 }}>
                <FormLabel id="show-details-view">Open the records</FormLabel>
                <RadioGroup row aria-labelledby="show-details-view" value={detailsView}
                    onChange={event => { setDetailsView(event.target.value as PivotDetailsView); }}>
                    <FormControlLabel value="tab" control={<Radio size="small" />} label="in a tab at the report's foot" />
                    <FormControlLabel value="dialog" control={<Radio size="small" />} label="in a dialog" />
                </RadioGroup>
            </FormControl>
            <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} detailsView={detailsView}
                viewportHeight={340} />
        </>
    );
}
