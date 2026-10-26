// The figures menu (ADR-0130): Excel's right-click on the status line, drawn in the grid's popover frame. Each item
// is a figure, ticked while shown; a press reports the new choice and the menu stays, as Excel's does. The grid
// holds no choice of its own: the ticks are the figures the Consumer hands back, painted again on every pass.

import { summaryLabelIdOf, summaryLabelIds } from '../chrome/summary-words';
import { summaryFigureOrder, type SummaryFigure } from '../summary/summary-accumulator';

/** The figures with `figure` ticked or unticked, in Excel's order. */
export function toggledFigures(shown: readonly SummaryFigure[], figure: SummaryFigure): readonly SummaryFigure[] {
    const ticked = !shown.includes(figure);
    return Object.freeze(summaryFigureOrder.filter(each => (each === figure ? ticked : shown.includes(each))));
}

/** The menu's contents: one item a figure, in Excel's order. */
export class SummaryMenu {
    readonly element: HTMLElement;
    readonly #items: readonly (readonly [SummaryFigure, HTMLButtonElement])[];
    #shown: readonly SummaryFigure[] | undefined;
    #labelsFrom: unknown;

    /** `toggle` hears a figure pressed. */
    constructor(doc: Document, toggle: (figure: SummaryFigure) => void) {
        this.element = doc.createElement('div');
        this.element.className = 'ex-summary-menu';
        this.element.setAttribute('role', 'menu');
        this.#items = summaryFigureOrder.map(figure => {
            const item = doc.createElement('button');
            item.type = 'button';
            item.setAttribute('role', 'menuitemcheckbox');
            // Numbered in the Input Order, as the grid's own controls are (ADR-0240).
            item.setAttribute('data-ex-claims', 'click');
            item.addEventListener('click', () => { toggle(figure); });
            return [figure, item] as const;
        });
        this.element.append(...this.#items.map(([, item]) => item));
        // A menu's keys (ADR-0039): ↑ and ↓ step through the items and wrap, Home and End go to either end. Escape and
        // Tab are the frame's.
        this.element.addEventListener('keydown', event => {
            const items = this.#items.map(([, item]) => item);
            const at = items.findIndex(item => item === event.target);
            const to = event.key === 'ArrowDown' ? (at + 1) % items.length
                : event.key === 'ArrowUp' ? (at - 1 + items.length) % items.length
                    : event.key === 'Home' ? 0
                        : event.key === 'End' ? items.length - 1 : undefined;
            if (to === undefined || at < 0 || event.ctrlKey || event.altKey || event.metaKey || event.shiftKey) return;
            event.preventDefault();
            items[to]?.focus({ preventScroll: true });
        });
    }

    /** Whether the menu still stands in its frame. */
    get standing(): boolean {
        return this.element.isConnected;
    }

    /** Ticks the figures shown, in the grid's words; `labelsFrom` is what the words come from, and the same figures
     * from the same source paint nothing. */
    paint(shown: readonly SummaryFigure[], words: (id: string) => string, labelsFrom: unknown): void {
        if (shown === this.#shown && labelsFrom === this.#labelsFrom) return;
        this.#shown = shown;
        this.#labelsFrom = labelsFrom;
        this.element.setAttribute('aria-label', words(summaryLabelIds.menu));
        const doc = this.element.ownerDocument;
        for (const [figure, item] of this.#items) {
            const ticked = shown.includes(figure);
            item.className = ticked ? 'ex-summary-menu-item ex-summary-menu-shown' : 'ex-summary-menu-item';
            item.setAttribute('aria-checked', ticked ? 'true' : 'false');
            const tick = doc.createElement('span');
            tick.className = 'ex-summary-menu-tick';
            tick.setAttribute('aria-hidden', 'true');
            tick.textContent = ticked ? '✓' : '';
            item.replaceChildren(tick, words(summaryLabelIdOf(figure)));
        }
    }
}
