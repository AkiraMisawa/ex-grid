import { useRef, useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { gridChartCommandLabel } from '@ex-grid/chart';
import { useChartDock } from '@ex-grid/chart-react';
import { MuiChartDock } from '@ex-grid/chart-mui';
import { MuiExGrid } from '@ex-grid/mui';
import type { ExGridApi } from '@ex-grid/react';
import { curveBridge, curveColumns, curveSource, type CurveRow } from './CurveHistory';

// useChartDock holds the charts made from the selection; the bridge's events go to it, and its commands join the
// grid's context menu in one line. A click on a point moves the grid's Focus to the cell it was drawn from.
//
// Under a ThemeProvider whose theme is made with createTheme({ cssVariables: true }), with @ex-grid/mui/styles.css and
// @ex-grid/chart-mui/styles.css loaded: each chart opens as a tab below the grid, never over it, its toolbar and menu
// MUI's components, the theme's primary leading its series.
export default function FromGridMui(): ReactElement {
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
        <MuiChartDock dock={dock} bridge={bridge} placement="bottom" chartWidth={720} chartHeight={280}>
            <MuiExGrid ref={grid} source={curveSource} columns={curveColumns} contextCommands={bridge.commands}
                commandLabel={gridChartCommandLabel} pinnedColumnCount={1} viewportHeight={240} viewportWidth="stretch" temporal={Temporal} />
        </MuiChartDock>
    );
}
