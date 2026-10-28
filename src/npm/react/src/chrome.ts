// The Chrome slots for React (ADR-0350 section 3, ADR-0390 section 3): a Wrapper, or a Consumer's own design
// system, renders a part of the grid's chrome in place of the built-in one, as a render function of what the core
// hands the slot. The ExGrid draws it through a portal into the slot the core made. What the part means stays
// the core's: the hooks here carry a key, a click and a focus request to the core and do what it answers, so a
// Wrapper draws its own components and decides nothing.

import {
    useLayoutEffect, useRef, useState, useSyncExternalStore, type FocusEvent as ReactFocusEvent, type KeyboardEvent as ReactKeyboardEvent,
    type MouseEvent as ReactMouseEvent, type ReactNode, type SyntheticEvent,
} from 'react';
import { answerMenuKey, answerValueListKey, takeFilterFocus, takeFindFocus, takeMenuFocus } from '@ex-grid/core';
import type {
    CellEditorView, FilterPanelView, FindPanelView, FormulaBarTextView, GridCellEditorSlotContext, GridCellMessageSlotContext, GridChrome,
    GridColumnMenuSlotContext, GridContextMenuSlotContext, GridEditorCompletionSlotContext, GridFilterPanelSlotContext, GridFindPanelSlotContext,
    GridFormulaBarTextSlotContext, GridLoadingIndicatorSlotContext, GridMenuSlotContext, GridNameBoxSlotContext, GridSelectionSummarySlotContext,
} from '@ex-grid/core';
import type { GridPortals } from './portals';

/**
 * The Chrome slots an ExGrid takes, each a render function of what the core hands that slot (ADR-0390 section 3).
 * A slot not given is drawn by the built-in Chrome. A render function is read when its part opens: a part standing
 * keeps the render it opened with. Its answer is rendered as a component of its own, so it may hold state, kept
 * while the part stands, and a hook belongs in a component it renders.
 */
export interface ExGridChrome<Row> {
    /** The column menu's commands (ADR-0010, ADR-0044), drawn with {@link useMenuItems}. */
    readonly columnMenu?: (context: GridColumnMenuSlotContext) => ReactNode;
    /** The Context Menu (ADR-0036), drawn with {@link useMenuItems}. */
    readonly contextMenu?: (context: GridContextMenuSlotContext<Row>) => ReactNode;
    /** A column's filter panel, under the column menu's commands (ADR-0009, ADR-0044), drawn with {@link useFilterPanel}. */
    readonly filterPanel?: (context: GridFilterPanelSlotContext) => ReactNode;
    /** The find panel and its Replace (ADR-0055, ADR-0332), drawn with {@link useFindPanel}. */
    readonly findPanel?: (context: GridFindPanelSlotContext) => ReactNode;
    /** The Cell Editor's field (ADR-0010), drawn with {@link useCellEditor}. */
    readonly cellEditor?: (context: GridCellEditorSlotContext) => ReactNode;
    /** A cell's message (ADR-0034): its text, drawn in the core's box below the cell. */
    readonly cellMessage?: (context: GridCellMessageSlotContext) => ReactNode;
    /** The loading indicator (ADR-0010), at the grid's foot: nothing while nothing loads. */
    readonly loadingIndicator?: (context: GridLoadingIndicatorSlotContext) => ReactNode;
    /** The Selection Summary's figures (ADR-0130), drawn in the core's box in the status line. */
    readonly selectionSummary?: (context: GridSelectionSummarySlotContext) => ReactNode;
    /** The Formula Bar's text field (ADR-0051), drawn with {@link useFormulaBarText}. Another render function draws
     * the field again: hold the function in a constant or a memo, not inline. */
    readonly formulaBarText?: (context: GridFormulaBarTextSlotContext) => ReactNode;
    /** The Formula Bar's Name Box (ADR-0051), drawn with {@link useNameBox}. */
    readonly nameBox?: (context: GridNameBoxSlotContext) => ReactNode;
    /** The completion list and argument hint beneath the editor (ADR-0051), drawn with {@link useEditorCompletion}. */
    readonly editorCompletion?: (context: GridEditorCompletionSlotContext) => ReactNode;
}

/** The mount functions made for an ExGrid's render functions, by slot: one handed again is mounted by the same. */
export type ChromeMountsMade = Map<string, {
    readonly render: unknown; readonly portals: GridPortals; readonly mount: (slot: HTMLElement, context: unknown) => () => void;
}>;

/** The ExGrid's Chrome as the core takes it: each slot's render function as a mount function that renders it
 * through the ExGrid's portals (ADR-0350 section 9). A render function handed again in another `chrome` keeps the
 * mount function `made` holds for it, so a part the core keeps standing, the Formula Bar's fields, is not drawn again.
 * Which slots there are is the core's to check, as it checks a mount function handed it directly; a render function
 * that is not a function is named here, in React's terms. Exported for the family's Framework Adapters, whose products
 * hand ExGrid's Chrome on to the grids they draw, not for a Consumer. */
export function chromeMounts<Row>(given: ExGridChrome<Row> | undefined, portals: GridPortals, made: ChromeMountsMade): GridChrome<Row> | undefined {
    // Checked as a script may hand it, whatever its type says.
    const chrome: unknown = given;
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) {
        throw new TypeError('chrome is an object of Chrome slots, each a render function of its context (ADR-0390).');
    }
    return Object.fromEntries(Object.entries(chrome).map(([name, render]: [string, unknown]) => {
        if (render === undefined) return [name, undefined];
        if (typeof render !== 'function') throw new TypeError(`chrome.${name} is a render function of the slot's context (ADR-0390).`);
        const before = made.get(name);
        if (before?.render === render && before.portals === portals) return [name, before.mount];
        const mount = (slot: HTMLElement, context: unknown): (() => void) => portals.mount(slot, () => {
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the core hands each slot's mount that slot's context, the one its render function takes
            const node = Reflect.apply(render, undefined, [context]) as ReactNode;
            return node;
        });
        made.set(name, { render, portals, mount });
        return [name, mount];
    }));
}

/** The props {@link useMenuItems} hands each item: spread them onto the control that draws it. */
export interface MenuItemProps {
    /** The item's role in the menu the core named. */
    readonly role: 'menuitem';
    /** Takes the control the item is drawn as, which the hook focuses. */
    readonly ref: (control: HTMLElement | null) => void;
    /** Whether the command may not run: a disabled item is shown, and never focused or run. */
    readonly disabled: boolean;
    /** The same, for assistive technology, on a control that is not a button. */
    readonly 'aria-disabled': boolean;
    /** Focusable by the menu, never a tab stop: Tab is the menu's key (ADR-0039). */
    readonly tabIndex: -1;
    /** Marks an enabled item as one of the grid's own controls whose click and keys it takes in turn (ADR-0240). */
    readonly 'data-ex-claims': 'click keydown' | undefined;
    /** Hands every key on the item to the core, and does what it answers. */
    readonly onKeyDown: (event: ReactKeyboardEvent) => void;
    /** Runs the command, as the core runs it. */
    readonly onClick: (event: ReactMouseEvent) => void;
    /** Tells the core the item took DOM focus, so the menu's place follows it (ADR-0039). */
    readonly onFocus: (event: ReactFocusEvent) => void;
}

/**
 * Draws a menu slot's items from its context (ADR-0390 section 3): answers the props of the item at `index` of
 * `context.commands`, one item per command, in that order. Every key on an item is the core's table against the
 * place the core keeps, and its default is prevented: a move focuses the item it names, a run invokes the command,
 * a close closes the menu, and a move into the column's filter the core has made. The first enabled item takes the
 * keyboard at each focus request, without scrolling the menu (ADR-0039).
 */
export function useMenuItems(context: GridMenuSlotContext): (index: number) => MenuItemProps {
    const items = useRef<(HTMLElement | null)[]>([]);
    const answered = useRef<number | undefined>(undefined);
    // The control item `index` is drawn as: one the contents never drew with these props can take no keyboard.
    const drawn = (index: number): HTMLElement => {
        const item = items.current[index];
        if (item === undefined || item === null) {
            throw new Error(`The menu's item ${index} is not drawn: spread useMenuItems' props onto the control of every command, and forward its ref.`);
        }
        return item;
    };
    useLayoutEffect(() => {
        if (answered.current === context.focusRequest) return;
        answered.current = context.focusRequest;
        takeMenuFocus(context, drawn);
    }, [context]);
    return (index: number): MenuItemProps => {
        const command = context.commands[index];
        if (command === undefined) {
            throw new RangeError(`useMenuItems is asked for item ${index} of a menu of ${context.commands.length} commands.`);
        }
        return {
            role: 'menuitem',
            ref: control => { items.current[index] = control; },
            disabled: !command.enabled,
            'aria-disabled': !command.enabled,
            tabIndex: -1,
            'data-ex-claims': command.enabled ? 'click keydown' : undefined,
            onKeyDown: event => {
                // Only the item's own: an event from a popup the item opened through a portal reaches it in React's
                // tree, but is not inside it in the page, and is that popup's.
                if (within(event)) answerMenuKey(context, event, drawn);
            },
            onClick: event => { if (within(event)) command.invoke(); },
            onFocus: event => { if (within(event)) context.itemFocused(index); },
        };
    };
}

/** Whether an event happened inside the item it reached, in the page and not only in React's tree. */
function within(event: SyntheticEvent): boolean {
    return event.target instanceof Node && event.currentTarget instanceof Node && event.currentTarget.contains(event.target);
}

/** What {@link useFilterPanel} hands a filter panel's contents. */
export interface FilterPanelHandles {
    /** What the panel shows now, drawn again at each change the core makes to it. */
    readonly view: FilterPanelView;
    /** Takes the panel's first control: Tab on a command focuses it (ADR-0044). */
    readonly first: (control: HTMLElement | null) => void;
    /** Takes the panel's last control: Shift+Tab on a command wraps there (ADR-0044). */
    readonly last: (control: HTMLElement | null) => void;
    /** Takes the field a search is typed into, Excel's E: the value list's search box, or the first condition's
     * value where no list stands (ADR-0044). */
    readonly search: (control: HTMLElement | null) => void;
    /** The value list's key handler, on the element that holds its checkboxes and "(Select All)", never on a text
     * field: Excel's letters run their command through the core, and their default is prevented (ADR-0044). */
    readonly onValueListKeyDown: (event: ReactKeyboardEvent) => void;
}

/**
 * Draws a filter panel slot from its context (ADR-0390 section 3): the core's view, read again at each change, and
 * the control the core asks for the keyboard. Each choice goes to the context's setter, which is what it means; the
 * hook focuses the control a request names, through the core, once it is drawn, and answers each request once,
 * whichever component of the contents holds the hook when it comes.
 */
export function useFilterPanel(context: GridFilterPanelSlotContext): FilterPanelHandles {
    const view = useSyncExternalStore(context.subscribe, context.view);
    const controls = useRef<Record<FocusTarget, HTMLElement | null>>({ first: null, last: null, search: null });
    useLayoutEffect(() => {
        if (context.focusTarget === undefined || answeredRequests.has(context)) return;
        answeredRequests.add(context);
        takeFilterFocus(context, target => {
            const control = controls.current[target];
            return control === null ? undefined : control;
        });
    }, [context]);
    // Made once, so a render hands React the same refs and it does not take them away and back.
    const [refs] = useState(() => {
        const taking = (target: FocusTarget) => (control: HTMLElement | null): void => { controls.current[target] = control; };
        return { first: taking('first'), last: taking('last'), search: taking('search') };
    });
    return {
        view,
        ...refs,
        onValueListKeyDown: event => {
            if (within(event)) answerValueListKey(context, event.nativeEvent);
        },
    };
}

type FocusTarget = NonNullable<GridFilterPanelSlotContext['focusTarget']>;

/** The contexts whose focus request has been answered. Each request comes in a context of its own, so one answered is
 * never answered again: not by a component of the contents mounted after it, nor by an effect run twice. */
const answeredRequests = new WeakSet<GridFilterPanelSlotContext>();

/** What {@link useFindPanel} hands a find panel's contents. */
export interface FindPanelHandles {
    /** What the panel shows now, drawn again at each change the core makes to it. */
    readonly view: FindPanelView;
    /** Takes the find field, which each request of the core's puts the keyboard on, its text selected (FD-1). */
    readonly field: (control: HTMLElement | null) => void;
}

/**
 * Draws the find panel slot from its context (ADR-0390 section 3): the core's view, read again at each change, and
 * the find field, which takes the keyboard at each request of the core's, answered once. Each change and step goes to
 * the context; Enter in a field is the core's form's, so the contents draw no form and no key handler of their own.
 */
export function useFindPanel(context: GridFindPanelSlotContext): FindPanelHandles {
    const view = useSyncExternalStore(context.subscribe, context.view);
    const control = useRef<HTMLElement | null>(null);
    useLayoutEffect(() => {
        if (answeredFinds.has(context)) return;
        answeredFinds.add(context);
        if (control.current === null) throw new Error("The find panel's field is not drawn: hand useFindPanel's `field` ref to the find field.");
        takeFindFocus(context, control.current);
    }, [context]);
    const [field] = useState(() => (element: HTMLElement | null): void => { control.current = element; });
    return { view, field };
}

/** The find panel's contexts whose request has been answered, as {@link answeredRequests} keeps the filter panel's. */
const answeredFinds = new WeakSet<GridFindPanelSlotContext>();

/** What {@link useCellEditor} hands the editor's contents. */
export interface CellEditorHandles {
    /** What the editor shows besides its text: the mode, a Reject's sentence, a pending commit. */
    readonly view: CellEditorView;
    /** Takes the text field, which the core reads and writes, and which takes the keyboard as the editor opens. */
    readonly field: (control: HTMLInputElement | HTMLTextAreaElement | null) => void;
    /** The text the field opens holding: hand it to the field as its default, and no value, since the text is the
     * core's to read and write (ADR-0010). */
    readonly defaultValue: string;
}

/**
 * Draws the Cell Editor's slot from its context (ADR-0390 section 3): one text field, uncontrolled, opening with the
 * core's text, which the core reads at each commit and writes where it types itself, as from the Formula Bar. The
 * field takes the keyboard once drawn, the caret at its end. Enter, Tab, Escape, F2 and Overwrite's arrows are the
 * core's, so the contents draw no key handler of their own.
 */
export function useCellEditor(context: GridCellEditorSlotContext): CellEditorHandles {
    const view = useSyncExternalStore(context.subscribe, context.view);
    const control = useRef<HTMLInputElement | HTMLTextAreaElement | null>(null);
    const handed = useRef<HTMLInputElement | HTMLTextAreaElement | null>(null);
    // After every render: the field drawn first is handed over for the keyboard the opening owes, and a field drawn
    // anew, an input swapped for a textarea say, is handed over too, so the core reads it and it keeps the keyboard.
    useLayoutEffect(() => {
        const field = control.current;
        if (!answeredEditors.has(context)) {
            answeredEditors.add(context);
            if (field === null) throw new Error("The cell editor's field is not drawn: hand useCellEditor's `field` ref to the text field.");
        } else if (field === null || field === handed.current) {
            return;
        }
        handed.current = field;
        context.takeFocus(field);
    });
    const [field] = useState(() => (element: HTMLInputElement | HTMLTextAreaElement | null): void => { control.current = element; });
    return { view, field, defaultValue: context.initialText };
}

/** The editors whose opening request has been answered: StrictMode runs an effect twice, and the keyboard is taken
 * once. */
const answeredEditors = new WeakSet<GridCellEditorSlotContext>();

/** Hands `takeField` the field `control` holds after every render where it is another than the one handed last: the
 * Formula Bar's fields are the core's to read and write, and take the keyboard only by the user's press. The first
 * render must draw it; a render that draws none for a while leaves the one handed last standing. */
function useHandedField(takeField: (control: HTMLElement) => void, name: string): (element: EditorField | null) => void {
    const control = useRef<EditorField | null>(null);
    const handed = useRef<EditorField | null>(null);
    useLayoutEffect(() => {
        const field = control.current;
        if (field === null && handed.current === null) throw new Error(`The ${name}'s field is not drawn: hand the hook's \`field\` ref to the text field.`);
        if (field === null || field === handed.current) return;
        handed.current = field;
        takeField(field);
    });
    const [field] = useState(() => (element: EditorField | null): void => { control.current = element; });
    return field;
}

/** A text field the core reads and writes. */
type EditorField = HTMLInputElement | HTMLTextAreaElement;

/** What {@link useFormulaBarText} hands the Formula Bar's text field. */
export interface FormulaBarTextHandles {
    /** What the field shows besides its text: whether it takes typing, and whether its text is borrowed. */
    readonly view: FormulaBarTextView;
    /** Takes the text field, which the core reads and writes. Draw it uncontrolled, with no value. */
    readonly field: (control: HTMLInputElement | HTMLTextAreaElement | null) => void;
}

/**
 * Draws the Formula Bar's text slot from its context (ADR-0390 section 3): one text field, uncontrolled, which the
 * core fills with the Focus cell's text, names and makes read only, and whose typing is the Cell Editor's (ADR-0051).
 * Enter, Tab, Escape and F2 are the core's, so the contents draw no key handler of their own.
 */
export function useFormulaBarText(context: GridFormulaBarTextSlotContext): FormulaBarTextHandles {
    const view = useSyncExternalStore(context.subscribe, context.view);
    const field = useHandedField(context.takeField, 'Formula Bar\'s text');
    return { view, field };
}

/** What {@link useNameBox} hands the Name Box. */
export interface NameBoxHandles {
    /** Takes the text input, which the core reads and writes. Draw it uncontrolled, with no value: an input, since
     * Enter in a textarea submits nothing. */
    readonly field: (control: HTMLInputElement | null) => void;
}

/**
 * Draws the Name Box's slot from its context (ADR-0390 section 3): one text field, uncontrolled, which the core
 * fills with the Focus's name. Enter is the core's form's and Escape the core's, so the contents draw no form and no
 * key handler of their own.
 */
export function useNameBox(context: GridNameBoxSlotContext): NameBoxHandles {
    return { field: useHandedField(context.takeField, 'Name Box') };
}

/** The props {@link useEditorCompletion} hands a candidate: spread them onto the element that draws it. */
export interface CompletionOptionProps {
    /** The id the editor names while the candidate is chosen. */
    readonly id: string;
    /** A candidate is an option of the list. */
    readonly role: 'option';
    /** Whether the candidate is the chosen one. */
    readonly 'aria-selected': boolean;
    /** A click accepts the candidate; the core's box takes the press, keeping the keyboard in the editor. */
    readonly onClick: () => void;
}

/** What {@link useEditorCompletion} hands the completion list. */
export interface EditorCompletionHandles {
    /** The props of the list: spread them onto the element holding the candidates. */
    readonly list: { readonly id: string; readonly role: 'listbox' };
    /** The props of the candidate at an index. */
    readonly option: (index: number) => CompletionOptionProps;
}

/**
 * Draws the completion slot from its context (ADR-0390 section 3): the ids the editor names, and a click on a
 * candidate accepting it. The core's box keeps the keyboard in the editor, and ↑, ↓, Tab and Escape are the
 * editor's, so the contents draw no key handler and take no focus.
 */
export function useEditorCompletion(context: GridEditorCompletionSlotContext): EditorCompletionHandles {
    return {
        list: { id: context.listId, role: 'listbox' },
        option: index => ({
            id: context.optionId(index),
            role: 'option',
            'aria-selected': index === context.selected,
            onClick: () => { context.accept(index); },
        }),
    };
}
