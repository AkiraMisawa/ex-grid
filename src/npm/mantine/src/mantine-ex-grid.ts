// MantineExGrid (ADR-0390, ADR-0030): ExGrid for React inside Mantine's Paper, its Chrome drawn in Mantine's
// components and its Visual Tokens mapped onto the Mantine theme by the Wrapper's stylesheet. It owns no geometry, no
// DOM inside the grid's root and no state: it hands the grid the widths of the font it sets, the preset Mantine's
// words map onto, and its Chrome, and the grid decides everything else.

import { createElement, useMemo, type CSSProperties, type ReactElement } from 'react';
import { Paper, type MantineRadius, type MantineShadow } from '@mantine/core';
import { ExGrid, type ExGridProps } from '@ex-grid/react';
import { MantineSizeContext, mantineChrome } from './chrome';
import { mantineDefaultFont, mantinePresentationDefaults, mantineSizeOf, type MantineExGridFont } from './presentation';

/** A MantineExGrid's props: an ExGrid's, and the Paper's and Table's words Mantine draws a grid with. No
 * `presentationDefaults`: the Wrapper's are its own, made from `font`, `striped` and `highlightOnHover`, so the
 * widths `####` is judged by are always the font's the Paper writes (ADR-0027). */
export type MantineExGridProps<Row> = Omit<ExGridProps<Row>, 'presentationDefaults'> & {
    /** The Paper's border, Mantine's `withBorder`. */
    readonly withBorder?: boolean;
    /** The Paper's corner radius, a theme radius or a CSS length; `0` sets the grid square in its Paper. */
    readonly radius?: MantineRadius;
    /** The Paper's shadow. */
    readonly shadow?: MantineShadow;
    /** Row Stripes where the grid is given no `stripeRows`, Mantine's Table's `striped` (ADR-0038). */
    readonly striped?: boolean;
    /** The hover band where the grid is given no `highlightHoverRow`, Mantine's Table's `highlightOnHover` (ADR-0029). */
    readonly highlightOnHover?: boolean;
    /** A rule between columns, Mantine's Table's `withColumnBorders`. */
    readonly withColumnBorders?: boolean;
    /** The cells' font with its widths (ADR-0027); Mantine's default stack unless given. */
    readonly font?: MantineExGridFont;
    /** A class for the Paper. */
    readonly className?: string;
    /** A style for the Paper. */
    readonly style?: CSSProperties;
};

/**
 * An Excel-like grid drawn with Mantine: the Chrome slots a `chrome` prop leaves are Mantine's, the cells are in
 * the font whose widths the grid judges `####` by, and the colours follow the theme and its scheme with no render.
 * Needs Mantine's MantineProvider above it and `@ex-grid/mantine/styles.css` loaded after Mantine's styles.
 */
export function MantineExGrid<Row>(props: MantineExGridProps<Row>): ReactElement {
    const {
        withBorder, radius, shadow, striped, highlightOnHover, withColumnBorders, font = mantineDefaultFont, className, style, chrome, ...grid
    } = props;
    const density = grid.density ?? 'compact'; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: an untouched grid is compact
    const defaults = useMemo(
        () => mantinePresentationDefaults(font, {
            ...(striped === undefined ? {} : { striped }), ...(highlightOnHover === undefined ? {} : { highlightOnHover }),
        }),
        [font, striped, highlightOnHover]);
    // The Consumer's slots win over the Wrapper's; one merge per chrome handed, so a render function handed again
    // keeps its mount (ADR-0390).
    const merged = useMemo(() => (chrome === undefined ? mantineChrome : { ...mantineChrome, ...chrome }), [chrome]);
    const classes = ['mantine-ex-grid'];
    if (radius === 0) classes.push('mantine-ex-grid-square');
    // A Stretch grid follows its container's width (ADR-0045), so the Paper round it does too, rather than take the
    // width of every column as its own.
    if (grid.viewportWidth === 'stretch') classes.push('mantine-ex-grid-stretch');
    if (withColumnBorders === true) classes.push('mantine-ex-grid-column-borders');
    if (className !== undefined) classes.push(className);
    // The family is written beside the widths the grid is handed, in the same render (ADR-0027).
    const paperStyle: CSSProperties & Readonly<Record<'--ex-font-family', string>> = { ...style, '--ex-font-family': font.family };
    return createElement(Paper<'div'>, {
        className: classes.join(' '),
        style: paperStyle,
        ...(withBorder === undefined ? {} : { withBorder }),
        ...(radius === undefined ? {} : { radius }),
        ...(shadow === undefined ? {} : { shadow }),
    }, createElement(MantineSizeContext.Provider, { value: mantineSizeOf(density) },
        createElement(ExGrid<Row>, { ...grid, presentationDefaults: defaults, chrome: merged })));
}
