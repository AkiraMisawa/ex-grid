import { ChartElement, type ChartOptions } from './chart-element';
import type { ChartFileFormat } from './chart-files';

/** A chart drawn into an element, handed back by {@link createChart}. */
export interface ChartHandle {
    /** Takes the options again, whole: what did not change costs nothing, and a part of the drawing that did
     * not change is not touched (ADR-0252). */
    update(options: ChartOptions): void;
    /** Stops the chart: it hears nothing more and empties its element. */
    destroy(): void;
    /**
     * Saves the chart as a file, as the user sees it (ADR-0282): `csv`, the visible series in the range shown;
     * `svg`, the drawing; `png`, that drawing painted at the screen's Device Pixel. One call is one save. False
     * for a CSV of a chart its Chart Source thinned (ADR-0344), and once the chart is destroyed.
     */
    download(format: ChartFileFormat): Promise<boolean>;
    /**
     * Returns the range the user narrowed to the whole without raising `onRangeChanged`: for a Framework Adapter
     * whose host learns only after the release that the data was replaced before it (ADR-0272, spine rule 6).
     */
    dropRange(): void;
}

/**
 * Draws a chart into `element`, which becomes its instance root (ADR-0370 section 1): the chart owns
 * everything inside it, and the element's own class and style it writes. A Framework Adapter renders the
 * element and hands the chart its props; nothing here assumes React's lifecycle or Blazor's.
 */
export function createChart(element: HTMLElement, options: ChartOptions): ChartHandle {
    const chart = new ChartElement(element, options);
    return Object.freeze({
        update: (next: ChartOptions) => { chart.update(next); },
        destroy: () => { chart.destroy(); },
        download: (format: ChartFileFormat) => chart.download(format),
        dropRange: () => { chart.dropRange(); },
    });
}
