import { useState, type ReactElement } from 'react';
import { RecordPivotSource, emptyPivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

export default function FieldsPane(): ReactElement {
    // Nothing placed yet: the report asks for fields until the user ticks some.
    const [layout, setLayout] = useState(emptyPivotLayout);
    // Held as well, so a pane the user hid stays hidden when you keep the setting.
    const [showFieldList, setShowFieldList] = useState(true);
    return <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} showFieldList={showFieldList}
        onShowFieldListChanged={({ shown }) => { setShowFieldList(shown); }} viewportHeight={380} />;
}
