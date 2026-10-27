// The Chrome slots (ADR-0350 section 9, ADR-0390 section 3): a part of the grid's chrome a Wrapper draws in
// place of the built-in one, as `IGridChrome` lets the C# core's Chrome do. The core makes the slot, an element
// inside the frame it places, and hands it to the slot's mount function with what the part is to show and the
// callbacks it answers through. What the part means is the core's: the commands and their order, which are
// enabled, what a key does, where the frame stands and what closes it (ADR-0010, ADR-0039, ADR-0040). The
// contents only draw, and call back. A slot left unfilled is drawn by the built-in Chrome.
//
// A context is handed whole at each mount and never changes under the contents. When what the core hands
// changes while the part stands (a focus request), the core takes the contents away and mounts them again on
// the same slot, as it does a Template cell's (ADR-0020): a framework that keys its rendering by the slot,
// as `@ex-grid/react`'s portals do, keeps its component and that component's state across the two.

import { letterOf, menuKeyNothing, type MenuKey } from '../chrome/menu-keys';
import type { ColumnType } from '../query/columns';
import type { SelectionRange } from '../selection/range';
import type { GridCommand } from './grid-command';
import { MenuState, type MenuAnswers, type MenuKeyTable, type MenuPlace } from './menu-popover';

/**
 * A Chrome slot's mount function (ADR-0350 section 9): it draws the part into `slot`, the element the core
 * made for it, from `context`, and answers the function that takes what it drew away. The core calls that
 * cleanup before the slot goes, and before it mounts again on the same slot with a new context.
 */
export type GridChromeSlot<Context> = (slot: HTMLElement, context: Context) => () => void;

/**
 * What a menu's contents are handed, the column menu's and the Context Menu's alike (ADR-0010, ADR-0036,
 * ADR-0039). The contents draw one item per command, in this order, a disabled one shown and never focused,
 * inside the slot, which is the menu (`role="menu"`) and is named by the core.
 */
export interface GridMenuSlotContext {
    /** The commands, in the core's order, each enabled or not as the core says. Invoking one runs it as the
     * built-in menu's item does: the menu closes and the keyboard is back on the grid before it runs, and a
     * disabled one runs nothing. The Chrome only invokes. */
    readonly commands: readonly GridCommand[];
    /** A command's words, by its id: the Consumer's `commandLabel`, else the built-in English, else the id
     * (ADR-0036). A Wrapper with words of its own for a command may use them instead. */
    readonly label: (id: string) => string;
    /** Closes the menu as a Cancel, for a dismissal of the contents' own; the keyboard returns to the grid. */
    readonly close: () => void;
    /** The count of requests for the menu to take the keyboard: each opening, and each return from the filter
     * below the column's commands (ADR-0044). Whenever it differs from the one the contents last answered, and
     * at their first mount, they put DOM focus on the first enabled item, through {@link GridMenuSlotContext.takeFocus}, without
     * scrolling (ADR-0039). */
    readonly focusRequest: number;
    /**
     * What a key pressed on an item means (`KeyboardEvent.key`, Shift, and whether Control, Alt or Meta was
     * held), answered by `MenuKeys`' table against the place the core keeps for the menu (ADR-0039). A `move`
     * has already moved that place, and the contents focus the item it names; on a `run` they invoke the
     * command it names, and on a `close` they call {@link GridMenuSlotContext.close}. A move into the column's filter
     * (`filterFirst`, `filterLast`, `filterSearch`) the core has carried out. The contents prevent every key's
     * default on an item: an arrow, Home or End would scroll the page under the menu, and Enter and Space run
     * the item through this answer rather than the control's own click.
     */
    readonly resolveKey: (key: string, shift: boolean, controlAltOrMeta: boolean) => MenuKey;
    /** An item took DOM focus by another way than the menu's keys (a press on it, assistive technology): the
     * menu's place follows it, so the next arrow moves from there and Enter runs the item focus shows
     * (ADR-0039). Called from the item's focus event, with the item's index in {@link GridMenuSlotContext.commands}. */
    readonly itemFocused: (index: number) => void;
    /** The contents report a popup of their own opening (`true`) and closing (`false`): while one is open,
     * Escape is the popup's, and the next closes the menu (ADR-0039). Contents that open none never call it. */
    readonly innerPopupChanged: (open: boolean) => void;
    /** Gives `control` DOM focus while the keyboard is still this grid's: DOM focus inside its root, or on
     * nothing (ADR-0204). An element that cannot take focus hands it to the first control inside it. */
    readonly takeFocus: (control: HTMLElement, options?: { readonly preventScroll?: boolean }) => void;
}

/** What the column menu's contents are handed (ADR-0010, ADR-0044): the menu's, and the column it is for. The
 * column's filter stands below the slot, drawn by the core. */
export interface GridColumnMenuSlotContext extends GridMenuSlotContext {
    /** The `name` of the column the menu is for. */
    readonly column: string;
    /** That column's type. */
    readonly columnType: ColumnType;
    /** The letter a command answers to, as Excel's drop-down marks it (S, O or C), or `undefined` for a command
     * that has none (ADR-0044). The keys answer the letters whether the contents mark them or not. */
    readonly letter: (id: string) => string | undefined;
}

/** What the Context Menu's contents are handed (ADR-0036): the menu's, and the cell and the Selection it was
 * opened over, as `contextCommands` is handed them. {@link GridMenuSlotContext.commands} is every command, the
 * core's and the Consumer's. */
export interface GridContextMenuSlotContext<Row> extends GridMenuSlotContext {
    /** The row the menu was opened on. */
    readonly row: Row;
    /** The `name` of the column the menu was opened on. */
    readonly column: string;
    /** That column's type. */
    readonly columnType: ColumnType;
    /** The Selection, in the order its ranges were made. */
    readonly selection: readonly SelectionRange[];
    /** The Row Sequence Version the Selection's positions are written in (ADR-0011). */
    readonly rowSequenceVersion: number;
}

/**
 * The Chrome slots a grid takes (ADR-0390 section 4): the parts a Wrapper draws, each in place of the built-in
 * Chrome's. A slot not given is the built-in Chrome's.
 */
export interface GridChrome<Row> {
    /** The column menu's commands (ADR-0010, ADR-0044). */
    readonly columnMenu?: GridChromeSlot<GridColumnMenuSlotContext>;
    /** The Context Menu (ADR-0036). */
    readonly contextMenu?: GridChromeSlot<GridContextMenuSlotContext<Row>>;
}

/** The slots a grid's `chrome` names. */
const slotNames: readonly (keyof GridChrome<unknown>)[] = ['columnMenu', 'contextMenu'];

/** A grid's `chrome`, checked: an object whose slots are each a mount function, or not given. */
export function checkedChrome<Row>(chrome: unknown): GridChrome<Row> | undefined {
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) {
        throw new TypeError('chrome is an object of Chrome slots, each a mount function (ADR-0390).');
    }
    for (const [name, slot] of Object.entries(chrome)) {
        if (!(slotNames as readonly string[]).includes(name)) {
            throw new TypeError(`chrome has no slot '${name}': its slots are ${slotNames.join(', ')} (ADR-0390).`);
        }
        if (slot !== undefined && typeof slot !== 'function') {
            throw new TypeError(`chrome.${name} is a mount function, (slot, context) => cleanup (ADR-0350 section 9).`);
        }
    }
    return chrome;
}

/** Mounts `context` into `slot` by `mount`, and answers the cleanup it returned, checked. */
function mounted<Context>(name: string, mount: GridChromeSlot<Context>, slot: HTMLElement, context: Context): () => void {
    const cleanup: unknown = mount(slot, context);
    if (typeof cleanup !== 'function') {
        throw new TypeError(`chrome.${name} answered ${typeof cleanup}: a mount function answers the function that takes its contents away.`);
    }
    return () => { Reflect.apply(cleanup, undefined, []); };
}

/**
 * A menu whose items a Chrome slot draws (ADR-0390): the place and the keys' meaning are the `MenuState` the
 * built-in `MenuItems` keeps too, and running a command and closing are the grid's; the contents draw and call
 * back.
 */
export class SlottedMenu<Extra extends object> implements MenuPlace {
    readonly #slot: HTMLElement;
    readonly #name: string;
    readonly #mount: GridChromeSlot<GridMenuSlotContext & Extra>;
    readonly #extra: Extra;
    readonly #shown: readonly GridCommand[];
    readonly #commands: readonly GridCommand[];
    readonly #state: MenuState;
    readonly #answers: MenuAnswers;
    readonly #takeFocus: (control: HTMLElement, preventScroll: boolean, owed: boolean) => void;
    readonly #label: (id: string) => string;
    #focusRequest = 1;
    // A request the grid made, the opening or a return from the filter, which the contents' focus answers: that
    // focus is the grid's own to give, as the built-in menu's is, wherever DOM focus stands (ADR-0039).
    #focusOwed = true;
    #cleanup: (() => void) | undefined;
    #live = true;
    innerPopupOpen = false;

    constructor(slot: HTMLElement, name: string, mount: GridChromeSlot<GridMenuSlotContext & Extra>, extra: Extra,
        commands: readonly GridCommand[], label: (id: string) => string, table: MenuKeyTable, answers: MenuAnswers,
        takeFocus: (control: HTMLElement, preventScroll: boolean, owed: boolean) => void) {
        this.#slot = slot;
        this.#name = name;
        this.#mount = mount;
        this.#extra = extra;
        this.#shown = commands;
        this.#state = new MenuState(commands, table, answers);
        this.#answers = answers;
        this.#takeFocus = takeFocus;
        this.#label = label;
        // What the contents invoke runs as the built-in item's press does: an input answered, and the command run
        // by the grid, which closes the menu first and asks the command again where it may have changed.
        this.#commands = Object.freeze(commands.map((command, index) => Object.freeze({
            id: command.id,
            enabled: command.enabled,
            invoke: () => {
                if (!this.#live) return;
                this.#answers.answered();
                this.#answers.run(index);
            },
        })));
    }

    /** Mounts the contents, whose first mount asks them to take the keyboard. */
    focus(): void {
        if (this.#live && this.#cleanup === undefined) this.#cleanup = mounted(this.#name, this.#mount, this.#slot, this.#context());
    }

    /** Puts the place back on the first enabled item, and asks the contents to focus it: they are mounted again
     * with the next request (ADR-0044). Contents that cannot be drawn again close the menu, rather than leave it
     * standing empty. */
    focusFirst(): void {
        if (!this.#live) return;
        this.#state.toFirst();
        this.#focusRequest += 1;
        this.#focusOwed = true;
        this.#unmount();
        try {
            this.#cleanup = mounted(this.#name, this.#mount, this.#slot, this.#context());
        } catch (error) {
            this.#answers.cancel();
            throw error;
        }
    }

    /** The menu is going: its contents are taken away, and nothing they call back reaches the grid again. */
    dispose(): void {
        if (!this.#live) return;
        this.#live = false;
        this.#unmount();
    }

    #unmount(): void {
        const cleanup = this.#cleanup;
        this.#cleanup = undefined;
        cleanup?.();
    }

    #context(): GridMenuSlotContext & Extra {
        return Object.freeze({
            ...this.#extra,
            commands: this.#commands,
            label: this.#label,
            close: this.#close,
            focusRequest: this.#focusRequest,
            resolveKey: this.#resolveKey,
            itemFocused: this.#itemFocused,
            innerPopupChanged: this.#innerPopupChanged,
            takeFocus: this.#focusControl,
        });
    }

    readonly #close = (): void => {
        if (!this.#live) return;
        this.#answers.answered();
        this.#answers.cancel();
    };

    readonly #resolveKey = (key: string, shift: boolean, controlAltOrMeta: boolean): MenuKey => {
        if (typeof key !== 'string' || typeof shift !== 'boolean' || typeof controlAltOrMeta !== 'boolean') {
            throw new TypeError('resolveKey is handed the key (KeyboardEvent.key), whether Shift was held, and whether Control, Alt or Meta was.');
        }
        if (!this.#live) return menuKeyNothing;
        // A key the user typed moves the keyboard on from here: no focus is owed to a request any longer.
        this.#focusOwed = false;
        return this.#state.key(key, shift, controlAltOrMeta);
    };

    readonly #itemFocused = (index: number): void => {
        if (!Number.isSafeInteger(index) || index < 0 || index >= this.#shown.length) {
            throw new RangeError(`itemFocused is handed the index of one of the menu's ${this.#shown.length} commands, not ${index}.`);
        }
        if (this.#live) this.#state.focused(index);
    };

    readonly #innerPopupChanged = (open: boolean): void => {
        if (typeof open !== 'boolean') throw new TypeError('innerPopupChanged is handed whether the popup is open.');
        if (this.#live) this.innerPopupOpen = open;
    };

    readonly #focusControl = (control: HTMLElement, options?: { readonly preventScroll?: boolean }): void => {
        if (!(control instanceof HTMLElement)) throw new TypeError('takeFocus is handed the element that takes the keyboard.');
        if (!this.#live) return;
        const owed = this.#focusOwed;
        this.#focusOwed = false;
        this.#takeFocus(control, options?.preventScroll === true, owed);
    };
}

/** The column menu's own part of its slot's context. */
export function columnMenuExtra(column: string, columnType: ColumnType): Omit<GridColumnMenuSlotContext, keyof GridMenuSlotContext> {
    return { column, columnType, letter: letterOf };
}
