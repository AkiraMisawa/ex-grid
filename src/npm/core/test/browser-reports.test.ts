// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 6): what the browser's own reports tell the grid, under a ResizeObserver the test drives.
import { afterEach, describe, expect, it, vi } from 'vitest';
import { browserReports } from '../src/dom/browser-reports';

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
