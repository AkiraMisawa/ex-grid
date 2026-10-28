import { useState, type ReactElement } from 'react';
import { MenuItem, TextField, Typography } from '@mui/material';
import { RecordPivotSource, createPivotLayout, type PivotLayout, type PivotShowValuesAs } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
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

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function ShowValuesAsMui(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    // The second Value Field, while the user has left it in Values; the choice is offered only then.
    const second = layout.values[1];
    return (
        <>
            {second === undefined ? <Typography variant="body2" sx={{ mb: 1.5 }}>Put a second value in Values to choose how it is shown.</Typography> : (
                <TextField select size="small" label="Show the second Revenue as" sx={{ minWidth: 240, mb: 1.5 }} value={second.showValuesAs}
                    onChange={event => { setLayout(showingAs(layout, event.target.value as PivotShowValuesAs)); }}>
                    {calculations.map(([value, title]) => <MenuItem key={value} value={value}>{title}</MenuItem>)}
                </TextField>
            )}
            <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={340} />
        </>
    );
}
