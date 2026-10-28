// ExPivot's Chrome slots filled with Razor (ADR-0410): the kind of each pivot slot over the Shared Script's
// RazorSlotHost, and the report grid's slots through the grid's own kinds. Each kind says what the slot's context
// crosses to .NET as, and wires the controls the Razor drew from the attributes they carry (section 2): a press runs
// the context's callback in the browser, a control and an anchor are handed in by name as a React ref hands them, and
// a request for the keyboard is answered on the control it names. Nothing a key means waits on the circuit.

import { gridRazorSlotKinds, isGridRazorSlotName, RazorSlotHost, type GridRazorSlotName, type HostedSlotKind, type RazorSlotHostOptions, type RazorSlotKind } from '@ex-grid/blazor-shared';
import type { GridChrome, GridHandle } from '@ex-grid/core';
import type {
    PivotChoice, PivotChrome, PivotFieldSettingsSlotContext, PivotFieldSettingsView, PivotItemFilterSlotContext, PivotItemFilterView, PivotMenuSlotContext,
    PivotMenuView, PivotPanelSlotContext, PivotPartRef, PivotReportFiltersSlotContext, PivotReportFiltersView, PivotStaleReportSlotContext,
    PivotStaleReportView, PivotToolbarSlotContext, PivotToolbarView, PivotValueFieldSettingsSlotContext, PivotValueFieldSettingsView,
} from '@ex-grid/pivot';
import { pivotItemKeyId } from '@ex-grid/pivot-engine';
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
    /** On a panel's text field, or an element holding it: `search`, `caption` or `number-format`, read as it is typed. */
    text: 'data-ex-pivot-text',
    /** On a panel's choice, or an element holding it: `subtotals` on each radio button, which names its value with
     * `value`, and `sort`, `aggregation` and `show-values-as` on a native select whose options' values are the view's. */
    choice: 'data-ex-pivot-choice',
    /** Beside `choice` on a radio button, or an element holding it: the value it chooses (`automatic`, `none`). */
    value: 'data-ex-pivot-value',
    /** On Filter…'s checkbox for one Item: the Item's index in the view's items. */
    item: 'data-ex-pivot-item',
    /** On Filter…'s (Select All). */
    all: 'data-ex-pivot-all',
    /** On a panel's first control, or an element holding it: the one each request for the keyboard is for. */
    first: 'data-ex-pivot-first',
});

/** The pivot's slots a Razor Chrome fills, as the pivot names them. */
export type PivotRazorSlotName = 'menu' | 'toolbar' | 'staleReport' | 'reportFilters' | 'fieldSettings' | 'valueFieldSettings' | 'itemFilter';

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

/** The report filter band's kind: its filters as data, each button pressed in the browser and handed in, with the
 * anchor around it, by its field. */
const reportFiltersKind: RazorSlotKind<PivotReportFiltersSlotContext, PivotReportFiltersView> = {
    takesKeyboard: false,
    data: band => {
        const view = band.view();
        return { json: JSON.stringify({ name: view.name, filters: view.filters }), drawn: view, memo: undefined };
    },
    subscribe: (band, again) => band.subscribe(again),
    wire: (contents, band, _answered, drawn) => {
        const fields = drawn().filters.map(filter => filter.field);
        return wireStanding(contents, Object.fromEntries(fields.map(field => [field, () => { band.toggle(field); }])),
            name => band.control(namedOf(name, fields, 'report filter band')), name => band.anchor(namedOf(name, fields, 'report filter band')));
    },
};

/** The text a panel's fields open with: drawn as the panel is mounted, and never written back by .NET after, which is a
 * round trip behind the typing (SRV-7). The script writes the draft into a field where it differs from what is typed. */
type PanelOpens = Readonly<Record<string, string>>;

/** Field Settings…' kind: the draft as data; the subtotals' radio buttons and the order's select read in the browser. */
const fieldSettingsKind: RazorSlotKind<PivotFieldSettingsSlotContext, PivotFieldSettingsView> = {
    takesKeyboard: true,
    data: panel => {
        const view = panel.view();
        return {
            json: JSON.stringify({
                caption: view.caption, subtotals: view.subtotals, sorts: view.sorts, sort: view.sort,
                words: wordsOf(panel, { subtotals: 'subtotals', automatic: 'subtotals-automatic', none: 'subtotals-none', sortOrder: 'sort-order', ok: 'ok', cancel: 'cancel' }),
            }),
            drawn: view,
            memo: undefined,
        };
    },
    subscribe: (panel, again) => panel.subscribe(again),
    wire: (contents, panel, answered, drawn) => wirePanel(contents, panel, answered, {
        choice: (name, value) => {
            if (name === 'subtotals') panel.setSubtotals(namedOf(value, ['automatic', 'none'], 'subtotals\' radio button') === 'automatic');
            else if (name === 'sort') panel.setSort(indexOf(value, drawn().sorts.length, 'order'));
            else throw new RangeError(`Field Settings… has no choice '${name}'.`);
        },
    }),
};

/** Value Field Settings…' kind: the draft as data; the caption and the number format typed, and the Aggregation and Show
 * Values As chosen, in the browser. */
const valueFieldSettingsKind: RazorSlotKind<PivotValueFieldSettingsSlotContext, PivotValueFieldSettingsView, PanelOpens> = {
    takesKeyboard: true,
    data: (panel, before) => {
        const view = panel.view();
        const opens = before ?? { caption: view.caption, 'number-format': view.numberFormat };
        return {
            json: JSON.stringify({
                sourceCaption: view.sourceCaption, caption: opens['caption'], aggregation: view.aggregation,
                aggregations: view.aggregations.map(choiceJson), showValuesAs: view.showValuesAs, showValuesAsChoices: view.showValuesAsChoices.map(choiceJson),
                numberFormat: opens['number-format'], numberFormatPlaceholder: view.numberFormatPlaceholder, sample: view.sample,
                refusal: view.refusal === undefined ? null : view.refusal,
                words: wordsOf(panel, {
                    title: 'value-field-settings', customName: 'custom-name', summarizeBy: 'summarize-by', showValuesAs: 'show-values-as',
                    numberFormat: 'number-format', ok: 'ok', cancel: 'cancel',
                }),
            }),
            drawn: view,
            memo: opens,
        };
    },
    subscribe: (panel, again) => panel.subscribe(again),
    wire: (contents, panel, answered, drawn) => wirePanel(contents, panel, answered, {
        text: (name, value) => {
            if (name === 'caption') panel.setCaption(value);
            else if (name === 'number-format') panel.setNumberFormat(value);
            else throw new RangeError(`Value Field Settings… has no text field '${name}'.`);
        },
        choice: (name, value) => {
            const view = drawn();
            if (name === 'aggregation') panel.setAggregation(offered(value, view.aggregations, 'an Aggregation'));
            else if (name === 'show-values-as') panel.setShowValuesAs(offered(value, view.showValuesAsChoices, 'a Show Values As'));
            else throw new RangeError(`Value Field Settings… has no choice '${name}'.`);
        },
        // The caption follows the Aggregation until the user writes one: the draft is written into a field only where
        // it differs from what is typed there, so the caret stays where the user put it, as the built-in panel's.
        texts: () => ({ caption: panel.view().caption, 'number-format': panel.view().numberFormat }),
    }),
};

/** Filter…' kind: the Items as data, the search typed and each Item ticked in the browser, against the Items the
 * contents were drawn with. */
const itemFilterKind: RazorSlotKind<PivotItemFilterSlotContext, PivotItemFilterView, PanelOpens> = {
    takesKeyboard: true,
    data: (panel, before) => {
        const view = panel.view();
        const opens = before ?? { search: view.search };
        return {
            json: JSON.stringify({
                caption: view.caption, search: opens['search'], items: view.items.map(item => ({ key: pivotItemKeyId(item.key), label: item.label, ticked: item.ticked })),
                allTicked: view.allTicked === undefined ? null : view.allTicked, loading: view.loading, updating: view.updating,
                unavailable: view.unavailable === undefined ? null : view.unavailable, note: view.note === undefined ? null : view.note,
                canApply: view.canApply, refusal: view.refusal === undefined ? null : view.refusal,
                words: wordsOf(panel, { search: 'search-items', selectAll: 'select-all', loading: 'loading', ok: 'ok', cancel: 'cancel' }),
            }),
            drawn: view,
            memo: opens,
        };
    },
    subscribe: (panel, again) => panel.subscribe(again),
    wire: (contents, panel, answered, drawn) => wirePanel(contents, panel, answered, {
        text: (name, value) => {
            if (name === 'search') panel.setSearch(value);
            else throw new RangeError(`Filter… has no text field '${name}'.`);
        },
        item: (index, ticked) => {
            // The Item the checkbox was drawn for: the one the user saw, though the pivot's list may have moved since.
            const item = drawn().items[index];
            if (item === undefined) throw new RangeError(`Filter…'s checkbox is marked ${pivotSlotAttributes.item}="${String(index)}", which names no Item it was drawn with.`);
            panel.setTicked(item.key, ticked);
        },
        // Pressed while every Item is ticked it unticks them, and otherwise ticks them, whatever state the checkbox's
        // own cycle would go to: judged on the draft as it stands, so two presses come to the same Items however many
        // renders arrived between them.
        all: () => { panel.setAll(panel.view().allTicked !== true); },
        // The search the draft filters on stays the one the field shows, though the field is drawn again.
        texts: () => ({ search: panel.view().search }),
    }),
};

// Every pivot slot a Razor Chrome fills has its kind: a name added to PivotRazorSlotName without one does not compile.
const pivotKindsByName = {
    menu: menuKind, toolbar: toolbarKind, staleReport: staleReportKind, reportFilters: reportFiltersKind,
    fieldSettings: fieldSettingsKind, valueFieldSettings: valueFieldSettingsKind, itemFilter: itemFilterKind,
} satisfies Record<PivotRazorSlotName, HostedSlotKind>;

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

// The elements of `contents` an attribute marks, each with its value. Not one inside an anchor: an anchor is drawn
// empty, and what stands in it is a part the pivot placed there, another slot's contents with marks of their own (the
// report filter band in the toolbar's, a menu in Layout ▾'s).
function marked(contents: HTMLElement, attribute: string): (readonly [HTMLElement, string])[] {
    // The contents' own element first: a part may mark the element it is drawn in.
    const all = [...(contents.hasAttribute(attribute) ? [contents] : []), ...contents.querySelectorAll<HTMLElement>(`[${attribute}]`)]
        .filter(element => {
            const anchor = element.parentElement?.closest(`[${pivotSlotAttributes.anchor}]`);
            return anchor === null || anchor === undefined || !contents.contains(anchor);
        });
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

/** The words a panel's contents draw, by the names .NET reads them under, each the pivot's word of the id given. */
function wordsOf(panel: PivotPanelSlotContext, ids: Readonly<Record<string, string>>): Readonly<Record<string, string>> {
    return Object.fromEntries(Object.entries(ids).map(([name, id]) => [name, panel.label(id)]));
}

/** A choice as .NET is handed it. */
function choiceJson(choice: PivotChoice<string>): object {
    return { value: choice.value, label: choice.label, enabled: choice.enabled, reason: choice.reason === undefined ? null : choice.reason };
}

/** The choice an option names: one the pivot offered, never one a changed option claims. */
function offered<T extends string>(value: string, choices: readonly PivotChoice<T>[], what: string): T {
    const found = choices.find(choice => choice.value === value);
    if (found === undefined) throw new RangeError(`'${value}' is not ${what} the panel offers.`);
    return found.value;
}

/** An option's index among `count`, as a select's value names it. */
function indexOf(value: string, count: number, what: string): number {
    const index = Number(value);
    if (value === '' || !Number.isSafeInteger(index) || index < 0 || index >= count) throw new RangeError(`'${value}' names no ${what} of the ${String(count)} the panel offers.`);
    return index;
}

/** What a panel's controls do: each is the panel's own, and a panel without one refuses a control marked for it. */
interface PanelHandlers {
    /** A text field named `name` typed in. */
    readonly text?: (name: string, value: string) => void;
    /** A choice named `name` made, `value` its radio button's or select's value. */
    readonly choice?: (name: string, value: string) => void;
    /** Filter…'s checkbox of the Item at `index` ticked or unticked. */
    readonly item?: (index: number, ticked: boolean) => void;
    /** (Select All) pressed. */
    readonly all?: () => void;
    /** The draft's text of each text field, written into the field where it differs from what is typed there. */
    readonly texts?: () => Readonly<Record<string, string>>;
}

/**
 * Wires a panel's contents (ADR-0410 section 2): each text typed and choice made goes to the panel at once, in the
 * browser, and claims nothing, so its keys are the field's (ADR-0240); OK and Cancel, marked as presses, apply and
 * cancel in the pivot's Input Order. The first control takes the keyboard at the request the context carries, unless
 * `answered`.
 */
function wirePanel(contents: HTMLElement, panel: PivotPanelSlotContext, answered: boolean, handlers: PanelHandlers): () => void {
    for (const attribute of [pivotSlotAttributes.text, pivotSlotAttributes.choice, pivotSlotAttributes.item, pivotSlotAttributes.all]) {
        for (const [element] of marked(contents, attribute)) element.setAttribute('data-ex-claims', '');
    }
    const fields = (): (readonly [HTMLInputElement | HTMLTextAreaElement, string])[] => marked(contents, pivotSlotAttributes.text).map(([element, name]) => {
        const field = element instanceof HTMLInputElement || element instanceof HTMLTextAreaElement ? element : element.querySelector('input, textarea');
        if (!(field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement)) throw new Error(`The panel's text field '${name}' is drawn with no field to type in.`);
        return [field, name] as const;
    });
    const input = (event: Event): void => {
        const field = event.target;
        if (!(field instanceof HTMLInputElement || field instanceof HTMLTextAreaElement)) return;
        const text = markedAround(contents, field, pivotSlotAttributes.text);
        if (text !== undefined) own(handlers.text, pivotSlotAttributes.text, text)(text, field.value);
    };
    const change = (event: Event): void => {
        const control = event.target;
        if (control instanceof HTMLSelectElement) {
            const choice = markedAround(contents, control, pivotSlotAttributes.choice);
            if (choice !== undefined) own(handlers.choice, pivotSlotAttributes.choice, choice)(choice, control.value);
            return;
        }
        if (!(control instanceof HTMLInputElement)) return;
        const choice = markedAround(contents, control, pivotSlotAttributes.choice);
        if (choice !== undefined) {
            const value = markedAround(contents, control, pivotSlotAttributes.value);
            if (value === undefined) throw new RangeError(`A radio button is marked ${pivotSlotAttributes.choice}="${choice}" with no ${pivotSlotAttributes.value} naming what it chooses.`);
            if (control.checked) own(handlers.choice, pivotSlotAttributes.choice, choice)(choice, value);
            return;
        }
        if (markedAround(contents, control, pivotSlotAttributes.all) !== undefined) {
            own(handlers.all, pivotSlotAttributes.all, '')();
            return;
        }
        const item = markedAround(contents, control, pivotSlotAttributes.item);
        if (item !== undefined) own(handlers.item, pivotSlotAttributes.item, item)(indexOf(item, Number.MAX_SAFE_INTEGER, 'Item'), control.checked);
    };
    contents.addEventListener('input', input);
    contents.addEventListener('change', change);
    const unpress = wireStanding(contents, { ok: panel.apply, cancel: panel.cancel }, name => {
        throw new RangeError(`A panel's control is marked ${pivotSlotAttributes.control}="${name}": a panel hands in no control by name.`);
    });
    const texts = handlers.texts;
    const write = (): void => {
        if (texts === undefined) return;
        const now = texts();
        for (const [field, name] of fields()) {
            const text = now[name];
            if (text === undefined) throw new RangeError(`The panel's text field '${name}' has no text in the draft.`);
            if (field.value !== text) field.value = text;
        }
    };
    write();
    const unsubscribe = texts === undefined ? () => undefined : panel.subscribe(write);
    if (!answered && panel.focusRequest > 0) {
        const first = marked(contents, pivotSlotAttributes.first)[0]?.[0];
        if (first === undefined) throw new Error(`The panel's first control is not marked ${pivotSlotAttributes.first}: each request of the pivot's for the keyboard is for it.`);
        panel.takeFocus(focusable(first));
    }
    return () => {
        unsubscribe();
        unpress();
        contents.removeEventListener('input', input);
        contents.removeEventListener('change', change);
    };
}

// A panel's handler of a control marked `attribute`, refused by name where the panel has none.
function own<T>(handler: T | undefined, attribute: string, value: string): T {
    if (handler === undefined) throw new RangeError(`A panel's control is marked ${attribute}="${value}", which this panel has none of.`);
    return handler;
}

// The value of the element at or around `target`, inside `contents`, that carries `attribute`.
function markedAround(contents: HTMLElement, target: Element, attribute: string): string | undefined {
    const element = target.closest(`[${attribute}]`);
    if (element === null || !contents.contains(element)) return undefined;
    const value = element.getAttribute(attribute);
    if (value === null) throw new Error(`Internal: an element found by ${attribute} carries none.`);
    return value;
}

// The control a mark stands for: the element itself where it takes the keyboard, else the first control inside it, as
// a component library places its attributes on a wrapper.
function focusable(element: HTMLElement): HTMLElement {
    if (element.matches('input, select, textarea, button, [tabindex]')) return element;
    const inside = element.querySelector<HTMLElement>('input, select, textarea, button, [tabindex]');
    if (inside === null) throw new Error(`The element marked ${pivotSlotAttributes.first} holds no control to take the keyboard.`);
    return inside;
}
