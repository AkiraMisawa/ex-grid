import { useState, type ReactElement } from 'react';
import { createPortal } from 'react-dom';
import { GridSource, type GridColumn } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { sampleTrades, type Trade } from '../Grid/Trades';

// Made once, outside the component, as for any grid.
const source = GridSource.from(sampleTrades());
const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: t => t.book },
    { name: 'Instrument', type: 'text', value: t => t.instrument },
    { name: 'Notional', type: 'number', value: t => t.notional },
    { name: 'Price', type: 'number', value: t => t.price },
];

/** A second window that reads the stylesheets this one reads. */
function openWindow(): Window {
    const opened = window.open('', '', 'popup,width=760,height=440');
    if (opened === null) throw new Error('The browser refused the window: the page is not allowed its pop-ups.');
    // The new window's document starts empty, and a grid reads its stylesheet from the document it is in.
    for (const sheet of document.querySelectorAll('style, link[rel="stylesheet"]')) opened.document.head.append(sheet.cloneNode(true));
    return opened;
}

export default function PoppedOutGrid(): ReactElement {
    const [popped, setPopped] = useState<Window>();
    const popOut = (): void => {
        const opened = openWindow();
        // Closed by the user, the window hands the grid back to the page.
        opened.addEventListener('pagehide', () => { setPopped(undefined); });
        setPopped(opened);
    };
    // The key names the window, so the grid is created again in the window it is drawn in. A grid whose element
    // was only moved to the other document would keep the first window's Device Pixel.
    const grid = <ExGrid key={popped === undefined ? 'page' : 'popped'} source={source} columns={columns} viewportHeight={320} />;
    return (
        <>
            <button type="button" onClick={popOut} disabled={popped !== undefined}>Pop out</button>
            {popped === undefined ? grid : createPortal(grid, popped.document.body)}
        </>
    );
}
