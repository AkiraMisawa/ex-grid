import { useState, type ReactElement } from 'react';
import type { PivotToolbarSlotContext } from '@ex-grid/pivot';
import { createPivotLayout, RecordPivotSource } from '@ex-grid/pivot-engine';
import { ExPivot, usePivotPart, type ExPivotChrome } from '@ex-grid/pivot-react';
import { salesFields, sampleSales } from './SalesData';

// Made once, outside the component: a new source is a refresh.
const source = new RecordPivotSource(sampleSales(), salesFields);

const firstLayout = createPivotLayout({
    rows: ['Region', 'Country'],
    values: [{ field: 'Revenue', aggregation: 'sum', numberFormat: '#,##0' }],
});

// Each button claims its click (data-ex-claims) and hands it to the pivot, so the click is handled in its place among
// the report's keys and presses. The buttons are handed in by ref, so the pivot gives the keyboard back to Layout ▾
// when its menu closes and opens the menu in the anchor around it; the report filter band is drawn in its anchor.
function OwnToolbarView({ context }: { readonly context: PivotToolbarSlotContext }): ReactElement {
    const view = usePivotPart(context);
    return (
        <div style={{ display: 'flex', gap: 8, alignItems: 'center', paddingBottom: 6 }}>
            <div ref={context.anchor('report-filters')} />
            <span ref={context.anchor('layout')} style={{ position: 'relative', marginLeft: 'auto' }}>
                <button ref={context.control('layout')} type="button" data-ex-claims="click" aria-haspopup="menu"
                    aria-expanded={view.layoutMenu.open} onClick={context.toggleLayoutMenu}>{view.layoutMenu.label} ▾</button>
            </span>
            <button ref={context.control('field-list')} type="button" data-ex-claims="click" aria-pressed={view.fieldList.shown}
                onClick={context.toggleFieldList}>{view.fieldList.label}</button>
            {view.refusal !== undefined && <span role="alert">{view.refusal}</span>}
        </div>
    );
}

// Your own Pivot Toolbar; every other part is left to ExPivot's built-in markup.
const chrome: ExPivotChrome = { toolbar: context => <OwnToolbarView context={context} /> };

export default function OwnToolbar(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    const [showFieldList, setShowFieldList] = useState(false);
    return <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} chrome={chrome}
        showFieldList={showFieldList} onShowFieldListChanged={({ shown }) => { setShowFieldList(shown); }} viewportHeight={320} />;
}
