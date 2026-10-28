import { useState, type ReactElement } from 'react';
import { RecordPivotSource, emptyPivotLayout } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/pivot-mantine/styles.css loaded: the pivot in Mantine's Paper, its Field List, toolbar, menus and panels
// Mantine's components, its colours the theme's.
export default function FieldsPaneMantine(): ReactElement {
    // Nothing placed yet: the report asks for fields until the user ticks some.
    const [layout, setLayout] = useState(emptyPivotLayout);
    // Held as well, so a pane the user hid stays hidden when you keep the setting.
    const [showFieldList, setShowFieldList] = useState(true);
    return <MantinePivot withBorder source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} showFieldList={showFieldList}
        onShowFieldListChanged={({ shown }) => { setShowFieldList(shown); }} viewportHeight={380} />;
}
