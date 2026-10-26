import { useMemo, useState, type ReactElement } from 'react';
import { RecordPivotSource, type PivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { DemoNav } from '../demo-nav';
import { demoRiskFields, demoRiskFieldsWithoutKey, demoRiskLayout, demoRiskPositions } from '../pivot-risk-data';

// The page's own sum over the positions, beside the report's grand total, which the engine computes from the same
// positions.
const net = `The positions' net PV01, summed by the page itself: ${demoRiskPositions.reduce((sum, p) => sum + p.pv01, 0).toLocaleString('en-US')} USD `
    + `over ${demoRiskPositions.length.toLocaleString('en-US')} positions.`;

/** The page /pivot-risk of the Blazor hosts: a rate-delta report by tenor, the tenors ordered by the page's Order
 * Key, months to maturity, or by their labels without it (ADR-0060). */
export function PivotRisk(): ReactElement {
    const [orderKey, setOrderKey] = useState(true);
    // The fields are the source's, so other fields are another source; the layout stays.
    const source = useMemo(() => new RecordPivotSource(demoRiskPositions, orderKey ? demoRiskFields : demoRiskFieldsWithoutKey), [orderKey]);
    const [layout, setLayout] = useState<PivotLayout>(demoRiskLayout);
    return (
        <>
            <DemoNav route="pivot-risk" />
            <h1>Pivot risk — a rate-delta report by tenor</h1>
            <p>
                <strong>The use case:</strong> a rate-delta report as a risk desk reads one: each desk's PV01 on each
                curve, bucketed by tenor, with desks and curves in Rows and tenors in Columns. The positions are invented
                from a fixed seed, the Blazor hosts' own.
            </p>
            <p>
                A tenor is text, and no alphabetical order holds the tenors, so the Tenor field declares an{' '}
                <strong>Order Key</strong>: months to maturity, called once per Item (ADR-0060). <code>18M</code> and{' '}
                <code>1Y6M</code> are both 18 months, and stand side by side as two Items: a key orders Items and never
                merges them.
            </p>
            <div className="demo-toolbar">
                <label>
                    <input id="risk-order-key" type="checkbox" checked={orderKey} onChange={event => { setOrderKey(event.target.checked); }} />
                    {' '}Order the tenors by <code>tenorMonths</code>
                </label>
            </div>
            <p className="demo-status"><span id="risk-net">{net}</span></p>
            <ExPivot source={source} layout={layout} onLayoutChanged={({ layout: next }) => { setLayout(next); }} locale="en-US" viewportHeight={420} />
        </>
    );
}
