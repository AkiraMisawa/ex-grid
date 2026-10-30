import { useCallback, useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import type { GridColumn } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { tradeColumns, tradeWindow, type DemoTrade } from '../demo-data';
import { useQuery } from '../router';

// Held once, never rebuilt per render (ADR-0003): DemoData.Window(500, seed: 3), as the Blazor page holds it.
const rows = tradeWindow(500, 3);

/** One grid over the page's rows that stretches on both axes (ADR-0028). */
function Stretched(): ReactElement {
    return <ExGrid columns={tradeColumns} rows={rows} rowHeight={24} viewportHeight="stretch" viewportWidth="stretch"
        temporal={Temporal} />;
}

// The window-box grid's columns, as the Blazor page's: Trader cannot be sorted or filtered, so its menu is the commands
// alone, of which Hide is enabled (KB-30).
const windowColumns: readonly GridColumn<DemoTrade>[] = Object.freeze(tradeColumns.map(column =>
    (column.name === 'Trader' ? { ...column, queryable: false } : column)));

/** The grid whose box follows the window; the page hides nothing, and says which column a Hide asked for. */
function WindowBox(): ReactElement {
    const [hidden, setHidden] = useState('none');
    const onColumnHidden = useCallback((column: string) => { setHidden(column); }, []);
    return (
        <>
            <p><span id="hidden-status">Hidden: {hidden}</span></p>
            {/* The box follows the window's height, so a resize is a change of Viewport. */}
            <section id="window-box" style={{ height: 'calc(100vh - 220px)', minHeight: 0 }}>
                <ExGrid columns={windowColumns} rows={rows} rowHeight={24} viewportHeight="stretch" viewportWidth="stretch"
                    temporal={Temporal} onColumnHidden={onColumnHidden} />
            </section>
        </>
    );
}

/** The page /stretch of the Blazor hosts: a Viewport that takes its parent's box. The Material paper's two grids
 * are the Wrapper's (ADR-0030), and join with `@ex-grid/mui`. The Blazor page's sort line reads a grid that sorts
 * and does not filter, which a grid over `rows`, which does both, has no counterpart of. */
export function Stretch(): ReactElement {
    const parent = useQuery('parent');
    return (
        <>
            <DemoNav route="stretch" />
            <h1>Stretch — a Viewport that takes its parent&apos;s box</h1>
            <p>
                <code>viewportHeight=&quot;stretch&quot;</code> takes the parent&apos;s height instead of a number (ADR-0028).
                The parent&apos;s side of the contract is one sentence: give it a definite height. Each grid here fills a
                parent that does: a sized box, and the rest of a flex column under a toolbar.{' '}
                <code>?parent=window</code> shows one grid whose box follows the window&apos;s height, and{' '}
                <code>?parent=auto</code> one whose parent has no height.
            </p>
            {parent === 'auto' ? (
                <section id="auto-box">
                    <Stretched />
                </section>
            ) : parent === 'window' ? (
                <WindowBox />
            ) : (
                <>
                    <section id="fixed-box" style={{ height: 300 }}>
                        <Stretched />
                    </section>
                    <section id="flex-box" style={{ display: 'flex', flexDirection: 'column', height: 360, marginTop: 16 }}>
                        <div id="flex-toolbar" style={{ height: 60, flex: 'none' }}>A toolbar the grid sits under</div>
                        <Stretched />
                    </section>
                </>
            )}
        </>
    );
}
