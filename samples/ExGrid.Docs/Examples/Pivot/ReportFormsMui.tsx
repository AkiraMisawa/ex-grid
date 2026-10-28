import { useState, type ReactElement } from 'react';
import { FormControlLabel, Stack, Switch, ToggleButton, ToggleButtonGroup } from '@mui/material';
import { RecordPivotSource, createPivotLayout, type PivotLayout, type PivotReportForm } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

// Every setting is a property of the layout: a change is a new layout.
const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Region', 'Country'],
    columns: ['Year'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

const forms: readonly (readonly [PivotReportForm, string])[] = [['compact', 'Compact'], ['outline', 'Outline'], ['tabular', 'Tabular']];

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function ReportFormsMui(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return (
        <>
            <Stack direction="row" spacing={2} sx={{ alignItems: 'center', flexWrap: 'wrap', mb: 1.5 }}>
                <ToggleButtonGroup size="small" exclusive value={layout.form}
                    onChange={(_, value: PivotReportForm | null) => { if (value !== null) setLayout({ ...layout, form: value }); }}>
                    {forms.map(([form, title]) => <ToggleButton key={form} value={form}>{title}</ToggleButton>)}
                </ToggleButtonGroup>
                <FormControlLabel label="Subtotals at the top" control={<Switch checked={layout.subtotalsAtTop}
                    onChange={event => { setLayout({ ...layout, subtotalsAtTop: event.target.checked }); }} />} />
                <FormControlLabel label="Grand Total row" control={<Switch checked={layout.grandTotalRow}
                    onChange={event => { setLayout({ ...layout, grandTotalRow: event.target.checked }); }} />} />
                <FormControlLabel label="Grand Total column" control={<Switch checked={layout.grandTotalColumn}
                    onChange={event => { setLayout({ ...layout, grandTotalColumn: event.target.checked }); }} />} />
            </Stack>
            <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={380} />
        </>
    );
}
