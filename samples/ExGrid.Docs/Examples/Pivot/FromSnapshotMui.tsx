import { useState, type ReactElement } from 'react';
import { createPivotLayout, datePartField, pivotField, SnapshotPivotSource } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { sampleSales } from './SalesData';
import { salesSnapshot } from './SalesSnapshot';

// Any Snapshot will do: built here from objects, or read from a CSV, a database or an Arrow stream. The fields name
// its columns; a date part names the Date column it is a part of. Made once, outside the component: a new source is a
// refresh.
const source = new SnapshotPivotSource(salesSnapshot(sampleSales()), [
    pivotField({ name: 'Region', type: 'text' }),
    pivotField({ name: 'Category', type: 'text' }),
    datePartField('Quarter', 'Date', 'quarter'),
    pivotField({ name: 'Units', type: 'number' }),
    pivotField({ name: 'Revenue', type: 'number', format: '#,##0.00' }),
]);

const firstLayout = createPivotLayout({
    rows: ['Category'],
    columns: ['Quarter'],
    values: [{ field: 'Units', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function FromSnapshotMui(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={340} />;
}
