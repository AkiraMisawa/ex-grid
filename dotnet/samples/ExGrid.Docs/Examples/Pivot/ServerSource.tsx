import { useState, type ReactElement } from 'react';
import { createPivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { fetchFrom, SalesServer } from './SalesServer';

const firstLayout = createPivotLayout({
    rows: ['Region'],
    columns: ['Year'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

export default function ServerSource(): ReactElement {
    // Stands for the server across the network, and the source that asks it: ExPivot asks for the Leaf Aggregates, and
    // builds the report, its totals and its order in the browser.
    const [source] = useState(() => fetchFrom(new SalesServer()));
    const [layout, setLayout] = useState(firstLayout);
    return <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={340} />;
}
