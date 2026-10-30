import { useState, type ReactElement } from 'react';
import { createPivotLayout } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
import { fetchFrom, SalesServer } from './SalesServer';

const firstLayout = createPivotLayout({
    rows: ['Region'],
    columns: ['Year'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function ServerSourceMantine(): ReactElement {
    // Stands for the server across the network, and the source that asks it: ExPivot asks for the Leaf Aggregates, and
    // builds the report, its totals and its order in the browser.
    const [source] = useState(() => fetchFrom(new SalesServer()));
    const [layout, setLayout] = useState(firstLayout);
    return <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={340} />;
}
