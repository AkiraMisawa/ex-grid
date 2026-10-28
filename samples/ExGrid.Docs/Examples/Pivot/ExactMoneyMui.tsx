import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, type PivotLayout } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { holdingFields, sampleHoldings } from './HoldingsData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleHoldings(), holdingFields);

// The same market values summed twice: as an exact decimal, and as a double, as binary floating point does. Compare
// the cents.
const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Custodian'],
    values: [
        { field: 'MarketValue', aggregation: 'sum', numberFormat: '#,##0.00' },
        { field: 'MarketValueDouble', aggregation: 'sum', numberFormat: '#,##0.00' },
    ],
});

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function ExactMoneyMui(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={260} />;
}
