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
const marketSize: Readonly<Record<string, number>> = { 'North America': 0, Europe: 1, 'Asia Pacific': 2 };

// The browser orders the report, so a field's Order Key is the browser's: the server's fields, with Region given one.
// Nothing about the order crosses the wire.
function ordered(fields: readonly PivotField[]): readonly PivotField[] {
    return fields.map(field => field.name === 'Region' ? { ...field, orderKey: region => marketSize[String(region)] ?? 3 } : field);
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
