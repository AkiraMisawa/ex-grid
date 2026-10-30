import { useState, type ReactElement } from 'react';
import { RecordPivotSource, emptyPivotLayout } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

// Under a ThemeProvider whose theme was made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css
// and @ex-grid/pivot-mui/styles.css loaded: the pivot in MUI's Paper, its Field List, toolbar, menus and panels MUI's
// components, its colours the theme's.
export default function FieldsPaneMui(): ReactElement {
    // Nothing placed yet: the report asks for fields until the user ticks some.
    const [layout, setLayout] = useState(emptyPivotLayout);
    // Held as well, so a pane the user hid stays hidden when you keep the setting.
    const [showFieldList, setShowFieldList] = useState(true);
    return <MuiPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} showFieldList={showFieldList}
        onShowFieldListChanged={({ shown }) => { setShowFieldList(shown); }} viewportHeight={380} />;
}
