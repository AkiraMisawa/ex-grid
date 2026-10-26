// @vitest-environment happy-dom
// The adapter's own tests (ADR-0350 section 13): React Testing Library mounts it. What the Sheet does is
// `@ex-grid/sheet`'s, tested there; here, that the adapter mounts it once, hands it each render's props, and
// destroys it with the component.
import { createElement, StrictMode } from 'react';
import { cleanup, render } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'vitest';
import { ExSheet } from '../src/ex-sheet';

afterEach(() => { cleanup(); });

describe('ExSheet', () => {
    it('ADR-0370: the element React renders is the Sheet\'s, drawn before the browser paints, the grid and its notice inside', () => {
        const { container } = render(createElement(ExSheet, { locale: 'en-US' }));
        const root = container.firstElementChild;
        expect(root?.classList.contains('ex-sheet')).toBe(true);
        expect(root?.querySelector('[role="grid"]')?.getAttribute('aria-colcount')).toBe('16384');
        expect(root?.querySelector('.ex-sheet-notice')?.getAttribute('role')).toBe('status');
    });

    it('ADR-0370 section 10: given no locale, a Sheet takes the browser\'s language once, and a render keeps it', () => {
        Object.defineProperty(navigator, 'language', { value: 'de-DE', configurable: true });
        try {
            const { container, rerender } = render(createElement(ExSheet, {}));
            const grid = container.querySelector('[role="grid"]');
            Object.defineProperty(navigator, 'language', { value: 'ja-JP', configurable: true });
            rerender(createElement(ExSheet, { viewportHeight: 200 }));
            expect(container.querySelector('[role="grid"]')).toBe(grid);
        } finally {
            Reflect.deleteProperty(navigator, 'language');
        }
    });

    it('ADR-0018: unmounted, the Sheet is destroyed and its element emptied; under StrictMode one Sheet stands', () => {
        const { container, unmount } = render(createElement(StrictMode, null, createElement(ExSheet, { locale: 'en-US' })));
        const root = container.firstElementChild;
        expect(root?.querySelectorAll('[role="grid"]')).toHaveLength(1);
        unmount();
        expect(root?.childElementCount).toBe(0);
    });
});
