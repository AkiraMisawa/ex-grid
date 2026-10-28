import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, readPivotLayoutJson, writePivotLayoutJson, type PivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Category'],
    columns: ['Channel'],
    values: [{ field: 'Units', aggregation: 'sum', numberFormat: '#,##0' }],
});

export default function SavedLayout(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    // What you would store as the user's setting: versioned JSON, holding no value.
    const [saved, setSaved] = useState<string>();
    return (
        <>
            <p>
                <button type="button" onClick={() => { setSaved(writePivotLayoutJson(layout)); }}>Save the layout</button>{' '}
                <button type="button" disabled={saved === undefined}
                    onClick={() => { if (saved !== undefined) setLayout(readPivotLayoutJson(saved)); }}>Restore the saved layout</button>
            </p>
            <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={320} />
            {saved !== undefined && <pre style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere', fontSize: 12 }}>{saved}</pre>}
        </>
    );
}
