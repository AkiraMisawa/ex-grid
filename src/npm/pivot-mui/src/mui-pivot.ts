// MuiPivot (ADR-0390, ADR-0030): ExPivot for React inside MUI's Paper, its Chrome and its report grid's drawn in MUI's
// components and its Visual Tokens mapped onto the theme's CSS variables by the Wrapper's stylesheet. It owns no
// geometry, no DOM inside the pivot's root and no state: it hands the pivot its Chrome, and the pivot decides the rest.
// A theme without CSS variables is refused, since its colours exist only in script and the pivot would be drawn in
// colours that are not the theme's (ADR-0390 section 5).

import { createElement, useMemo, type CSSProperties, type ReactElement } from 'react';
import { Paper } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { MuiSizeContext, muiChrome, muiSizeOf, refuseThemeWithoutVariables } from '@ex-grid/mui';
import { ExPivot, layPivotChrome, type ExPivotProps } from '@ex-grid/pivot-react';
import { muiPivotChrome } from './pivot-chrome';

/** A MuiPivot's props: an ExPivot's, and the words MUI's Paper takes. */
export type MuiPivotProps = ExPivotProps & {
    /** The Paper's variant: `outlined`, the default here, draws a border; `elevation` a shadow, and under the dark
     * scheme MUI lightens an elevated Paper with an overlay the report's own ground does not take. */
    readonly variant?: 'elevation' | 'outlined';
    /** The Paper's shadow depth, 0 to 24, under the `elevation` variant. */
    readonly elevation?: number;
    /** Square corners: the Paper's own word, and the pivot set square in it with no inset. */
    readonly square?: boolean;
    /** A class for the Paper. */
    readonly className?: string;
    /** A style for the Paper. */
    readonly style?: CSSProperties;
};

/**
 * Excel's PivotTable drawn with MUI: the Chrome slots a `chrome` prop leaves, the pivot's and its report grid's, are
 * MUI's, and the colours follow the theme and its scheme with no render. Needs a ThemeProvider above it whose theme is
 * made with `createTheme({ cssVariables: true })`, and `@ex-grid/mui/styles.css` and `@ex-grid/pivot-mui/styles.css`
 * loaded.
 */
export function MuiPivot(props: MuiPivotProps): ReactElement {
    const { variant = 'outlined', elevation, square, className, style, chrome, ...pivot } = props;
    refuseThemeWithoutVariables(useTheme(), 'MuiPivot');
    const density = pivot.density ?? 'compact'; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: an untouched grid is compact
    // One merge per chrome handed, so a render function handed again keeps its mount (ADR-0390).
    const slots = useMemo(() => layPivotChrome(muiPivotChrome, muiChrome, chrome), [chrome]);
    const classes = ['mui-ex-grid', 'mui-ex-pivot'];
    if (square === true) classes.push('mui-ex-grid-square');
    // The report takes what the Field List leaves of the pivot's width unless it is given one (ADR-0045), so the Paper
    // round it is as wide as its container, as a Stretch grid's is, rather than as wide as its content.
    if (pivot.viewportWidth === undefined || pivot.viewportWidth === 'stretch') classes.push('mui-ex-grid-stretch');
    if (className !== undefined) classes.push(className);
    return createElement(Paper, {
        className: classes.join(' '),
        variant,
        ...(style === undefined ? {} : { style }),
        ...(elevation === undefined ? {} : { elevation }),
        ...(square === undefined ? {} : { square }),
    }, createElement(MuiSizeContext.Provider, { value: muiSizeOf(density) },
        createElement(ExPivot, { ...pivot, chrome: slots })));
}
