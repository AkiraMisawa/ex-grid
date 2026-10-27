// MantinePivot (ADR-0390, ADR-0030): ExPivot for React inside Mantine's Paper, its Chrome and its report grid's drawn
// in Mantine's components and its Visual Tokens mapped onto the Mantine theme by the Wrapper's stylesheet. It owns no
// geometry, no DOM inside the pivot's root and no state: it hands the pivot its Chrome, and the pivot decides the rest.

import { createElement, useMemo, type CSSProperties, type ReactElement } from 'react';
import { Paper, type MantineRadius, type MantineShadow } from '@mantine/core';
import { MantineSizeContext, mantineChrome, mantineSizeOf } from '@ex-grid/mantine';
import { ExPivot, type ExPivotChrome, type ExPivotProps } from '@ex-grid/pivot-react';
import { mantinePivotChrome } from './pivot-chrome';

/** A MantinePivot's props: an ExPivot's, and the Paper's words Mantine draws it with. */
export type MantinePivotProps = ExPivotProps & {
    /** The Paper's border, Mantine's `withBorder`. */
    readonly withBorder?: boolean;
    /** The Paper's corner radius, a theme radius or a CSS length. */
    readonly radius?: MantineRadius;
    /** The Paper's shadow. */
    readonly shadow?: MantineShadow;
    /** A class for the Paper. */
    readonly className?: string;
    /** A style for the Paper. */
    readonly style?: CSSProperties;
};

/** The slots of `over` given a render function laid over `under`: a slot given as `undefined` leaves the Wrapper's,
 * as one not named does. */
function laid<T extends object>(under: T, over: Partial<T>): T {
    return { ...under, ...Object.fromEntries(Object.entries(over).filter(([, slot]) => slot !== undefined)) };
}

/** The Wrapper's Chrome with the Consumer's slots over it, the report grid's included. The grid's Chrome is the
 * Wrapper's own object while the Consumer names none of its slots, so a Chrome handed again with only pivot slots does
 * not dress the report's grid again. */
function merged(chrome: ExPivotChrome | undefined): ExPivotChrome {
    if (chrome === undefined) return { ...mantinePivotChrome, grid: mantineChrome };
    const { grid, ...pivot } = chrome;
    return { ...laid(mantinePivotChrome, pivot), grid: grid === undefined ? mantineChrome : laid(mantineChrome, grid) };
}

/**
 * Excel's PivotTable drawn with Mantine: the Chrome slots a `chrome` prop leaves, the pivot's and its report grid's,
 * are Mantine's, and the colours follow the theme and its scheme with no render. Needs Mantine's MantineProvider above
 * it, and `@ex-grid/mantine/styles.css` and `@ex-grid/pivot-mantine/styles.css` loaded after Mantine's styles.
 */
export function MantinePivot(props: MantinePivotProps): ReactElement {
    const { withBorder, radius, shadow, className, style, chrome, ...pivot } = props;
    const density = pivot.density ?? 'compact'; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: an untouched grid is compact
    // One merge per chrome handed, so a render function handed again keeps its mount (ADR-0390).
    const slots = useMemo(() => merged(chrome), [chrome]);
    const classes = ['mantine-ex-grid', 'mantine-ex-pivot'];
    // The report takes what the Field List leaves of the pivot's width unless it is given one (ADR-0045), so the Paper
    // round it is as wide as its container, as a Stretch grid's is, rather than as wide as its content.
    if (pivot.viewportWidth === undefined || pivot.viewportWidth === 'stretch') classes.push('mantine-ex-grid-stretch');
    if (className !== undefined) classes.push(className);
    return createElement(Paper<'div'>, {
        className: classes.join(' '),
        ...(style === undefined ? {} : { style }),
        ...(withBorder === undefined ? {} : { withBorder }),
        ...(radius === undefined ? {} : { radius }),
        ...(shadow === undefined ? {} : { shadow }),
    }, createElement(MantineSizeContext.Provider, { value: mantineSizeOf(density) },
        createElement(ExPivot, { ...pivot, chrome: slots })));
}
