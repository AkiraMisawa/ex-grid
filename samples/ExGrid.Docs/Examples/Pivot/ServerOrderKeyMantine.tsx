import { useState, type ReactElement } from 'react';
import { createPivotLayout, type PivotField } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
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

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function ServerOrderKeyMantine(): ReactElement {
    const [source] = useState(() => {
        const server = new SalesServer();
        return fetchFrom(server, ordered(server.fields));
    });
    const [layout, setLayout] = useState(firstLayout);
    return <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />;
}
