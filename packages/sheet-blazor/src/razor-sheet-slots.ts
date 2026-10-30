// ExSheet's own Chrome slots filled with Razor (ADR-0410): the kind of each of the Sheet's slots over the Shared
// Script's RazorSlotHost, beside the grid's kinds, which the Sheet's grid's slots take. Each kind says what the slot's
// context crosses to .NET as, and wires the controls the Razor drew from the attributes they carry (section 2): a press
// runs the context's call in the browser, and a Toolbar Item's list opens, moves and closes here, on the toolbar's
// requests, as the React hook's does. Nothing a key means waits on the circuit.

import { gridRazorSlotKinds, type HostedSlotKind, type RazorSlotKind } from '@ex-grid/blazor-shared';
import {
    toolbarChoiceMovedTo, toolbarChoiceRows, toolbarChoiceStart, type SheetTabSlotContext, type SheetToolbarContent, type SheetToolbarItemSlotContext,
    type SheetToolbarItemView,
} from '@ex-grid/sheet';
import { formatCellsKind } from './razor-format-cells';
import { crossed, marked } from './slot-markup';

/** The attributes the Razor of a Sheet slot writes on its controls (ADR-0410 section 2), as `SheetScriptSlotAttributes`
 * names them in .NET. */
export const sheetSlotAttributes = Object.freeze({
    /** On a Toolbar Item's button: what a press on it does, `invoke` (a toggle, a button, a split control's face, a
     * menu's face) or `list` (a menu's face, a split control's arrow, which opens the item's list). */
    press: 'data-ex-sheet-press',
    /** On the element that holds a Toolbar Item's choices, drawn whatever the list's state: shown while it is open. */
    list: 'data-ex-sheet-list',
    /** On a choice's control: the choice's index in the view's choices, once for every choice. */
    choice: 'data-ex-sheet-choice',
    /** On the empty element where a Consumer's button's own content goes, for an item that has some. */
    content: 'data-ex-sheet-content',
});

/** The Sheet's slots a Razor Chrome fills, as the Sheet names them. */
export type SheetRazorSlotName = 'toolbarItem' | 'formatCells' | 'sheetTab' | 'sheetTabAdd';

/** What a Toolbar Item's list holds across the renders .NET makes of its contents: whether it is open, the toolbar's
 * requests answered, and what a press elsewhere closes. */
interface ItemList {
    open: boolean;
    seenOpen: number;
    seenClose: number;
    /** The contents as last wired, what they show, the list they draw and the buttons that open it. */
    contents: HTMLElement | undefined;
    drawn: (() => SheetToolbarItemView) | undefined;
    holder: HTMLElement | undefined;
    openers: readonly HTMLElement[];
    /** Stops hearing presses outside the item, while the list is open. */
    outside: (() => void) | undefined;
    /** A Consumer's button's own content, where the contents drew its place: kept across the renders of the same
     * place, so it is not drawn again for each. */
    content: { readonly element: HTMLElement; readonly mounted: SheetToolbarContent; readonly remove: () => void } | undefined;
}

const itemLists = new WeakMap<SheetToolbarItemSlotContext, ItemList>();

function listOf(item: SheetToolbarItemSlotContext): ItemList {
    const known = itemLists.get(item);
    if (known !== undefined) return known;
    const made: ItemList = {
        open: false, seenOpen: 0, seenClose: 0, contents: undefined, drawn: undefined, holder: undefined, openers: [], outside: undefined, content: undefined,
    };
    itemLists.set(item, made);
    return made;
}

/** A Toolbar Item's kind: its view as data, less what the band keeps (the active mark) and what the script answers
 * (the toolbar's requests); its buttons pressed in the browser, and its list opened, moved and closed there. The
 * contents take the keyboard, since the list does, so every input waits until they stand. */
const toolbarItemKind: RazorSlotKind<SheetToolbarItemSlotContext, SheetToolbarItemView> = {
    takesKeyboard: true,
    data: item => {
        const view = item.view();
        return {
            json: JSON.stringify({
                id: item.id, kind: item.kind, name: view.name, tooltip: view.tooltip, icon: crossed(view.icon), text: crossed(view.text),
                swatch: crossed(view.swatch), moreName: crossed(view.moreName), pressed: view.pressed, disabledReason: crossed(view.disabledReason),
                choices: view.choices.map(choice => ({
                    label: choice.label, swatch: crossed(choice.swatch), selected: choice.selected, disabledReason: crossed(choice.disabledReason),
                })),
                // Laid out as the keys move, so the Razor draws the rows the keys walk and keeps no count of its own.
                rows: toolbarChoiceRows(view.choices).map(row => (typeof row === 'number' ? [row] : row)),
                content: view.content !== undefined,
            }),
            drawn: view,
            memo: undefined,
        };
    },
    subscribe: (item, again) => {
        const list = listOf(item);
        const stop = item.subscribe(() => {
            answerRequests(item, list);
            again();
        });
        // The item gone takes its list with it, as the React hook's state goes: mounted again, it stands closed.
        return () => {
            stop();
            list.open = false;
            list.outside?.();
            list.outside = undefined;
            list.contents = undefined;
            list.holder = undefined;
            list.openers = [];
            removeContent(list);
        };
    },
    wire: (contents, item, _answered, drawn) => wireToolbarItem(contents, item, drawn),
};

/** A Sheet Tab's face: the Sheet's name and whether it is shown, drawn; it takes no keyboard and hears nothing, so
 * nothing is wired. The Sheet mounts it again whenever either changes. */
const sheetTabKind: RazorSlotKind<SheetTabSlotContext> = {
    takesKeyboard: false,
    data: tab => ({ json: JSON.stringify({ name: tab.name, selected: tab.selected }), drawn: undefined, memo: undefined }),
    wire: () => () => undefined,
};

/** The face of the button that adds a Sheet: nothing to send, and nothing to wire. */
const sheetTabAddKind: RazorSlotKind<undefined> = {
    takesKeyboard: false,
    data: () => ({ json: '{}', drawn: undefined, memo: undefined }),
    wire: () => () => undefined,
};

/** A Consumer's ToolbarButton's ChildContent (ADR-0100), which .NET draws from the button's own markup, not from the
 * Chrome: the button by the number .NET gave it. It takes no keyboard and hears nothing. */
export interface ToolbarContentContext {
    readonly button: number;
}

/** The slot of a button's content, which no Chrome names: the core hands each button's content a mount of its own. */
export const toolbarContentSlotName = 'toolbarContent';

const toolbarContentKind: RazorSlotKind<ToolbarContentContext> = {
    takesKeyboard: false,
    data: context => ({ json: JSON.stringify({ button: context.button }), drawn: undefined, memo: undefined }),
    wire: () => () => undefined,
};

// Every slot a Razor Chrome fills has its kind: a name added to SheetRazorSlotName without one does not compile.
const sheetKindsByName = {
    toolbarItem: toolbarItemKind, formatCells: formatCellsKind, sheetTab: sheetTabKind, sheetTabAdd: sheetTabAddKind,
} satisfies Record<SheetRazorSlotName, HostedSlotKind>;

/** The kinds of the slots a Razor Chrome fills on a Sheet, by the core's slot names: the Sheet's own, and its grid's. */
export const sheetRazorSlotKinds: ReadonlyMap<string, HostedSlotKind> = new Map([
    ...gridRazorSlotKinds, ...Object.entries(sheetKindsByName), [toolbarContentSlotName, toolbarContentKind],
]);

/** Whether `name` is one of the Sheet's own slots a Razor Chrome fills. */
export function isSheetRazorSlotName(name: unknown): name is SheetRazorSlotName {
    return typeof name === 'string' && Object.hasOwn(sheetKindsByName, name);
}

/** The list the contents drew, and their choices by index. */
function listParts(contents: HTMLElement, view: SheetToolbarItemView): { readonly list: HTMLElement | undefined; readonly choices: ReadonlyMap<number, HTMLElement> } {
    const lists = marked(contents, sheetSlotAttributes.list);
    if (lists.length > 1) throw new Error(`A Toolbar Item draws ${String(lists.length)} lists: mark the one element holding its choices with ${sheetSlotAttributes.list}.`);
    const choices = new Map<number, HTMLElement>();
    for (const [control, value] of marked(contents, sheetSlotAttributes.choice)) {
        const index = Number(value);
        if (value === '' || !Number.isSafeInteger(index) || index < 0 || index >= view.choices.length || choices.has(index)) {
            throw new RangeError(`A Toolbar Item's choice is marked ${sheetSlotAttributes.choice}="${value}": each of its ${String(view.choices.length)} choices is marked once, by its index.`);
        }
        choices.set(index, control);
    }
    const list = lists[0]?.[0];
    if (choices.size !== view.choices.length) {
        throw new Error(`The Toolbar Item ${view.name} draws ${String(choices.size)} of its ${String(view.choices.length)} choices: mark the control of every choice with ${sheetSlotAttributes.choice}.`);
    }
    if (choices.size > 0 && list === undefined) throw new Error(`The Toolbar Item ${view.name} draws its choices in no element marked ${sheetSlotAttributes.list}.`);
    return { list, choices };
}

/** Keeps the keyboard where it is on a pointer press: on the Sheet, as Excel's ribbon leaves it in the cell. */
function keepKeyboard(event: Event): void {
    event.preventDefault();
}

/** Wires a Toolbar Item's contents as .NET drew them: each button's press, the list's keys and its choices. */
function wireToolbarItem(contents: HTMLElement, item: SheetToolbarItemSlotContext, drawn: () => SheetToolbarItemView): () => void {
    const list = listOf(item);
    list.contents = contents;
    list.drawn = drawn;
    const view = drawn();
    const unwire: (() => void)[] = [];
    const openers: HTMLElement[] = [];
    const listen = (element: HTMLElement, type: string, handler: (event: Event) => void): void => {
        element.addEventListener(type, handler);
        unwire.push(() => { element.removeEventListener(type, handler); });
    };
    for (const [button, value] of marked(contents, sheetSlotAttributes.press)) {
        if (value !== 'invoke' && value !== 'list') throw new RangeError(`A Toolbar Item's button is marked ${sheetSlotAttributes.press}="${value}": it says invoke or list.`);
        if (value === 'list' && item.kind !== 'menu' && item.kind !== 'split') throw new Error(`The Toolbar Item ${view.name} has no list for its button marked ${sheetSlotAttributes.press}="list" to open.`);
        // Out of the Tab order, the toolbar being one Tab stop, and its press taken in the Sheet's order (ADR-0240).
        button.tabIndex = -1;
        button.setAttribute('data-ex-claims', 'click');
        listen(button, 'mousedown', keepKeyboard);
        if (value === 'invoke') {
            listen(button, 'click', () => { item.invoke(); });
            continue;
        }
        button.setAttribute('aria-haspopup', 'menu');
        openers.push(button);
        // A press opens the list only where it can open, as the React hook's toggle: one that cannot does nothing, and
        // tells the Sheet nothing, which asked for nothing.
        listen(button, 'click', () => {
            if (list.open) dismiss(item, list);
            else if (canOpen(item, list)) openList(item, list);
        });
    }
    const { list: holder, choices } = listParts(contents, view);
    list.holder = holder;
    list.openers = openers;
    expanded(list);
    if (holder !== undefined) {
        holder.setAttribute('role', 'menu');
        holder.setAttribute('aria-label', view.name);
        holder.hidden = !list.open;
        if (list.open) place(contents, holder);
        listen(holder, 'mousedown', keepKeyboard);
        listen(holder, 'keydown', event => { if (event instanceof KeyboardEvent) listKey(item, list, event); });
    }
    for (const [index, control] of choices) {
        const choice = view.choices[index];
        if (choice === undefined) throw new Error(`Internal: the Toolbar Item's choice ${String(index)} has no view.`);
        control.setAttribute('role', 'menuitemradio');
        control.setAttribute('aria-checked', String(choice.selected));
        control.tabIndex = -1;
        if (choice.disabledReason === undefined) {
            control.removeAttribute('aria-disabled');
            // Its click and keys wait their turn behind a commit or an opening (ADR-0240): a disabled one hears no click,
            // and claims none, which would hold every input behind it.
            control.setAttribute('data-ex-claims', 'click keydown');
        } else {
            control.setAttribute('aria-disabled', 'true');
            control.removeAttribute('data-ex-claims');
        }
        listen(control, 'click', () => {
            if (drawn().choices[index]?.disabledReason === undefined) choose(item, list, index);
        });
    }
    placeContent(contents, view, list);
    // A request made before the contents stood is answered on them.
    answerRequests(item, list);
    return () => { for (const each of unwire) each(); };
}

/** A Consumer's button's own content, mounted where the contents drew its place, and taken away once that place, or the
 * content, is another. A button with content whose contents draw no place for it would quietly lose it, and is refused. */
function placeContent(contents: HTMLElement, view: SheetToolbarItemView, list: ItemList): void {
    const places = marked(contents, sheetSlotAttributes.content);
    if (places.length > 1) throw new Error(`The Toolbar Item ${view.name} draws ${String(places.length)} places for its content: mark one element with ${sheetSlotAttributes.content}.`);
    const element = places[0]?.[0];
    const content = view.content;
    if (content !== undefined && element === undefined) {
        throw new Error(`The Toolbar Item ${view.name} has content of its own, and its contents draw no place for it: draw an empty element marked ${sheetSlotAttributes.content}.`);
    }
    if (list.content?.element === element && list.content?.mounted === content) return;
    removeContent(list);
    if (content === undefined || element === undefined) return;
    list.content = { element, mounted: content, remove: content(element) };
}

function removeContent(list: ItemList): void {
    list.content?.remove();
    list.content = undefined;
}

/** The toolbar's requests: one to close reaches the list asked for, or any opened by a press; one to open opens it,
 * or, where it cannot open, is closed at once choosing nothing. Answered once the contents stand. */
function answerRequests(item: SheetToolbarItemSlotContext, list: ItemList): void {
    if (list.contents === undefined) return;
    const view = item.view();
    if (view.closeRequest !== list.seenClose) {
        list.seenOpen = view.openRequest;
        list.seenClose = view.closeRequest;
        closeList(list);
        return;
    }
    if (view.openRequest === list.seenOpen) return;
    list.seenOpen = view.openRequest;
    openList(item, list);
}

/** Whether the item's list can open now: it stands, the item can be used, and it has choices. */
function canOpen(item: SheetToolbarItemSlotContext, list: ItemList): boolean {
    const view = list.drawn?.();
    return view !== undefined && list.contents !== undefined && item.view().disabledReason === undefined && view.choices.length > 0;
}

/** Tells assistive technology whether the list is open, on the buttons that open it. */
function expanded(list: ItemList): void {
    for (const button of list.openers) button.setAttribute('aria-expanded', String(list.open));
}

/** Opens the list, the keyboard on the choice the Focus cell has; an item that cannot open it says so at once. */
function openList(item: SheetToolbarItemSlotContext, list: ItemList): void {
    const view = list.drawn?.();
    const contents = list.contents;
    if (view === undefined || contents === undefined || !canOpen(item, list)) {
        item.choicesClosed(undefined, false);
        return;
    }
    const { list: holder, choices } = listParts(contents, view);
    if (holder === undefined) throw new Error('Internal: a Toolbar Item with choices stands with no list.');
    list.open = true;
    expanded(list);
    holder.hidden = false;
    place(contents, holder);
    const start = toolbarChoiceStart(view.choices);
    if (start !== undefined) choices.get(start)?.focus({ preventScroll: true });
    // A press outside the item closes its list, heard from the opening, so a press made at once after it is heard too;
    // the page scrolled or resized moves it with its item.
    if (list.outside === undefined) {
        const doc = contents.ownerDocument;
        const win = doc.defaultView;
        const outside = (event: Event): void => {
            if (!(event.target instanceof Node) || list.contents?.contains(event.target) !== true) dismiss(item, list);
        };
        const moved = (event: Event): void => {
            const standing = list.contents;
            const shown = list.open ? list.holder : undefined;
            if (standing === undefined || shown === undefined || (event.target instanceof Node && shown.contains(event.target))) return;
            place(standing, shown);
        };
        doc.addEventListener('mousedown', outside, true);
        doc.addEventListener('touchstart', outside, true);
        doc.addEventListener('scroll', moved, true);
        win?.addEventListener('resize', moved);
        list.outside = () => {
            doc.removeEventListener('mousedown', outside, true);
            doc.removeEventListener('touchstart', outside, true);
            doc.removeEventListener('scroll', moved, true);
            win?.removeEventListener('resize', moved);
        };
    }
}

/**
 * Places the list at its item's foot, fixed, so that the band, which clips what its rows hold, does not cut it, as a
 * React Wrapper's floating list stands. Read as the list opens, and as the page scrolls or is resized while it is open:
 * the item's place is the browser's to tell, and nothing on the grid's way to a paint waits for it. An ancestor that
 * makes itself the frame fixed elements stand in, by a transform, moves the list by its own offset, which is taken off.
 */
function place(contents: HTMLElement, holder: HTMLElement): void {
    const item = contents.getBoundingClientRect();
    holder.style.position = 'fixed';
    holder.style.left = `${String(item.left)}px`;
    holder.style.top = `${String(item.bottom)}px`;
    const stood = holder.getBoundingClientRect();
    if (stood.left === item.left && stood.top === item.bottom) return;
    holder.style.left = `${String((2 * item.left) - stood.left)}px`;
    holder.style.top = `${String((2 * item.bottom) - stood.top)}px`;
}

/** Hides the list, telling nothing. */
function closeList(list: ItemList): void {
    list.open = false;
    list.outside?.();
    list.outside = undefined;
    expanded(list);
    if (list.holder !== undefined) list.holder.hidden = true;
}

/** A press elsewhere closes the list; the Sheet hears it only while the list holds the keyboard, which then goes back
 * to it, and a control pressed outside keeps the keyboard it took. */
function dismiss(item: SheetToolbarItemSlotContext, list: ItemList): void {
    const holder = list.holder;
    const held = holder?.contains(holder.ownerDocument.activeElement) === true;
    if (held) item.choicesClosed(undefined, false);
    closeList(list);
}

/** A choice made: handed back while the keyboard is still in the list, which the Sheet takes on, and the list closes. */
function choose(item: SheetToolbarItemSlotContext, list: ItemList, index: number): void {
    item.choicesClosed(index, false);
    closeList(list);
}

/** The list's keys, the built-in list's: none of them reaches the Sheet. */
function listKey(item: SheetToolbarItemSlotContext, list: ItemList, event: KeyboardEvent): void {
    const contents = list.contents;
    const view = list.drawn?.();
    if (contents === undefined || view === undefined) return;
    const { choices } = listParts(contents, view);
    const active = contents.ownerDocument.activeElement;
    let at = -1;
    for (const [index, control] of choices) if (control === active) at = index;
    event.preventDefault();
    event.stopPropagation();
    if (event.key === 'Escape') {
        item.choicesClosed(undefined, true);
        closeList(list);
    } else if (event.key === 'Enter' || event.key === ' ') {
        if (at >= 0 && view.choices[at]?.disabledReason === undefined) choose(item, list, at);
    } else {
        const to = toolbarChoiceMovedTo(event.key, at, view.choices);
        if (to !== undefined) choices.get(to)?.focus({ preventScroll: true });
    }
}
