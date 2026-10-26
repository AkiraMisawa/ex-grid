// The React Framework Adapter (ADR-0350 section 3): it renders the element the Script Core draws into,
// hands the core its props, and unmounts it. It holds no state of the grid's, draws nothing inside the
// instance root, and decides nothing. The rows are never React's: a component per row would repeat,
// through `memo`, the fight with re-rendering that ADR-0003 measured.

import { createElement, Fragment, useImperativeHandle, useLayoutEffect, useRef, type ReactElement, type Ref } from 'react';
import { createGrid, type GridHandle, type GridOptions } from '@ex-grid/core';
import { holdPortals, useTemplatePortals } from './template-column';

/**
 * An ExGrid's props: the Script Core's options, whole. `locale` is the browser's language where not given,
 * read once when the grid is made (ADR-0370 section 10); the core never reads it on its own. `temporal` is
 * never the browser's: a grid with a Date column whose filter is offered is given one on every browser
 * alike, or refused on every browser alike (ADR-0350 section 16).
 */
export type ExGridProps<Row> = GridOptions<Row> & {
    /** A ref to what the Consumer may ask of the grid it rendered. */
    readonly ref?: Ref<ExGridApi<Row>>;
};

/**
 * What a ref to an ExGrid reaches: the Script Core's grid handle, less what the adapter itself does with it
 * (taking the props again, and destroying the grid on unmount). Asked of a grid not mounted, it throws.
 */
export type ExGridApi<Row> = Omit<GridHandle<Row>, 'update' | 'destroy'>;

/** The props as the core takes them: the browser's language where the Consumer gave no locale. */
function withLocale<Row>(props: GridOptions<Row>, browserLocale: string): GridOptions<Row> {
    return props.locale === undefined ? { ...props, locale: browserLocale } : props;
}

/**
 * An Excel-like grid. The element it renders is the grid's instance root, which the Script Core owns; every
 * render hands the core the props again, and what did not change costs nothing (ADR-0003).
 */
export function ExGrid<Row>(props: ExGridProps<Row>): ReactElement {
    const { ref, ...options } = props;
    const element = useRef<HTMLDivElement>(null);
    const grid = useRef<GridHandle<Row> | undefined>(undefined);
    const browserLocale = useRef<string | undefined>(undefined);
    // The Template cells' content, rendered by this component into the slots the core keeps (ADR-0020).
    const portals = useTemplatePortals();
    // Drawn before the browser paints, so a mounted grid is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the grid to draw into.');
        holdPortals(root, portals);
        browserLocale.current ??= navigator.language;
        const taken = withLocale(options, browserLocale.current);
        if (grid.current === undefined) {
            portals.begin();
            grid.current = createGrid(root, taken);
        } else {
            grid.current.update(taken);
        }
    });
    useImperativeHandle(ref, () => ({
        placeSelection: (range, focus, rowSequenceVersion) => mounted(grid.current).placeSelection(range, focus, rowSequenceVersion),
        readView: () => mounted(grid.current).readView(),
        showViewWith: (view, rowSequenceVersion) => { mounted(grid.current).showViewWith(view, rowSequenceVersion); },
        focus: () => { mounted(grid.current).focus(); },
        keyName: (event) => mounted(grid.current).keyName(event),
        openPopover: (content, label) => mounted(grid.current).openPopover(content, label),
        closePopover: () => { mounted(grid.current).closePopover(); },
    }), []);
    useLayoutEffect(() => () => {
        // The content goes with the grid, and nothing renders for it again.
        portals.end();
        grid.current?.destroy();
        grid.current = undefined;
        browserLocale.current = undefined;
    }, [portals]);
    return createElement(Fragment, null, createElement('div', { ref: element }), ...portals.elements());
}

function mounted<Row>(grid: GridHandle<Row> | undefined): GridHandle<Row> {
    if (grid === undefined) throw new Error('This ExGrid is not mounted: a grid not drawn is asked nothing.');
    return grid;
}
