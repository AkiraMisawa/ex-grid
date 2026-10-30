// A Toolbar Item's choices (ADR-0100): a menu in the grid's popover frame, as Format Cells stands there (ADR-0050
// item 16). The frame is the grid's — where it stands, its bound, Escape, a press elsewhere — and gives the keyboard
// back when it closes (ADR-0039). Colours are a grid of swatches, ten across as Excel's palette is; everything else is
// a list. The arrow keys move among the choices that can be made, Home and End go to the ends, and Enter, Space or a
// press makes one. A choice that cannot be made says why beside it. The elements wear the C# Sheet's classes, so one
// stylesheet lays out both. What a choice means is the Sheet's: this draws, and reports what was chosen.

import { setAttr } from './dom-attributes';

/** One choice of a Toolbar Item's list. */
export interface SheetToolbarChoice {
    readonly label: string;
    /** For a colour, the colour as CSS paints it: drawn as a swatch, the label its name. */
    readonly swatch: string | undefined;
    /** Whether it is what the Focus cell has. */
    readonly selected: boolean;
    /** Why it cannot be made, or `undefined` where it can. */
    readonly disabledReason: string | undefined;
    /** Whether it opens a popover in the list's place, as More Number Formats… opens Format Cells. */
    readonly opensPopover: boolean;
    readonly choose: () => void;
}

/** What the list tells the Sheet. */
export interface SheetChoicesHooks {
    /** A choice was made: the list closes, unless the choice opens a popover in its place, and the choice is made. */
    readonly chosen: (choice: SheetToolbarChoice) => void;
}

// Ten swatches to a row, as Excel's palette lays them out.
const swatchesAcross = 10;

/** A Toolbar Item's choices as one opening shows them. One choice per opening: the first closes it. */
export class SheetChoicesPanel {
    readonly element: HTMLElement;
    readonly #choices: readonly SheetToolbarChoice[];
    readonly #buttons: HTMLButtonElement[];
    readonly #hooks: SheetChoicesHooks;
    #focused: number;
    #chosen = false;

    constructor(doc: Document, title: string, choices: readonly SheetToolbarChoice[], hooks: SheetChoicesHooks) {
        this.#choices = choices;
        this.#hooks = hooks;
        this.element = doc.createElement('div');
        this.element.className = 'ex-sheet-choices';
        this.element.setAttribute('role', 'menu');
        this.element.setAttribute('aria-label', title);
        this.element.addEventListener('keydown', this.#onKey);
        this.#buttons = choices.map((choice, at) => {
            const button = doc.createElement('button');
            button.type = 'button';
            button.setAttribute('role', 'menuitemradio');
            button.setAttribute('aria-checked', choice.selected ? 'true' : 'false');
            if (choice.swatch === undefined) {
                button.className = 'ex-sheet-choice';
                const label = doc.createElement('span');
                label.className = 'ex-sheet-choice-label';
                label.textContent = choice.label;
                button.append(label);
                if (choice.disabledReason !== undefined) {
                    button.setAttribute('aria-disabled', 'true');
                    button.title = choice.disabledReason;
                    const reason = doc.createElement('span');
                    reason.className = 'ex-sheet-choice-reason';
                    reason.textContent = choice.disabledReason;
                    button.append(reason);
                }
            } else {
                button.className = 'ex-sheet-swatch-choice';
                button.title = choice.label;
                button.setAttribute('aria-label', choice.label);
                const swatch = doc.createElement('span');
                swatch.className = 'ex-sheet-swatch';
                // The colour is the user's choice, document data painted as recorded (ADR-0071).
                swatch.style.backgroundColor = choice.swatch;
                button.append(swatch);
            }
            // A press is a choice; the browser's click of Enter or Space never comes, the keydown having taken it.
            button.addEventListener('click', () => { this.#choose(at); });
            button.addEventListener('focus', () => {
                this.#focused = at;
                this.#showFocused();
            });
            return button;
        });
        this.element.append(...this.#buttons);
        // The keys start from what the Focus cell has, or from the first choice that can be made.
        const selected = choices.findIndex(choice => choice.selected && choice.disabledReason === undefined);
        this.#focused = selected >= 0 ? selected : this.#next(-1, 1);
        this.#showFocused();
    }

    /** Puts the keyboard on the choice the keys start from: the list stands in the frame, which took it first. */
    takeKeyboard(): void {
        this.#buttons[this.#focused]?.focus({ preventScroll: true });
    }

    // One tab stop, the choice the keys are on.
    #showFocused(): void {
        this.#buttons.forEach((button, at) => { setAttr(button, 'tabindex', at === this.#focused ? '0' : '-1'); });
    }

    // The next choice from `from` that can be made, `step` at a time; `from` itself, kept inside the list, when none is.
    #next(from: number, step: number): number {
        for (let at = from + step; at >= 0 && at < this.#choices.length; at += step) {
            if (this.#choices[at]?.disabledReason === undefined) return at;
        }
        return Math.min(Math.max(from, 0), Math.max(this.#choices.length - 1, 0));
    }

    readonly #onKey = (event: KeyboardEvent): void => {
        if (event.isComposing || event.ctrlKey || event.altKey || event.metaKey) return;
        if ((event.key === 'Enter' || event.key === ' ') && !event.shiftKey) {
            event.preventDefault();
            this.#choose(this.#focused);
            return;
        }
        const across = this.#choices[this.#focused]?.swatch === undefined ? 1 : swatchesAcross;
        let moved: number;
        switch (event.key) {
            case 'ArrowDown': moved = this.#next(this.#focused, across); break;
            case 'ArrowUp': moved = across > 1 ? this.#upFromSwatch() : this.#next(this.#focused, -1); break;
            case 'ArrowRight': moved = across > 1 ? this.#next(this.#focused, 1) : this.#focused; break;
            case 'ArrowLeft': moved = across > 1 ? this.#next(this.#focused, -1) : this.#focused; break;
            case 'Home': moved = this.#next(-1, 1); break;
            case 'End': moved = this.#next(this.#choices.length, -1); break;
            default: return;
        }
        event.preventDefault();
        if (moved === this.#focused) return;
        this.#focused = moved;
        this.#showFocused();
        this.#buttons[moved]?.focus();
    };

    // Up from a swatch: the swatch above, or from the first row the choice listed before the swatches, as Automatic and
    // No Fill stand above Excel's palette.
    #upFromSwatch(): number {
        const above = this.#next(this.#focused, -swatchesAcross);
        if (above !== this.#focused) return above;
        const firstSwatch = this.#choices.findIndex(choice => choice.swatch !== undefined);
        return firstSwatch > 0 ? this.#next(firstSwatch, -1) : this.#focused;
    }

    #choose(at: number): void {
        const choice = this.#choices[at];
        if (this.#chosen || choice === undefined || choice.disabledReason !== undefined) return;
        this.#chosen = true;
        this.#hooks.chosen(choice);
    }
}
