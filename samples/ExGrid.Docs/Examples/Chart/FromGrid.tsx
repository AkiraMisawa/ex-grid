import { useRef, useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { gridChartCommandLabel } from '@ex-grid/chart';
import { DockedChart, useChartDock } from '@ex-grid/chart-react';
import { ExGrid, type ExGridApi } from '@ex-grid/react';
import { curveBridge, curveColumns, curveSource, type CurveRow } from './CurveHistory';

// The charts are text, never what the grid shows; one that can no longer be drawn says why where it stood.
const refused = (why: string): ReactElement => <p role="status">{why}</p>;

// useChartDock holds the charts made from the selection; the bridge's events go to it, and its commands join the
// grid's context menu in one line. A click on a point moves the grid's Focus to the cell it was drawn from.
export default function FromGrid(): ReactElement {
    const dock = useChartDock();
    const grid = useRef<ExGridApi<CurveRow>>(null);
    const [bridge] = useState(() => curveBridge({
        ...dock.bridgeOptions,
        placeSelection: ({ range, focus, rowSequenceVersion }) => {
            if (grid.current === null) throw new Error('The grid is not mounted.');
            return grid.current.placeSelection(range, focus, rowSequenceVersion);
        },
    }));
    return (
        <>
            <ExGrid ref={grid} source={curveSource} columns={curveColumns} contextCommands={bridge.commands} commandLabel={gridChartCommandLabel}
                pinnedColumnCount={1} viewportHeight={240} viewportWidth="stretch" temporal={Temporal} />
            <p role="status">{dock.refusal ?? (dock.charts.length === 0 ? 'Select some rates and open the context menu.' : '')}</p>
            {dock.charts.map(target => (
                <DockedChart key={target.name} bridge={bridge} target={target} width={720} height={280} refused={refused} />
            ))}
        </>
    );
}
