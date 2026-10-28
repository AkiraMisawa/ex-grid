import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, textItemKey, type PivotLayout } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
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

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function FindInReportMantine(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={380} />;
}
