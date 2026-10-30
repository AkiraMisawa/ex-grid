import { useState, type ReactElement } from 'react';
import type { PivotDetailsView } from '@ex-grid/pivot';
import { RecordPivotSource, createPivotLayout, type PivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Category'],
    columns: ['Channel'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

export default function ShowDetails(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    const [detailsView, setDetailsView] = useState<PivotDetailsView>('tab');
    return (
        <>
            <p>
                <label>
                    Open the records{' '}
                    <select value={detailsView} onChange={event => { setDetailsView(event.target.value as PivotDetailsView); }}>
                        <option value="tab">in a tab at the report's foot</option>
                        <option value="dialog">in a dialog</option>
                    </select>
                </label>
            </p>
            <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} detailsView={detailsView}
                viewportHeight={340} />
        </>
    );
}
