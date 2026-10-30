import { useState, type ReactElement } from 'react';
import { Group, SegmentedControl, Switch } from '@mantine/core';
import { RecordPivotSource, createPivotLayout, type PivotLayout, type PivotReportForm } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
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

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function ReportFormsMantine(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return (
        <>
            <Group mb="sm">
                <SegmentedControl size="xs" data={forms.map(([value, label]) => ({ value, label }))} value={layout.form}
                    onChange={value => { setLayout({ ...layout, form: value as PivotReportForm }); }} />
                <Switch label="Subtotals at the top" checked={layout.subtotalsAtTop}
                    onChange={event => { setLayout({ ...layout, subtotalsAtTop: event.currentTarget.checked }); }} />
                <Switch label="Grand Total row" checked={layout.grandTotalRow}
                    onChange={event => { setLayout({ ...layout, grandTotalRow: event.currentTarget.checked }); }} />
                <Switch label="Grand Total column" checked={layout.grandTotalColumn}
                    onChange={event => { setLayout({ ...layout, grandTotalColumn: event.currentTarget.checked }); }} />
            </Group>
            <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={380} />
        </>
    );
}
