import { useState, type ReactElement } from 'react';
import { createPivotLayout, datePartField, pivotField, SnapshotPivotSource } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
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

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function FromSnapshotMantine(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={340} />;
}
