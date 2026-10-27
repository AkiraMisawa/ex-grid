import { useCallback, useMemo, useState, type ReactElement } from 'react';
import { MantineProvider } from '@mantine/core';
import { Temporal } from 'temporal-polyfill';
import { GridSource, type GridColumn, type GridDensity, type GridEditIntent } from '@ex-grid/core';
import { MantineExGrid } from '@ex-grid/mantine';
// Mantine's styles and the Wrapper's, as text this page writes into a <style> of its own and takes away when it
// goes: the other pages, the bare grid's fixtures, stay bare, and a spec file's document is the one it booted
// with once the page is left (ADR-0056).
import mantineStyles from '@mantine/core/styles.css?inline';
import wrapperStyles from '../../../../src/npm/mantine/styles.css?inline';
import { DemoNav } from '../demo-nav';
import { tradeWindow, type DemoTrade } from '../demo-data';

const rows = tradeWindow(200, 11);

// Held once, never rebuilt per render (ADR-0003). Book offers the filter panel; Notional edits.
const columns: readonly GridColumn<DemoTrade>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (r: DemoTrade) => r.book, width: 120, filterUi: 'both' },
    { name: 'Trader', type: 'text', value: (r: DemoTrade) => r.trader, width: 120 },
    { name: 'Notional', type: 'number', value: (r: DemoTrade) => r.notional, width: 140, editable: true },
    { name: 'TradeDate', type: 'date', value: (r: DemoTrade) => r.tradeDate, header: 'Trade date', width: 140 },
]);

/** The page /mantine, React's alone: ExGrid inside @ex-grid/mantine's Paper under a MantineProvider, its Chrome
 * Mantine's components, and the buttons layer 3 drives (ADR-0390 section 7): the scheme, the Density, the square
 * corners. The provider keeps its scheme and its variables on this page's own element, not on the document. */
export function MantineDemo(): ReactElement {
    const [scope, setScope] = useState<HTMLDivElement | null>(null);
    const [dark, setDark] = useState(false);
    const [density, setDensity] = useState<GridDensity>('compact');
    const [square, setSquare] = useState(false);
    const [edited, setEdited] = useState('none');
    const source = useMemo(() => GridSource.from(rows), []);
    const rootOf = useCallback((): HTMLElement => {
        if (scope === null) throw new Error('Internal: the provider is drawn only inside its element.');
        return scope;
    }, [scope]);
    const onEdit = useCallback((intent: GridEditIntent<DemoTrade>) => {
        setEdited(`${intent.column} = ${intent.value}`);
    }, []);
    return (
        <>
            <style>{mantineStyles}{wrapperStyles}</style>
            <DemoNav route="mantine" />
            <h1>@ex-grid/mantine — the Wrapper, under Mantine&apos;s theme</h1>
            <div className="demo-toolbar">
                <button id="toggle-dark" type="button" onClick={() => { setDark(on => !on); }}>Toggle dark mode</button>
                <button id="toggle-dense" type="button" onClick={() => { setDensity(at => (at === 'compact' ? 'standard' : 'compact')); }}>Toggle density</button>
                <button id="toggle-square" type="button" onClick={() => { setSquare(on => !on); }}>Toggle square</button>
            </div>
            <p className="demo-status">
                <span id="edit-status">Edited: {edited}</span>
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
                        <MantineExGrid
                            className="demo-paper-a"
                            withBorder
                            shadow="sm"
                            striped
                            highlightOnHover
                            {...(square ? { radius: 0 } : {})}
                            density={density}
                            columns={columns}
                            source={source}
                            viewportHeight={240}
                            viewportWidth={560}
                            highlightFocusRow
                            temporal={Temporal}
                            onEdit={onEdit} />
                    </MantineProvider>
                )}
            </div>
        </>
    );
}
