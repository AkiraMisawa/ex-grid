import type { ChartFileFormat } from './chart-files';
import { SurfaceElement, type SurfaceOptions } from './surface-element';

/** A 3D surface drawn into an element, handed back by {@link createSurface}. */
export interface SurfaceHandle {
    /** Takes the options again, whole: what did not change costs nothing. */
    update(options: SurfaceOptions): void;
    /** Stops the surface: it hears nothing more and empties its element. */
    destroy(): void;
    /**
     * Saves the drawing as it stands as `svg`, or as `png` painted at the screen's Device Pixel (ADR-0282). One
     * call is one save. False for `csv` — a surface's values are read in "View as table" — and once destroyed.
     */
    download(format: ChartFileFormat): Promise<boolean>;
    /** The drawing as it stands as SVG text, its colours resolved; `undefined` once destroyed (ADR-0282). */
    svgText(): string | undefined;
}

/**
 * Draws a 3D surface into `element`, which becomes its instance root (ADR-0274, ADR-0370 section 1): the
 * surface owns everything inside it. A Framework Adapter renders the element and hands the surface its props.
 */
export function createSurface(element: HTMLElement, options: SurfaceOptions): SurfaceHandle {
    const surface = new SurfaceElement(element, options);
    return Object.freeze({
        update: (next: SurfaceOptions) => { surface.update(next); },
        destroy: () => { surface.destroy(); },
        download: (format: ChartFileFormat) => surface.download(format),
        svgText: () => surface.svgText(),
    });
}
