// The grid in a DOM emulation (ADR-0350 section 13), as the layer-2 tests mount it: what the browser tells
// — the gutter, the ceiling, the Device Pixel, the frames — is told here by hand.
import { defined } from '../src/must';
import type { CopyFlavours } from '../src/clipboard/clipboard-data';
import type { BrowserReports, ViewportReport } from '../src/dom/browser-reports';
import type { GridColumn } from '../src/dom/grid-column';
import { GridView, type GridOptions } from '../src/dom/grid-view';
import { FakeClock } from './fake-clock';

export interface Trade {
    readonly book: string;
    readonly notional: number;
}

export const columns: readonly GridColumn<Trade>[] = [
    { name: 'Book', type: 'text', value: trade => trade.book, width: 120 },
    { name: 'Notional', type: 'number', value: trade => trade.notional, width: 100 },
];

export function trades(count: number): readonly Trade[] {
    return Array.from({ length: count }, (_, i) => ({ book: `Book-${i}`, notional: i * 1000 }));
}

/** The browser's reports, told by the test. */
export class Told implements BrowserReports {
    readonly clock = new FakeClock();
    #viewport: ((report: ViewportReport) => void) | undefined;
    #ceiling: ((px: number) => void) | undefined;

    observeViewport(_scroller: HTMLElement, tell: (report: ViewportReport) => void): () => void {
        this.#viewport = tell;
        return () => { this.#viewport = undefined; };
    }

    observeCeiling(_probe: HTMLElement, tell: (px: number) => void): () => void {
        this.#ceiling = tell;
        return () => { this.#ceiling = undefined; };
    }

    observeDevicePixel(tell: (ratio: number) => void): () => void {
        tell(1);
        return () => { /* told once: the emulation's resolution never moves */ };
    }

    viewport(report: Partial<ViewportReport> = {}): void {
        defined(this.#viewport, 'the viewport\'s listener')({
            gutterWidthPx: 0, gutterHeightPx: 0, contentWidthPx: 800, contentHeightPx: 480, ...report,
        });
    }

    ceiling(px = 33_554_428): void {
        defined(this.#ceiling, 'the ceiling\'s listener')(px);
    }

    #frames: (() => void)[] = [];
    #layouts: (() => void)[] = [];
    /** Whether the emulation holds what is written until `laidOut()`, as a browser lays it out in a later frame.
     * Otherwise nothing is laid out, and the grid is told so as it asks. */
    holdLayout = false;

    afterLayout(_element: HTMLElement, callback: () => void): () => void {
        if (!this.holdLayout) {
            callback();
            return () => { /* run already */ };
        }
        this.#layouts.push(callback);
        return () => { this.#layouts = this.#layouts.filter(layout => layout !== callback); };
    }

    /** Lays out what was held, telling the grid the boxes it changed are told. */
    laidOut(): void {
        const due = this.#layouts;
        this.#layouts = [];
        for (const callback of due) callback();
    }
    /** Whether the test's platform is an Apple one, where Meta is the primary modifier. */
    primaryIsMeta = false;

    metaIsPrimary(): boolean {
        return this.primaryIsMeta;
    }

    nextFrame(_element: HTMLElement, callback: () => void): () => void {
        this.#frames.push(callback);
        return () => { this.#frames = this.#frames.filter(frame => frame !== callback); };
    }

    /** Runs the frames asked for so far, as the browser runs them before its next paint. */
    frame(): void {
        const due = this.#frames;
        this.#frames = [];
        for (const callback of due) callback();
    }

    get framesAsked(): number {
        return this.#frames.length;
    }

    /** What the asynchronous copy route wrote, in order: a copy that came to nothing writes nothing. */
    readonly clipboard: CopyFlavours[] = [];
    /** Whether the browser refuses the next writes, as it does without permission. */
    clipboardRefused = false;
    /** The writes started and not yet finished. */
    writing = 0;

    #held: Promise<void> | undefined;

    /** Holds every write from now, its flavours in hand, until the function returned lets them be made. */
    holdWrites(): () => void {
        const release: (() => void)[] = [];
        this.#held = new Promise(resolve => { release.push(resolve); });
        return () => {
            this.#held = undefined;
            for (const resolve of release) resolve();
        };
    }

    async writeClipboard(_element: HTMLElement, flavours: Promise<CopyFlavours | undefined>): Promise<boolean> {
        this.writing += 1;
        const held = this.#held;
        try {
            const copy = await flavours;
            if (copy === undefined) return false;
            await held;
            if (this.clipboardRefused) throw new DOMException('Write permission denied.', 'NotAllowedError');
            this.clipboard.push(copy);
            return true;
        } finally {
            this.writing -= 1;
        }
    }

    /** Whether the browser can say the clipboard changed, as Chrome 143 and later can; set before mounting. */
    clipboardWatched = true;
    #clipboardChange: ((change: object) => void) | undefined;

    observeClipboard(_element: HTMLElement, tell: (change: object) => void): (() => void) | undefined {
        if (!this.clipboardWatched) return undefined;
        this.#clipboardChange = tell;
        return () => { this.#clipboardChange = undefined; };
    }

    /** The browser reports a change of the clipboard: a write by the grid's own copy, or by anyone else. Every
     * grid of the document hears the same change: pass it to each grid's reports to tell them all. */
    clipboardChanged(change: object = {}): void {
        defined(this.#clipboardChange, 'the clipboard\'s listener')(change);
    }

    get listening(): boolean {
        return this.#viewport !== undefined || this.#ceiling !== undefined || this.#clipboardChange !== undefined;
    }
}

/** Lets the pass a change asked for run. */
export async function painted(): Promise<void> {
    await Promise.resolve();
}

const mounted: { destroy(): void }[] = [];

/**
 * Destroys every grid mounted since the last call. The grids of a test file share its document, and with it the
 * clipboard's ledger (ADR-0170): a write one test counted and never heard would answer the next test's change.
 */
export function unmountAll(): void {
    for (const view of mounted.splice(0)) view.destroy();
}

/** Mounts a grid; `browser` sets what the browser can do before the grid attaches, and `parent` is what the grid's
 * root is placed in. */
export function mount<Row>(
    options: GridOptions<Row>, browser: Partial<Pick<Told, 'clipboardWatched'>> = {}, parent: HTMLElement = document.body,
): { readonly root: HTMLElement; readonly view: GridView<Row>; readonly told: Told } {
    const root = document.createElement('div');
    parent.append(root);
    const told = Object.assign(new Told(), browser);
    const view = new GridView(root, options, told);
    mounted.push(view);
    told.viewport();
    told.ceiling();
    return { root, view, told };
}

export function q(root: HTMLElement, selector: string): HTMLElement {
    const found = root.querySelector<HTMLElement>(selector);
    if (found === null) throw new Error(`Nothing matches ${selector}.`);
    return found;
}
export const rowsOf = (root: HTMLElement): HTMLElement[] => [...root.querySelectorAll<HTMLElement>('.ex-viewport > .ex-row')];

export async function scrollTo(root: HTMLElement, top: number, left = 0): Promise<void> {
    const scroller = q(root, '.ex-scroller');
    scroller.scrollTop = top;
    scroller.scrollLeft = left;
    scroller.dispatchEvent(new Event('scroll'));
    await painted();
}
