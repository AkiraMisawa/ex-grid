// MuiExGrid (ADR-0390, ADR-0030): ExGrid for React inside MUI's Paper, its Chrome drawn in MUI's components and its
// Visual Tokens mapped onto the theme's CSS variables by the Wrapper's stylesheet. It owns no geometry, no DOM inside
// the grid's root and no state: it hands the grid the widths of the font it sets and its Chrome, and the grid decides
// everything else. A theme without CSS variables is refused, since its colours exist only in script and the grid
// would be drawn in colours that are not the theme's (ADR-0390 section 5).

import { createElement, useMemo, type CSSProperties, type ReactElement } from 'react';
import { Paper } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { ExGrid, type ExGridProps } from '@ex-grid/react';
import { MuiSizeContext, muiChrome } from './chrome';
import { muiDefaultFont, muiPresentationDefaults, muiSizeOf, type MuiExGridFont } from './presentation';
import { refuseThemeWithoutVariables } from './theme';

/** A MuiExGrid's props: an ExGrid's, and the words MUI's Paper takes. No `presentationDefaults`: the Wrapper's are its
 * own, made from `font`, so the widths `####` is judged by are always the font's the Paper writes (ADR-0027). */
export type MuiExGridProps<Row> = Omit<ExGridProps<Row>, 'presentationDefaults'> & {
    /** The Paper's variant: `outlined`, the default here, draws a border; `elevation` a shadow, and under the dark
     * scheme MUI lightens an elevated Paper with an overlay the grid's own ground does not take, so the Paper's inset
     * reads a shade lighter than the cells. */
    readonly variant?: 'elevation' | 'outlined';
    /** The Paper's shadow depth, 0 to 24, under the `elevation` variant. */
    readonly elevation?: number;
    /** Square corners: the Paper's own word, and the grid set square in it with no inset. */
    readonly square?: boolean;
    /** A rule between columns, as MUI's Table draws none by default. */
    readonly withColumnBorders?: boolean;
    /** The cells' font with its widths (ADR-0027); MUI's default stack unless given. */
    readonly font?: MuiExGridFont;
    /** A class for the Paper. */
    readonly className?: string;
    /** A style for the Paper. */
    readonly style?: CSSProperties;
};

/**
 * An Excel-like grid drawn with MUI: the Chrome slots a `chrome` prop leaves are MUI's, the cells are in the font
 * whose widths the grid judges `####` by, and the colours follow the theme and its scheme with no render. Needs a
 * ThemeProvider above it whose theme is made with `createTheme({ cssVariables: true })`, and
 * `@ex-grid/mui/styles.css` loaded.
 */
export function MuiExGrid<Row>(props: MuiExGridProps<Row>): ReactElement {
    const {
        variant = 'outlined', elevation, square, withColumnBorders, font = muiDefaultFont, className, style, chrome, ...grid
    } = props;
    refuseThemeWithoutVariables(useTheme(), 'MuiExGrid');
    const density = grid.density ?? 'compact'; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: an untouched grid is compact
    const defaults = useMemo(() => muiPresentationDefaults(font), [font]);
    // The Consumer's slots win over the Wrapper's; one merge per chrome handed, so a render function handed again
    // keeps its mount (ADR-0390).
    const merged = useMemo(() => (chrome === undefined ? muiChrome : { ...muiChrome, ...chrome }), [chrome]);
    const classes = ['mui-ex-grid'];
    if (square === true) classes.push('mui-ex-grid-square');
    // A Stretch grid follows its container's width (ADR-0045), so the Paper round it does too, rather than take the
    // width of every column as its own.
    if (grid.viewportWidth === 'stretch') classes.push('mui-ex-grid-stretch');
    if (withColumnBorders === true) classes.push('mui-ex-grid-column-borders');
    if (className !== undefined) classes.push(className);
    // The family is written beside the widths the grid is handed, in the same render (ADR-0027).
    const paperStyle: CSSProperties & Readonly<Record<'--ex-font-family', string>> = { ...style, '--ex-font-family': font.family };
    return createElement(Paper, {
        className: classes.join(' '),
        style: paperStyle,
        variant,
        ...(elevation === undefined ? {} : { elevation }),
        ...(square === undefined ? {} : { square }),
    }, createElement(MuiSizeContext.Provider, { value: muiSizeOf(density) },
        createElement(ExGrid<Row>, { ...grid, presentationDefaults: defaults, chrome: merged })));
}
