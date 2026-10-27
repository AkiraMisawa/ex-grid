// The Chrome slots for React (ADR-0350 section 3, ADR-0390 section 3): a Wrapper, or a Consumer's own design
// system, renders a part of the grid's chrome in place of the built-in one, as a render function of what the core
// hands the slot. The ExGrid draws it through a portal into the slot the core made. What the part means stays
// the core's: the hooks here carry a key, a click and a focus request to the core and do what it answers, so a
// Wrapper draws its own components and decides nothing.

import {
    useLayoutEffect, useRef, useState, useSyncExternalStore, type FocusEvent as ReactFocusEvent, type KeyboardEvent as ReactKeyboardEvent,
    type MouseEvent as ReactMouseEvent, type ReactNode, type SyntheticEvent,
} from 'react';
import type {
    FilterPanelView, GridChrome, GridColumnMenuSlotContext, GridContextMenuSlotContext, GridFilterPanelSlotContext, GridMenuSlotContext,
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
}

/** The ExGrid's Chrome as the core takes it: each slot's render function as a mount function that renders it
 * through the ExGrid's portals (ADR-0350 section 9). Which slots there are is the core's to check, as it checks a
 * mount function handed it directly; a render function that is not a function is named here, in React's terms. */
export function chromeMounts<Row>(given: ExGridChrome<Row> | undefined, portals: GridPortals): GridChrome<Row> | undefined {
    // Checked as a script may hand it, whatever its type says.
    const chrome: unknown = given;
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) {
        throw new TypeError('chrome is an object of Chrome slots, each a render function of its context (ADR-0390).');
    }
    return Object.fromEntries(Object.entries(chrome).map(([name, render]: [string, unknown]) => {
        if (render === undefined) return [name, undefined];
        if (typeof render !== 'function') throw new TypeError(`chrome.${name} is a render function of the slot's context (ADR-0390).`);
        return [name, (slot: HTMLElement, context: unknown) => portals.mount(slot, () => {
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the core hands each slot's mount that slot's context, the one its render function takes
            const node = Reflect.apply(render, undefined, [context]) as ReactNode;
            return node;
        })];
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
        const first = context.commands.findIndex(command => command.enabled);
        if (first >= 0) context.takeFocus(drawn(first), { preventScroll: true });
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
                if (!within(event)) return;
                event.preventDefault();
                const meaning = context.resolveKey(event.key, event.shiftKey, event.ctrlKey || event.altKey || event.metaKey);
                if (meaning.kind === 'move') {
                    context.takeFocus(drawn(meaning.item));
                } else if (meaning.kind === 'run') {
                    const run = context.commands[meaning.item];
                    if (run === undefined) throw new Error(`Internal: the core ran item ${meaning.item} of ${context.commands.length}.`);
                    run.invoke();
                } else if (meaning.kind === 'close') {
                    context.close();
                }
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
        const target = context.focusTarget;
        if (target === undefined || answeredRequests.has(context)) return;
        answeredRequests.add(context);
        const control = controls.current[target];
        if (control === null) {
            throw new Error(`The filter panel's ${target} control is not drawn: hand useFilterPanel's \`${target}\` ref to the control the core asks for.`);
        }
        context.takeFocus(control);
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
            if (!within(event) || event.nativeEvent.isComposing) return;
            if (context.valueListKey(event.key, event.ctrlKey || event.altKey || event.metaKey)) event.preventDefault();
        },
    };
}

type FocusTarget = NonNullable<GridFilterPanelSlotContext['focusTarget']>;

/** The contexts whose focus request has been answered. Each request comes in a context of its own, so one answered is
 * never answered again: not by a component of the contents mounted after it, nor by an effect run twice. */
const answeredRequests = new WeakSet<GridFilterPanelSlotContext>();
