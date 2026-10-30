import { useCallback, useMemo, useState, type ReactElement } from 'react';
import { MantineProvider } from '@mantine/core';
import type { GridDensity } from '@ex-grid/core';
import { RecordPivotSource, type PivotLayout } from '@ex-grid/pivot-engine';
import { MantinePivot } from '@ex-grid/pivot-mantine';
// Mantine's styles and the Wrappers', as text this page writes into a <style> of its own and takes away when it goes:
// the other pages stay bare, and a spec file's document is the one it booted with once the page is left (ADR-0056).
import mantineStyles from '@mantine/core/styles.css?inline';
import gridStyles from '../../../../packages/mantine/styles.css?inline';
import pivotStyles from '../../../../packages/pivot-mantine/styles.css?inline';
import { DemoNav } from '../demo-nav';
import { demoPivotFields, demoPivotFirstLayout, demoPivotTrades } from '../pivot-data';
import { useQuery } from '../router';

/** The page /pivot-mantine, React's alone: the trades of /pivot in @ex-grid/pivot-mantine's Paper under a
 * MantineProvider, every Chrome slot of the pivot and of its report grid Mantine's components, and the buttons that
 * switch the scheme and the Density (ADR-0390). ?details=dialog opens Show Details in the dialog, whose content is
 * the Wrapper's too. The provider keeps its scheme and its variables on this page's own element. */
export function PivotMantineDemo(): ReactElement {
    const detailsAsked = useQuery('details');
    const [scope, setScope] = useState<HTMLDivElement | null>(null);
    const [dark, setDark] = useState(false);
    const [density, setDensity] = useState<GridDensity>('compact');
    const [layout, setLayout] = useState<PivotLayout>(demoPivotFirstLayout);
    const [showFieldList, setShowFieldList] = useState(true);
    const source = useMemo(() => new RecordPivotSource(demoPivotTrades(), demoPivotFields), []);
    const rootOf = useCallback((): HTMLElement => {
        if (scope === null) throw new Error('Internal: the provider is drawn only inside its element.');
        return scope;
    }, [scope]);
    return (
        <>
            <style>{mantineStyles}{gridStyles}{pivotStyles}</style>
            <DemoNav route="pivot-mantine" />
            <h1>@ex-grid/pivot-mantine — ExPivot under Mantine&apos;s theme</h1>
            <p>
                The trades of the Pivot page, drawn by <code>MantinePivot</code>: the Field List, the Pivot Toolbar, its
                menus and panels, the report filters, the Stale Report&apos;s notice and Show Details&apos; tabs and
                dialog are Mantine&apos;s components, and the report grid&apos;s Chrome is @ex-grid/mantine&apos;s. What
                each part shows and does is the pivot&apos;s; the Wrapper draws it and calls back.
            </p>
            <div className="demo-toolbar">
                <button id="toggle-dark" type="button" onClick={() => { setDark(on => !on); }}>Toggle dark mode</button>
                <button id="toggle-dense" type="button" onClick={() => { setDensity(at => (at === 'compact' ? 'standard' : 'compact')); }}>Toggle density</button>
            </div>
            <p className="demo-status">
                <span id="dark-status">Dark: {dark ? 'True' : 'False'}</span>
                <span id="density-status">Density: {density}</span>
            </p>
            <div ref={setScope} className="demo-mantine">
                {scope === null ? null : (
                    <MantineProvider
                        forceColorScheme={dark ? 'dark' : 'light'}
                        getRootElement={rootOf}
                        cssVariablesSelector=".demo-mantine"
                        deduplicateCssVariables={false}>
                        <MantinePivot
                            withBorder
                            shadow="sm"
                            density={density}
                            source={source}
                            layout={layout}
                            onLayoutChanged={({ layout: next }) => { setLayout(next); }}
                            showFieldList={showFieldList}
                            onShowFieldListChanged={({ shown }) => { setShowFieldList(shown); }}
                            detailsView={detailsAsked === 'dialog' ? 'dialog' : 'tab'}
                            locale="en-US"
                            viewportHeight={460} />
                    </MantineProvider>
                )}
            </div>
        </>
    );
}
