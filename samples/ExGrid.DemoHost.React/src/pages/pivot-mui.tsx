import { useMemo, useState, type ReactElement } from 'react';
import createCache, { type EmotionCache } from '@emotion/cache';
import { CacheProvider } from '@emotion/react';
import { ThemeProvider, createTheme, useColorScheme } from '@mui/material/styles';
import type { GridDensity } from '@ex-grid/core';
import { RecordPivotSource, type PivotLayout } from '@ex-grid/pivot-engine';
import { MuiPivot } from '@ex-grid/pivot-mui';
// The Wrappers' stylesheets, as text this page writes into a <style> of its own and takes away when it goes: the
// other pages stay bare, and a spec file's document is the one it booted with once the page is left (ADR-0056).
import gridStyles from '../../../../packages/mui/styles.css?inline';
import pivotStyles from '../../../../packages/pivot-mui/styles.css?inline';
import { DemoNav } from '../demo-nav';
import { demoPivotFields, demoPivotFirstLayout, demoPivotTrades } from '../pivot-data';
import { useQuery } from '../router';

// The theme a Consumer makes for the Wrappers: CSS variables on, both schemes, kept on this page's own element.
const theme = createTheme({
    cssVariables: { rootSelector: '.demo-mui', colorSchemeSelector: '.demo-mui-%s' },
    colorSchemes: { light: true, dark: true },
});

/** The page's buttons and the pivot, inside the provider, where the scheme is set. */
function MuiPivotPage(): ReactElement {
    const detailsAsked = useQuery('details');
    const { mode, setMode } = useColorScheme();
    const dark = mode === 'dark';
    const [density, setDensity] = useState<GridDensity>('compact');
    const [layout, setLayout] = useState<PivotLayout>(demoPivotFirstLayout);
    const [showFieldList, setShowFieldList] = useState(true);
    const source = useMemo(() => new RecordPivotSource(demoPivotTrades(), demoPivotFields), []);
    return (
        <>
            <div className="demo-toolbar">
                <button id="toggle-dark" type="button" onClick={() => { setMode(dark ? 'light' : 'dark'); }}>Toggle dark mode</button>
                <button id="toggle-dense" type="button" onClick={() => { setDensity(at => (at === 'compact' ? 'standard' : 'compact')); }}>Toggle density</button>
            </div>
            <p className="demo-status">
                <span id="dark-status">Dark: {dark ? 'True' : 'False'}</span>
                <span id="density-status">Density: {density}</span>
            </p>
            <MuiPivot
                density={density}
                source={source}
                layout={layout}
                onLayoutChanged={({ layout: next }) => { setLayout(next); }}
                showFieldList={showFieldList}
                onShowFieldListChanged={({ shown }) => { setShowFieldList(shown); }}
                detailsView={detailsAsked === 'dialog' ? 'dialog' : 'tab'}
                locale="en-US"
                viewportHeight={460} />
        </>
    );
}

/** The page /pivot-mui, React's alone: the trades of /pivot in @ex-grid/pivot-mui's Paper under a ThemeProvider whose
 * theme has CSS variables, every Chrome slot of the pivot and of its report grid MUI's components, and the buttons
 * that switch the scheme and the Density (ADR-0390). ?details=dialog opens Show Details in the dialog, whose content
 * is the Wrapper's too. Emotion writes MUI's styles, and the provider its variables, into this page's own element, and
 * its scheme's class goes on that element, so leaving the page leaves the document as it was. */
export function PivotMuiDemo(): ReactElement {
    const [scope, setScope] = useState<HTMLDivElement | null>(null);
    const cache = useMemo<EmotionCache | null>(() => (scope === null ? null : createCache({ key: 'demo-mui', container: scope })), [scope]);
    return (
        <>
            <style>{gridStyles}{pivotStyles}</style>
            <DemoNav route="pivot-mui" />
            <h1>@ex-grid/pivot-mui — ExPivot under MUI&apos;s theme</h1>
            <p>
                The trades of the Pivot page, drawn by <code>MuiPivot</code>: the Field List, the Pivot Toolbar, its menus
                and panels, the report filters, the Stale Report&apos;s notice and Show Details&apos; tabs and dialog are
                MUI&apos;s components, and the report grid&apos;s Chrome is @ex-grid/mui&apos;s. What each part shows and
                does is the pivot&apos;s; the Wrapper draws it and calls back.
            </p>
            <div ref={setScope} className="demo-mui">
                {scope === null || cache === null ? null : (
                    <CacheProvider value={cache}>
                        <ThemeProvider theme={theme} colorSchemeNode={scope} storageManager={null} defaultMode="light">
                            <MuiPivotPage />
                        </ThemeProvider>
                    </CacheProvider>
                )}
            </div>
        </>
    );
}
