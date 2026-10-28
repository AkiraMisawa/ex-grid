// The React Framework Adapter of ExChart (ADR-0370 section 1): it renders the element the chart's Script
// Core draws into, hands the core its props, and destroys it with the component. It holds no state of the
// chart's, draws nothing inside the instance root but the Chrome slots it is handed, and decides nothing: what the
// user changes reaches the Consumer through the props' events and comes back as props (ADR-0272).

import { createElement, Fragment, useImperativeHandle, useLayoutEffect, useMemo, useRef, useState, type ReactElement, type ReactNode, type Ref } from 'react';
import {
    createChart, type ChartChrome, type ChartFileFormat, type ChartHandle, type ChartMenuSlotContext, type ChartOptions, type ChartToolbarSlotContext,
} from '@ex-grid/chart';
import { useGridPortals, type GridPortals } from '@ex-grid/react';

/**
 * The chart's Chrome slots for React (ADR-0281, ADR-0390 section 3), each a render function of what the chart hands
 * that slot, rendered through a portal into the slot the chart made. A slot not given is drawn by the built-in markup.
 * The chart hands a slot a new context only when what it shows changed, and the portal is keyed by the slot, so the
 * component a render function answers keeps its state across contexts: the text typed in Chart Filters, say. Hand
 * the same function on every render, declared outside the component or memoised: a new one draws its part again.
 */
export interface ExChartChrome {
    /** The toolbar above the plot, with Chart Filters while it is open. */
    readonly toolbar?: (context: ChartToolbarSlotContext) => ReactNode;
    /** The context menu's contents, inside the chart's frame. */
    readonly menu?: (context: ChartMenuSlotContext) => ReactNode;
}

/** What a ref to an ExChart reaches: the chart's own saves, as its toolbar makes them (ADR-0282). */
export interface ExChartApi {
    /** Saves the chart as a file, as the user sees it; false where the chart saves none (`ChartHandle.download`). */
    download(format: ChartFileFormat): Promise<boolean>;
}

/** An ExChart's props: the Script Core's options, whole, its `chrome` as render functions, and a ref to its saves. */
export type ExChartProps = Omit<ChartOptions, 'chrome'> & {
    /** The parts a Wrapper's Chrome, or the Consumer's own, draws in place of the built-in markup. */
    readonly chrome?: ExChartChrome;
    /** Reaches the chart's saves. */
    readonly ref?: Ref<ExChartApi>;
};

/**
 * A chart of a Chart Definition, drawn as SVG. The element it renders is the chart's instance root, which the
 * Script Core owns; every render hands the core the props again, and what did not change costs nothing
 * (ADR-0252).
 */
export function ExChart(props: ExChartProps): ReactElement {
    const { ref, chrome, ...given } = props;
    const element = useRef<HTMLDivElement>(null);
    const chart = useRef<ChartHandle | undefined>(undefined);
    const portals = useGridPortals();
    // A render function handed again keeps its mount function, so the chart does not draw its part again.
    const [made] = useState<ChartMountsMade>(() => ({}));
    const mounts = useMemo(() => chartChromeMounts(chrome, portals, made), [chrome, portals, made]);
    const options: ChartOptions = mounts === undefined ? given : { ...given, chrome: mounts };
    useImperativeHandle(ref, () => ({
        download: (format: ChartFileFormat) => {
            const drawn = chart.current;
            if (drawn === undefined) {
                throw new Error('This ExChart is not mounted, so it has no drawing to save.');
            }
            return drawn.download(format);
        },
    }), []);
    // Drawn before the browser paints, so a mounted chart is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the chart to draw into.');
        if (chart.current === undefined) {
            portals.begin();
            chart.current = createChart(root, options);
        } else {
            chart.current.update(options);
        }
    });
    useLayoutEffect(() => () => {
        // The portals go first: the contents the chart takes away as it goes ask for no render of a component gone.
        portals.end();
        chart.current?.destroy();
        chart.current = undefined;
    }, [portals]);
    return createElement(Fragment, null, createElement('div', { ref: element }), ...portals.elements());
}

// A mount function, which takes any slot's context: each is made for one slot, and handed only that slot's.
type AnyMount = (slot: HTMLElement, context: unknown) => () => void;

/** The mount functions made for an ExChart's render functions, by slot: one handed again is mounted by the same. */
export interface ChartMountsMade {
    /** The toolbar's render function, and the mount function made for it. */
    toolbar?: { readonly render: unknown; readonly mount: (slot: HTMLElement, context: unknown) => () => void };
    /** The context menu's render function, and the mount function made for it. */
    menu?: { readonly render: unknown; readonly mount: (slot: HTMLElement, context: unknown) => () => void };
}

/** The chart's Chrome as the core takes it: each render function as a mount function rendering through the portals.
 * Which slots there are is the core's to check; a render function that is not a function is named here. Exported for
 * the family's Framework Adapters, whose products hand ExChart's Chrome on to the charts they draw, not for a
 * Consumer. */
export function chartChromeMounts(given: ExChartChrome | undefined, portals: GridPortals, made: ChartMountsMade): ChartChrome | undefined {
    // Checked as a script may hand it, whatever its type says.
    const chrome: unknown = given;
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) {
        throw new TypeError('An ExChart\'s chrome is an object of Chrome slots, each a render function of its context (ADR-0390).');
    }
    const { toolbar, menu, ...others }: { readonly toolbar?: unknown; readonly menu?: unknown } = chrome;
    const mountOf = (name: keyof ChartMountsMade, render: unknown): AnyMount | undefined => {
        if (render === undefined) return undefined;
        if (typeof render !== 'function') throw new TypeError(`An ExChart's chrome.${name} is a render function of the slot's context (ADR-0390).`);
        const before = made[name];
        if (before?.render === render) return before.mount;
        const mount: AnyMount = (slot, context) => portals.mount(slot, () => {
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the chart hands each slot's mount that slot's context, the one its render function takes
            const node = Reflect.apply(render, undefined, [context]) as ReactNode;
            return node;
        });
        made[name] = { render, mount };
        return mount;
    };
    const toolbarMount = mountOf('toolbar', toolbar);
    const menuMount = mountOf('menu', menu);
    // A slot the core does not have is handed on, for the core to refuse by name.
    return {
        ...others,
        ...(toolbarMount === undefined ? {} : { toolbar: toolbarMount }),
        ...(menuMount === undefined ? {} : { menu: menuMount }),
    };
}
