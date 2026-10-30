// @vitest-environment happy-dom
// Layer 2 (ADR-0350 section 13): the off-screen indicator (ADR-0014, ADR-0015, SL-10, PST-3). A Selection none of
// whose rectangles meets what is painted is said on the status line with its size, judged with no row data, and
// its button brings the Focus back into view.
import { afterEach, describe, expect, it } from 'vitest';
import { columns, mount, painted, q, scrollTo, trades, unmountAll } from './grid-harness';

afterEach(unmountAll);

const rowY = (row: number): number => (row * 24) + 5;

function press(root: HTMLElement, x: number, y: number): void {
    for (const type of ['mousedown', 'mouseup']) {
        const event = new MouseEvent(type, { bubbles: true, cancelable: true, button: 0, buttons: type === 'mousedown' ? 1 : 0 });
        Object.defineProperty(event, 'offsetX', { value: x });
        Object.defineProperty(event, 'offsetY', { value: y });
        q(root, '.ex-viewport').dispatchEvent(event);
    }
}

// The status line's words; the line is taken out while it says nothing, which reads as no words.
const status = (root: HTMLElement): string => {
    const line = root.querySelector('.ex-status');
    return line === null ? '' : line.textContent;
};

describe('the off-screen indicator', () => {
    it('ADR-0014 SL-10: a Selection scrolled out of view is said with its size, and Go to selection brings its Focus back', async () => {
        const { root } = mount({ columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, showSelectionSummary: false });
        await painted();
        press(root, 130, rowY(0));
        root.dispatchEvent(new KeyboardEvent('keydown', { key: 'ArrowDown', shiftKey: true, bubbles: true, cancelable: true }));
        await painted();
        expect(status(root)).toBe('');

        await scrollTo(root, 6000);
        expect(status(root)).toBe('2 cells selected (outside the visible range)Go to selection');

        q(root, '.ex-status button').click();
        await painted();
        expect([q(root, '.ex-scroller').scrollTop, status(root)]).toEqual([0, '']);
    });

    it('ADR-0014: the words are the Consumer\'s where it answers them, and a Selection on a painted row in view says nothing', async () => {
        const { root } = mount({
            columns, rows: trades(1000), rowHeight: 24, viewportHeight: 480, showSelectionSummary: false,
            commandLabel: id => ({ 'selected-outside-view': '{count} hors de vue', 'go-to-selection': 'Y aller' })[id],
        });
        await painted();
        press(root, 10, rowY(3));
        await painted();
        await scrollTo(root, 24);
        expect(status(root)).toBe('');
        await scrollTo(root, 4800);
        expect(status(root)).toBe('1 hors de vueY aller');
    });
});
