// The React Framework Adapter (ADR-0350 section 3): it renders the element the Script Core draws into,
// hands the core its props, and unmounts it. It holds no state of the grid's, draws nothing inside the
// instance root, and decides nothing. The rows are never React's: a component per row would repeat,
// through `memo`, the fight with re-rendering that ADR-0003 measured.

import { createElement, Fragment, useLayoutEffect, useRef, type ReactElement } from 'react';
import { createGrid, type GridHandle, type GridOptions } from '@ex-grid/core';
import { holdPortals, useTemplatePortals } from './template-column';

/**
 * An ExGrid's props: the Script Core's options, whole. `locale` is the browser's language where not given,
 * read once when the grid is made (ADR-0370 section 10); the core never reads it on its own. `temporal` is
 * never the browser's: a grid with a Date column whose filter is offered is given one on every browser
 * alike, or refused on every browser alike (ADR-0350 section 16).
 */
export type ExGridProps<Row> = GridOptions<Row>;

/** The props as the core takes them: the browser's language where the Consumer gave no locale. */
function withLocale<Row>(props: ExGridProps<Row>, browserLocale: string): GridOptions<Row> {
    return props.locale === undefined ? { ...props, locale: browserLocale } : props;
}

/**
 * An Excel-like grid. The element it renders is the grid's instance root, which the Script Core owns; every
 * render hands the core the props again, and what did not change costs nothing (ADR-0003).
 */
export function ExGrid<Row>(props: ExGridProps<Row>): ReactElement {
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
        const options = withLocale(props, browserLocale.current);
        if (grid.current === undefined) {
            portals.begin();
            grid.current = createGrid(root, options);
        } else {
            grid.current.update(options);
        }
    });
    useLayoutEffect(() => () => {
        // The content goes with the grid, and nothing renders for it again.
        portals.end();
        grid.current?.destroy();
        grid.current = undefined;
        browserLocale.current = undefined;
    }, [portals]);
    return createElement(Fragment, null, createElement('div', { ref: element }), ...portals.elements());
}
