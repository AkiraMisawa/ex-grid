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

import type { FilterPanelModel, FilterPanelView, FilterUi } from '../chrome/filter-panel-model';
import { letterOf, menuKeyNothing, type MenuKey } from '../chrome/menu-keys';
import type { ColumnType, DateType } from '../query/columns';
import type { FilterCombinator, FilterOperator, FilterSpec } from '../query/filter';
import type { SelectionRange } from '../selection/range';
import { filterSentinel, type FilterPanelAnswers, type FilterPlace } from './filter-panel';
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
 * What a column's filter panel's contents are handed (ADR-0009, ADR-0044): the column, what the panel shows and
 * the choices that change it, and the exits. The panel stands under the column's commands in the one popover
 * Alt+↓ and the ▾ open, inside the slot, `.ex-popover-filter`; Tab off either end of it is the core's, which
 * places a sentinel either side, so the contents need no wrap of their own.
 *
 * What a choice means is the core's: the contents read {@link GridFilterPanelSlotContext.view}, draw it, and hand
 * each choice to its setter, which answers the input (ADR-0220) and changes the view. OK is
 * {@link GridFilterPanelSlotContext.apply}. The "clear-filter" command above the panel clears the column's filter,
 * so the contents draw no Clear of their own.
 */
export interface GridFilterPanelSlotContext {
    /** The `name` of the column the panel filters. */
    readonly column: string;
    /** That column's type. */
    readonly columnType: ColumnType;
    /** A Date column's declared date type (ADR-0023): what a typed value reads as. */
    readonly dateType: DateType;
    /** What the column declared its panel lays out (ADR-0009). The view says which form stands. */
    readonly mode: FilterUi;
    /** The column's spec in force when the panel opened. */
    readonly current: FilterSpec | undefined;
    /** What the panel shows now: a new object at each change, and the same one otherwise, so a framework may
     * compare it by reference. */
    readonly view: () => FilterPanelView;
    /** Hears each change to the view; answers the function that stops it. */
    readonly subscribe: (listener: () => void) => () => void;
    /** The search box's text. */
    readonly setSearch: (text: string) => void;
    /** "Add current selection to filter" ticked or not. */
    readonly setAddToCurrent: (on: boolean) => void;
    /** A value's checkbox ticked or not: the `value` of one of the view's entries. */
    readonly setChecked: (value: unknown, checked: boolean) => void;
    /** "(Select All)" pressed: toggles the values the search shows. */
    readonly toggleAll: () => void;
    /** The first condition's operator, one of the view's `allowed` (FN-18). */
    readonly setOperator: (operator: FilterOperator) => void;
    /** The text typed for the first condition's value. */
    readonly setOperand: (text: string) => void;
    /** How the two conditions are joined. */
    readonly setCombinator: (combinator: FilterCombinator) => void;
    /** The second condition's operator, or `undefined` for "(none)". */
    readonly setSecond: (operator: FilterOperator | undefined) => void;
    /** The text typed for the second condition's value. */
    readonly setSecondOperand: (text: string) => void;
    /** OK: the panel's answer is applied and the popover closes. While the view says OK is unavailable, or a typed
     * value does not read, nothing is applied and the panel stands, saying why. The slot is a form, and Enter in a
     * text field submits it as the browser does, which applies too: a submit button of the contents' is an OK. */
    readonly apply: () => void;
    /** Cancel: the popover closes, and nothing is applied. The keyboard returns to the grid. */
    readonly cancel: () => void;
    /**
     * A key pressed on the value list, its checkboxes or "(Select All)", never in a text field (ADR-0044): Excel's
     * letters act there as on the commands, S, O and C running their command and closing the popover, and E
     * moving to the search box. Handed `KeyboardEvent.key` and whether Control, Alt or Meta was held; answers
     * whether the key was one of those letters, whose default the contents then prevent. Not handed while an IME
     * composes.
     */
    readonly valueListKey: (key: string, controlAltOrMeta: boolean) => boolean;
    /** The count of the core's requests for a control of the panel to take the keyboard, zero at the opening, where
     * the commands hold it. Each request mounts the contents again; whenever the count differs from the one they
     * last answered, they focus the control {@link GridFilterPanelSlotContext.focusTarget} names through
     * {@link GridFilterPanelSlotContext.takeFocus}. */
    readonly focusRequest: number;
    /** Which control the latest request names: the panel's `first` (Tab on a command, and the value list arriving
     * while the keyboard was in the panel), its `last` (Shift+Tab on a command, wrapping), or its `search`, the
     * field a search is typed into, Excel's E: the value list's search box, or the first condition's value where no
     * list stands (ADR-0044). `undefined` before any request. */
    readonly focusTarget: 'first' | 'last' | 'search' | undefined;
    /** The contents report a popup of their own (a select's list, a date picker) opening (`true`) and closing
     * (`false`): while one is open, Escape is the popup's, and the next closes the popover (ADR-0039). */
    readonly innerPopupChanged: (open: boolean) => void;
    /** Gives `control` DOM focus while the keyboard is still this grid's: DOM focus inside its root, or on
     * nothing (ADR-0204). An element that cannot take focus hands it to the first control inside it. */
    readonly takeFocus: (control: HTMLElement, options?: { readonly preventScroll?: boolean }) => void;
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
    /** A column's filter panel, under the column menu's commands (ADR-0009, ADR-0044). */
    readonly filterPanel?: GridChromeSlot<GridFilterPanelSlotContext>;
}

/** The slots a grid's `chrome` names. */
const slotNames: readonly (keyof GridChrome<unknown>)[] = ['columnMenu', 'contextMenu', 'filterPanel'];

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

/** The grid's focus function, as a slot's part is handed it: wherever DOM focus stands where `owed`, the grid's own
 * request, and otherwise only while the keyboard is still the grid's (ADR-0204). */
export type ChromeFocus = (control: HTMLElement, preventScroll: boolean, owed: boolean) => void;

/**
 * A Chrome slot's contents while their part stands (ADR-0390): mounted with a context, mounted again with the next
 * one for each focus request, and taken away when the part goes, after which nothing they call back reaches the
 * grid. The focus a request asks for is owed, the grid's own to give wherever DOM focus stands, as the built-in
 * part's is, until the contents give it or the user makes an input in the part (ADR-0039); any other focus the
 * contents ask for is given only while the keyboard is still the grid's (ADR-0204).
 */
class SlotMount<Context> {
    readonly #name: string;
    readonly #mount: GridChromeSlot<Context>;
    readonly #slot: HTMLElement;
    readonly #focus: ChromeFocus;
    readonly #cancel: () => void;
    #owed: boolean;
    #cleanup: (() => void) | undefined;
    #live = true;
    /** Whether a popup of the contents' own is open: Escape is then the popup's (ADR-0039). */
    innerPopupOpen = false;

    /** `owed` says whether the first mount answers a request of the grid's; `cancel` closes the part. */
    constructor(name: string, mount: GridChromeSlot<Context>, slot: HTMLElement, focus: ChromeFocus, cancel: () => void, owed: boolean) {
        this.#name = name;
        this.#mount = mount;
        this.#slot = slot;
        this.#focus = focus;
        this.#cancel = cancel;
        this.#owed = owed;
    }

    /** Whether the part stands. */
    get live(): boolean {
        return this.#live;
    }

    /** Mounts the contents, once. */
    mount(context: Context): void {
        if (this.#live && this.#cleanup === undefined) this.#cleanup = mounted(this.#name, this.#mount, this.#slot, context);
    }

    /** A focus request: the contents are mounted again with `context`, which names it. Contents that cannot be drawn
     * again close the part, rather than leave it standing empty. */
    request(context: Context): void {
        if (!this.#live) return;
        this.#owed = true;
        this.#unmount();
        try {
            this.#cleanup = mounted(this.#name, this.#mount, this.#slot, context);
        } catch (error) {
            this.#cancel();
            throw error;
        }
    }

    /** An input the user made in the part: it moves the keyboard on from here, and no focus is owed any longer. */
    heard(): void {
        this.#owed = false;
    }

    /** The part is going: its contents are taken away. */
    dispose(): void {
        if (!this.#live) return;
        this.#live = false;
        this.#unmount();
    }

    readonly innerPopupChanged = (open: boolean): void => {
        if (typeof open !== 'boolean') throw new TypeError('innerPopupChanged is handed whether the popup is open.');
        if (this.#live) this.innerPopupOpen = open;
    };

    readonly takeFocus = (control: HTMLElement, options?: { readonly preventScroll?: boolean }): void => {
        if (!(control instanceof HTMLElement)) throw new TypeError('takeFocus is handed the element that takes the keyboard.');
        if (!this.#live) return;
        const owed = this.#owed;
        this.#owed = false;
        this.#focus(control, options?.preventScroll === true, owed);
    };

    #unmount(): void {
        const cleanup = this.#cleanup;
        this.#cleanup = undefined;
        cleanup?.();
    }
}

/**
 * A menu whose items a Chrome slot draws (ADR-0390): the place and the keys' meaning are the `MenuState` the
 * built-in `MenuItems` keeps too, and running a command and closing are the grid's; the contents draw and call
 * back.
 */
export class SlottedMenu<Extra extends object> implements MenuPlace {
    readonly #slot: SlotMount<GridMenuSlotContext & Extra>;
    readonly #extra: Extra;
    readonly #shown: readonly GridCommand[];
    readonly #commands: readonly GridCommand[];
    readonly #state: MenuState;
    readonly #answers: MenuAnswers;
    readonly #label: (id: string) => string;
    #focusRequest = 1;

    constructor(slot: HTMLElement, name: string, mount: GridChromeSlot<GridMenuSlotContext & Extra>, extra: Extra,
        commands: readonly GridCommand[], label: (id: string) => string, table: MenuKeyTable, answers: MenuAnswers, takeFocus: ChromeFocus) {
        // The opening is a request of the grid's: its focus is the grid's to give, as the built-in menu's is.
        this.#slot = new SlotMount(name, mount, slot, takeFocus, () => { answers.cancel(); }, true);
        this.#extra = extra;
        this.#shown = commands;
        this.#state = new MenuState(commands, table, answers);
        this.#answers = answers;
        this.#label = label;
        // What the contents invoke runs as the built-in item's press does: an input answered, and the command run
        // by the grid, which closes the menu first and asks the command again where it may have changed.
        this.#commands = Object.freeze(commands.map((command, index) => Object.freeze({
            id: command.id,
            enabled: command.enabled,
            invoke: () => {
                if (!this.#slot.live) return;
                this.#answers.answered();
                this.#answers.run(index);
            },
        })));
    }

    get innerPopupOpen(): boolean {
        return this.#slot.innerPopupOpen;
    }

    /** Mounts the contents, whose first mount asks them to take the keyboard. */
    focus(): void {
        this.#slot.mount(this.#context());
    }

    /** Puts the place back on the first enabled item, and asks the contents to focus it: they are mounted again
     * with the next request (ADR-0044). */
    focusFirst(): void {
        if (!this.#slot.live) return;
        this.#state.toFirst();
        this.#focusRequest += 1;
        this.#slot.request(this.#context());
    }

    /** The menu is going: its contents are taken away, and nothing they call back reaches the grid again. */
    dispose(): void {
        this.#slot.dispose();
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
            innerPopupChanged: this.#slot.innerPopupChanged,
            takeFocus: this.#slot.takeFocus,
        });
    }

    readonly #close = (): void => {
        if (!this.#slot.live) return;
        this.#answers.answered();
        this.#answers.cancel();
    };

    readonly #resolveKey = (key: string, shift: boolean, controlAltOrMeta: boolean): MenuKey => {
        if (typeof key !== 'string' || typeof shift !== 'boolean' || typeof controlAltOrMeta !== 'boolean') {
            throw new TypeError('resolveKey is handed the key (KeyboardEvent.key), whether Shift was held, and whether Control, Alt or Meta was.');
        }
        if (!this.#slot.live) return menuKeyNothing;
        this.#slot.heard();
        return this.#state.key(key, shift, controlAltOrMeta);
    };

    readonly #itemFocused = (index: number): void => {
        if (!Number.isSafeInteger(index) || index < 0 || index >= this.#shown.length) {
            throw new RangeError(`itemFocused is handed the index of one of the menu's ${this.#shown.length} commands, not ${index}.`);
        }
        if (this.#slot.live) this.#state.focused(index);
    };
}

/** The column menu's own part of its slot's context. */
export function columnMenuExtra(column: string, columnType: ColumnType): Omit<GridColumnMenuSlotContext, keyof GridMenuSlotContext> {
    return { column, columnType, letter: letterOf };
}

/**
 * A column's filter whose panel a Chrome slot draws (ADR-0390): what each choice means is the `FilterPanelModel`
 * the built-in panel draws from too, and applying, closing and the letters are the grid's. The slot is a form, so
 * Enter in a text field submits it as the browser does, which applies, and never on the Enter that confirms an
 * IME's candidate (ADR-0010). A focus request mounts the contents again with the next count, as the menu's does.
 */
export class SlottedFilterPanel implements FilterPlace {
    readonly lead: HTMLElement;
    readonly body: HTMLElement;
    readonly trail: HTMLElement;
    readonly #slot: SlotMount<GridFilterPanelSlotContext>;
    readonly #model: FilterPanelModel;
    readonly #context: Omit<GridFilterPanelSlotContext, 'focusRequest' | 'focusTarget'>;
    #focusRequest = 0;
    #focusTarget: GridFilterPanelSlotContext['focusTarget'];
    #arrived: boolean;

    constructor(doc: Document, mount: GridChromeSlot<GridFilterPanelSlotContext>, model: FilterPanelModel, answers: FilterPanelAnswers,
        takeFocus: ChromeFocus) {
        this.lead = filterSentinel(doc, answers);
        this.trail = filterSentinel(doc, answers);
        const form = doc.createElement('form');
        form.className = 'ex-popover-filter';
        form.noValidate = true;
        form.setAttribute('data-ex-claims', 'submit');
        this.body = form;
        // The commands hold the keyboard at the opening: the panel's first mount is asked for nothing.
        const slot = new SlotMount('filterPanel', mount, form, takeFocus, () => { answers.cancel(); }, false);
        this.#slot = slot;
        this.#model = model;
        this.#arrived = model.arrived;
        // The value list arriving changes the form under the keyboard: a keyboard that was in the panel is asked onto
        // its new first control, not left on a control the contents take away. Heard ahead of the contents, which
        // subscribe once mounted, so the keyboard is read where the old form had it.
        model.subscribe(() => {
            if (!slot.live || this.#arrived === model.arrived) return;
            this.#arrived = model.arrived;
            if (form.contains(doc.activeElement)) this.focusFirst();
        });
        // Each choice is an input the user made in the panel, answered once (ADR-0220).
        const set = <Args extends unknown[]>(change: (...args: Args) => void) => (...args: Args): void => {
            if (!slot.live) return;
            slot.heard();
            answers.answered();
            change(...args);
        };
        const apply = set(() => {
            const answer = model.answer();
            if (answer.reads) answers.apply(answer.spec);
        });
        form.addEventListener('submit', event => {
            event.preventDefault();
            apply();
        });
        const column = model.column;
        this.#context = {
            column: column.name, columnType: column.type, dateType: column.dateType, mode: column.filterUi, current: model.current,
            view: () => model.view,
            subscribe: listener => {
                if (typeof listener !== 'function') throw new TypeError('subscribe is handed the function that hears each change to the view.');
                return model.subscribe(listener);
            },
            setSearch: set((text: string) => { model.setSearch(text); }),
            setAddToCurrent: set((on: boolean) => { model.setAddToCurrent(on); }),
            setChecked: set((value: unknown, checked: boolean) => { model.setChecked(value, checked); }),
            toggleAll: set(() => { model.toggleAll(); }),
            setOperator: set((operator: FilterOperator) => { model.setOperator(operator); }),
            setOperand: set((text: string) => { model.setOperand(text); }),
            setCombinator: set((combinator: FilterCombinator) => { model.setCombinator(combinator); }),
            setSecond: set((operator: FilterOperator | undefined) => { model.setSecond(operator); }),
            setSecondOperand: set((text: string) => { model.setSecondOperand(text); }),
            apply,
            cancel: set(() => { answers.cancel(); }),
            // As the built-in list hears them: a character typed alone is an input answered, and maybe a letter.
            valueListKey: (key: string, controlAltOrMeta: boolean): boolean => {
                if (typeof key !== 'string' || typeof controlAltOrMeta !== 'boolean') {
                    throw new TypeError('valueListKey is handed the key (KeyboardEvent.key) and whether Control, Alt or Meta was held.');
                }
                if (!slot.live || key.length !== 1 || controlAltOrMeta) return false;
                slot.heard();
                answers.answered();
                return answers.letter(key);
            },
            innerPopupChanged: slot.innerPopupChanged,
            takeFocus: slot.takeFocus,
        };
    }

    get fetching(): boolean {
        return this.#model.fetching;
    }

    get innerPopupOpen(): boolean {
        return this.#slot.innerPopupOpen;
    }

    /** Mounts the contents. */
    open(): void {
        this.#slot.mount(this.#made());
    }

    focusFirst(): void {
        this.#request('first');
    }

    focusLast(): void {
        this.#request('last');
    }

    focusSearch(): void {
        this.#request('search');
    }

    close(): void {
        try {
            this.#slot.dispose();
        } finally {
            this.#model.close();
        }
    }

    #request(target: 'first' | 'last' | 'search'): void {
        if (!this.#slot.live) return;
        this.#focusRequest += 1;
        this.#focusTarget = target;
        this.#slot.request(this.#made());
    }

    #made(): GridFilterPanelSlotContext {
        return Object.freeze({ ...this.#context, focusRequest: this.#focusRequest, focusTarget: this.#focusTarget });
    }
}
