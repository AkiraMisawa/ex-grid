// ExPivot's Chrome slots filled with Razor (ADR-0410): the kind of each pivot slot over the Shared Script's
// RazorSlotHost, and the report grid's slots through the grid's own kinds. Each kind says what the slot's context
// crosses to .NET as, and wires the controls the Razor drew from the attributes they carry (section 2): a press runs
// the context's callback in the browser, a control and an anchor are handed in by name as a React ref hands them, and
// a request for the keyboard is answered on the control it names. Nothing a key means waits on the circuit.

import { gridRazorSlotKinds, isGridRazorSlotName, RazorSlotHost, type GridRazorSlotName, type HostedSlotKind, type RazorSlotHostOptions, type RazorSlotKind } from '@ex-grid/blazor-shared';
import type { GridChrome, GridHandle } from '@ex-grid/core';
import type {
    PivotChrome, PivotMenuSlotContext, PivotMenuView, PivotPartRef, PivotStaleReportSlotContext, PivotStaleReportView, PivotToolbarSlotContext,
    PivotToolbarView,
} from '@ex-grid/pivot';
import type { DotNetHandle } from './dotnet-pivot-source';

/** The attributes the Razor of a pivot slot writes on its controls (ADR-0410 section 2), as `PivotScriptSlotAttributes`
 * names them in .NET. */
export const pivotSlotAttributes = Object.freeze({
    /** On a menu's control for one command: the command's index in the view's commands, once for every command. */
    menuItem: 'data-ex-pivot-menu-item',
    /** On a button: what a press on it does, by the context's name for it (`layout`, `refresh`, `field-list`, `retry`). */
    press: 'data-ex-pivot-press',
    /** On a control the pivot gives the keyboard to: its name, handed in through the context's `control`. */
    control: 'data-ex-pivot-control',
    /** On an element the pivot places a popup's frame in: its name, handed in through the context's `anchor`. */
    anchor: 'data-ex-pivot-anchor',
});

/** The pivot's slots a Razor Chrome fills, as the pivot names them. */
export type PivotRazorSlotName = 'menu' | 'toolbar' | 'staleReport';

/** A menu's kind: its commands as data, each control taking its command's role and running the command drawn under its
 * index; the first enabled one takes the keyboard at each request of the pivot's. */
const menuKind: RazorSlotKind<PivotMenuSlotContext, PivotMenuView> = {
    takesKeyboard: true,
    data: menu => {
        const view = menu.view();
        return {
            json: JSON.stringify({
                name: view.name,
                commands: view.commands.map(command => ({
                    id: command.id, label: command.label, enabled: command.enabled,
                    checked: command.checked === undefined ? null : command.checked, heading: command.heading === undefined ? null : command.heading,
                })),
            }),
            drawn: view,
            memo: undefined,
        };
    },
    subscribe: (menu, again) => menu.subscribe(again),
    wire: (contents, menu, answered, drawn) => wireMenu(contents, menu, answered, drawn),
};

/** The Pivot Toolbar's kind: its view as data; Layout ▾, Refresh and the Field List's toggle pressed in the browser,
 * each button and the anchors around Layout ▾ and the report filter band handed in by name. */
const toolbarKind: RazorSlotKind<PivotToolbarSlotContext, PivotToolbarView> = {
    takesKeyboard: false,
    data: toolbar => {
        const view = toolbar.view();
        return {
            json: JSON.stringify({
                layoutMenu: view.layoutMenu,
                refresh: view.refresh === undefined ? null : view.refresh,
                fieldList: view.fieldList,
                reportFilters: view.reportFilters,
                refusal: view.refusal === undefined ? null : view.refusal,
            }),
            drawn: view,
            memo: undefined,
        };
    },
    subscribe: (toolbar, again) => toolbar.subscribe(again),
    wire: (contents, toolbar) => wireStanding(contents, {
        layout: toolbar.toggleLayoutMenu, refresh: toolbar.refresh, 'field-list': toolbar.toggleFieldList,
    }, name => toolbar.control(namedOf(name, ['layout', 'refresh', 'field-list'], 'toolbar')),
    name => toolbar.anchor(namedOf(name, ['layout', 'report-filters'], 'toolbar'))),
};

/** The Stale Report's notice's kind: its view as data, and Retry pressed in the browser. */
const staleReportKind: RazorSlotKind<PivotStaleReportSlotContext, PivotStaleReportView> = {
    takesKeyboard: false,
    data: notice => {
        const view = notice.view();
        return { json: JSON.stringify({ message: view.message, asOfText: view.asOfText, reason: view.reason, retry: view.retry }), drawn: view, memo: undefined };
    },
    subscribe: (notice, again) => notice.subscribe(again),
    wire: (contents, notice) => wireStanding(contents, { retry: notice.retry }, name => notice.control(namedOf(name, ['retry'], 'staleReport'))),
};

// Every pivot slot a Razor Chrome fills has its kind: a name added to PivotRazorSlotName without one does not compile.
const pivotKindsByName = { menu: menuKind, toolbar: toolbarKind, staleReport: staleReportKind } satisfies Record<PivotRazorSlotName, HostedSlotKind>;

/** The kinds of the pivot's slots a Razor Chrome fills, by name: for a host of a product that draws pivots, beside the
 * report grid's kinds (`gridRazorSlotKinds`). */
export const pivotRazorSlotKinds: ReadonlyMap<string, HostedSlotKind> = new Map(Object.entries(pivotKindsByName));

/**
 * What a host of pivot slots holds (ADR-0410 section 5): the Settled Mark and the inputs of the grid the slot stands
 * for, the report's or a Show Details grid's, and the keyboard handed back to it. `grids` finds that grid from the
 * slot, so a host serving several pivots, as a board's does, answers for each from the pivot the slot stands in.
 */
export function pivotSlotHostOptions(grids: PivotGrids): RazorSlotHostOptions {
    return {
        hold: slot => grids.holdSettled(slot),
        order: (until, slot) => {
            // With no grid there is nothing to hold the inputs in: an empty report's Layout menu, whose keys typed
            // before its Razor arrives are not ordered (ADR-0410 section 5's note of 2026-10-28).
            void grids.gridAround(slot)?.inOrder(() => until);
        },
        keyboardBack: slot => { grids.gridAround(slot)?.focus(); },
    };
}

/** Whether `name` is one of the pivot's slots a Razor Chrome fills. */
export function isPivotRazorSlotName(name: unknown): name is PivotRazorSlotName {
    return typeof name === 'string' && Object.hasOwn(pivotKindsByName, name);
}

/** The slots .NET fills: the pivot's, and the report grid's. */
export interface WireRazorSlots {
    readonly pivot: readonly string[];
    readonly grid: readonly string[];
}

/**
 * The Razor Chrome of one pivot (ADR-0410): mount functions for the slots .NET fills, the pivot's and its grids', and
 * the contents .NET renders for each. Each slot holds the Settled Mark and the inputs of the grid it stands for: the
 * report's, or a Show Details grid's (ADR-0240 section 6).
 */
export class RazorPivotSlots {
    readonly #host: RazorSlotHost;
    readonly #pivot: readonly PivotRazorSlotName[];
    readonly #grid: readonly GridRazorSlotName[];
    #chrome: PivotChrome | undefined;

    /** `grids` answers the grid a slot stands for, and holds a Settled Mark while the report draws no grid. */
    constructor(dotnet: DotNetHandle, names: WireRazorSlots, grids: PivotGrids) {
        this.#pivot = names.pivot.map(name => {
            if (!isPivotRazorSlotName(name)) throw new TypeError(`Internal: .NET named '${name}' as a pivot slot its Chrome fills.`);
            return name;
        });
        this.#grid = names.grid.map(name => {
            if (!isGridRazorSlotName(name)) throw new TypeError(`Internal: .NET named '${name}' as a grid slot its Chrome fills.`);
            return name;
        });
        const kinds = new Map<string, HostedSlotKind>([...pivotRazorSlotKinds, ...gridRazorSlotKinds]);
        this.#host = new RazorSlotHost(dotnet, kinds, pivotSlotHostOptions(grids));
    }

    /** The pivot's Chrome for the slots .NET fills, the same object for the pivot's life. */
    chrome(): PivotChrome | undefined {
        if (this.#pivot.length === 0 && this.#grid.length === 0) return undefined;
        if (this.#chrome !== undefined) return this.#chrome;
        // One call for every slot, so each keeps its mount function: a standing part is not drawn again for an update.
        const mounts = this.#host.mounts([...this.#pivot, ...this.#grid]);
        const pick = (names: readonly string[]): Record<string, (slot: HTMLElement, context: unknown) => () => void> =>
            Object.fromEntries(names.map(name => {
                const mount = mounts[name];
                if (mount === undefined) throw new Error(`Internal: the host made no mount for the slot '${name}'.`);
                return [name, mount];
            }));
        const grid: GridChrome<unknown> | undefined = this.#grid.length === 0 ? undefined : pick(this.#grid);
        // Each mount is handed the context of its own slot, the one its kind reads.
        const pivot: Omit<PivotChrome, 'grid'> = pick(this.#pivot);
        this.#chrome = Object.freeze({ ...pivot, ...(grid === undefined ? {} : { grid }) });
        return this.#chrome;
    }

    /** .NET rendered the contents of slot `id` for the data numbered `version` in `contents`. */
    rendered(id: number, version: number, contents: HTMLElement | null): void {
        this.#host.rendered(id, version, contents);
    }

    /** The pivot is going: every slot's contents go back to their holders, and nothing is asked of .NET again. */
    dispose(): void {
        this.#host.dispose();
    }
}

/** What the slots ask of the pivot's grids: `PivotHandle`'s. */
export interface PivotGrids {
    gridAround(element: Element): GridHandle<unknown> | undefined;
    holdSettled(element: Element): () => void;
}

// A name the Razor wrote, as the context takes it: one of `names`, or refused by name.
function namedOf<Name extends string>(name: string, names: readonly Name[], slot: string): Name {
    const found = names.find(each => each === name);
    if (found === undefined) throw new RangeError(`The ${slot}'s Razor names '${name}', which is none of ${names.join(', ')}.`);
    return found;
}

// The elements of `contents` an attribute marks, each with its value.
function marked(contents: HTMLElement, attribute: string): (readonly [HTMLElement, string])[] {
    // The contents' own element first: a part may mark the element it is drawn in.
    const all = [...(contents.hasAttribute(attribute) ? [contents] : []), ...contents.querySelectorAll<HTMLElement>(`[${attribute}]`)];
    return all.map(element => {
        const value = element.getAttribute(attribute);
        if (value === null) throw new Error(`Internal: an element found by ${attribute} carries none.`);
        return [element, value] as const;
    });
}

/**
 * Wires a standing part's contents (ADR-0410 section 2): each button marked `data-ex-pivot-press` runs the callback
 * named, in the pivot's Input Order (ADR-0240); each control and anchor is handed in by its name, and handed back as the
 * wiring comes off, so contents moved out leave nothing of theirs for the pivot to place a popup in.
 */
function wireStanding(contents: HTMLElement, presses: Readonly<Record<string, () => void>>,
    control: (name: string) => PivotPartRef, anchor?: (name: string) => PivotPartRef): () => void {
    const unwire: (() => void)[] = [];
    for (const [button, name] of marked(contents, pivotSlotAttributes.press)) {
        const press = presses[name];
        if (press === undefined) throw new RangeError(`A button is marked ${pivotSlotAttributes.press}="${name}": the part presses ${Object.keys(presses).join(', ')}.`);
        button.setAttribute('data-ex-claims', 'click');
        const click = (): void => { press(); };
        button.addEventListener('click', click);
        unwire.push(() => { button.removeEventListener('click', click); });
    }
    for (const [element, name] of marked(contents, pivotSlotAttributes.control)) {
        const ref = control(name);
        ref(element);
        unwire.push(() => { ref(null); });
    }
    for (const [element, name] of marked(contents, pivotSlotAttributes.anchor)) {
        if (anchor === undefined) throw new RangeError(`An element is marked ${pivotSlotAttributes.anchor}="${name}": the part has no anchors.`);
        const ref = anchor(name);
        ref(element);
        unwire.push(() => { ref(null); });
    }
    return () => { for (const each of unwire) each(); };
}

/**
 * Wires a menu's items (ADR-0410 section 2): one control per command, each marked with its index; a choice is a
 * `menuitemradio`, marked while it is the current one, any other a `menuitem`; a press runs the command the contents
 * were drawn under, which runs the one the pivot drew last under its id. The first enabled one takes the keyboard at
 * the request the context carries, unless `answered`.
 */
function wireMenu(contents: HTMLElement, menu: PivotMenuSlotContext, answered: boolean, drawn: () => PivotMenuView): () => void {
    const view = drawn();
    const items = new Map<number, HTMLElement>();
    for (const [control, value] of marked(contents, pivotSlotAttributes.menuItem)) {
        const index = Number(value);
        if (!Number.isSafeInteger(index) || index < 0 || index >= view.commands.length || items.has(index)) {
            throw new RangeError(`A menu's item is marked ${pivotSlotAttributes.menuItem}="${value}": each of the menu's ${view.commands.length} commands is marked once, by its index.`);
        }
        items.set(index, control);
    }
    if (items.size !== view.commands.length) throw new Error(`The menu draws ${items.size} of its ${view.commands.length} commands: mark the control of every command with ${pivotSlotAttributes.menuItem}.`);
    const unwire: (() => void)[] = [];
    for (const [index, control] of items) {
        const command = view.commands[index];
        if (command === undefined) throw new Error(`Internal: the menu's item ${index} has no command.`);
        control.setAttribute('role', command.checked === undefined ? 'menuitem' : 'menuitemradio');
        if (command.checked === undefined) control.removeAttribute('aria-checked');
        else control.setAttribute('aria-checked', String(command.checked));
        control.setAttribute('aria-disabled', String(!command.enabled));
        if (command.enabled) control.setAttribute('data-ex-claims', 'click');
        else control.removeAttribute('data-ex-claims');
        const click = (): void => {
            // The command the contents show under this index, as they were drawn: the pivot runs the one drawn last
            // under its id, and nothing where it is disabled now.
            const shown = drawn().commands[index];
            if (shown?.enabled === true) shown.invoke();
        };
        control.addEventListener('click', click);
        unwire.push(() => { control.removeEventListener('click', click); });
    }
    if (!answered && menu.focusRequest > 0) {
        // With no command enabled the pivot gives the frame the keyboard itself.
        const first = view.commands.findIndex(command => command.enabled);
        const control = items.get(first);
        if (control !== undefined) menu.takeFocus(control);
    }
    return () => { for (const each of unwire) each(); };
}
