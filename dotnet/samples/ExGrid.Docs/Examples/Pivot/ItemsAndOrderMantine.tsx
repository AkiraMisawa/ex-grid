import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, pivotSort, textItemKey, type PivotLayout } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

const firstLayout: PivotLayout = createPivotLayout({
    rows: [
        // Every region collapsed but Europe.
        { field: 'Region', collapsed: true, toggledItems: [textItemKey('Europe')] },
        // Countries by their revenue, largest first: the Value Field at index 0.
        { field: 'Country', sort: pivotSort('descending', 0) },
    ],
    // Direct sales left out of every cell and every total.
    columns: [{ field: 'Channel', hiddenItems: [textItemKey('Direct')] }],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function ItemsAndOrderMantine(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={380} />;
}
