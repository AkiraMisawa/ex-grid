// A Consumer's popover (ADR-0050, item 16): the Consumer's own contents in the grid's popover frame, which ExSheet
// uses for Format Cells. The frame is every popover's: placed under the header band, centred across the grid and
// bounded by its box (ADR-0040), and dismissed by Escape, by a press elsewhere in the instance and by the box
// shrinking below a row (ADR-0010, ADR-0039). A sentinel either side wraps Tab inside it. The frame wears the C#
// core's classes, so one stylesheet lays out both cores' frames. The core holds nothing the contents render beyond
// the element they hand it.

/** What a Consumer's popover's contents are handed (ADR-0050, item 16). */
export interface GridPopoverContext {
    /** Closes the popover, as a Cancel as far as the grid is concerned; the keyboard returns to the grid. */
    readonly close: () => void;
    /** The contents report a popup of their own opening (`true`) and closing (`false`): while one is open, Escape
     * is the popup's (ADR-0039). */
    readonly innerPopupChanged: (open: boolean) => void;
}

// What Tab reaches: a control that is not disabled, and not taken out of the tab order (a tab list's other tabs).
const focusable = ['button', 'input', 'select', 'textarea'].map(tag => `${tag}:not(:disabled):not([tabindex="-1"])`)
    .concat('[tabindex]:not([tabindex="-1"]):not(.ex-focus-wrap)').join(', ');

/** The frame around a Consumer's contents. */
export class ConsumerPopoverFrame {
    readonly element: HTMLElement;
    readonly #body: HTMLElement;
    /** Whether a popup of the contents' own is open: Escape is then the popup's. */
    innerPopupOpen = false;

    /** `answered` counts a Tab that wraps as an input answered (ADR-0220). */
    constructor(doc: Document, label: string, contents: HTMLElement, answered: () => void) {
        this.element = doc.createElement('div');
        this.element.className = 'ex-popover ex-popover-consumer';
        this.element.setAttribute('role', 'dialog');
        this.element.setAttribute('aria-label', label);
        // Focusable only by script: the keyboard rests on the frame where the contents hold no control.
        this.element.tabIndex = -1;
        this.#body = doc.createElement('div');
        this.#body.className = 'ex-popover-body';
        this.#body.append(contents);
        // Tab off the last control comes back to the first, Shift+Tab off the first to the last (ADR-0039). A sentinel
        // reached from outside the contents was entered from that side, and the keyboard enters at the near end.
        this.element.append(
            this.#sentinel(doc, inside => { answered(); if (inside) this.focusLast(); else this.focusFirst(); }),
            this.#body,
            this.#sentinel(doc, inside => { answered(); if (inside) this.focusFirst(); else this.focusLast(); }));
    }

    /** Puts the keyboard on the contents' first control, or on the frame where they hold none. */
    focusFirst(): void {
        (this.#controls()[0] ?? this.element).focus({ preventScroll: true });
    }

    /** Puts the keyboard on the contents' last control, or on the frame where they hold none. */
    focusLast(): void {
        (this.#controls().at(-1) ?? this.element).focus({ preventScroll: true });
    }

    #controls(): HTMLElement[] {
        return [...this.#body.querySelectorAll<HTMLElement>(focusable)];
    }

    #sentinel(doc: Document, wrap: (fromInside: boolean) => void): HTMLElement {
        const element = doc.createElement('span');
        element.className = 'ex-focus-wrap';
        element.tabIndex = 0;
        // Numbered in the Input Order, as the grid's own panels' wraps are (ADR-0240).
        element.setAttribute('data-ex-claims', 'focus');
        element.addEventListener('focus', event => {
            const from = event.relatedTarget;
            wrap(from instanceof Node && this.#body.contains(from));
        });
        return element;
    }
}
