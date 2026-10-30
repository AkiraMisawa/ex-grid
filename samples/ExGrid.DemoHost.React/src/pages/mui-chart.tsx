import { useMemo, useRef, useState, type ReactElement } from 'react';
import createCache, { type EmotionCache } from '@emotion/cache';
import { CacheProvider } from '@emotion/react';
import { ThemeProvider, createTheme, useColorScheme } from '@mui/material/styles';
import { Temporal } from 'temporal-polyfill';
import { gridChartCommandLabel } from '@ex-grid/chart';
import { useChartDock } from '@ex-grid/chart-react';
import { MuiChartDock } from '@ex-grid/chart-mui';
import type { ExGridApi } from '@ex-grid/react';
import { MuiExGrid } from '@ex-grid/mui';
// The Wrappers' stylesheets, as text this page writes into a <style> of its own and takes away when it goes: the
// other pages stay bare, and a spec file's document is the one it booted with once the page is left (ADR-0056).
import gridStyles from '../../../../packages/mui/styles.css?inline';
import chartStyles from '../../../../packages/chart-mui/styles.css?inline';
import { DemoNav } from '../demo-nav';
import { curveBridge, curveColumns, curveGroups, type CurveRow } from '../dock-curves';

// The theme a Consumer makes for the Wrappers: CSS variables on, both schemes, kept on this page's own element.
const theme = createTheme({
    cssVariables: { rootSelector: '.demo-mui', colorSchemeSelector: '.demo-mui-%s' },
    colorSchemes: { light: true, dark: true },
});

/** The page's button and the dock, inside the provider, where the scheme is set. */
function MuiChartPage(): ReactElement {
    const { mode, setMode } = useColorScheme();
    const dark = mode === 'dark';
    const dock = useChartDock();
    const grid = useRef<ExGridApi<CurveRow>>(null);
    const [{ source, bridge }] = useState(() => curveBridge(dock.bridgeOptions, grid));
    return (
        <>
            <div className="demo-toolbar">
                <button id="toggle-dark" type="button" onClick={() => { setMode(dark ? 'light' : 'dark'); }}>Toggle dark mode</button>
            </div>
            <p className="demo-status"><span id="dark-status">Dark: {dark ? 'True' : 'False'}</span></p>
            <MuiChartDock dock={dock} bridge={bridge} chartWidth={520} chartHeight={260}>
                <MuiExGrid
                    ref={grid}
                    columns={curveColumns}
                    source={source}
                    headerGroups={curveGroups}
                    contextCommands={bridge.commands}
                    commandLabel={gridChartCommandLabel}
                    pinnedColumnCount={1}
                    viewportHeight={260}
                    viewportWidth={480}
                    temporal={Temporal} />
            </MuiChartDock>
        </>
    );
}

/** The page /mui-chart, React's alone (ADR-0258, ADR-0281, ADR-0390): the curve history of /chart-bridge in
 * @ex-grid/mui's paper, inside @ex-grid/chart-mui's Chart Dock, under a ThemeProvider whose theme has CSS variables.
 * Emotion writes MUI's styles, and the provider its variables, into this page's own element, and its scheme's class
 * goes on that element, so leaving the page leaves the document as it was. */
export function MuiChartDemo(): ReactElement {
    const [scope, setScope] = useState<HTMLDivElement | null>(null);
    const cache = useMemo<EmotionCache | null>(() => (scope === null ? null : createCache({ key: 'demo-mui', container: scope })), [scope]);
    return (
        <>
            <style>{gridStyles}{chartStyles}</style>
            <DemoNav route="mui-chart" />
            <h1>@ex-grid/chart-mui — the Chart Dock, under MUI&apos;s theme</h1>
            <div ref={setScope} className="demo-mui">
                {scope === null || cache === null ? null : (
                    <CacheProvider value={cache}>
                        <ThemeProvider theme={theme} colorSchemeNode={scope} storageManager={null} defaultMode="light">
                            <MuiChartPage />
                        </ThemeProvider>
                    </CacheProvider>
                )}
            </div>
        </>
    );
}
