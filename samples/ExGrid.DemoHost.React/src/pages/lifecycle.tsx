import { useState, type ReactElement } from 'react';
import { Temporal } from 'temporal-polyfill';
import { GridSource, type GridSource as Source } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { tradeColumns, tradeWindow, type DemoTrade } from '../demo-data';

// Held once, never rebuilt per render (ADR-0003): DemoData.Window(1_000, seed: 7), as the Blazor page holds it.
const rows = tradeWindow(1_000, 7);

/** What the page holds: the source of the grid on screen, if one is, and how many mounts there have been. */
interface Mounting {
    readonly source: Source<DemoTrade> | undefined;
    readonly mounts: number;
}

/** The page /lifecycle of the Blazor hosts: a grid that comes and goes, and takes back all it attached (ADR-0018,
 * ADR-0021). */
export function Lifecycle(): ReactElement {
    const [{ source, mounts }, setMounting] = useState<Mounting>({ source: undefined, mounts: 0 });
    // A fresh source per mount, as a page that owns its grid would make one: the source's binding to the grid is part
    // of what a dispose has to release.
    const toggle = (): void => {
        setMounting(before => (before.source === undefined
            ? { source: GridSource.from(rows), mounts: before.mounts + 1 }
            : { source: undefined, mounts: before.mounts }));
    };
    const mounted = source !== undefined;
    return (
        <>
            <DemoNav route="lifecycle" />
            <h1>Lifecycle — mount it, dispose it, and nothing stays behind</h1>
            <p>
                A grid that comes and goes. Each mount attaches the grid&apos;s listeners on its own root — the
                capture-phase keys, the pointer, the clipboard — and its observers, and binds an in-memory source. Each
                dispose has to take all of it back (ADR-0018, ADR-0021).
            </p>
            <div className="demo-toolbar">
                <button id="toggle-grid" type="button" onClick={toggle}>{mounted ? 'Dispose the grid' : 'Mount the grid'}</button>
            </div>
            <p className="demo-status">
                <span id="lifecycle-status">Mounted: {mounted ? 'yes' : 'no'}, {mounts} mounts so far.</span>
            </p>
            {mounted && (
                <ExGrid columns={tradeColumns} source={source} rowHeight={24} viewportHeight={300} highlightHoverRow
                    temporal={Temporal} />
            )}
        </>
    );
}
