// MantineExSheet (ADR-0390, ADR-0030): ExSheet for React inside Mantine's Paper, its Chrome drawn in Mantine's
// components and its Visual Tokens mapped onto the Mantine theme by the Wrappers' stylesheets, ExGrid's and the
// Sheet's. It owns no geometry, no DOM inside the Sheet's element and no state. The cells keep the Sheet's own font,
// whose widths its columns are measured in characters by (ADR-0027): a Sheet's look is its document's, so Mantine's
// font is the Chrome's, never the cells'.

import { createElement, useMemo, type CSSProperties, type ReactElement } from 'react';
import { Paper, type MantineRadius, type MantineShadow } from '@mantine/core';
import { MantineSizeContext, mantineSizeOf } from '@ex-grid/mantine';
import { ExSheet, type ExSheetProps } from '@ex-grid/sheet-react';
import { mantineSheetChrome } from './chrome';

/** A MantineExSheet's props: an ExSheet's, and the Paper's words Mantine draws it with. */
export type MantineExSheetProps = ExSheetProps & {
    /** The Paper's border, Mantine's `withBorder`. */
    readonly withBorder?: boolean;
    /** The Paper's corner radius, a theme radius or a CSS length; `0` sets the Sheet square in its Paper. */
    readonly radius?: MantineRadius;
    /** The Paper's shadow. */
    readonly shadow?: MantineShadow;
    /** A class for the Paper. */
    readonly className?: string;
    /** A style for the Paper. */
    readonly style?: CSSProperties;
};

/**
 * An Excel-like sheet with formulas drawn with Mantine: the Chrome slots a `chrome` prop leaves are Mantine's, the
 * Sheet Toolbar, Format Cells and the Sheet Tabs among them, and the colours follow the theme and its scheme with no
 * render. Needs Mantine's MantineProvider above it, and `@ex-grid/mantine/styles.css` and
 * `@ex-grid/sheet-mantine/styles.css` loaded after Mantine's styles.
 */
export function MantineExSheet(props: MantineExSheetProps): ReactElement {
    const { withBorder, radius, shadow, className, style, chrome, ...sheet } = props;
    const density = sheet.density ?? 'compact'; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: an untouched Sheet is compact
    // The Consumer's slots win over the Wrapper's; one merge per chrome handed, so a render function handed again
    // keeps its mount (ADR-0390).
    const merged = useMemo(() => (chrome === undefined ? mantineSheetChrome : { ...mantineSheetChrome, ...chrome }), [chrome]);
    const classes = ['mantine-ex-grid', 'mantine-ex-sheet'];
    if (radius === 0) classes.push('mantine-ex-grid-square');
    // A Stretch Sheet follows its container's width (ADR-0045), so the Paper round it does too.
    if (sheet.viewportWidth === 'stretch') classes.push('mantine-ex-grid-stretch');
    if (className !== undefined) classes.push(className);
    return createElement(Paper<'div'>, {
        className: classes.join(' '),
        ...(style === undefined ? {} : { style }),
        ...(withBorder === undefined ? {} : { withBorder }),
        ...(radius === undefined ? {} : { radius }),
        ...(shadow === undefined ? {} : { shadow }),
    }, createElement(MantineSizeContext.Provider, { value: mantineSizeOf(density) },
        createElement(ExSheet, { ...sheet, chrome: merged })));
}
