import { useEffect, useState, type ReactElement } from 'react';
import { createPortal } from 'react-dom';
import { Temporal } from 'temporal-polyfill';
import { ExGrid } from '@ex-grid/react';
import { DemoNav } from '../demo-nav';
import { tradeColumns, tradeWindow } from '../demo-data';

// Held once, never rebuilt per render (ADR-0003).
const rows = tradeWindow(1_000, 7);

/** What the page holds: the window it opened, while that is open, and whether a grid is mounted in it. */
interface PoppedOut {
    readonly opened: Window | undefined;
    readonly mounted: boolean;
}

const closed: PoppedOut = Object.freeze({ opened: undefined, mounted: false });

/** The page /popout, which only this host has: a grid drawn in a second window this page opened, by this page's
 * script, as a docking layout pops a panel out. That window has a display scale and a zoom of its own, and the grid
 * is told its Device Pixel there, not here (ADR-0090's note of 2026-11-12). */
export function Popout(): ReactElement {
    const [{ opened, mounted }, setPoppedOut] = useState<PoppedOut>(closed);
    // The window goes with the page that opened it.
    useEffect(() => () => { opened?.close(); }, [opened]);

    const open = (): void => {
        const made = window.open('', 'ex-popout', 'popup,width=760,height=440');
        if (made === null) throw new Error('The browser refused the window: allow this page its pop-ups.');
        // The window's document starts empty, and a grid reads its stylesheet from the document it is in.
        for (const sheet of document.querySelectorAll('style, link[rel="stylesheet"]')) made.document.head.append(sheet.cloneNode(true));
        made.document.title = 'ExGrid in a window the page opened';
        made.addEventListener('pagehide', () => { setPoppedOut(closed); });
        setPoppedOut({ opened: made, mounted: false });
    };
    const toggle = (): void => { setPoppedOut(before => ({ opened: before.opened, mounted: !before.mounted })); };

    return (
        <>
            <DemoNav route="popout" />
            <h1>A grid in a window the page opened</h1>
            <p>
                Open a second window, then mount a grid in it. The grid is this page&apos;s component, drawn through a
                portal into the other window&apos;s document, so its script runs here and its element lives there. What
                the browser tells it — the Device Pixel above all, which follows the display that window is on and its
                own zoom — is that window&apos;s, never this one&apos;s (ADR-0090). Drag the window to a display of
                another scale, or zoom it, and its lines stay whole Device Pixels.
            </p>
            <div className="demo-toolbar">
                <button id="open-window" type="button" onClick={open} disabled={opened !== undefined}>Open the window</button>
                <button id="toggle-grid" type="button" onClick={toggle} disabled={opened === undefined}>
                    {mounted ? 'Dispose the grid' : 'Mount the grid'}
                </button>
            </div>
            <p className="demo-status">
                <span id="popout-status">Window: {opened === undefined ? 'closed' : 'open'}. Grid: {mounted ? 'mounted' : 'not mounted'}.</span>
            </p>
            {opened !== undefined && mounted && createPortal(
                <ExGrid columns={tradeColumns} rows={rows} rowHeight={24} viewportHeight={300} highlightHoverRow temporal={Temporal} />,
                opened.document.body,
            )}
        </>
    );
}
