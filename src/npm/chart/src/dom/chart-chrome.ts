// ExChart's Chrome slots (ADR-0281, ADR-0390 section 3): the chart's toolbar, with Chart Filters, and its context
// menu's contents, drawn by a Wrapper in place of the built-in markup, as the C# `IChartChrome` lets a Chrome do.
// The chart makes the slot, an element where the part stands, and hands it to the slot's mount function with what
// the part shows and the callbacks it answers through. What the part means is the chart's: the commands and their
// order, which are enabled, what a typed range reads as, where the menu opens and what closes it. The contents only
// draw, and call back. The legend, the tooltip and the plot are always the chart's own (ADR-0255).
//
// A context is handed whole at each mount and never changes under the contents. When what the chart hands changes,
// the chart takes the contents away and mounts them again on the same slot, as the grid's slots are (ADR-0390): a
// framework that keys its rendering by the slot, as `@ex-grid/react`'s portals do, keeps its component and that
// component's state across the two. A context equal to the last is not mounted again, so a pointer moving over the
// plot draws no part of the Chrome (ADR-0252).

import type { AxisScale, AxisValues } from '../model';

/**
 * A Chrome slot's mount function (ADR-0350 section 9): it draws the part into `slot`, the element the chart made
 * for it, from `context`, and answers the function that takes what it drew away. The chart calls that cleanup
 * before the slot goes, and before it mounts again on the same slot with a new context.
 */
export type ChartChromeSlot<Context> = (slot: HTMLElement, context: Context) => () => void;

/** One command the chart decides (ADR-0281): what it is called, whether it is available, and what it does. */
export interface ChartCommand {
    /** Stable, for an icon: one of `chartWordIds`' command ids. */
    readonly id: string;
    /** What it is called, in the chart's words. */
    readonly label: string;
    /** Whether it is available: Reset range is not while nothing is narrowed, CSV not on a chart its Chart Source thinned. */
    readonly enabled: boolean;
    /** For a toggle, Chart Filters and View as table, whether what it opens is open; absent on a plain command. */
    readonly expanded?: boolean;
    /** Why it is not available, where the chart says why (CSV on a thinned chart, ADR-0344); absent otherwise. */
    readonly title?: string;
    /** Runs it, as the built-in control's click does; from the context menu it closes the menu first. Nothing once
     * the contents it was handed to are taken away or handed another context: what they drew is no longer the
     * chart's (spine rule 6). */
    readonly invoke: () => void;
}

/**
 * Chart Filters (ADR-0273): the range typed in the axis' own values, and the axis switch. The contents hold what is
 * being typed; the chart reads it when it is applied, and says what it could not read rather than guess (spine
 * rule 1).
 */
export interface ChartFiltersSlotContext {
    /** What the axis holds: a date axis may take a date input, a category axis one of {@link ChartFiltersSlotContext.labels}. */
    readonly values: AxisValues;
    /** The categories of a category axis, in order; empty otherwise. */
    readonly labels: readonly string[];
    /** The range's start as the chart holds it, in the axis' text (a date as yyyy-MM-dd); `null` for the axis' own
     * start. It changes when Chart Filters opens, a drag narrows the range, or Reset range returns it, and what the
     * contents hold typed is then replaced by it. */
    readonly from: string | null;
    /** The range's end, likewise; `null` for the axis' own end. */
    readonly to: string | null;
    /** What a value looks like on this axis. */
    readonly hint: string;
    /** Why the last range applied was not taken; `null` when it was. */
    readonly error: string | null;
    /** Applies a typed range: an end empty or `null` is the axis' own end. Nothing once the contents are handed
     * another context, as a command's `invoke`. */
    readonly apply: (from: string | null, to: string | null) => void;
    /** How positions are spaced, where the axis can be switched; absent where it cannot. */
    readonly scale?: AxisScale;
    /** Switches the axis; nothing once the contents are handed another context. */
    readonly setScale: (scale: AxisScale) => void;
}

/** What the toolbar's contents are handed (ADR-0281). The slot stands above the plot; the contents draw the toolbar
 * (`role="toolbar"`, named {@link ChartToolbarSlotContext.name}) and, while {@link ChartToolbarSlotContext.filters} is
 * given, Chart Filters below it. */
export interface ChartToolbarSlotContext {
    /** The toolbar's accessible name: the chart's title, or its word for a chart. */
    readonly name: string;
    /** Chart Filters' toggle, Reset range, View as table, then CSV, SVG and PNG, in that order. */
    readonly commands: readonly ChartCommand[];
    /** Chart Filters while it is open; absent while it is closed. */
    readonly filters?: ChartFiltersSlotContext;
    /** One of the chart's words by its id (`chartWordIds`): the Consumer's `label`, else the built-in English. */
    readonly label: (id: string) => string;
}

/** What the context menu's contents are handed (ADR-0281). The slot is the menu's frame (`role="menu"`, named by
 * the chart), which opens where it was asked for and takes the keyboard; a click outside it, Escape on it or a
 * command invoked closes it. The contents draw one item per command, in this order. */
export interface ChartMenuSlotContext {
    /** The menu's accessible name. */
    readonly name: string;
    /** Reset range, View as table, then CSV, SVG and PNG. */
    readonly commands: readonly ChartCommand[];
    /** Closes the menu, for a dismissal of the contents' own; nothing once the menu has closed. */
    readonly close: () => void;
}

/** The Chrome slots a chart takes: the parts a Wrapper draws, each in place of the built-in markup's. A slot not
 * given is the built-in markup's. */
export interface ChartChrome {
    /** The toolbar, with Chart Filters' range and the axis switch (ADR-0273, ADR-0281). */
    readonly toolbar?: ChartChromeSlot<ChartToolbarSlotContext>;
    /** The context menu's contents (ADR-0281). */
    readonly menu?: ChartChromeSlot<ChartMenuSlotContext>;
}

const slotNames: readonly (keyof ChartChrome)[] = ['toolbar', 'menu'];

/** A chart's `chrome`, checked as a script may hand it: an object whose slots are each a mount function. */
export function checkedChartChrome(chrome: unknown): ChartChrome | undefined {
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) {
        throw new TypeError('An ExChart\'s chrome is an object of Chrome slots, each a mount function (ADR-0390).');
    }
    for (const [name, slot] of Object.entries(chrome)) {
        if (!(slotNames as readonly string[]).includes(name)) {
            throw new TypeError(`An ExChart's chrome has no slot '${name}': its slots are ${slotNames.join(', ')} (ADR-0390).`);
        }
        if (slot !== undefined && typeof slot !== 'function') {
            throw new TypeError(`An ExChart's chrome.${name} is a mount function, (slot, context) => cleanup (ADR-0350 section 9).`);
        }
    }
    return chrome;
}

/**
 * The contents a slot shows: mounted by a mount function with a context, mounted again only when the mount function
 * or what the context holds (its `key`) changed, and taken away when the part goes. A context's callbacks act only
 * while the contents it was handed to stand: one handed to contents taken away, or to contents since mounted again
 * with another, does nothing, as the grid's slots' do (ADR-0390), so a press on what is no longer drawn never acts on
 * what the chart now shows (spine rule 6).
 */
export class ChartSlot<Context> {
    readonly #name: string;
    /** The slot, which stands where the part does. */
    readonly element: HTMLElement;
    #mount: ChartChromeSlot<Context> | undefined;
    #key: string | undefined;
    #cleanup: (() => void) | undefined;
    // Counts the mounts, so a callback handed to one reaches nothing once another stands.
    #mounts = 0;
    // What failed to mount, which is not tried again on every paint: it is thrown once.
    #failed: { readonly mount: ChartChromeSlot<Context>; readonly key: string } | undefined;

    constructor(name: string, element: HTMLElement) {
        this.#name = name;
        this.element = element;
        // The chart's own handlers leave what happens inside the slot to its contents.
        element.setAttribute('data-ex-chart-slot', name);
    }

    /** Whether contents stand, mounted by `mount` for `key`. */
    shows(mount: ChartChromeSlot<Context>, key: string): boolean {
        return this.#cleanup !== undefined && this.#mount === mount && this.#key === key;
    }

    /** Shows the contents `mount` draws from the context `context` makes, unless they already stand for `key`.
     * `context` is handed whether the mount it is made for still stands, for its callbacks to ask. */
    show(mount: ChartChromeSlot<Context>, key: string, context: (standing: () => boolean) => Context): void {
        if (this.shows(mount, key) || (this.#failed?.mount === mount && this.#failed.key === key)) return;
        this.hide();
        this.#mounts += 1;
        const made = this.#mounts;
        const standing = (): boolean => this.#cleanup !== undefined && this.#mounts === made;
        const answered: unknown = (() => {
            try {
                return mount(this.element, context(standing));
            } catch (error) {
                this.#failed = { mount, key };
                throw error;
            }
        })();
        if (typeof answered !== 'function') {
            this.#failed = { mount, key };
            throw new TypeError(`An ExChart's chrome.${this.#name} answered ${typeof answered}: a mount function answers the function that takes its contents away.`);
        }
        this.#cleanup = () => { Reflect.apply(answered, undefined, []); };
        this.#mount = mount;
        this.#key = key;
    }

    /** The part shows nothing of the contents: they are taken away. */
    hide(): void {
        const cleanup = this.#cleanup;
        this.#cleanup = undefined;
        this.#mount = undefined;
        this.#key = undefined;
        this.#failed = undefined;
        cleanup?.();
    }
}
