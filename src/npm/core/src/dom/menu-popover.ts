// The built-in Chrome's menu (ADR-0010, ADR-0036, ADR-0039): an item per command, and the keys inside it,
// answered by `MenuKeys`' table against the menu's place. The grid's key listener takes Escape alone from
// inside a popover (ADR-0012); every other key is the item's, and what it means is the table's, so another
// Chrome answers it the same way. Where the menu stands, and what closing it does, are the grid's.

import { firstEnabled, type MenuItem, type MenuKey } from '../chrome/menu-keys';
import { itemAt } from '../must';
import type { GridCommand } from './grid-command';

/** A popover is no narrower than this, unless the grid is (ADR-0040). Emitted inline, never a stylesheet
 * value beside it (ADR-0027). */
const popoverMinWidthPx = 200;
/** And no wider than this, nor than the grid. */
const popoverMaxWidthPx = 320;

/**
 * Where a popover stands across the grid and how wide it may grow (ADR-0027, ADR-0040): no narrower than
 * {@link popoverMinWidthPx}, no wider than {@link popoverMaxWidthPx} or the grid, and its left edge clamped
 * by that widest, so however wide its contents make it, its right edge stays inside the grid's box.
 */
export function popoverAcross(wantedLeftPx: number, visibleWidthPx: number): { readonly leftPx: number; readonly widths: string } {
    const widest = Math.min(popoverMaxWidthPx, Math.max(0, visibleWidthPx));
    const narrowest = Math.min(popoverMinWidthPx, widest);
    const leftPx = Math.min(Math.max(wantedLeftPx, 0), Math.max(0, visibleWidthPx - widest));
    return { leftPx, widths: `min-width: ${narrowest}px; max-width: ${widest}px` };
}

/**
 * Where the Context Menu stands (ADR-0040): at the pointer, `atPx` down the grid's box and clamped inside
 * it, on whichever side of the pointer the grid has more room, and bounded by that room. Above, it hangs from
 * the pointer by its own height, a translate of its own box: nothing is measured.
 */
export function contextMenuStyle(leftPx: number, atPx: number, visibleWidthPx: number, visibleHeightPx: number): string {
    const at = Math.min(Math.max(atPx, 0), Math.max(0, visibleHeightPx));
    const below = visibleHeightPx - at;
    const { leftPx: left, widths } = popoverAcross(leftPx, visibleWidthPx);
    const hangs = below >= at ? '' : 'transform: translateY(-100%); ';
    return `left: ${left}px; top: ${at}px; ${hangs}${widths}; max-height: ${Math.max(below, at)}px`;
}

/** The room the Context Menu has, opened `atPx` down a box `visibleHeightPx` tall: the side it opened on. */
export function contextMenuRoomPx(atPx: number, visibleHeightPx: number): number {
    const at = Math.min(Math.max(atPx, 0), Math.max(0, visibleHeightPx));
    return Math.max(visibleHeightPx - at, at);
}

/** What a menu's keys mean, as `MenuKeys` answers them: the Context Menu's table, or the column menu's. */
export type MenuKeyTable = (pressed: string, shift: boolean, controlAltOrMeta: boolean, commands: readonly MenuItem[],
    current: number) => MenuKey;

/** What the grid does for its menu. */
export interface MenuAnswers {
    /** The command at `index`, enabled, was chosen: by a press, Enter or Space. */
    readonly run: (index: number) => void;
    /** The menu closes as a Cancel (Tab). */
    readonly cancel: () => void;
    /** An input reached the menu and was answered (ADR-0220). */
    readonly answered: () => void;
}

/**
 * The items of a menu, in `container`: a button per command, a disabled one shown and never focusable. The
 * menu's place is held here, not read from DOM focus: it starts on the first enabled item, every key moves
 * it or reads it, and focus follows it, for the eye and for assistive technology (ADR-0039).
 */
export class MenuItems {
    readonly #items: readonly HTMLButtonElement[];
    readonly #commands: readonly GridCommand[];
    readonly #table: MenuKeyTable;
    readonly #answers: MenuAnswers;
    #place: number;

    constructor(container: HTMLElement, commands: readonly GridCommand[], labelOf: (id: string) => string, table: MenuKeyTable,
        answers: MenuAnswers) {
        const doc = container.ownerDocument;
        this.#commands = commands;
        this.#table = table;
        this.#answers = answers;
        this.#place = firstEnabled(commands);
        this.#items = commands.map(command => {
            const item = doc.createElement('button');
            item.type = 'button';
            item.setAttribute('role', 'menuitem');
            if (command.enabled) item.setAttribute('data-ex-claims', 'click keydown');
            else item.disabled = true;
            item.textContent = labelOf(command.id);
            return item;
        });
        container.append(...this.#items);
        container.addEventListener('keydown', this.#onKey);
        container.addEventListener('click', this.#onClick);
        container.addEventListener('focusin', this.#onFocusIn);
    }

    /** Puts DOM focus on the menu's place, the first enabled item at the opening, without scrolling the menu:
     * one already scrolled is not pulled back to its top (ADR-0039). With nothing enabled, focus stays. */
    focus(): void {
        if (this.#place >= 0) itemAt(this.#items, this.#place).focus({ preventScroll: true });
    }

    // Every key on an item is the menu's, and its default is prevented: an arrow, Home or End would otherwise
    // scroll the page under the menu, and Enter and Space run the item here rather than through the button's
    // own click.
    readonly #onKey = (event: KeyboardEvent): void => {
        const index = this.#indexOf(event.target);
        if (index < 0) return;
        event.preventDefault();
        this.#answers.answered();
        const meaning = this.#table(event.key, event.shiftKey, event.ctrlKey || event.altKey || event.metaKey, this.#commands, this.#place);
        switch (meaning.kind) {
            case 'move':
                this.#place = meaning.item;
                itemAt(this.#items, meaning.item).focus();
                return;
            case 'run':
                this.#answers.run(meaning.item);
                return;
            case 'close':
                this.#answers.cancel();
                return;
            case 'none':
            case 'filterFirst':
            case 'filterLast':
            case 'filterSearch':
                return;
        }
    };

    // Focus an item took by another way than the menu's keys — a press on it that did not end in a click on
    // it, assistive technology — is where the user now is: the place follows it, so the next arrow moves from
    // there and Enter runs the item the focus shows, never one the user left (ADR-0039). A move's own focus
    // lands here too, on the place it has already set.
    readonly #onFocusIn = (event: FocusEvent): void => {
        const index = this.#indexOf(event.target);
        if (index >= 0) this.#place = index;
    };

    readonly #onClick = (event: MouseEvent): void => {
        const index = this.#indexOf(event.target);
        if (index < 0 || !itemAt(this.#commands, index).enabled) return;
        this.#answers.answered();
        this.#answers.run(index);
    };

    #indexOf(target: EventTarget | null): number {
        return this.#items.findIndex(item => item === target);
    }
}
