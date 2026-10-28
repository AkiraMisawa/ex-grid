import { useState, type ReactElement } from 'react';
import { RecordPivotSource, createPivotLayout, type PivotLayout, type PivotReportForm } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
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

export default function ReportForms(): ReactElement {
    const [layout, setLayout] = useState(firstLayout);
    return (
        <>
            <p>
                <label>
                    Form{' '}
                    <select value={layout.form} onChange={event => { setLayout({ ...layout, form: event.target.value as PivotReportForm }); }}>
                        {forms.map(([form, title]) => <option key={form} value={form}>{title}</option>)}
                    </select>
                </label>{' '}
                <label>
                    <input type="checkbox" checked={layout.subtotalsAtTop}
                        onChange={event => { setLayout({ ...layout, subtotalsAtTop: event.target.checked }); }} /> Subtotals at the top
                </label>{' '}
                <label>
                    <input type="checkbox" checked={layout.grandTotalRow}
                        onChange={event => { setLayout({ ...layout, grandTotalRow: event.target.checked }); }} /> Grand Total row
                </label>{' '}
                <label>
                    <input type="checkbox" checked={layout.grandTotalColumn}
                        onChange={event => { setLayout({ ...layout, grandTotalColumn: event.target.checked }); }} /> Grand Total column
                </label>
            </p>
            <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} viewportHeight={380} />
        </>
    );
}
