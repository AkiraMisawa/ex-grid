import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, type PivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
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

export default function Aggregations(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />;
}
