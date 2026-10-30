import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, type PivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
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

export default function ExactMoney(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={260} />;
}
