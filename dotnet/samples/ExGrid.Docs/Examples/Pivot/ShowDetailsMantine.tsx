import { useState, type ReactElement } from 'react';
import { Group, Radio } from '@mantine/core';
import type { PivotDetailsView } from '@ex-grid/pivot';
import { RecordPivotSource, createPivotLayout, type PivotLayout } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Category'],
    columns: ['Channel'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function ShowDetailsMantine(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    const [detailsView, setDetailsView] = useState<PivotDetailsView>('tab');
    return (
        <>
            <Radio.Group value={detailsView} onChange={value => { setDetailsView(value as PivotDetailsView); }} label="Open the records" mb="sm">
                <Group mt="xs">
                    <Radio value="tab" label="in a tab at the report's foot" />
                    <Radio value="dialog" label="in a dialog" />
                </Group>
            </Radio.Group>
            <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }}
                detailsView={detailsView} viewportHeight={340} />
        </>
    );
}
