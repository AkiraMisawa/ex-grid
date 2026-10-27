import { useRef, useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { gridChartCommandLabel } from '@ex-grid/chart';
import { useChartDock } from '@ex-grid/chart-react';
import { MantineChartDock } from '@ex-grid/chart-mantine';
import { MantineExGrid } from '@ex-grid/mantine';
import type { ExGridApi } from '@ex-grid/react';
import { curveBridge, curveColumns, curveSource, type CurveRow } from './CurveHistory';

// useChartDock holds the charts made from the selection; the bridge's events go to it, and its commands join the
// grid's context menu in one line. A click on a point moves the grid's Focus to the cell it was drawn from.
//
// Under a MantineProvider, with @mantine/core/styles.css, @ex-grid/mantine/styles.css and
// @ex-grid/chart-mantine/styles.css loaded: each chart opens as a tab below the grid, never over it, its toolbar and
// menu Mantine's components, the theme's primary leading its series.
export default function FromGridMantine(): ReactElement {
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
        <MantineChartDock dock={dock} bridge={bridge} placement="bottom" chartWidth={720} chartHeight={280}>
            <MantineExGrid ref={grid} withBorder source={curveSource} columns={curveColumns} contextCommands={bridge.commands}
                commandLabel={gridChartCommandLabel} pinnedColumnCount={1} viewportHeight={240} viewportWidth="stretch" temporal={Temporal} />
        </MantineChartDock>
    );
}
