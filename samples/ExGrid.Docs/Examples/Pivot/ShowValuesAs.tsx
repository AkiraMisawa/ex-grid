import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, type PivotLayout, type PivotShowValuesAs } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Region'],
    columns: ['Year'],
    values: [
        { field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' },
        { field: 'Revenue', aggregation: 'sum', caption: 'Share', showValuesAs: 'percentOfColumnTotal', numberFormat: '0.0%' },
    ],
});

const calculations: readonly (readonly [PivotShowValuesAs, string])[] = [
    ['percentOfGrandTotal', '% of Grand Total'],
    ['percentOfColumnTotal', '% of Column Total'],
    ['percentOfRowTotal', '% of Row Total'],
    ['noCalculation', 'No Calculation'],
];

// The layout is immutable: the second Value Field changed is a new one, in a new layout.
function showingAs(layout: PivotLayout, showValuesAs: PivotShowValuesAs): PivotLayout {
    return { ...layout, values: layout.values.map((value, i) => (i === 1 ? { ...value, showValuesAs } : value)) };
}

export default function ShowValuesAs(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    // The second Value Field, while the user has left it in Values; the choice is offered only then.
    const second = layout.values[1];
    return (
        <>
            <p>
                {second === undefined ? 'Put a second value in Values to choose how it is shown.' : (
                    <label>
                        Show the second Revenue as{' '}
                        <select value={second.showValuesAs} onChange={event => { setLayout(showingAs(layout, event.target.value as PivotShowValuesAs)); }}>
                            {calculations.map(([value, title]) => <option key={value} value={value}>{title}</option>)}
                        </select>
                    </label>
                )}
            </p>
            <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={340} />
        </>
    );
}
