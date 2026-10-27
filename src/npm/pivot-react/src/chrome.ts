// ExPivot's Chrome slots for React (ADR-0061, ADR-0390 section 3): a Wrapper, or a Consumer's own design system,
// renders a part of the pivot in place of the built-in markup, as a render function of what the pivot hands the
// slot. The ExPivot draws it through a portal into the slot the pivot made. What the part means stays the pivot's: the
// hooks here read the pivot's view of the part and give the keyboard to the control the pivot asks for, so a Wrapper
// draws its own components and decides nothing.

import { useLayoutEffect, useRef, useState, useSyncExternalStore, type ReactNode } from 'react';
import type {
    PivotChrome, PivotFieldListSlotContext, PivotFieldSettingsSlotContext, PivotItemFilterSlotContext, PivotMenuSlotContext, PivotMenuView, PivotPanelSlotContext, PivotReportFiltersSlotContext,
    PivotStaleReportSlotContext, PivotStandingSlotContext, PivotToolbarSlotContext, PivotValueFieldSettingsSlotContext,
} from '@ex-grid/pivot';
import { chromeMounts, type ChromeMountsMade, type ExGridChrome, type GridPortals } from '@ex-grid/react';

/**
 * The Chrome slots an ExPivot takes, each a render function of what the pivot hands that slot (ADR-0390 section 3),
 * and `grid`, ExGrid's own, which dresses the report's grid and Show Details' grids. A slot not given is drawn by the
 * built-in markup. A render function is read when its part opens; its answer is rendered as a component of its own,
 * so it may hold state, kept while the part stands, and a hook belongs in a component it renders.
 */
export interface ExPivotChrome {
    /** ExGrid's Chrome for the report's grid and Show Details' grids; the Context Menu's commands are worded by the
     * pivot. */
    readonly grid?: ExGridChrome<unknown>;
    /** A placed field's menu and the Layout menu, drawn with {@link usePivotMenu}. */
    readonly menu?: (context: PivotMenuSlotContext) => ReactNode;
    /** Filter…, drawn with {@link usePivotPanel}. */
    readonly itemFilter?: (context: PivotItemFilterSlotContext) => ReactNode;
    /** Field Settings…, drawn with {@link usePivotPanel}. */
    readonly fieldSettings?: (context: PivotFieldSettingsSlotContext) => ReactNode;
    /** Value Field Settings…, drawn with {@link usePivotPanel}. */
    readonly valueFieldSettings?: (context: PivotValueFieldSettingsSlotContext) => ReactNode;
    /** The Pivot Toolbar, drawn with {@link usePivotPart}. */
    readonly toolbar?: (context: PivotToolbarSlotContext) => ReactNode;
    /** The report filter band, drawn with {@link usePivotPart}. */
    readonly reportFilters?: (context: PivotReportFiltersSlotContext) => ReactNode;
    /** The Stale Report's notice, drawn with {@link usePivotPart}. */
    readonly staleReport?: (context: PivotStaleReportSlotContext) => ReactNode;
    /** The Field List, drawn with {@link usePivotPart}. */
    readonly fieldList?: (context: PivotFieldListSlotContext) => ReactNode;
}

/** The mount functions made for an ExPivot's render functions: the pivot's slots', and the grid's. */
export interface PivotMountsMade {
    readonly pivot: Map<string, { readonly render: unknown; readonly mount: (slot: HTMLElement, context: unknown) => () => void }>;
    readonly grid: ChromeMountsMade;
    gridChrome?: { readonly given: unknown; readonly mounts: PivotChrome['grid'] };
}

const pivotSlots: readonly string[] = ['menu', 'itemFilter', 'fieldSettings', 'valueFieldSettings', 'toolbar', 'reportFilters', 'staleReport', 'fieldList'];

/** The ExPivot's Chrome as the pivot takes it: each slot's render function as a mount function rendering it through
 * the ExPivot's portals, and `grid`'s as ExGrid for React makes its own. A render function handed again keeps its
 * mount function. Which slots there are is the pivot's to check; a render function that is not a function is named
 * here, in React's terms. */
export function pivotChromeMounts(given: ExPivotChrome | undefined, portals: GridPortals, made: PivotMountsMade): PivotChrome | undefined {
    // Checked as a script may hand it, whatever its type says.
    const chrome: unknown = given;
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) {
        throw new TypeError('An ExPivot\'s chrome is an object of Chrome slots, each a render function of its context (ADR-0390).');
    }
    const mounts: Record<string, unknown> = {};
    for (const entry of Object.entries(chrome)) {
        const name = entry[0];
        const render: unknown = entry[1];
        if (render === undefined) continue;
        if (name === 'grid') {
            // The grid's mounts are made again only for another grid chrome, so its standing parts keep theirs.
            let grid = made.gridChrome;
            if (grid === undefined || grid.given !== render) {
                // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- checked by chromeMounts, as ExGrid for React checks its own chrome
                grid = { given: render, mounts: chromeMounts(render as ExGridChrome<unknown>, portals, made.grid) };
                made.gridChrome = grid;
            }
            mounts[name] = grid.mounts;
            continue;
        }
        // A slot the pivot does not have is handed on, for the pivot to refuse by name.
        if (!pivotSlots.includes(name)) {
            mounts[name] = render;
            continue;
        }
        if (typeof render !== 'function') throw new TypeError(`An ExPivot's chrome.${name} is a render function of the slot's context (ADR-0390).`);
        const before = made.pivot.get(name);
        if (before !== undefined && before.render === render) {
            mounts[name] = before.mount;
            continue;
        }
        const mount = (slot: HTMLElement, context: unknown): (() => void) => portals.mount(slot, () => {
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the pivot hands each slot's mount that slot's context, the one its render function takes
            const node = Reflect.apply(render, undefined, [context]) as ReactNode;
            return node;
        });
        made.pivot.set(name, { render, mount });
        mounts[name] = mount;
    }
    return mounts;
}

/** What a part's context offers every hook here: its view, and the keyboard's requests. */
interface ViewedPart<View> {
    readonly view: () => View;
    readonly subscribe: (listener: () => void) => () => void;
    readonly focusRequest: number;
    readonly takeFocus: (control: HTMLElement) => void;
}

/** The contexts whose request for the keyboard has been answered. Each request comes in a context of its own, so one
 * answered is never answered again: not by a component of the contents mounted after it, nor by an effect run twice. */
const answered = new WeakSet<object>();

/** The view, read again at each change, and the ref of the control a request for the keyboard is for, focused once it
 * is drawn. */
function useViewedPart<View>(context: ViewedPart<View>, what: string): { readonly view: View; readonly focus: (control: HTMLElement | null) => void } {
    const view = useSyncExternalStore(context.subscribe, context.view);
    const control = useRef<HTMLElement | null>(null);
    useLayoutEffect(() => {
        if (context.focusRequest === 0 || answered.has(context)) return;
        answered.add(context);
        const target = control.current;
        if (target === null) throw new Error(`The ${what} is not drawn: hand the hook's \`focus\` ref to it.`);
        context.takeFocus(target);
    }, [context, what]);
    // Made once, so a render hands React the same ref and it does not take it away and back.
    const [focus] = useState(() => (element: HTMLElement | null): void => { control.current = element; });
    return { view, focus };
}

/** What {@link usePivotPanel} hands a panel's contents. */
export interface PivotPanelHandles<View> {
    /** What the panel shows now, drawn again at each change the pivot makes to it. */
    readonly view: View;
    /** Takes the panel's first control: the one each request of the pivot's for the keyboard is for. */
    readonly first: (control: HTMLElement | null) => void;
}

/**
 * Draws one of the pivot's panels from its context (ADR-0390 section 3): Filter…, Field Settings… or Value Field
 * Settings…. The view is the pivot's draft, read again at each change; each choice goes to the context's setter, and
 * the hook gives the panel's first control the keyboard at each request, once it is drawn, through the pivot.
 */
export function usePivotPanel<View>(context: PivotPanelSlotContext & { readonly view: () => View }): PivotPanelHandles<View> {
    const { view, focus } = useViewedPart(context, 'panel\'s first control');
    return { view, first: focus };
}

/**
 * Draws one of the pivot's standing parts from its context (ADR-0390 section 3): the Pivot Toolbar, the report filter
 * band, the Stale Report's notice or the Field List. The view is the pivot's, read again at each change; each press goes to the
 * context's callback, and each control and anchor is handed in by its `ref`, `context.control(name)` and
 * `context.anchor(name)`, so the pivot gives the keyboard to the control it means and places a popup's frame in its
 * anchor itself.
 */
export function usePivotPart<View>(context: PivotStandingSlotContext<View>): View {
    return useSyncExternalStore(context.subscribe, context.view);
}

/** The props {@link usePivotMenu} hands each command's control: spread them onto it. */
export interface PivotMenuItemProps {
    /** A choice is a `menuitemradio`, any other command a `menuitem`. */
    readonly role: 'menuitem' | 'menuitemradio';
    /** On a choice: whether it is the current one. */
    readonly 'aria-checked'?: boolean;
    /** Whether the command cannot run now; a disabled command is shown, never left out. */
    readonly disabled: boolean;
    /** Claims the click for the pivot's Input Order (ADR-0240). */
    readonly 'data-ex-claims': 'click';
    /** Runs the command the pivot drew last under this one's id. */
    readonly onClick: () => void;
    /** On the first enabled command: the one a request for the keyboard is for. */
    readonly ref?: (control: HTMLElement | null) => void;
}

/** What {@link usePivotMenu} hands a menu's contents. */
export interface PivotMenuHandles {
    /** What the menu shows now: its commands, in the pivot's order, with their group headings. */
    readonly view: PivotMenuView;
    /** The props of the command at `index` in the view's commands. */
    readonly item: (index: number) => PivotMenuItemProps;
}

/**
 * Draws a menu from its context (ADR-0390 section 3): one control per command, in the pivot's order, a choice as a
 * `menuitemradio` marked while it is the current one. A press runs the command the pivot drew last under its id, and
 * the first enabled command takes the keyboard at each request of the pivot's.
 */
export function usePivotMenu(context: PivotMenuSlotContext): PivotMenuHandles {
    const { view, focus } = useViewedPart(context, 'menu\'s first enabled command');
    const first = view.commands.findIndex(command => command.enabled);
    return {
        view,
        item: index => {
            const command = view.commands[index];
            if (command === undefined) throw new RangeError(`The menu has ${view.commands.length} commands, not one at ${index}.`);
            return {
                role: command.checked === undefined ? 'menuitem' : 'menuitemradio',
                ...(command.checked === undefined ? {} : { 'aria-checked': command.checked }),
                disabled: !command.enabled,
                'data-ex-claims': 'click',
                onClick: command.invoke,
                ...(index === first ? { ref: focus } : {}),
            };
        },
    };
}
