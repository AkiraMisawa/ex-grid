import { useState, type ReactElement } from 'react';
import { createPivotLayout, datePartField, pivotField, SnapshotPivotSource } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
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

export default function FromSnapshot(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={340} />;
}
