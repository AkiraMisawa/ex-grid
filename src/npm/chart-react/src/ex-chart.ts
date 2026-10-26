// The React Framework Adapter of ExChart (ADR-0370 section 1): it renders the element the chart's Script
// Core draws into, hands the core its props, and destroys it with the component. It holds no state of the
// chart's, draws nothing inside the instance root, and decides nothing: what the user changes reaches the
// Consumer through the props' events and comes back as props (ADR-0272).

import { createElement, useImperativeHandle, useLayoutEffect, useRef, type ReactElement, type Ref } from 'react';
import { createChart, type ChartFileFormat, type ChartHandle, type ChartOptions } from '@ex-grid/chart';

/** What a ref to an ExChart reaches: the chart's own saves, as its toolbar makes them (ADR-0282). */
export interface ExChartApi {
    /** Saves the chart as a file, as the user sees it; false where the chart saves none (`ChartHandle.download`). */
    download(format: ChartFileFormat): Promise<boolean>;
}

/** An ExChart's props: the Script Core's options, whole, and a ref to its saves. */
export type ExChartProps = ChartOptions & {
    /** Reaches the chart's saves. */
    readonly ref?: Ref<ExChartApi>;
};

/**
 * A chart of a Chart Definition, drawn as SVG. The element it renders is the chart's instance root, which the
 * Script Core owns; every render hands the core the props again, and what did not change costs nothing
 * (ADR-0252).
 */
export function ExChart(props: ExChartProps): ReactElement {
    const { ref, ...options } = props;
    const element = useRef<HTMLDivElement>(null);
    const chart = useRef<ChartHandle | undefined>(undefined);
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
            chart.current = createChart(root, options);
        } else {
            chart.current.update(options);
        }
    });
    useLayoutEffect(() => () => {
        chart.current?.destroy();
        chart.current = undefined;
    }, []);
    return createElement('div', { ref: element });
}
