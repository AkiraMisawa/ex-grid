import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, type PivotLayout } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

// Five Value Fields side by side, under Σ Values in Columns.
const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Category'],
    values: [
        { field: 'Units', aggregation: 'sum', numberFormat: '#,##0' },
        { field: 'Product', aggregation: 'count', caption: 'Order lines', numberFormat: '#,##0' },
        { field: 'Revenue', aggregation: 'average', numberFormat: '#,##0.00' },
        { field: 'Revenue', aggregation: 'max', numberFormat: '#,##0.00' },
        { field: 'Revenue', aggregation: 'stdDev', numberFormat: '#,##0.00' },
    ],
});

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function AggregationsMui(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />;
}
