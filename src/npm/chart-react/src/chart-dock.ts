// The Chart Dock's state (ADR-0258, ADR-0281): the charts made from a grid's selection, each a tab named "Chart N",
// the tab shown, and the last refusal. A design-system Wrapper draws it (ADR-0390 section 3: shared below a design
// system, nothing above it), as the C# MudChartDock holds it. The dock takes the bridge's events: a chart made opens
// as a new tab, one with a series added replaces its tab, a refusal is said above the tabs, and every open chart is
// a target of "Add to chart".

import { createElement, Fragment, useEffect, useMemo, useRef, useState, useSyncExternalStore, type ReactElement, type ReactNode, type RefObject } from 'react';
import { ChartRefusedError, type ChartDefinition, type ChartPointClick, type ChartTarget, type GridChart, type GridChartBridge, type GridChartBridgeOptions } from '@ex-grid/chart';
import { ExChart, type ExChartChrome } from './ex-chart';

/** Where a Chart Dock puts its charts: beside the grid or below it, never over it (ADR-0258). */
export type ChartDockPlacement = 'right' | 'bottom';

/** The bridge's events the dock takes, to spread into a `GridChartBridge`'s options. Made once per dock. */
export type ChartDockBridgeOptions = Required<Pick<GridChartBridgeOptions<unknown>, 'targets' | 'onChart' | 'onAdded' | 'onRefused'>>;

/** What a Chart Dock shows, and what its controls do. A new value is made only when something in it changed. */
export interface ChartDockState {
    /** The open charts, by name, in their tabs' order. */
    readonly charts: readonly ChartTarget[];
    /** The name of the tab shown; `undefined` while no chart is open. */
    readonly active: string | undefined;
    /** Why the last selection was not charted, or a point not shown in the grid; `undefined` once dismissed, and once
     * a chart is made or added to. */
    readonly refusal: string | undefined;
    /** The bridge's events, for the `GridChartBridge` of the dock's grid: the same object for the dock's lifetime. */
    readonly bridgeOptions: ChartDockBridgeOptions;
    /** Opens a chart in a new tab, named "Chart N", and shows it. */
    readonly open: (chart: GridChart) => void;
    /** Shows the tab named `name`. */
    readonly select: (name: string) => void;
    /** Closes the tab named `name`; the tab beside it is shown in its place. */
    readonly close: (name: string) => void;
    /** Dismisses the refusal. */
    readonly dismiss: () => void;
}

/** The store behind {@link useChartDock}: it changes outside React, when the bridge raises an event. */
class ChartDockStore {
    #charts: readonly ChartTarget[] = Object.freeze([]);
    #active: string | undefined;
    #refusal: string | undefined;
    #made = 0;
    #state: ChartDockState;
    readonly #listeners = new Set<() => void>();

    readonly bridgeOptions: ChartDockBridgeOptions = Object.freeze({
        targets: () => this.#charts,
        onChart: (chart: GridChart) => { this.open(chart); },
        onAdded: (target: ChartTarget, chart: GridChart) => {
            const at = this.#charts.findIndex(c => c.name === target.name);
            // "Add to chart" was taken against the chart the menu offered: one closed or changed since is refused by
            // name, never overwritten with what it held then (spine rule 6).
            if (at < 0) {
                this.#refuse(`${target.name} was closed before the series was added to it.`);
                return;
            }
            if (this.#charts[at]?.chart !== target.chart) {
                this.#refuse(`${target.name} changed before the series was added to it; add it again.`);
                return;
            }
            this.#charts = Object.freeze(this.#charts.map((c, i) => (i === at ? Object.freeze({ name: c.name, chart }) : c)));
            this.#active = target.name;
            this.#refusal = undefined;
            this.#changed();
        },
        onRefused: (why: string) => {
            if (typeof why !== 'string') throw new TypeError('A refusal is the sentence that says why.');
            this.#refuse(why);
        },
    });

    constructor() {
        this.#state = this.#snapshot();
    }

    readonly subscribe = (listener: () => void): (() => void) => {
        this.#listeners.add(listener);
        return () => { this.#listeners.delete(listener); };
    };

    readonly current = (): ChartDockState => this.#state;

    readonly open = (chart: GridChart): void => {
        this.#made += 1;
        const name = `Chart ${String(this.#made)}`;
        this.#charts = Object.freeze([...this.#charts, Object.freeze({ name, chart })]);
        this.#active = name;
        this.#refusal = undefined;
        this.#changed();
    };

    readonly select = (name: string): void => {
        if (!this.#charts.some(c => c.name === name)) throw new RangeError(`No open chart is named '${name}'.`);
        if (this.#active === name) return;
        this.#active = name;
        this.#changed();
    };

    readonly close = (name: string): void => {
        const at = this.#charts.findIndex(c => c.name === name);
        if (at < 0) return; // Closed already: a second press on a tab going away.
        const charts = this.#charts.filter((_, i) => i !== at);
        if (this.#active === name) {
            this.#active = charts[Math.min(at, charts.length - 1)]?.name;
        }
        this.#charts = Object.freeze(charts);
        this.#changed();
    };

    readonly dismiss = (): void => {
        if (this.#refusal === undefined) return;
        this.#refusal = undefined;
        this.#changed();
    };

    #refuse(why: string): void {
        this.#refusal = why;
        this.#changed();
    }

    #changed(): void {
        this.#state = this.#snapshot();
        for (const listener of this.#listeners) listener();
    }

    #snapshot(): ChartDockState {
        return Object.freeze({
            charts: this.#charts,
            active: this.#active,
            refusal: this.#refusal,
            bridgeOptions: this.bridgeOptions,
            open: this.open,
            select: this.select,
            close: this.close,
            dismiss: this.dismiss,
        });
    }
}

/**
 * Holds a Chart Dock's state for a component's lifetime. Spread its `bridgeOptions` into the grid's
 * `GridChartBridge`, and hand the state to a design system's dock (`MantineChartDock`, `MuiChartDock`) or to one of
 * the Consumer's own: the context menu's "Chart selection" then opens a tab directly, "Add to chart" offers every
 * open tab, and a refusal is said above the tabs.
 */
export function useChartDock(): ChartDockState {
    const [store] = useState(() => new ChartDockStore());
    return useSyncExternalStore(store.subscribe, store.current, store.current);
}

/** What {@link useChartDockKeyboard} hands a design system's dock. */
export interface ChartDockKeyboard {
    /** For the panel that holds the tabs. */
    readonly panel: RefObject<HTMLDivElement | null>;
    /** For the element that holds the grid. */
    readonly content: RefObject<HTMLDivElement | null>;
    /** Closes the tab named `name` from the keyboard, Delete on the tab, and hands the keyboard on. */
    readonly closeByKey: (name: string) => void;
}

// What takes the keyboard in the grid's place: the first stop the browser's Tab would reach.
const tabStop = '[tabindex]:not([tabindex="-1"]), button:not([disabled]), input:not([disabled]), select:not([disabled]), textarea:not([disabled]), a[href]';

/**
 * Where the keyboard goes when a Chart Dock's tab is closed from it: to the tab shown in its place once that tab is
 * drawn, or, the last tab closed and the panel gone with it, back to the grid, so the next key reaches what is on
 * screen rather than the page's body.
 */
export function useChartDockKeyboard(dock: ChartDockState): ChartDockKeyboard {
    const panel = useRef<HTMLDivElement>(null);
    const content = useRef<HTMLDivElement>(null);
    const pending = useRef(false);
    useEffect(() => {
        if (!pending.current) return;
        pending.current = false;
        const shown = panel.current?.querySelector<HTMLElement>('[role=tab][aria-selected=true]');
        if (shown !== null && shown !== undefined) shown.focus();
        else content.current?.querySelector<HTMLElement>(tabStop)?.focus();
    });
    const { close } = dock;
    const closeByKey = useMemo(() => (name: string): void => {
        pending.current = true;
        close(name);
    }, [close]);
    return { panel, content, closeByKey };
}

/** What a {@link DockedChart} is drawn from. */
export interface DockedChartProps<Row> {
    /** The bridge the chart was made by, which draws it from the grid's rows as they are now. */
    readonly bridge: GridChartBridge<Row>;
    /** The open chart. */
    readonly target: ChartTarget;
    /** The chart's width in CSS pixels. */
    readonly width: number;
    /** The chart's height in CSS pixels. */
    readonly height: number;
    /** The chart's Chrome: the design system's toolbar and menu. */
    readonly chrome?: ExChartChrome;
    /** Draws why the chart can no longer be drawn, in the design system's alert. */
    readonly refused: (why: string) => ReactNode;
}

/**
 * One chart of a Chart Dock, drawn again from the grid's rows on every render, so it follows them (ADR-0258); the
 * chart skips what did not change. A chart that can no longer be drawn, its axis column gone from the grid, says why
 * in its tab, rather than show what it last drew as if it were current (spine rule 1). A click on a point shows its
 * cell in the grid. Never memoised: the rows change under the same props, and the page's render is what tells it.
 */
export function DockedChart<Row>(props: DockedChartProps<Row>): ReactElement {
    const { bridge, target, width, height, chrome, refused } = props;
    const { chart, name } = target;
    // Held per chart, never made per render: a new function is a changed option.
    const onPointClick = useMemo(() => (click: ChartPointClick): void => {
        void bridge.showInGrid(chart, click);
    }, [bridge, chart]);
    let definition: ChartDefinition;
    try {
        definition = bridge.draw(chart);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- spine rule 1: a chart its rows no longer draw says why in its tab; anything else is thrown on
        if (error instanceof ChartRefusedError) return createElement(Fragment, null, refused(error.message));
        throw error;
    }
    return createElement(ExChart, { definition, title: name, width, height, onPointClick, ...(chrome === undefined ? {} : { chrome }) });
}

