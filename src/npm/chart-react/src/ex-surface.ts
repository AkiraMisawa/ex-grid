// The React Framework Adapter of ExChart's 3D surface (ADR-0370 section 1, ADR-0274): it renders the element
// the surface's Script Core draws into, hands the core its props, and destroys it with the component. The
// view and the slice reach the Consumer through the props' events and come back as props (ADR-0272).

import { createElement, useImperativeHandle, useLayoutEffect, useRef, type ReactElement, type Ref } from 'react';
import { createSurface, type ChartFileFormat, type SurfaceHandle, type SurfaceOptions } from '@ex-grid/chart';

/** What a ref to an ExSurface reaches: the surface's own saves, and its drawing as SVG text (ADR-0282). */
export interface ExSurfaceApi {
    /** Saves the drawing as it stands; false where the surface saves none (`SurfaceHandle.download`). */
    download(format: ChartFileFormat): Promise<boolean>;
    /** The drawing as it stands as SVG text, its colours resolved (`SurfaceHandle.svgText`). */
    svgText(): string | undefined;
}

/** An ExSurface's props: the Script Core's options, whole, and a ref to its saves. */
export type ExSurfaceProps = SurfaceOptions & {
    /** Reaches the surface's saves. */
    readonly ref?: Ref<ExSurfaceApi>;
};

/**
 * A 3D surface of a Surface Definition, turned, zoomed and panned by the user, with the slice clicked drawn
 * beside it. The element it renders is the surface's instance root, which the Script Core owns.
 */
export function ExSurface(props: ExSurfaceProps): ReactElement {
    const { ref, ...options } = props;
    const element = useRef<HTMLDivElement>(null);
    const surface = useRef<SurfaceHandle | undefined>(undefined);
    useImperativeHandle(ref, () => {
        const mounted = (): SurfaceHandle => {
            const drawn = surface.current;
            if (drawn === undefined) {
                throw new Error('This ExSurface is not mounted, so it has no drawing.');
            }
            return drawn;
        };
        return {
            download: (format: ChartFileFormat) => mounted().download(format),
            svgText: () => mounted().svgText(),
        };
    }, []);
    // Drawn before the browser paints, so a mounted surface is never seen empty.
    useLayoutEffect(() => {
        const root = element.current;
        if (root === null) throw new Error('Internal: React mounted no element for the surface to draw into.');
        if (surface.current === undefined) {
            surface.current = createSurface(root, options);
        } else {
            surface.current.update(options);
        }
    });
    useLayoutEffect(() => () => {
        surface.current?.destroy();
        surface.current = undefined;
    }, []);
    return createElement('div', { ref: element });
}
