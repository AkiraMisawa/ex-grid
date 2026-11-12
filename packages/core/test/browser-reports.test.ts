// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 6): what the browser's own reports tell the grid, under a ResizeObserver the test drives.
import { Window as OtherWindow } from 'happy-dom';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { browserReports } from '../src/dom/browser-reports';
import { createGrid } from '../src/dom/create-grid';

type Observed = (entries: { borderBoxSize: { blockSize: number; inlineSize: number }[] }[]) => void;

// One ResizeObserver whose callback the test calls, as the browser would after a layout.
function drivenObserver(): (blockSize: number) => void {
    let observed: Observed | undefined;
    vi.stubGlobal('ResizeObserver', class {
        constructor(callback: Observed) { observed = callback; }
        observe(): void { /* the test calls the callback */ }
        disconnect(): void { /* nothing to let go of */ }
    });
    return blockSize => {
        if (observed === undefined) throw new Error('Nothing observes the probe.');
        observed([{ borderBoxSize: [{ blockSize, inlineSize: 1 }] }]);
    };
}

afterEach(() => {
    vi.unstubAllGlobals();
    vi.useRealTimers();
});

describe('the Layout Ceiling report (ADR-0053)', () => {
    it('ADR-0053: a probe laid out at nothing, taken out of the document or hidden, tells no ceiling', () => {
        vi.useFakeTimers();
        const lays = drivenObserver();
        const told: number[] = [];
        const stop = browserReports.observeCeiling(document.createElement('div'), ceilingPx => { told.push(ceilingPx); });

        lays(33_554_400);
        vi.runAllTimers();
        // Removed before the grid is disposed (a Blazor Server navigation), or under display: none.
        lays(0);
        vi.runAllTimers();
        // Laid out again at another scale or zoom, the ceiling it now has is told.
        lays(22_369_600);
        vi.runAllTimers();
        stop();

        expect(told).toEqual([33_554_400, 22_369_600]);
    });
});

/** A window at `ratio` whose resolution the test moves, as a display's scale or a zoom moves it, and an element of
 * its document. Each media query it is asked for keeps its own listeners, as a browser's does: `asked` counts the
 * queries, and `listening` the listeners every one of them still holds. */
function windowAt(ratio: number): {
    readonly element: HTMLElement;
    readonly asked: () => number;
    readonly listening: () => number;
    readonly moveTo: (ratio: number) => void;
} {
    const view = new OtherWindow();
    const queries: { readonly media: string; readonly listeners: Set<() => void> }[] = [];
    let current = ratio;
    Object.defineProperty(view, 'devicePixelRatio', { get: () => current });
    Object.defineProperty(view, 'matchMedia', {
        value: (media: string) => {
            const query = { media, listeners: new Set<() => void>() };
            queries.push(query);
            return {
                addEventListener: (_type: string, listener: () => void): void => { query.listeners.add(listener); },
                removeEventListener: (_type: string, listener: () => void): void => { query.listeners.delete(listener); },
            };
        },
    });
    return {
        element: view.document.createElement('div') as unknown as HTMLElement,
        asked: () => queries.length,
        listening: () => queries.reduce((count, query) => count + query.listeners.size, 0),
        moveTo: next => {
            const before = `(resolution: ${current}dppx)`;
            const after = `(resolution: ${next}dppx)`;
            current = next;
            // A query hears a change when it starts or stops matching, and only then. Those are found before any
            // is told: one asked for while the change is told is asked of the window as it now is.
            const changed = queries.filter(query => (query.media === before) !== (query.media === after));
            for (const query of changed) for (const listener of query.listeners) listener();
        },
    };
}

/** A listener as its event and phase, which is what a removal has to name again. */
function listenerOf(type: string, options?: boolean | EventListenerOptions): string {
    return `${type}${(typeof options === 'boolean' ? options : options?.capture === true) ? ' (capture)' : ''}`;
}

describe('the Device Pixel report (ADR-0090)', () => {
    it('ADR-0090: an element in another window than the script\'s is told that window\'s Device Pixel (#565)', () => {
        const other = windowAt(2);
        const told: number[] = [];
        const stop = browserReports.observeDevicePixel(other.element, ratio => { told.push(ratio); });
        stop();

        expect(window.devicePixelRatio, 'the script\'s own window is at another scale').toBe(1);
        expect(told).toEqual([2]);
    });

    it('ADR-0090: a change is heard from the element\'s window, and the script\'s window is never asked (#565)', () => {
        const own = vi.spyOn(window, 'matchMedia');
        const other = windowAt(2);
        const told: number[] = [];
        const stop = browserReports.observeDevicePixel(other.element, ratio => { told.push(ratio); });

        // Dragged to a display of another scale, then zoomed: one query listened to at a time.
        other.moveTo(1);
        expect(other.listening()).toBe(1);
        other.moveTo(1.5);
        expect(other.listening()).toBe(1);
        stop();
        // Nothing hears the window once the grid has let go of it.
        other.moveTo(3);

        expect(told).toEqual([2, 1, 1.5]);
        expect(other.asked()).toBe(3);
        expect(other.listening(), 'the listeners left on any of the window\'s queries').toBe(0);
        expect(own).not.toHaveBeenCalled();
    });

    it('ADR-0090: an element of a document that no window shows has no Device Pixel, and is refused (#565)', () => {
        const unshown = document.implementation.createHTMLDocument('').createElement('div');

        expect(() => browserReports.observeDevicePixel(unshown, () => { /* never told */ }))
            .toThrow('The grid\'s element is in a document with no window: it has no Device Pixel.');
    });

    it('ADR-0090, ADR-0018: a grid refused for a root that no window shows leaves no observer and no listener behind (#565)', () => {
        let observing = 0;
        vi.stubGlobal('ResizeObserver', class {
            observe(): void { observing++; }
            disconnect(): void { observing--; }
        });
        const unshown = document.implementation.createHTMLDocument('').createElement('div');
        // What the root hears, by event and phase, as the grid adds and removes it.
        const listening: string[] = [];
        const add = unshown.addEventListener.bind(unshown);
        const remove = unshown.removeEventListener.bind(unshown);
        unshown.addEventListener = (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | AddEventListenerOptions): void => {
            listening.push(listenerOf(type, options));
            add(type, listener, options);
        };
        unshown.removeEventListener = (type: string, listener: EventListenerOrEventListenerObject, options?: boolean | EventListenerOptions): void => {
            const at = listening.indexOf(listenerOf(type, options));
            if (at !== -1) listening.splice(at, 1);
            remove(type, listener, options);
        };

        expect(() => createGrid(unshown, { columns: [{ name: 'Book', type: 'text', value: (row: { readonly book: string }) => row.book }], rows: [{ book: 'Rates' }] }))
            .toThrow('The grid\'s element is in a document with no window: it has no Device Pixel.');
        expect(observing).toBe(0);
        expect(listening).toEqual([]);
    });
});
