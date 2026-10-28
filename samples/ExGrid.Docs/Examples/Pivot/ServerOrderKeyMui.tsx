import { useState, type ReactElement } from 'react';
import { createPivotLayout, type PivotField } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { fetchFrom, SalesServer } from './SalesServer';

const firstLayout = createPivotLayout({
    rows: ['Region'],
    columns: ['Year'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// The regions by the size of their market, largest first, rather than by their names.
const marketSize: ReadonlyMap<string, number> = new Map([['North America', 0], ['Europe', 1], ['Asia Pacific', 2], ['Latin America', 3]]);

// A region the table does not rank is refused by name, never ordered last as if it were the smallest.
function sizeOf(region: unknown): number {
    const size = marketSize.get(String(region));
    if (size === undefined) throw new Error(`No market size is known for the region '${String(region)}'.`);
    return size;
}

// The browser orders the report, so a field's Order Key is the browser's: the server's fields, with Region given one.
// Nothing about the order crosses the wire.
function ordered(fields: readonly PivotField[]): readonly PivotField[] {
    return fields.map(field => field.name === 'Region' ? { ...field, orderKey: sizeOf } : field);
}

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function ServerOrderKeyMui(): ReactElement {
    const [source] = useState(() => {
        const server = new SalesServer();
        return fetchFrom(server, ordered(server.fields));
    });
    const [layout, setLayout] = useState(firstLayout);
    return <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={300} />;
}
