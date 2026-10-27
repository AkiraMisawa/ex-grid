// ExPivot's Chrome slots (ADR-0061, ADR-0390 section 3): the parts of the pivot a Wrapper draws in place of the
// built-in markup, as the C# `IPivotChrome` lets a Chrome do. The pivot makes the slot, an element inside the frame
// it places, and hands it to the slot's mount function with what the part shows and the callbacks it answers
// through. What the part means is the pivot's: the commands and their order, which are enabled, what a draft holds
// and what OK makes of it, where the frame stands and what closes it. The contents only draw, and call back. A slot
// left unfilled is drawn by the built-in markup.
//
// A context is handed whole at each mount, as the grid's are (ADR-0390): what the part shows is its `view`, read
// again at each change its `subscribe` tells, and a request for the keyboard mounts the contents again with the next
// `focusRequest`, on the same slot, so a framework that keys its rendering by the slot keeps its component and that
// component's state across the two. Once the part goes, nothing the contents call back reaches the pivot.

import type { GridChrome } from '@ex-grid/core';
import type { PivotAggregation, PivotItemKey, PivotShowValuesAs } from '@ex-grid/pivot-engine';

/**
 * A Chrome slot's mount function (ADR-0350 section 9): it draws the part into `slot`, the element the pivot made for
 * it, from `context`, and answers the function that takes what it drew away. The pivot calls that cleanup before the
 * slot goes, and before it mounts again on the same slot with a new context.
 */
export type PivotChromeSlot<Context> = (slot: HTMLElement, context: Context) => () => void;

/** One command of a menu the pivot decides (ADR-0061): what it is called, whether it is available, and what it does. */
export interface PivotMenuCommand {
    /** The command's id, which is also the id of its word, stable for an icon. */
    readonly id: string;
    /** What it is called, in the pivot's words. */
    readonly label: string;
    /** Whether it can be pressed: one that would change nothing is disabled. */
    readonly enabled: boolean;
    /** For a choice among others, the Layout menu's, whether it is the current one, which the menu marks; absent on a
     * plain command. */
    readonly checked?: boolean;
    /** The heading of the group of choices that starts at this command: the Layout menu's Subtotals, Grand Totals and
     * Report Layout; absent where it continues the group above. */
    readonly heading?: string;
    /** Runs it and closes the menu, or opens the panel it names in the menu's place. A disabled command runs nothing,
     * and so does one pressed once the menu has closed or been drawn again without it. */
    readonly invoke: () => void;
}

/** What a menu shows now (ADR-0061): a new object at each change, and the same one otherwise. */
export interface PivotMenuView {
    /** The menu's accessible name, which its frame carries. */
    readonly name: string;
    /** Excel's commands, in its order. */
    readonly commands: readonly PivotMenuCommand[];
}

/** What the slots of a part that takes the keyboard share: their words, and the keyboard's requests. */
export interface PivotPartSlotContext {
    /** One of the pivot's words by its id: the Consumer's `label`, else the built-in English. */
    readonly label: (id: string) => string;
    /** The count of the pivot's requests for the part to take the keyboard; zero asks nothing. Each request mounts the
     * contents again, and whenever the count differs from the one they last answered they give DOM focus, through
     * `takeFocus`, to the control the part's context names. */
    readonly focusRequest: number;
    /** Gives `control` DOM focus: wherever DOM focus stands while a request is owed, until the contents answer it or
     * the user makes an input in the part, and otherwise only while the keyboard is still the pivot's (ADR-0204). */
    readonly takeFocus: (control: HTMLElement) => void;
}

/**
 * What a menu's contents are handed (ADR-0061): a placed field's menu, under its entry in the Field List, or the
 * Layout menu, under Layout ▾. The slot stands inside the pivot's frame (`role="menu"`, named by the pivot), which
 * Escape anywhere in it closes. The contents draw one item per command, in this order, a choice as a
 * `menuitemradio` marked while it is the current one and a group's heading where one starts. A request for the
 * keyboard is for the first enabled command; with none enabled the pivot gives the frame the keyboard itself.
 */
export interface PivotMenuSlotContext extends PivotPartSlotContext {
    /** What the menu shows now. */
    readonly view: () => PivotMenuView;
    /** Hears each change to the view; answers the function that stops it. */
    readonly subscribe: (listener: () => void) => () => void;
    /** Closes the menu without running anything, for a dismissal of the contents' own; the keyboard goes back to what
     * opened it. */
    readonly close: () => void;
}

/** What the slots of a panel share (ADR-0061): its exits, and a popup of the contents' own. A panel holds a draft the
 * pivot keeps; OK says it as the next layout, and Cancel and Escape drop it. */
export interface PivotPanelSlotContext extends PivotPartSlotContext {
    /** Hears each change to the view; answers the function that stops it. */
    readonly subscribe: (listener: () => void) => () => void;
    /** OK: the draft is applied and the panel closes. While the view says it cannot be, or the pivot refuses it, the
     * panel stands and says why. */
    readonly apply: () => void;
    /** Cancel: the draft is dropped, the panel closes, and the keyboard goes back to what opened it. */
    readonly cancel: () => void;
    /** The contents report a popup of their own (a select's list) opening (`true`) and closing (`false`): while one
     * is open, Escape is the popup's, and the next closes the panel (ADR-0039). */
    readonly innerPopupChanged: (open: boolean) => void;
}

/** What Field Settings… shows now (ADR-0061): a new object at each change, and the same one otherwise. */
export interface PivotFieldSettingsView {
    /** The field's caption, the panel's title. */
    readonly caption: string;
    /** The draft's subtotals: Automatic (`true`) or None. */
    readonly subtotals: boolean;
    /** The orders there are, each by its words: A to Z and Z to A by label, then each Value Field's two. */
    readonly sorts: readonly string[];
    /** The draft's order, by its index in `sorts`. */
    readonly sort: number;
}

/** What Field Settings…' contents are handed (ADR-0061): a row or column field's subtotals and order. A request for
 * the keyboard is for the first control, the subtotals' choice. */
export interface PivotFieldSettingsSlotContext extends PivotPanelSlotContext {
    /** What the panel shows now. */
    readonly view: () => PivotFieldSettingsView;
    /** Sets the draft's subtotals. */
    readonly setSubtotals: (automatic: boolean) => void;
    /** Sets the draft's order, by its index in the view's `sorts`. */
    readonly setSort: (index: number) => void;
}

/** A choice a panel offers: its value, its words, and whether it may be chosen. */
export interface PivotChoice<T> {
    /** The choice. */
    readonly value: T;
    /** What it is called. */
    readonly label: string;
    /** Whether it may be chosen: an Aggregation the Pivot Source does not answer is offered, and not available
     * (ADR-0066). */
    readonly enabled: boolean;
    /** Why it may not be chosen, in words; absent while it may. */
    readonly reason?: string;
}

/** What Value Field Settings… shows now (ADR-0061): a new object at each change, and the same one otherwise. */
export interface PivotValueFieldSettingsView {
    /** The Pivot Field's caption, the Source Name. */
    readonly sourceCaption: string;
    /** The draft's caption, as its field shows it: one the user has not written follows the Aggregation. */
    readonly caption: string;
    /** The draft's Aggregation. */
    readonly aggregation: PivotAggregation;
    /** Every Aggregation, with its name and whether the source answers it. */
    readonly aggregations: readonly PivotChoice<PivotAggregation>[];
    /** The draft's Show Values As. */
    readonly showValuesAs: PivotShowValuesAs;
    /** Every Show Values As, with its name. */
    readonly showValuesAsChoices: readonly PivotChoice<PivotShowValuesAs>[];
    /** The draft's number format, as typed; empty for General. */
    readonly numberFormat: string;
    /** What an empty number format means, the field's placeholder: General. */
    readonly numberFormatPlaceholder: string;
    /** A number shown in the draft's format, or the reason it cannot be. */
    readonly sample: string;
    /** Why OK last refused, in words: a caption taken or empty, a format that cannot be used; absent otherwise. */
    readonly refusal?: string;
}

/** What Value Field Settings…' contents are handed (ADR-0061). A request for the keyboard is for the first control,
 * the caption's field. */
export interface PivotValueFieldSettingsSlotContext extends PivotPanelSlotContext {
    /** What the panel shows now. */
    readonly view: () => PivotValueFieldSettingsView;
    /** The caption as typed. */
    readonly setCaption: (text: string) => void;
    /** Sets the draft's Aggregation: one the source does not answer is never chosen. */
    readonly setAggregation: (aggregation: PivotAggregation) => void;
    /** Sets the draft's Show Values As. */
    readonly setShowValuesAs: (showValuesAs: PivotShowValuesAs) => void;
    /** The number format as typed. */
    readonly setNumberFormat: (text: string) => void;
}

/** One Item as Filter… lists it. */
export interface PivotItemChoice {
    /** The Item. */
    readonly key: PivotItemKey;
    /** Its label. */
    readonly label: string;
    /** Whether it is shown in the draft. */
    readonly ticked: boolean;
}

/** What Filter… shows now (ADR-0061, ADR-0066): a new object at each change, and the same one otherwise. */
export interface PivotItemFilterView {
    /** The field's caption, which names the list. */
    readonly caption: string;
    /** The search as typed. */
    readonly search: string;
    /** The Items the search leaves, in the field's order, at most the most Filter… lists. */
    readonly items: readonly PivotItemChoice[];
    /** (Select All)'s state over `items`: `true` for every one, `false` for none (and over no Items), or `undefined` for
     * some. */
    readonly allTicked: boolean | undefined;
    /** Whether the Items, or the source's answer to the search, are on their way, with nothing yet to list. */
    readonly loading: boolean;
    /** Whether newer Items are on their way while what is shown stands: an earlier Source Version's Items, kept in
     * view (ADR-0066 refined), or why they could not be listed, while they are asked again. A Chrome may mark the list
     * as updating; it does not blank it, nor dim it. */
    readonly updating: boolean;
    /** Why the Items cannot be listed, in words, said in place of the list; absent while they can. */
    readonly unavailable?: string;
    /** A note under the Items: that more match than are listed, or that none matches the search; absent otherwise. */
    readonly note?: string;
    /** Whether OK may be pressed: not while every Item is unticked, nor while the Items are on their way. */
    readonly canApply: boolean;
    /** Why OK cannot apply, in words; absent while it can. */
    readonly refusal?: string;
}

/** What Filter…' contents are handed (ADR-0061, ADR-0066): a field's Items, each ticked while shown, a search that
 * narrows them, and OK and Cancel. A request for the keyboard is for the search. */
export interface PivotItemFilterSlotContext extends PivotPanelSlotContext {
    /** What the panel shows now. */
    readonly view: () => PivotItemFilterView;
    /** The search as typed. */
    readonly setSearch: (text: string) => void;
    /** Ticks or unticks one Item. */
    readonly setTicked: (key: PivotItemKey, ticked: boolean) => void;
    /** (Select All): ticks or unticks every Item the search lists. */
    readonly setAll: (ticked: boolean) => void;
}

/**
 * The Chrome slots a pivot takes (ADR-0061, ADR-0390): the parts a Wrapper draws, each in place of the built-in
 * markup's. A slot not given is the built-in markup's. A slot is read when its part opens: a part standing keeps the
 * slot it opened with.
 */
export interface PivotChrome {
    /** The report grid's Chrome, and Show Details' grids', so one Chrome dresses the pivot and the grids it draws. The
     * Context Menu's commands are worded by the pivot. */
    readonly grid?: GridChrome<unknown>;
    /** A placed field's menu and the Layout menu. */
    readonly menu?: PivotChromeSlot<PivotMenuSlotContext>;
    /** Filter…, under a field's entry or a report filter's button. */
    readonly itemFilter?: PivotChromeSlot<PivotItemFilterSlotContext>;
    /** Field Settings… of a row or column field. */
    readonly fieldSettings?: PivotChromeSlot<PivotFieldSettingsSlotContext>;
    /** Value Field Settings…. */
    readonly valueFieldSettings?: PivotChromeSlot<PivotValueFieldSettingsSlotContext>;
}

const slotNames: readonly (keyof PivotChrome)[] = ['grid', 'menu', 'itemFilter', 'fieldSettings', 'valueFieldSettings'];

/** A pivot's `chrome`, checked as a script may hand it: an object whose slots are each a mount function, and whose
 * `grid` is an object the report grid checks. */
export function checkedPivotChrome(chrome: unknown): PivotChrome | undefined {
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) {
        throw new TypeError('An ExPivot\'s chrome is an object of Chrome slots, each a mount function (ADR-0390).');
    }
    for (const [name, slot] of Object.entries(chrome)) {
        if (!(slotNames as readonly string[]).includes(name)) {
            throw new TypeError(`An ExPivot's chrome has no slot '${name}': its slots are ${slotNames.join(', ')} (ADR-0390).`);
        }
        if (name === 'grid') {
            if (slot !== undefined && (typeof slot !== 'object' || slot === null)) {
                throw new TypeError('An ExPivot\'s chrome.grid is a grid\'s chrome: an object of its Chrome slots (ADR-0390).');
            }
        } else if (slot !== undefined && typeof slot !== 'function') {
            throw new TypeError(`An ExPivot's chrome.${name} is a mount function, (slot, context) => cleanup (ADR-0350 section 9).`);
        }
    }
    return chrome;
}

/** Mounts `context` into `slot` by `mount`, and answers the cleanup it returned, checked. */
function mounted<Context>(name: string, mount: PivotChromeSlot<Context>, slot: HTMLElement, context: Context): () => void {
    const cleanup: unknown = mount(slot, context);
    if (typeof cleanup !== 'function') {
        throw new TypeError(`An ExPivot's chrome.${name} answered ${typeof cleanup}: a mount function answers the function that takes its contents away.`);
    }
    return () => { Reflect.apply(cleanup, undefined, []); };
}

/** The listeners a slot's view is told each change through, by the context's `subscribe`. */
export class ViewListeners {
    readonly #listeners = new Set<() => void>();

    /** The context's `subscribe`: adds `listener`, and answers the function that removes it. */
    readonly subscribe = (listener: () => void): (() => void) => {
        if (typeof listener !== 'function') throw new TypeError('subscribe is handed the function that hears each change to the view.');
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    };

    /** Tells every listener the view changed. */
    tell(): void {
        for (const listener of this.#listeners) listener();
    }

    /** The part is gone: nothing is told any more. */
    clear(): void {
        this.#listeners.clear();
    }
}

/**
 * A Chrome slot's contents while their part stands (ADR-0390): mounted with a context, mounted again with the next
 * one for each request for the keyboard, and taken away when the part goes, after which nothing they call back
 * reaches the pivot. The focus a request asks for is owed, the pivot's to give wherever DOM focus stands, until the
 * contents give it or the user makes an input in the part; any other focus they ask for is given only while the
 * keyboard is still the pivot's (ADR-0204), as ExGrid's own slots do.
 */
export class SlotMount<Context> {
    readonly #name: string;
    readonly #mount: PivotChromeSlot<Context>;
    /** The slot, which the pivot stands inside the part's frame. */
    readonly element: HTMLElement;
    readonly #context: (focusRequest: number) => Context;
    readonly #cancel: () => void;
    #cleanup: (() => void) | undefined;
    #focusRequest = 0;
    #owed = false;
    #live = true;
    /** Whether a popup of the contents' own is open: Escape is then the popup's (ADR-0039). */
    innerPopupOpen = false;

    /** `context` makes the context for a focus request's count; `cancel` closes the part. */
    constructor(name: string, mount: PivotChromeSlot<Context>, element: HTMLElement, context: (focusRequest: number) => Context, cancel: () => void) {
        this.#name = name;
        this.#mount = mount;
        this.element = element;
        this.#context = context;
        this.#cancel = cancel;
        element.setAttribute('data-ex-pivot-slot', name);
    }

    /** Whether the part stands. */
    get live(): boolean {
        return this.#live;
    }

    /** Mounts the contents, once. */
    mount(): void {
        if (this.#live && this.#cleanup === undefined) this.#draw();
    }

    /** A request for the keyboard: the contents are mounted again with the next count. */
    request(): void {
        if (!this.#live) return;
        this.#focusRequest += 1;
        this.#owed = true;
        this.#unmount();
        this.#draw();
    }

    /** `change` as an input the user made in the part: no focus is owed any longer, and nothing is made once the part
     * is gone. */
    input<Args extends unknown[]>(change: (...args: Args) => void): (...args: Args) => void {
        return (...args: Args): void => {
            if (!this.#live) return;
            this.#owed = false;
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

    readonly takeFocus = (control: HTMLElement): void => {
        if (!(control instanceof HTMLElement)) throw new TypeError('takeFocus is handed the element that takes the keyboard.');
        if (!this.#live || !control.isConnected) return;
        const owed = this.#owed;
        this.#owed = false;
        if (owed || this.#keyboardHere()) control.focus();
    };

    // DOM focus inside the pivot the part stands in, or on nothing.
    #keyboardHere(): boolean {
        const doc = this.element.ownerDocument;
        const active = doc.activeElement;
        const pivot = this.element.closest('.ex-pivot');
        return active === null || active === doc.body || active === doc.documentElement || (pivot !== null && pivot.contains(active));
    }

    // Contents that cannot be drawn close the part, rather than leave it standing empty.
    #draw(): void {
        try {
            this.#cleanup = mounted(this.#name, this.#mount, this.element, this.#context(this.#focusRequest));
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
