// MuiExSheet (ADR-0390, ADR-0030): ExSheet for React inside MUI's Paper, its Chrome drawn in MUI's components and its
// Visual Tokens mapped onto the theme's CSS variables by the Wrappers' stylesheets, ExGrid's and the Sheet's. It owns
// no geometry, no DOM inside the Sheet's element and no state. The cells keep the Sheet's own font, whose widths its
// columns are measured in characters by (ADR-0027): a Sheet's look is its document's, so MUI's font is the Chrome's,
// never the cells', and no font is handed the grid. A theme without CSS variables is refused, since its colours exist
// only in script and the Chrome would be drawn in colours that are not the theme's (ADR-0390 section 5).

import { createElement, useMemo, type CSSProperties, type ReactElement } from 'react';
import { Paper } from '@mui/material';
import { useTheme } from '@mui/material/styles';
import { MuiSizeContext, muiSizeOf, refuseThemeWithoutVariables } from '@ex-grid/mui';
import { ExSheet, type ExSheetProps } from '@ex-grid/sheet-react';
import { muiSheetChrome } from './chrome';

/** A MuiExSheet's props: an ExSheet's, and the words MUI's Paper takes. */
export type MuiExSheetProps = ExSheetProps & {
    /** The Paper's variant: `outlined`, the default here, draws a border; `elevation` a shadow, and under the dark
     * scheme MUI lightens an elevated Paper with an overlay the Sheet's own Paper does not take. */
    readonly variant?: 'elevation' | 'outlined';
    /** The Paper's shadow depth, 0 to 24, under the `elevation` variant. */
    readonly elevation?: number;
    /** Square corners: the Paper's own word, and the Sheet set square in it with no inset. */
    readonly square?: boolean;
    /** A class for the Paper. */
    readonly className?: string;
    /** A style for the Paper. */
    readonly style?: CSSProperties;
};

/**
 * An Excel-like sheet with formulas drawn with MUI: the Chrome slots a `chrome` prop leaves are MUI's, the Sheet
 * Toolbar, Format Cells and the Sheet Tabs among them, and the colours follow the theme and its scheme with no render.
 * Needs a ThemeProvider above it whose theme is made with `createTheme({ cssVariables: true })`, and
 * `@ex-grid/mui/styles.css` and `@ex-grid/sheet-mui/styles.css` loaded.
 */
export function MuiExSheet(props: MuiExSheetProps): ReactElement {
    const { variant = 'outlined', elevation, square, className, style, chrome, ...sheet } = props;
    refuseThemeWithoutVariables(useTheme(), 'MuiExSheet');
    const density = sheet.density ?? 'compact'; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0028: an untouched Sheet is compact
    // The Consumer's slots win over the Wrapper's; one merge per chrome handed, so a render function handed again
    // keeps its mount (ADR-0390).
    const merged = useMemo(() => (chrome === undefined ? muiSheetChrome : { ...muiSheetChrome, ...chrome }), [chrome]);
    const classes = ['mui-ex-grid', 'mui-ex-sheet'];
    if (square === true) classes.push('mui-ex-grid-square');
    // A Stretch Sheet follows its container's width (ADR-0045), so the Paper round it does too.
    if (sheet.viewportWidth === 'stretch') classes.push('mui-ex-grid-stretch');
    if (className !== undefined) classes.push(className);
    return createElement(Paper, {
        className: classes.join(' '),
        variant,
        ...(style === undefined ? {} : { style }),
        ...(elevation === undefined ? {} : { elevation }),
        ...(square === undefined ? {} : { square }),
    }, createElement(MuiSizeContext.Provider, { value: muiSizeOf(density) },
        createElement(ExSheet, { ...sheet, chrome: merged })));
}
