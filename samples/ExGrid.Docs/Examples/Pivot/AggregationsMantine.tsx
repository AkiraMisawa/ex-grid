import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, type PivotLayout } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
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

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function AggregationsMantine(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />;
}
