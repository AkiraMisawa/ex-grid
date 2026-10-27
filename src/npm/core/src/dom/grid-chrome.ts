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
import { findPanelLabelIds } from '../chrome/find-panel-labels';
import { letterOf, menuKeyNothing, type MenuKey } from '../chrome/menu-keys';
import type { ColumnType, DateType } from '../query/columns';
import type { FilterCombinator, FilterOperator, FilterSpec } from '../query/filter';
import type { SelectionRange } from '../selection/range';
import type { SelectionSummaryStatus, SummaryFigureShown } from './selection-summary';
import { filterSentinel, type FilterPanelAnswers, type FilterPlace } from './filter-panel';
import { findElement, findForm, findSentinel, type FindHeld, type FindPanelAnswers, type FindPlace } from './find-panel';
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

/** What the find panel shows now (ADR-0055, ADR-0332): a new object at each change, and the same one otherwise. */
export interface FindPanelView {
    /** What to look for. */
    readonly text: string;
    /** Match case. */
    readonly matchCase: boolean;
    /** Match entire cell contents. */
    readonly wholeCell: boolean;
    /** Whether the panel offers Replace: opened by Ctrl+H, and a Replace can land (ADR-0332 §2). While it does, the
     * contents draw the Replace field and the two writes, Replace and Replace All. */
    readonly replaceShown: boolean;
    /** What Replace writes in place of a match. */
    readonly replaceText: string;
    /** Whether a Replace All is out with the Consumer: Replace and Replace All wait for its answer, and the contents
     * show them unavailable meanwhile (ADR-0332 §3). */
    readonly replaceWaiting: boolean;
    /** What the last step or Replace came to, in the grid's words, for a live region; empty when it says nothing
     * (ADR-0033). */
    readonly outcome: string;
}

/**
 * What the find panel's contents are handed (ADR-0055, ADR-0332): what the panel shows and the changes and steps the
 * user makes in it. The core owns the search, the request, the answer and the Focus it moves; the contents draw a
 * field, the two options, the steps and, while Replace is shown, its field and its two writes. The slot stands in
 * the core's popover, inside a form the core made: Enter in a field submits it as the browser does, which runs a
 * step, backward with Shift, so the contents draw no form of their own; a submit by a button of theirs steps
 * nothing, so each button calls the step it means. Tab off either end of the panel is the
 * core's, which hands the keyboard back to the field. What the panel holds outlives it: the core keeps the text, the
 * options and the replacement across a close and a reopen, as Excel's dialog does.
 */
export interface GridFindPanelSlotContext {
    /** What the panel shows now. */
    readonly view: () => FindPanelView;
    /** Hears each change to the view; answers the function that stops it. */
    readonly subscribe: (listener: () => void) => () => void;
    /** The panel's words, by the ids `findPanelLabelIds` names: the Consumer's `commandLabel`, else the built-in
     * English. */
    readonly label: (id: string) => string;
    /** The text typed into the find field. */
    readonly setText: (text: string) => void;
    /** Match case ticked or not: a step after it searches with it as it now stands (ADR-0240). */
    readonly setMatchCase: (on: boolean) => void;
    /** Match entire cell contents ticked or not. */
    readonly setWholeCell: (on: boolean) => void;
    /** The text typed into the Replace field. */
    readonly setReplaceText: (text: string) => void;
    /** Find next. */
    readonly next: () => void;
    /** Find previous. */
    readonly previous: () => void;
    /** Replace: the current match written, then the next found. Nothing while Replace is not shown or a Replace All
     * is out. */
    readonly replace: () => void;
    /** Replace All, asked of the Consumer. Nothing while Replace is not shown or a Replace All is out. */
    readonly replaceAll: () => void;
    /** Closes the panel as a Cancel, for a dismissal of the contents' own; the keyboard returns to the grid. */
    readonly close: () => void;
    /** The count of the core's requests for the find field to take the keyboard: the opening, Ctrl+F or Ctrl+H again,
     * and Tab off either end of the panel. Each request mounts the contents again; whenever the count differs from the
     * one they last answered, they focus the find field through {@link GridFindPanelSlotContext.takeFocus}. Its text
     * is then selected, so what is typed next replaces it (FD-1), except after a Tab, which brings the field back as
     * it was. */
    readonly focusRequest: number;
    /** The contents report a popup of their own opening (`true`) and closing (`false`): while one is open, Escape is
     * the popup's, and the next closes the panel (ADR-0039). */
    readonly innerPopupChanged: (open: boolean) => void;
    /** Gives `control` DOM focus while the keyboard is still this grid's: DOM focus inside its root, or on nothing
     * (ADR-0204). An element that cannot take focus hands it to the first control inside it. */
    readonly takeFocus: (control: HTMLElement, options?: { readonly preventScroll?: boolean }) => void;
}

/** What the Cell Editor shows besides its text (ADR-0010, ADR-0034, ADR-0380). */
export interface CellEditorView {
    /** Overwrite, Excel's Enter mode, where the arrows commit and move; Caret, where they are the field's; or Point,
     * only where the Consumer declares it, where the arrows and the mouse point at cells and the Reference is written
     * at the caret (ADR-0051). F2 switches Overwrite and Caret, and from Point to Caret; typing ends Point. */
    readonly mode: 'overwrite' | 'caret' | 'point';
    /** The sentence a Reject or a refusal holds the editor with, while one does: the core has marked the field
     * `aria-invalid` and described it by the message, and the contents may paint their own error state. */
    readonly error: string | undefined;
    /** Whether the commit is pending (ADR-0380 section 3): the core has made the field read only, and the box busy. */
    readonly pending: boolean;
}

/**
 * What the Cell Editor's contents are handed (ADR-0010, ADR-0030). The core owns the box, its place over the cell,
 * the keys and the uncommitted text; the contents draw one text field, an `input` or a `textarea`, inside the slot,
 * the core's `ex-editor` box, which the field fills. The field is the core's to read and write: it opens holding
 * `initialText`, uncontrolled, and the core reads its value and places its caret, so what was typed is the core's
 * at the Enter that commits it, whatever the contents' framework has rendered (ADR-0010). Enter, Tab, Escape, F2
 * and Overwrite's arrows never reach the field: the root's capture listener takes them.
 */
export interface GridCellEditorSlotContext {
    /** The name of the column the cell is in. */
    readonly column: string;
    /** The column's type. */
    readonly columnType: ColumnType;
    /** The text the field opens holding: what was typed to open it, or the cell's text. */
    readonly initialText: string;
    /** What the editor shows now besides its text. */
    readonly view: () => CellEditorView;
    /** Hears each change to the view; answers the function that stops it. */
    readonly subscribe: (listener: () => void) => () => void;
    /** The core's request that the field take the keyboard as the editor opens: the contents answer it once drawn,
     * by handing the field to {@link GridCellEditorSlotContext.takeFocus}. */
    readonly focusRequest: number;
    /** Gives the editor the keyboard, the caret at the end of its text, while the keyboard is still this grid's
     * (ADR-0010, ADR-0204). `control` is the field, or an element holding it. */
    readonly takeFocus: (control: HTMLElement, options?: { readonly preventScroll?: boolean }) => void;
}

/**
 * What a cell's message is handed while it shows (ADR-0034): the Consumer's sentence for a flagged cell, or the
 * standing Reject's while an editor holds. The core decides when it shows and where, and supplies the text; the
 * contents draw it inside the slot, the core's `ex-message` box below the cell, which the editor names with
 * `aria-describedby`. A tooltip, not a live region: the root's one region has already said a Reject (ADR-0033). A
 * new message mounts the contents again.
 */
export interface GridCellMessageSlotContext {
    /** The name of the column the cell is in. */
    readonly column: string;
    /** What the cell's message says, as the Consumer or the Reject worded it. */
    readonly message: string;
    /** Whether it is the Reject an editor holds with, rather than a flagged cell's message. */
    readonly editorError: boolean;
    /** Takes a flagged cell's message away, as the pointer leaving does. A Reject stays while its editor holds. */
    readonly close: () => void;
}

/**
 * What the loading indicator is handed (ADR-0010): whether rows are on their way, and how many painted rows are
 * Placeholders. The Placeholder rows and the root's `ex-loading` class stay the core's (ADR-0004); the contents draw
 * what they add, a bar say, inside the slot, which stands at the root's foot, and draw nothing while nothing loads.
 * A change of either mounts the contents again.
 */
export interface GridLoadingIndicatorSlotContext {
    /** Whether rows are on their way. */
    readonly loading: boolean;
    /** How many of the painted rows are Placeholders. */
    readonly placeholderRows: number;
}

/**
 * What the Selection Summary is handed (ADR-0130): where it stands and the figures to show. The core decides when
 * the question is asked, which answer stands and how each figure reads; the contents paint them inside the slot, the
 * core's `ex-summary` box in the status line, which keeps the right-click that opens the figures menu. A change
 * mounts the contents again.
 */
export interface GridSelectionSummarySlotContext {
    /** Where the summary stands: while it is `pending`, the line says the words of `summaryLabelIds.pending`. */
    readonly status: SelectionSummaryStatus;
    /** The figures to show, in Excel's order, each with the id of its name and its text as the core formatted it;
     * empty unless the summary is answered. */
    readonly figures: readonly SummaryFigureShown[];
    /** The answerer's reason, shown in place of the figures while the summary is declined. */
    readonly declineReason: string | undefined;
    /** The summary's words, by the ids `summaryLabelIds` names: the Consumer's `commandLabel`, else the built-in
     * English. */
    readonly label: (id: string) => string;
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
    /** The find panel, and its Replace (ADR-0055, ADR-0332). */
    readonly findPanel?: GridChromeSlot<GridFindPanelSlotContext>;
    /** The Cell Editor's field (ADR-0010). */
    readonly cellEditor?: GridChromeSlot<GridCellEditorSlotContext>;
    /** A cell's message (ADR-0034). */
    readonly cellMessage?: GridChromeSlot<GridCellMessageSlotContext>;
    /** The loading indicator (ADR-0010). */
    readonly loadingIndicator?: GridChromeSlot<GridLoadingIndicatorSlotContext>;
    /** The Selection Summary in the status line (ADR-0130). */
    readonly selectionSummary?: GridChromeSlot<GridSelectionSummarySlotContext>;
}

/** The slots a grid's `chrome` names. */
const slotNames: readonly (keyof GridChrome<unknown>)[] = [
    'columnMenu', 'contextMenu', 'filterPanel', 'findPanel', 'cellEditor', 'cellMessage', 'loadingIndicator', 'selectionSummary',
];

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
 * A part a Chrome slot draws and the grid only shows (ADR-0390): it takes no keyboard, so asks for no focus. Its
 * contents are mounted with what it shows, mounted again on the same slot when that changes, which `key` tells, and
 * taken away when it hides. Contents standing keep the mount function they were mounted by, as an open menu keeps
 * its render: another one, the Consumer having handed another `chrome`, draws from the next change. The slot is an
 * element of its own, which the grid stands in the part's box in place of the built-in Chrome's text, and never
 * empties: the contents' framework may still be taking away what it drew there.
 */
export class ShownSlot<Context> {
    readonly #name: string;
    /** The slot, which the grid stands in the part's box while the contents draw it. */
    readonly element: HTMLElement;
    #key: string | undefined;
    #cleanup: (() => void) | undefined;
    // Counts the mounts, so a callback handed to one reaches nothing once another stands.
    #mounts = 0;
    // What failed to mount, which is not tried again on every pass: it is thrown once.
    #failed: { readonly mount: GridChromeSlot<Context>; readonly key: string } | undefined;

    constructor(name: string, element: HTMLElement) {
        this.#name = name;
        this.element = element;
    }

    /** Whether contents stand, mounted for `key`. */
    shows(key: string): boolean {
        return this.#cleanup !== undefined && this.#key === key;
    }

    /** Shows what `key` names by `mount`: the contents are mounted with `context`, unless they already stand for it.
     * `context` is handed whether the mount it is made for still stands, for a callback of its own to ask. */
    show(mount: GridChromeSlot<Context>, key: string, context: (standing: () => boolean) => Context): void {
        if (this.shows(key) || (this.#failed?.mount === mount && this.#failed.key === key)) return;
        this.hide();
        this.#mounts += 1;
        const made = this.#mounts;
        try {
            this.#cleanup = mounted(this.#name, mount, this.element, context(() => this.#cleanup !== undefined && this.#mounts === made));
        } catch (error) {
            this.#failed = { mount, key };
            throw error;
        }
        this.#key = key;
    }

    /** The part shows nothing of the contents: they are taken away. */
    hide(): void {
        const cleanup = this.#cleanup;
        this.#cleanup = undefined;
        this.#key = undefined;
        this.#failed = undefined;
        cleanup?.();
    }
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

    /** Mounts the contents, once. Contents that cannot be drawn close the part, rather than leave it standing empty. */
    mount(context: Context): void {
        if (this.#live && this.#cleanup === undefined) this.#draw(context);
    }

    /** A focus request: the contents are mounted again with `context`, which names it. */
    request(context: Context): void {
        if (!this.#live) return;
        this.#owed = true;
        this.#unmount();
        this.#draw(context);
    }

    /** An input the user made in the part: it moves the keyboard on from here, and no focus is owed any longer. */
    heard(): void {
        this.#owed = false;
    }

    /** `change` as an input the user made in the part: heard, answered once by `answered` (ADR-0220), and made;
     * nothing once the part is gone. */
    input<Args extends unknown[]>(answered: () => void, change: (...args: Args) => void): (...args: Args) => void {
        return (...args: Args): void => {
            if (!this.#live) return;
            this.heard();
            answered();
            change(...args);
        };
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

    #draw(context: Context): void {
        try {
            this.#cleanup = mounted(this.#name, this.#mount, this.#slot, context);
        } catch (error) {
            this.#cancel();
            throw error;
        }
    }

    // The contents go, and a popup of theirs with them: they are not asked to say so.
    #unmount(): void {
        const cleanup = this.#cleanup;
        this.#cleanup = undefined;
        this.innerPopupOpen = false;
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
        const set = <Args extends unknown[]>(change: (...args: Args) => void): ((...args: Args) => void) => slot.input(answers.answered, change);
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

/**
 * The find panel, drawn by a Chrome slot (ADR-0390): what it holds is the grid's `FindHeld`, which the built-in panel
 * writes too, and a step, a Replace and a Replace All are the grid's. The slot stands inside the core's form, between
 * the core's sentinels; each request for the field mounts the contents again with the next count.
 */
export class SlottedFindPanel implements FindPlace {
    readonly element: HTMLElement;
    readonly #slot: SlotMount<GridFindPanelSlotContext>;
    readonly #held: FindHeld;
    readonly #title: () => string;
    readonly #listeners = new Set<() => void>();
    readonly #context: Omit<GridFindPanelSlotContext, 'focusRequest'>;
    #focusRequest = 0;
    // Whether the request standing selects the field's text: Ctrl+F's does, a Tab off either end does not (FD-1).
    #selecting = false;
    #replaceShown = false;
    #waiting = false;
    #outcome = '';
    #view: FindPanelView | undefined;

    constructor(doc: Document, mount: GridChromeSlot<GridFindPanelSlotContext>, held: FindHeld, label: (id: string) => string,
        answers: FindPanelAnswers & { readonly close: () => void }, takeFocus: ChromeFocus) {
        this.#held = held;
        this.#title = () => label(this.#replaceShown ? findPanelLabelIds.replaceTitle : findPanelLabelIds.title);
        this.element = findElement(doc, this.#title());
        const body = doc.createElement('div');
        body.className = 'ex-popover-find-body ex-popover-body';
        // Enter in a field is an input the user made in the panel, as each change is: no focus is owed after it.
        const form = findForm(doc, { ...answers, answered: () => { this.#slot.heard(); answers.answered(); } });
        const slot = doc.createElement('div');
        slot.className = 'ex-find-slot';
        form.append(slot);
        body.append(form);
        // Tab off either end comes back to the field as it was, as under the built-in panel.
        const toField = (): void => { this.#request(false); };
        this.element.append(findSentinel(doc, answers.answered, toField), body, findSentinel(doc, answers.answered, toField));
        // Ctrl+F or Ctrl+H puts the keyboard on the field with its text selected, so what is typed next replaces the
        // text (FD-1).
        const focusing: ChromeFocus = (control, preventScroll, owed) => {
            takeFocus(control, preventScroll, owed);
            const active = doc.activeElement;
            if (owed && this.#selecting && (active instanceof HTMLInputElement || active instanceof HTMLTextAreaElement) && slot.contains(active)) {
                active.select();
            }
        };
        const contents = new SlotMount('findPanel', mount, slot, focusing, answers.close, false);
        this.#slot = contents;
        // Each change and step is an input the user made in the panel, answered once (ADR-0220).
        const act = <Args extends unknown[]>(change: (...args: Args) => void): ((...args: Args) => void) => contents.input(answers.answered, change);
        const holding = (change: () => void): void => {
            change();
            answers.heldChanged();
            this.#changed();
        };
        const text = (name: string, set: (text: string) => void) => act((value: string) => {
            if (typeof value !== 'string') throw new TypeError(`${name} is handed the text typed.`);
            holding(() => { set(value); });
        });
        const option = (name: string, set: (on: boolean) => void) => act((on: boolean) => {
            if (typeof on !== 'boolean') throw new TypeError(`${name} is handed whether the option is ticked.`);
            holding(() => { set(on); });
        });
        this.#context = {
            view: () => this.#made(),
            subscribe: listener => {
                if (typeof listener !== 'function') throw new TypeError('subscribe is handed the function that hears each change to the view.');
                this.#listeners.add(listener);
                return () => { this.#listeners.delete(listener); };
            },
            label,
            setText: text('setText', value => { held.text = value; }),
            setMatchCase: option('setMatchCase', on => { held.matchCase = on; }),
            setWholeCell: option('setWholeCell', on => { held.wholeCell = on; }),
            setReplaceText: text('setReplaceText', value => { held.replaceText = value; }),
            next: act(() => { answers.step(false); }),
            previous: act(() => { answers.step(true); }),
            replace: act(() => { answers.replaceOne(); }),
            replaceAll: act(() => { answers.replaceAll(); }),
            close: act(() => { answers.close(); }),
            innerPopupChanged: contents.innerPopupChanged,
            takeFocus: contents.takeFocus,
        };
    }

    get replaceShown(): boolean {
        return this.#replaceShown;
    }

    get innerPopupOpen(): boolean {
        return this.#slot.innerPopupOpen;
    }

    showReplace(shown: boolean): void {
        if (shown === this.#replaceShown) return;
        this.#replaceShown = shown;
        this.element.setAttribute('aria-label', this.#title());
        if (shown) this.element.setAttribute('data-ex-replace', '');
        else this.element.removeAttribute('data-ex-replace');
        this.#changed();
    }

    waitForReplace(waiting: boolean): void {
        if (waiting === this.#waiting) return;
        this.#waiting = waiting;
        this.#changed();
    }

    say(text: string): void {
        if (text === this.#outcome) return;
        this.#outcome = text;
        this.#changed();
    }

    /** Mounts the contents with the next request for the field, whose text is selected. */
    focusField(): void {
        this.#request(true);
    }

    dispose(): void {
        this.#slot.dispose();
        this.#listeners.clear();
    }

    #request(selecting: boolean): void {
        if (!this.#slot.live) return;
        this.#focusRequest += 1;
        this.#selecting = selecting;
        this.#slot.request(Object.freeze({ ...this.#context, focusRequest: this.#focusRequest }));
    }

    #made(): FindPanelView {
        this.#view ??= Object.freeze({
            text: this.#held.text, matchCase: this.#held.matchCase, wholeCell: this.#held.wholeCell, replaceShown: this.#replaceShown,
            replaceText: this.#held.replaceText, replaceWaiting: this.#waiting, outcome: this.#outcome,
        });
        return this.#view;
    }

    #changed(): void {
        this.#view = undefined;
        for (const listener of this.#listeners) listener();
    }
}

/** A text field the core reads and writes. */
export type EditorField = HTMLInputElement | HTMLTextAreaElement;

/**
 * The Cell Editor, drawn by a Chrome slot (ADR-0390): the box is the core's, standing over the cell as the built-in
 * editor does, and the contents draw the field inside it and hand it over through `takeFocus`, as they draw it and
 * whenever they draw it anew. The core reads the field's text and places its caret (ADR-0010); until a field is
 * handed over, the text is the one the editor opened with, or one the core wrote since, which a field is handed as
 * it is taken. Mounted once, its focus owed: the core gives the first field the keyboard as it gives its own editor.
 */
export class SlottedEditor {
    readonly box: HTMLElement;
    readonly #slot: SlotMount<GridCellEditorSlotContext>;
    readonly #context: GridCellEditorSlotContext;
    readonly #view: () => CellEditorView;
    readonly #taken: (field: EditorField) => void;
    readonly #listeners = new Set<() => void>();
    // The text as the core last knew it: the opening text, what the field held at its last input, or what the core
    // wrote. A field handed over anew is handed it.
    #text: string;
    #field: EditorField | undefined;
    #shown: CellEditorView | undefined;

    /** `taken` hears each field handed over, for the core to make it what its editor is now: read only while a commit
     * is pending, say. */
    constructor(doc: Document, mount: GridChromeSlot<GridCellEditorSlotContext>, opened: { readonly column: string; readonly columnType: ColumnType;
        readonly initialText: string; }, view: () => CellEditorView, takeFocus: ChromeFocus, cancel: () => void, taken: (field: EditorField) => void) {
        this.box = doc.createElement('div');
        this.#text = opened.initialText;
        this.#view = view;
        this.#taken = taken;
        this.#slot = new SlotMount('cellEditor', mount, this.box, takeFocus, cancel, true);
        // What is typed is kept, for a field the contents draw anew.
        this.box.addEventListener('input', event => {
            if (event.target === this.#field) this.#text = this.#field.value;
        });
        this.#context = Object.freeze({
            ...opened,
            view: () => this.#made(),
            subscribe: (listener: () => void) => {
                if (typeof listener !== 'function') throw new TypeError('subscribe is handed the function that hears each change to the view.');
                this.#listeners.add(listener);
                return () => { this.#listeners.delete(listener); };
            },
            focusRequest: 1,
            takeFocus: (control: HTMLElement, options?: { readonly preventScroll?: boolean }) => {
                if (!(control instanceof HTMLElement) || !this.box.contains(control) || control === this.box) {
                    throw new TypeError('takeFocus is handed the field the cell editor drew, or an element holding it, inside the slot.');
                }
                const field = control instanceof HTMLInputElement || control instanceof HTMLTextAreaElement ? control
                    : control.querySelector<EditorField>('input, textarea');
                if (field === null) throw new Error('The cell editor\'s contents handed over no text field: draw one input or textarea in the slot.');
                if (!this.#slot.live) return;
                if (field !== this.#field) {
                    this.#field = field;
                    if (field.value !== this.#text) field.value = this.#text;
                    this.#taken(field);
                }
                this.#slot.takeFocus(field, options);
            },
        });
    }

    /** Mounts the contents. */
    open(): void {
        this.#slot.mount(this.#context);
    }

    /** The field the contents handed over, while it stands; undefined before. */
    field(): EditorField | undefined {
        const field = this.#field;
        return field !== undefined && field.isConnected && this.box.contains(field) ? field : undefined;
    }

    /** The editor's text. Read for a commit, it is refused while the contents have handed over no field: what they
     * show is then not what would be written (the spine's first rule). */
    get text(): string {
        // Before a field is handed over, the text is the one the core holds (ADR-0010).
        return this.field()?.value ?? this.#text;
    }

    /** Writes the editor's text, as the core writes its own editor's: the field's, where one stands. */
    set text(text: string) {
        this.#text = text;
        const field = this.field();
        if (field !== undefined && field.value !== text) field.value = text;
    }

    /** The text to commit: the field's, which must stand. */
    committed(): string {
        const field = this.field();
        if (field === undefined) throw new Error('The cell editor\'s contents drew no text field to commit: hand the field to takeFocus once drawn (ADR-0390).');
        return field.value;
    }

    /** Tells the contents the view, where it changed since they last heard it. */
    review(): void {
        const view = this.#view();
        const shown = this.#shown;
        if (shown !== undefined && shown.mode === view.mode && shown.error === view.error && shown.pending === view.pending) return;
        this.#shown = Object.freeze(view);
        if (shown === undefined) return;
        for (const listener of this.#listeners) listener();
    }

    /** The editor is going: its contents are taken away. */
    dispose(): void {
        this.#slot.dispose();
        this.#listeners.clear();
    }

    #made(): CellEditorView {
        this.#shown ??= Object.freeze(this.#view());
        return this.#shown;
    }
}
