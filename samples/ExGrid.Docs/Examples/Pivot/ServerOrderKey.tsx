import { useState, type ReactElement } from 'react';
import { createPivotLayout, type PivotField } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { fetchFrom, SalesServer } from './SalesServer';

const firstLayout = createPivotLayout({
    rows: ['Region'],
    columns: ['Year'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// The regions by the size of their market, largest first, rather than by their names.
const marketSize: Readonly<Record<string, number>> = { 'North America': 0, Europe: 1, 'Asia Pacific': 2 };

// The browser orders the report, so a field's Order Key is the browser's: the server's fields, with Region given one.
// Nothing about the order crosses the wire.
function ordered(fields: readonly PivotField[]): readonly PivotField[] {
    return fields.map(field => field.name === 'Region' ? { ...field, orderKey: region => marketSize[String(region)] ?? 3 } : field);
}

export default function ServerOrderKey(): ReactElement {
    const [source] = useState(() => {
        const server = new SalesServer();
        return fetchFrom(server, ordered(server.fields));
    });
    const [layout, setLayout] = useState(firstLayout);
    return <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />;
}
