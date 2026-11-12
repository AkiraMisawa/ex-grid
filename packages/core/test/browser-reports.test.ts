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
 * its document. `asked` counts the media queries it was asked for. */
function windowAt(ratio: number): { readonly element: HTMLElement; readonly asked: () => number; readonly moveTo: (ratio: number) => void } {
    const view = new OtherWindow();
    let heard: (() => void) | undefined;
    let asked = 0;
    Object.defineProperty(view, 'devicePixelRatio', { value: ratio, configurable: true });
    Object.defineProperty(view, 'matchMedia', {
        value: () => {
            asked++;
            return {
                addEventListener: (_type: string, listener: () => void): void => { heard = listener; },
                removeEventListener: (_type: string, listener: () => void): void => { if (heard === listener) heard = undefined; },
            };
        },
    });
    return {
        element: view.document.createElement('div') as unknown as HTMLElement,
        asked: () => asked,
        moveTo: next => {
            Object.defineProperty(view, 'devicePixelRatio', { value: next, configurable: true });
            heard?.();
        },
    };
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

        // Dragged to a display of another scale, then zoomed.
        other.moveTo(1);
        other.moveTo(1.5);
        stop();
        // Nothing hears the window once the grid has let go of it.
        other.moveTo(3);

        expect(told).toEqual([2, 1, 1.5]);
        expect(other.asked()).toBe(3);
        expect(own).not.toHaveBeenCalled();
    });

    it('ADR-0090: an element of a document that no window shows has no Device Pixel, and is refused (#565)', () => {
        const unshown = document.implementation.createHTMLDocument('').createElement('div');

        expect(() => browserReports.observeDevicePixel(unshown, () => { /* never told */ }))
            .toThrow('The grid\'s element is in a document with no window: it has no Device Pixel.');
    });

    it('ADR-0090, ADR-0018: a grid refused for a root that no window shows leaves no observer behind (#565)', () => {
        let observing = 0;
        vi.stubGlobal('ResizeObserver', class {
            observe(): void { observing++; }
            disconnect(): void { observing--; }
        });
        const unshown = document.implementation.createHTMLDocument('').createElement('div');

        expect(() => createGrid(unshown, { columns: [{ name: 'Book', type: 'text', value: (row: { readonly book: string }) => row.book }], rows: [{ book: 'Rates' }] }))
            .toThrow('The grid\'s element is in a document with no window: it has no Device Pixel.');
        expect(observing).toBe(0);
    });
});
