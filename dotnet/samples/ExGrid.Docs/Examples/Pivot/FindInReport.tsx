import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, textItemKey, type PivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

// Nothing to wire: Ctrl+F in the report finds a label or a value as shown, and marks the matches on screen. Every
// region but Europe is collapsed, so only Europe's rows can be found.
const firstLayout: PivotLayout = createPivotLayout({
    rows: [
        { field: 'Region', collapsed: true, toggledItems: [textItemKey('Europe')] },
        'Country',
        'Product',
    ],
    columns: ['Channel'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

export default function FindInReport(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={380} />;
}
