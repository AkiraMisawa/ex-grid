import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, unitedKingdomPivotWords, type PivotLayout } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

// Excel's English (UK) words, whatever the reader's language: Max. of Revenue and Min. of Revenue. Open either's
// Value Field Settings… for "Summarise value field by", and Max. and Min. in its list.
const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Category'],
    values: [
        { field: 'Revenue', aggregation: 'max', numberFormat: '#,##0.00' },
        { field: 'Revenue', aggregation: 'min', numberFormat: '#,##0.00' },
    ],
});

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function UnitedKingdomWordsMantine(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} label={unitedKingdomPivotWords} viewportHeight={380} />;
}
