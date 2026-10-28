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

// The second Value Field, while the user has left it in Values.
function shownOf(layout: PivotLayout): PivotShowValuesAs {
    const second = layout.values[1];
    return second === undefined ? 'noCalculation' : second.showValuesAs;
}

// The layout is immutable: the Value Field changed is a new one, in a new layout.
function showingAs(layout: PivotLayout, showValuesAs: PivotShowValuesAs): PivotLayout {
    if (layout.values.length < 2) return layout;
    return { ...layout, values: layout.values.map((value, i) => (i === 1 ? { ...value, showValuesAs } : value)) };
}

export default function ShowValuesAs(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return (
        <>
            <p>
                <label>
                    Show the second Revenue as{' '}
                    <select value={shownOf(layout)} onChange={event => { setLayout(showingAs(layout, event.target.value as PivotShowValuesAs)); }}>
                        {calculations.map(([value, title]) => <option key={value} value={value}>{title}</option>)}
                    </select>
                </label>
            </p>
            <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={340} />
        </>
    );
}
