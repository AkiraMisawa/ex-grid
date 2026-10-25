// What the browser tells the grid (ADR-0350 section 6): the grid is told when something the browser
// knows has changed, and never measures it on the path to a paint. Each report is the C# core's
// allowlisted one, kept to the same shape: a ResizeObserver for the Scrollbar Gutter and the Viewport's
// own size (ADR-0013, ADR-0028), one for the Layout Ceiling (ADR-0053), and a media query on the current
// resolution for the Device Pixel (ADR-0090), and the clipboard's own change for the Copied Range
// (ADR-0170, ADR-0021's clipboard entry). A layer-2 test hands in its own, and tells by hand.

import type { CopyFlavours } from '../clipboard/clipboard-data';
import type { GridClock } from '../source/grid-clock';
import { systemClock } from '../source/grid-clock';

/** The Scrollbar Gutter and the scroller's content box, as one report. */
export interface ViewportReport {
    /** What a vertical scrollbar takes out of the scroller's width. */
    readonly gutterWidthPx: number;
    /** What a horizontal scrollbar takes out of its height. */
    readonly gutterHeightPx: number;
    /** The content box's width: under a Stretch width, the Viewport's width. */
    readonly contentWidthPx: number;
    /** The content box's height: under a Stretch height, the Viewport's height. */
    readonly contentHeightPx: number;
}

/** Where the grid hears what the browser knows. Each `observe…` returns what stops hearing it. */
export interface BrowserReports {
    /** Hears the scroller's gutter and content box: once when it is first laid out, then on each change. */
    observeViewport(scroller: HTMLElement, tell: (report: ViewportReport) => void): () => void;
    /** Hears the height the probe's tallest element is laid out at: the Layout Ceiling. */
    observeCeiling(probe: HTMLElement, tell: (ceilingPx: number) => void): () => void;
    /** Hears the Device Pixel's size: at once, then whenever the scale or the zoom moves it. */
    observeDevicePixel(tell: (devicePixelRatio: number) => void): () => void;
    /** The clock the settle delay after a fling runs on (ADR-0004), and the announcement's (ADR-0033). */
    readonly clock: GridClock;
    /** Calls `callback` before the next frame of `element`'s window is painted; the answer cancels the call
     * if it has not run. A reveal's repaint moves the offset in the frames after the reveal's (ADR-0012). */
    nextFrame(element: HTMLElement, callback: () => void): () => void;
    /** Whether Meta is this platform's primary modifier, as Command is on an Apple platform: only the
     * browser can answer it (ADR-0012). */
    metaIsPrimary(): boolean;
    /**
     * Writes a copy to the clipboard by the asynchronous route (ADR-0005), started now, inside the gesture,
     * while its flavours are still being gathered: Chrome takes a promise as a clipboard item's value. Answers
     * whether anything was written, `false` when the flavours came to nothing, and the clipboard holds what
     * it held; a write the browser refuses rejects with the browser's error.
     */
    writeClipboard(element: HTMLElement, flavours: Promise<CopyFlavours | undefined>): Promise<boolean>;
    /** Hears each change of the clipboard, by any write, the grid's own included (ADR-0170): `undefined`
     * where the browser cannot say the clipboard changed, and the grid then outlines no copy. */
    observeClipboard(element: HTMLElement, tell: () => void): (() => void) | undefined;
}

/**
 * Tells from a task of its own, queued once however many notifications come before it runs and carrying
 * the latest values: a report told from inside a ResizeObserver's callback repaints in the observation that
 * caused it, and the browser reports the loop on `window` (CON-4).
 */
function toldAfterTheObservation(tell: () => void): () => void {
    let queued = false;
    return () => {
        if (queued) return;
        queued = true;
        setTimeout(() => {
            queued = false;
            tell();
        }, 0);
    };
}

/** The browser's own reports. */
export const browserReports: BrowserReports = Object.freeze({
    observeViewport(scroller: HTMLElement, tell: (report: ViewportReport) => void): () => void {
        let last: ViewportReport | undefined;
        let latest: ViewportReport | undefined;
        const told = toldAfterTheObservation(() => {
            if (latest !== undefined) tell(latest);
        });
        const observer = new ResizeObserver(entries => {
            const entry = entries.at(-1);
            const border = entry?.borderBoxSize[0];
            const content = entry?.contentBoxSize[0];
            if (border === undefined || content === undefined) return;
            // `.ex-scroller` has no border and no padding, so what the border box has and the content box
            // does not is the scrollbar (ADR-0013).
            const report = Object.freeze({
                gutterWidthPx: Math.max(0, border.inlineSize - content.inlineSize),
                gutterHeightPx: Math.max(0, border.blockSize - content.blockSize),
                contentWidthPx: content.inlineSize,
                contentHeightPx: content.blockSize,
            });
            if (last !== undefined && last.gutterWidthPx === report.gutterWidthPx && last.gutterHeightPx === report.gutterHeightPx
                && last.contentWidthPx === report.contentWidthPx && last.contentHeightPx === report.contentHeightPx) {
                return;
            }
            last = report;
            latest = report;
            told();
        });
        observer.observe(scroller, { box: 'content-box' });
        return () => { observer.disconnect(); };
    },
    observeCeiling(probe: HTMLElement, tell: (ceilingPx: number) => void): () => void {
        let ceiling = -1;
        const told = toldAfterTheObservation(() => { tell(ceiling); });
        const observer = new ResizeObserver(entries => {
            const size = entries.at(-1)?.borderBoxSize[0];
            if (size === undefined || size.blockSize === ceiling) return;
            ceiling = size.blockSize;
            told();
        });
        observer.observe(probe);
        return () => { observer.disconnect(); };
    },
    observeDevicePixel(tell: (devicePixelRatio: number) => void): () => void {
        let query: MediaQueryList | undefined;
        let told = -1;
        const arm = (): void => {
            query?.removeEventListener('change', arm);
            const ratio = window.devicePixelRatio;
            query = window.matchMedia(`(resolution: ${ratio}dppx)`);
            query.addEventListener('change', arm);
            if (ratio === told) return;
            told = ratio;
            tell(ratio);
        };
        arm();
        return () => { query?.removeEventListener('change', arm); };
    },
    clock: systemClock,
    writeClipboard(element: HTMLElement, flavours: Promise<CopyFlavours | undefined>): Promise<boolean> {
        const view = element.ownerDocument.defaultView;
        if (view === null) throw new Error('The grid\'s element is in a document with no window: it has no clipboard.');
        const flavour = (type: string, pick: (copy: CopyFlavours) => string): Promise<Blob> => flavours.then(copy => {
            // Nothing to write rejects the item, and with it the whole write: never a part of the copy.
            if (copy === undefined) throw new Error('The copy was refused.');
            return new view.Blob([pick(copy)], { type });
        });
        const item = new view.ClipboardItem({
            'text/plain': flavour('text/plain', copy => copy.text),
            'text/html': flavour('text/html', copy => copy.html),
        });
        // A copy refused while it was gathered wrote nothing, which the grid has already said; any other
        // rejection is the browser's, and is the caller's to say.
        // oxlint-disable-next-line ex-grid/no-catch -- a copy refused while it was gathered answers false; anything else is rethrown
        return view.navigator.clipboard.write([item]).then(() => true, async (error: unknown) => {
            if (await flavours === undefined) return false;
            throw error;
        });
    },
    observeClipboard(element: HTMLElement, tell: () => void): (() => void) | undefined {
        const view = element.ownerDocument.defaultView;
        if (view === null) throw new Error('The grid\'s element is in a document with no window: it has no clipboard.');
        // A browser before Chrome 143 has no clipboardchange, and an insecure context no clipboard at all.
        const clipboard = view.navigator.clipboard as Clipboard | undefined;
        if (typeof Reflect.get(view, 'ClipboardChangeEvent') !== 'function' || typeof clipboard?.addEventListener !== 'function') {
            return undefined;
        }
        const heard = (): void => { tell(); };
        clipboard.addEventListener('clipboardchange', heard);
        return () => { clipboard.removeEventListener('clipboardchange', heard); };
    },
    nextFrame(element: HTMLElement, callback: () => void): () => void {
        const view = element.ownerDocument.defaultView;
        if (view === null) throw new Error('The grid\'s element is in a document with no window: nothing paints it.');
        const frame = view.requestAnimationFrame(callback);
        return () => { view.cancelAnimationFrame(frame); };
    },
    metaIsPrimary(): boolean {
        // `userAgentData` is Chromium's; the others still answer `platform`.
        const { userAgentData } = navigator as Navigator & { readonly userAgentData?: { readonly platform: string } };
        return /mac|iphone|ipad|ipod/i.test(userAgentData === undefined ? navigator.platform : userAgentData.platform);
    },
});
