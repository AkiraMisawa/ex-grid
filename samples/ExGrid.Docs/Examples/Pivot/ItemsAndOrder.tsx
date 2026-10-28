import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, pivotSort, textItemKey, type PivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
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

export default function ItemsAndOrder(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={380} />;
}
