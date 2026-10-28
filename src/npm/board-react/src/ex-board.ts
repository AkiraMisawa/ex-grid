// The React Framework Adapter of ExBoard's Script Core (ADR-0370 section 1): it renders the element the Board draws
// into, hands the Board its props, and unmounts it. It holds no state of the Board's, draws nothing inside its root
// but the Chrome slots it is handed, and decides nothing.

import { createElement, Fragment, useLayoutEffect, useMemo, useRef, useState, type ReactElement } from 'react';
import { createBoard, type BoardChrome, type BoardHandle, type BoardOptions } from '@ex-grid/board';
import { chartChromeMounts, type ChartMountsMade, type ExChartChrome } from '@ex-grid/chart-react';
import { browserPivotLocale } from '@ex-grid/pivot';
import { pivotChromeMounts, type ExPivotChrome, type PivotMountsMade } from '@ex-grid/pivot-react';
import { chromeMounts, useGridPortals, type ChromeMountsMade, type ExGridChrome, type GridPortals } from '@ex-grid/react';

/**
 * The Chrome a Board's views are drawn with, by kind, each as its product's React component takes it: the grids'
 * as ExGrid's `chrome`, the pivots' as ExPivot's, the charts' as ExChart's. ExBoard has no Wrapper of its own
 * (ADR-0370 section 5): a Wrapper's Chromes dress it, `mantineChrome`, `mantinePivotChrome` and `mantineChartChrome`
 * for Mantine. The grids' Chrome also dresses the pivots' report grids where `pivot` names no grid of its own.
 */
export interface ExBoardChrome {
    /** The grids' Chrome, and the pivots' report grids' where `pivot` names no grid of its own. */
    readonly grid?: ExGridChrome<unknown>;
    /** The pivots' Chrome. */
    readonly pivot?: ExPivotChrome;
    /** The charts' Chrome. */
    readonly chart?: ExChartChrome;
}

/**
 * An ExBoard's props: the Script Core's options, whole, its `chrome` as render functions. `locale` is the browser's
 * language where not given, the first of its languages a pivot writes in, read once, the first time the Board is given
 * none (ADR-0370 section 10); the core never reads it on its own.
 */
export type ExBoardProps = Omit<BoardOptions, 'chrome'> & {
    /** The Chrome each kind of view is drawn with (ADR-0390); the built-in markup where a kind has none. */
    readonly chrome?: ExBoardChrome;
};

/** The props as the core takes them: the browser's language where the Consumer gave no locale. */
function withLocale(props: BoardOptions, browserLocale: string | undefined): BoardOptions {
    if (props.locale !== undefined) return props;
    if (browserLocale === undefined) throw new Error('Internal: the browser\'s locale was not read for a Board given none.');
    return { ...props, locale: browserLocale };
}

/** The mount functions made for an ExBoard's render functions, by kind, and the Board's Chrome last made of them. */
interface BoardMountsMade {
    readonly grid: ChromeMountsMade;
    readonly pivot: PivotMountsMade;
    readonly chart: ChartMountsMade;
    laid?: { readonly grid: unknown; readonly pivot: unknown; readonly chart: unknown; readonly portals: GridPortals; readonly chrome: BoardChrome };
}

/** The Board's Chrome as the core takes it: each kind's render functions as the mount functions its product's React
 * component makes of them, rendered through the ExBoard's portals. A render function handed again keeps its mount
 * function, so a part a view keeps standing is not drawn again. */
function boardChromeMounts(given: ExBoardChrome | undefined, portals: GridPortals, made: BoardMountsMade): BoardChrome | undefined {
    // Checked as a script may hand it, whatever its type says.
    const chrome: unknown = given;
    if (chrome === undefined) return undefined;
    if (typeof chrome !== 'object' || chrome === null) {
        throw new TypeError('An ExBoard\'s chrome is an object of each kind\'s Chrome: grid, pivot and chart (ADR-0390).');
    }
    const { grid, pivot, chart, ...others }: { readonly grid?: unknown; readonly pivot?: unknown; readonly chart?: unknown } = chrome;
    const other = Object.keys(others)[0];
    if (other !== undefined) throw new TypeError(`An ExBoard's chrome has no kind '${other}': its kinds are grid, pivot and chart (ADR-0390).`);
    // The same kinds' Chromes in another object, as a Consumer writes them inline, are the Board's Chrome as it stands:
    // handed another, every view would be dressed again for each render of the Consumer's.
    const { laid } = made;
    if (laid !== undefined && laid.grid === grid && laid.pivot === pivot && laid.chart === chart && laid.portals === portals) return laid.chrome;
    // Each product's own maker checks what it is handed, in its own words, as its React component checks its chrome.
    // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- checked by chromeMounts
    const gridMounts = chromeMounts(grid as ExGridChrome<unknown> | undefined, portals, made.grid);
    // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- checked by pivotChromeMounts
    const pivotMounts = pivotChromeMounts(pivot as ExPivotChrome | undefined, portals, made.pivot);
    // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- checked by chartChromeMounts
    const chartMounts = chartChromeMounts(chart as ExChartChrome | undefined, portals, made.chart);
    const laidNow: BoardChrome = {
        ...(gridMounts === undefined ? {} : { grid: gridMounts }),
        ...(pivotMounts === undefined ? {} : { pivot: pivotMounts }),
        ...(chartMounts === undefined ? {} : { chart: chartMounts }),
    };
    made.laid = { grid, pivot, chart, portals, chrome: laidNow };
    return laidNow;
}

/**
 * A Board (ADR-0340): its grids, pivots and charts laid out in its rows, drawn by ExGrid, ExPivot and ExChart. The
 * element it renders is the Board's root, which the Script Core owns; every render hands the core the props again, and
 * what did not change costs nothing.
 */
export function ExBoard(props: ExBoardProps): ReactElement {
    const { chrome, ...given } = props;
    const element = useRef<HTMLDivElement>(null);
    const board = useRef<BoardHandle | undefined>(undefined);
    const browserLocale = useRef<string | undefined>(undefined);
    // The Chrome slots' content, rendered by this component into the slots its views keep (ADR-0390).
    const portals = useGridPortals();
    const [made] = useState<BoardMountsMade>(() => ({ grid: new Map(), pivot: { pivot: new Map(), grid: new Map() }, chart: {} }));
    const mounts = useMemo(() => boardChromeMounts(chrome, portals, made), [chrome, portals, made]);
    // Drawn before the browser paints, so a mounted Board is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the Board to draw into.');
        // The browser is asked only where the Consumer gave no locale: one that did needs nothing of it.
        if (given.locale === undefined) browserLocale.current ??= browserPivotLocale(navigator.languages.length > 0 ? navigator.languages : [navigator.language]);
        const options = withLocale(mounts === undefined ? given : { ...given, chrome: mounts }, browserLocale.current);
        if (board.current === undefined) {
            portals.begin();
            board.current = createBoard(root, options);
        } else {
            board.current.update(options);
        }
    });
    useLayoutEffect(() => () => {
        // The portals go first: the contents the Board's views take away as they go ask for no render of a component gone.
        portals.end();
        board.current?.destroy();
        board.current = undefined;
        browserLocale.current = undefined;
    }, [portals]);
    return createElement(Fragment, null, createElement('div', { ref: element }), ...portals.elements());
}
