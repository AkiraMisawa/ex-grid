import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, unitedKingdomPivotWords, type PivotLayout } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

// Excel's English (UK) words, whatever the reader's language: Max. of Revenue and Min. of Revenue. Open either's
// Value Field Settings… for "Summarise value field by", and Max. and Min. in its list.
const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Category'],
    values: [
        { field: 'Revenue', aggregation: 'max', numberFormat: '#,##0.00' },
        { field: 'Revenue', aggregation: 'min', numberFormat: '#,##0.00' },
    ],
});

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function UnitedKingdomWordsMui(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} label={unitedKingdomPivotWords} viewportHeight={380} />;
}
