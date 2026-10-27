import { useState, type CSSProperties, type ReactElement } from 'react';
import { ExGrid } from '@ex-grid/react';
import { columns, quoteBoard, useQuoteFeed } from './Quotes';

// Made once, outside the component: a new source or column list on every render would render every row again.
const { source, cellChangedAt, tick } = quoteBoard();

// The bare grid paints no colour for a tone: your theme says what a gain and a loss look like.
const tones: CSSProperties & Record<'--ex-tone-positive-color' | '--ex-tone-negative-color', string> = {
    '--ex-tone-positive-color': 'light-dark(#1a7f37, #4ac26b)',
    '--ex-tone-negative-color': 'light-dark(#cf222e, #ff7b72)',
};

export default function LiveQuotes(): ReactElement {
    const [paused, setPaused] = useState(false);
    // Stands in for a price feed.
    useQuoteFeed(tick, !paused);
    return (
        <>
            <div style={tones}>
                <ExGrid source={source} columns={columns} cellChangedAt={cellChangedAt} viewportHeight={300} viewportWidth="stretch" />
            </div>
            <div style={{ marginTop: 12 }}>
                <button type="button" onClick={() => { setPaused(p => !p); }}>{paused ? 'Resume' : 'Pause'}</button>
            </div>
        </>
    );
}
