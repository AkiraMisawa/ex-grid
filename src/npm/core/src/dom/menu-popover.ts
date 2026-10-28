// The built-in Chrome's menu (ADR-0010, ADR-0036, ADR-0039): an item per command, and the keys inside it,
// answered by `MenuKeys`' table against the menu's place. The grid's key listener takes Escape alone from
// inside a popover (ADR-0012); every other key is the item's, and what it means is the table's, so another
// Chrome answers it the same way. Where the menu stands, and what closing it does, are the grid's.

import { firstEnabled, letterOf, splitAtLetter, type MenuItem, type MenuKey } from '../chrome/menu-keys';
import { itemAt } from '../must';
import type { GridCommand } from './grid-command';

/** A popover is no narrower than this, unless the grid is (ADR-0040). Emitted inline, never a stylesheet
 * value beside it (ADR-0027). */
export const popoverMinWidthPx = 200;
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
    /** Tab, Shift+Tab or E on a command of a column menu with its filter below (ADR-0044): the keyboard goes
     * to that filter's first control, its last, or the field a search is typed into. */
    readonly toFilter?: (where: 'filterFirst' | 'filterLast' | 'filterSearch') => void;
}

/**
 * A menu's place and what its keys mean (ADR-0039), shared by every Chrome's menu: the built-in items and a Chrome
 * slot's. The place is held here, not read from DOM focus: it starts on the first enabled item, every key moves it
 * or reads it, and an item focused by another way moves it there.
 */
export class MenuState {
    readonly #shown: readonly MenuItem[];
    readonly #keys: MenuKeyTable;
    readonly #answering: MenuAnswers;
    #at: number;

    constructor(commands: readonly MenuItem[], table: MenuKeyTable, answers: MenuAnswers) {
        this.#shown = commands;
        this.#keys = table;
        this.#answering = answers;
        this.#at = firstEnabled(commands);
    }

    /** The item the menu stands on, or -1 where none is enabled. */
    get place(): number {
        return this.#at;
    }

    /** The place goes back to the first enabled item (ADR-0044). */
    toFirst(): void {
        this.#at = firstEnabled(this.#shown);
    }

    /** An item took DOM focus by another way than the keys: the place follows it. */
    focused(index: number): void {
        this.#at = index;
    }

    /** A key on an item, answered (ADR-0220) by the table against the place: a move moves the place, and a move
     * into the column's filter is sent. What else it means is the caller's to do. */
    key(pressed: string, shift: boolean, controlAltOrMeta: boolean): MenuKey {
        this.#answering.answered();
        const meaning = this.#keys(pressed, shift, controlAltOrMeta, this.#shown, this.#at);
        if (meaning.kind === 'move') this.#at = meaning.item;
        else if (meaning.kind === 'filterFirst' || meaning.kind === 'filterLast' || meaning.kind === 'filterSearch') this.#answering.toFilter?.(meaning.kind);
        return meaning;
    }
}

/** A menu as the grid holds it while it stands: the built-in items, or a Chrome slot's (ADR-0390). */
export interface MenuPlace {
    /** Puts the keyboard on the menu's place, at its opening. */
    focus(): void;
    /** Puts the menu's place back on its first enabled item, and the keyboard with it (ADR-0044). */
    focusFirst(): void;
    /** The menu is going. */
    dispose(): void;
    /** Whether a popup of the contents' own is open: Escape is then the popup's (ADR-0039). */
    readonly innerPopupOpen: boolean;
    /**
     * A key typed on the grid while the menu stands: the menu's while its contents still owe the opening focus,
     * as a Chrome slot's do until a Framework Adapter draws them (ADR-0410 section 5), answered against the place
     * as a key on an item is. Answers whether the key was the menu's.
     */
    keyOnGrid(key: string, shift: boolean, controlAltOrMeta: boolean): boolean;
}

/**
 * The items of a menu, in `container`: a button per command, a disabled one shown and never focusable. The
 * menu's place is held here, not read from DOM focus: it starts on the first enabled item, every key moves
 * it or reads it, and focus follows it, for the eye and for assistive technology (ADR-0039).
 */
export class MenuItems implements MenuPlace {
    readonly #items: readonly HTMLButtonElement[];
    readonly #commands: readonly GridCommand[];
    readonly #answers: MenuAnswers;
    readonly #state: MenuState;
    /** The built-in items open no popup of their own. */
    readonly innerPopupOpen = false;
    /** The built-in items take the keyboard as they are drawn, so a key on the grid is never theirs. */
    readonly keyOnGrid = (): boolean => false;

    /** `lettered` marks each command's letter the way Excel shows it (ADR-0044): the column menu's, whose sort
     * and clear-filter commands answer S, O and C; the Context Menu answers no letter and marks none. */
    constructor(container: HTMLElement, commands: readonly GridCommand[], labelOf: (id: string) => string, table: MenuKeyTable,
        answers: MenuAnswers, lettered = false) {
        const doc = container.ownerDocument;
        this.#commands = commands;
        this.#answers = answers;
        this.#state = new MenuState(commands, table, answers);
        this.#items = commands.map(command => {
            const item = doc.createElement('button');
            item.type = 'button';
            item.setAttribute('role', 'menuitem');
            if (command.enabled) item.setAttribute('data-ex-claims', 'click keydown');
            else item.disabled = true;
            const label = labelOf(command.id);
            const letter = lettered ? letterOf(command.id) : undefined;
            if (letter === undefined) {
                item.textContent = label;
            } else {
                // Underlined where the label has the letter, appended as "(S)" where it does not; the underline
                // is the markup's own, so a theme that restyles u restyles it.
                const [before, key, after] = splitAtLetter(label, letter);
                const mark = doc.createElement('u');
                mark.textContent = key;
                item.append(before, mark, after);
            }
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
        const place = this.#state.place;
        if (place >= 0) itemAt(this.#items, place).focus({ preventScroll: true });
    }

    /** Puts the menu's place back on its first enabled item, and DOM focus with it: Tab off either end of a
     * filter below the commands comes back here (ADR-0044). */
    focusFirst(): void {
        this.#state.toFirst();
        this.focus();
    }

    /** Nothing to take away: the items go with the popover's element. */
    dispose(): void {
        // The listeners are on the container, which goes with the popover.
    }

    // Every key on an item is the menu's, and its default is prevented: an arrow, Home or End would otherwise
    // scroll the page under the menu, and Enter and Space run the item here rather than through the button's
    // own click.
    readonly #onKey = (event: KeyboardEvent): void => {
        const index = this.#indexOf(event.target);
        if (index < 0) return;
        event.preventDefault();
        const meaning = this.#state.key(event.key, event.shiftKey, event.ctrlKey || event.altKey || event.metaKey);
        switch (meaning.kind) {
            case 'move':
                itemAt(this.#items, meaning.item).focus();
                return;
            case 'run':
                this.#answers.run(meaning.item);
                return;
            case 'close':
                this.#answers.cancel();
                return;
            case 'filterFirst':
            case 'filterLast':
            case 'filterSearch':
            case 'none':
                return;
        }
    };

    // Focus an item took by another way than the menu's keys — a press on it that did not end in a click on
    // it, assistive technology — is where the user now is: the place follows it, so the next arrow moves from
    // there and Enter runs the item the focus shows, never one the user left (ADR-0039). A move's own focus
    // lands here too, on the place it has already set.
    readonly #onFocusIn = (event: FocusEvent): void => {
        const index = this.#indexOf(event.target);
        if (index >= 0) this.#state.focused(index);
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
