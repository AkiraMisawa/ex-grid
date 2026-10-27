import { useCallback, useMemo, useState, type ReactElement } from 'react';
import createCache, { type EmotionCache } from '@emotion/cache';
import { CacheProvider } from '@emotion/react';
import { ThemeProvider, createTheme, useColorScheme } from '@mui/material/styles';
import { Temporal } from 'temporal-polyfill';
import { EditorCompletion, GridSource, type GridColumn, type GridDensity, type GridEditIntent } from '@ex-grid/core';
import { MuiExGrid } from '@ex-grid/mui';
// The Wrapper's stylesheet, as text this page writes into a <style> of its own and takes away when it goes: the other
// pages, the bare grid's fixtures, stay bare, and a spec file's document is the one it booted with once the page is
// left (ADR-0056).
import wrapperStyles from '../../../../src/npm/mui/styles.css?inline';
import { DemoNav } from '../demo-nav';
import { tradeWindow, type DemoTrade } from '../demo-data';

// The theme is made with CSS variables, so the provider takes the props that go with them (MUI's own instruction).
declare module '@mui/material/styles' {
    interface CssThemeVariables {
        enabled: true;
    }
}

const rows = tradeWindow(200, 11);

// The day alone: a trade date's time is always midnight.
function isoDate(value: unknown): string {
    if (!(value instanceof Temporal.PlainDateTime)) throw new TypeError('A trade date is a PlainDateTime.');
    return value.toPlainDate().toString();
}

// Held once, never rebuilt per render (ADR-0003). Book offers the filter panel; Trader edits, with completion.
const columns: readonly GridColumn<DemoTrade>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (r: DemoTrade) => r.book, width: 120, filterUi: 'both' },
    { name: 'Trader', type: 'text', value: (r: DemoTrade) => r.trader, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: (r: DemoTrade) => r.notional, width: 140 },
    { name: 'TradeDate', type: 'date', value: (r: DemoTrade) => r.tradeDate, header: 'Trade date', width: 140, format: isoDate },
]);

const traders = [...new Set(rows.map(row => row.trader))].toSorted();

// The trader names that begin with what is typed, the whole text replaced by the one accepted (ADR-0051): the
// completion list the Wrapper draws in MUI's theme. Trader is the one column that edits, so every edit is a trader's
// name.
function completeTrader(text: string): EditorCompletion | undefined {
    const typed = text.trim().toLowerCase();
    if (typed.length === 0) return undefined;
    const candidates = traders.filter(name => name.toLowerCase().startsWith(typed))
        .map(name => ({ label: name, start: 0, length: text.length, insert: name }));
    return new EditorCompletion({ candidates });
}

// The theme a Consumer makes for the Wrapper: CSS variables on, both schemes. Its variables and its scheme's class are
// kept on this page's own element, not on the document.
const theme = createTheme({
    cssVariables: { rootSelector: '.demo-mui', colorSchemeSelector: '.demo-mui-%s' },
    colorSchemes: { light: true, dark: true },
});

/** The page's buttons and the grid, inside the provider, where the scheme is set. */
function MuiPage(): ReactElement {
    const { mode, setMode } = useColorScheme();
    const dark = mode === 'dark';
    const [density, setDensity] = useState<GridDensity>('compact');
    const [square, setSquare] = useState(false);
    const [edited, setEdited] = useState('none');
    const source = useMemo(() => GridSource.from(rows), []);
    const onEdit = useCallback((intent: GridEditIntent<DemoTrade>) => {
        setEdited(`${intent.column} = ${intent.value}`);
    }, []);
    return (
        <>
            <div className="demo-toolbar">
                <button id="toggle-dark" type="button" onClick={() => { setMode(dark ? 'light' : 'dark'); }}>Toggle dark mode</button>
                <button id="toggle-dense" type="button" onClick={() => { setDensity(at => (at === 'compact' ? 'standard' : 'compact')); }}>Toggle density</button>
                <button id="toggle-square" type="button" onClick={() => { setSquare(on => !on); }}>Toggle square</button>
            </div>
            <p className="demo-status">
                <span id="edit-status">Edited: {edited}</span>
                <span id="dark-status">Dark: {dark ? 'True' : 'False'}</span>
                <span id="density-status">Density: {density}</span>
            </p>
            <MuiExGrid
                className="demo-paper-a"
                square={square}
                density={density}
                columns={columns}
                source={source}
                viewportHeight={240}
                viewportWidth={560}
                stripeRows
                highlightHoverRow
                highlightFocusRow
                temporal={Temporal}
                completeEditorText={completeTrader}
                onEdit={onEdit} />
        </>
    );
}

/** The page /mui, React's alone: ExGrid inside @ex-grid/mui's Paper under a ThemeProvider whose theme has CSS
 * variables, its Chrome MUI's components, and the buttons layer 3 drives (ADR-0390 section 7): the scheme, the
 * Density, the square corners. Emotion writes MUI's styles, and the provider its variables, into this page's own
 * element, and its scheme's class goes on that element, so leaving the page leaves the document as it was. */
export function MuiDemo(): ReactElement {
    const [scope, setScope] = useState<HTMLDivElement | null>(null);
    const cache = useMemo<EmotionCache | null>(() => (scope === null ? null : createCache({ key: 'demo-mui', container: scope })), [scope]);
    return (
        <>
            <style>{wrapperStyles}</style>
            <DemoNav route="mui" />
            <h1>@ex-grid/mui — the Wrapper, under MUI&apos;s theme</h1>
            <div ref={setScope} className="demo-mui">
                {scope === null || cache === null ? null : (
                    <CacheProvider value={cache}>
                        <ThemeProvider theme={theme} colorSchemeNode={scope} storageManager={null} defaultMode="light">
                            <MuiPage />
                        </ThemeProvider>
                    </CacheProvider>
                )}
            </div>
        </>
    );
}
