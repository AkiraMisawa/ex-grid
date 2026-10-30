// What @ex-grid/mui hands the grids inside it (ADR-0030, ADR-0390): the widths of the font its Paper sets and the
// size MUI's components are drawn at for each Density. Whoever sets the font owes the widths (ADR-0027), so the font
// and its widths travel in one value.

import { GridPresentationDefaults, type GridDensity, type GridPresentationDefaultsOptions } from '@ex-grid/core';
import { muiClassWidths, muiGlyphWidths } from './mui-glyph-widths';

/** The widths a font is charged at, in the forms the grid takes: everything a Wrapper's presentation defaults hold
 * but the Density, hover band and Row Stripes, which are the grid's own props under MUI. */
export type MuiExGridFontWidths = Omit<GridPresentationDefaultsOptions, 'density' | 'highlightHoverRow' | 'stripeRows'>;

/** A font for the grid's cells, with the widths measured for it (ADR-0027): `family` is written as
 * `--ex-font-family` and `widths` are what `####` is judged by, in the same render. */
export interface MuiExGridFont {
    /** The CSS font family list, as `font-family` takes it. */
    readonly family: string;
    /** The widths measured for that family (`tests/GlyphWidths` measures one). */
    readonly widths: MuiExGridFontWidths;
}

/** MUI's default font stack (`Roboto`, `Helvetica`, `Arial`, `sans-serif`) with the widest widths any face it
 * resolves to was measured at: Roboto, Liberation Sans for Helvetica and Arial, whose widths it was made to match, and
 * DejaVu Sans. What the cells are drawn in unless a font is given. */
export const muiDefaultFont: MuiExGridFont = Object.freeze({
    family: '"Roboto", "Helvetica", "Arial", sans-serif',
    widths: Object.freeze({
        ...muiClassWidths,
        get glyphWidths() { return muiGlyphWidths(); },
    }),
});

/** What the Wrapper hands a grid: `font`'s widths. A value the grid is given beats these, and these beat the preset
 * (ADR-0028). */
export function muiPresentationDefaults(font: MuiExGridFont): GridPresentationDefaults {
    return new GridPresentationDefaults({ ...font.widths });
}

/** The size MUI's components are drawn at inside a grid of one Density (ADR-0390 section 10). */
export interface MuiExGridSize {
    /** The `size` MUI's buttons, fields, checkboxes and radios take. */
    readonly size: 'small' | 'medium';
    /** Whether menu items are drawn `dense`, MUI's own word for a compact list. */
    readonly dense: boolean;
}

const compactSize: MuiExGridSize = Object.freeze({ size: 'small', dense: true });
const defaultSize: MuiExGridSize = Object.freeze({ size: 'medium', dense: false });

/** The size MUI's components are drawn at in a grid of `density` (ADR-0390 section 10): `small` and dense menus
 * for the compact ones, MUI's own default, `medium`, for Standard and Comfortable, since MUI has no larger field. */
export function muiSizeOf(density: GridDensity): MuiExGridSize {
    switch (density) {
        case 'compact':
        case 'excel':
            return compactSize;
        case 'standard':
        case 'comfortable':
            return defaultSize;
        default:
            throw new RangeError(`Unknown density preset '${String(density satisfies never)}' (ADR-0028).`);
    }
}
