// What the browser tells the grid (ADR-0350 section 6): the grid is told when something the browser
// knows has changed, and never measures it on the path to a paint. Each report is the C# core's
// allowlisted one, kept to the same shape: a ResizeObserver for the Scrollbar Gutter and the Viewport's
// own size (ADR-0013, ADR-0028), one for the Layout Ceiling (ADR-0053), and a media query on the current
// resolution for the Device Pixel (ADR-0090). A layer-2 test hands in its own, and tells by hand.

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
    /** The clock the settle delay after a fling runs on (ADR-0004). */
    readonly clock: GridClock;
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
});
