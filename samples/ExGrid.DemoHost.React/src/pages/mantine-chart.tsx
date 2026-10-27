import { useCallback, useRef, useState, type ReactElement } from 'react';
import { MantineProvider } from '@mantine/core';
import { Temporal } from 'temporal-polyfill';
import { gridChartCommandLabel } from '@ex-grid/chart';
import { useChartDock } from '@ex-grid/chart-react';
import { MantineChartDock } from '@ex-grid/chart-mantine';
import type { ExGridApi } from '@ex-grid/react';
import { MantineExGrid } from '@ex-grid/mantine';
// Mantine's styles and the Wrappers', as text this page writes into a <style> of its own and takes away when it
// goes: the other pages stay bare, and a spec file's document is the one it booted with once the page is left
// (ADR-0056).
import mantineStyles from '@mantine/core/styles.css?inline';
import gridStyles from '../../../../src/npm/mantine/styles.css?inline';
import chartStyles from '../../../../src/npm/chart-mantine/styles.css?inline';
import { DemoNav } from '../demo-nav';
import { curveBridge, curveColumns, curveGroups, type CurveRow } from '../dock-curves';

/**
 * The page /mantine-chart, React's alone (ADR-0258, ADR-0281, ADR-0390): the curve history of /chart-bridge in
 * @ex-grid/mantine's paper, inside @ex-grid/chart-mantine's Chart Dock. The grid's context menu opens each chart made
 * from the selection as a Mantine tab beside the grid, its toolbar and context menu in Mantine's components, and the
 * theme's primary leads its series in both schemes. The provider keeps its scheme and variables on this page's own
 * element, not on the document.
 */
export function MantineChartDemo(): ReactElement {
    const [scope, setScope] = useState<HTMLDivElement | null>(null);
    const [dark, setDark] = useState(false);
    const dock = useChartDock();
    const grid = useRef<ExGridApi<CurveRow>>(null);
    const [{ source, bridge }] = useState(() => curveBridge(dock.bridgeOptions, grid));
    const rootOf = useCallback((): HTMLElement => {
        if (scope === null) throw new Error('Internal: the provider is drawn only inside its element.');
        return scope;
    }, [scope]);
    return (
        <>
            <style>{mantineStyles}{gridStyles}{chartStyles}</style>
            <DemoNav route="mantine-chart" />
            <h1>@ex-grid/chart-mantine — the Chart Dock, under Mantine&apos;s theme</h1>
            <div className="demo-toolbar">
                <button id="toggle-dark" type="button" onClick={() => { setDark(on => !on); }}>Toggle dark mode</button>
            </div>
            <p className="demo-status"><span id="dark-status">Dark: {dark ? 'True' : 'False'}</span></p>
            <div ref={setScope} className="demo-mantine">
                {scope === null ? null : (
                    <MantineProvider
                        forceColorScheme={dark ? 'dark' : 'light'}
                        getRootElement={rootOf}
                        cssVariablesSelector=".demo-mantine"
                        deduplicateCssVariables={false}>
                        <MantineChartDock dock={dock} bridge={bridge} chartWidth={520} chartHeight={260}>
                            <MantineExGrid
                                ref={grid}
                                withBorder
                                columns={curveColumns}
                                source={source}
                                headerGroups={curveGroups}
                                contextCommands={bridge.commands}
                                commandLabel={gridChartCommandLabel}
                                pinnedColumnCount={1}
                                viewportHeight={260}
                                viewportWidth={480}
                                temporal={Temporal} />
                        </MantineChartDock>
                    </MantineProvider>
                )}
            </div>
        </>
    );
}
