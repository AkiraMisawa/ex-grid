import { useState, type ReactElement } from 'react';
import { Button, Code, Group } from '@mantine/core';
import { RecordPivotSource, createPivotLayout, readPivotLayoutJson, writePivotLayoutJson, type PivotLayout } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

const firstLayout: PivotLayout = createPivotLayout({
    rows: ['Category'],
    columns: ['Channel'],
    values: [{ field: 'Units', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function SavedLayoutMantine(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    // What you would store as the user's setting: versioned JSON, holding no value.
    const [saved, setSaved] = useState<string>();
    return (
        <>
            <Group mb="sm">
                <Button size="xs" onClick={() => { setSaved(writePivotLayoutJson(layout)); }}>Save the layout</Button>
                <Button size="xs" variant="default" disabled={saved === undefined}
                    onClick={() => { if (saved !== undefined) setLayout(readPivotLayoutJson(saved)); }}>Restore the saved layout</Button>
            </Group>
            <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={320} />
            {saved !== undefined && <Code block mt="sm" style={{ whiteSpace: 'pre-wrap', overflowWrap: 'anywhere' }}>{saved}</Code>}
        </>
    );
}
