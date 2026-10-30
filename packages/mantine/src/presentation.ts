// What @ex-grid/mantine hands the grids inside it (ADR-0030, ADR-0390): the widths of the font its stylesheet sets
// and the preset Mantine's own words map onto. Whoever sets the font owes the widths (ADR-0027), so the font and its
// widths travel in one value.

import { GridPresentationDefaults, type GridDensity, type GridPresentationDefaultsOptions } from '@ex-grid/core';
import type { MantineSize } from '@mantine/core';
import { mantineClassWidths, mantineGlyphWidths } from './mantine-glyph-widths';

/** The widths a font is charged at, in the forms the grid takes: everything a Wrapper's presentation defaults hold
 * but the Density, hover band and Row Stripes, which are the Wrapper's words. */
export type MantineExGridFontWidths = Omit<GridPresentationDefaultsOptions, 'density' | 'highlightHoverRow' | 'stripeRows'>;

/** A font for the grid's cells, with the widths measured for it (ADR-0027): `family` is written as
 * `--ex-font-family` and `widths` are what `####` is judged by, in the same render. */
export interface MantineExGridFont {
    /** The CSS font family list, as `font-family` takes it. */
    readonly family: string;
    /** The widths measured for that family (`tests/GlyphWidths` measures one). */
    readonly widths: MantineExGridFontWidths;
}

/** Mantine's default font stack (`-apple-system`, `BlinkMacSystemFont`, `Segoe UI`, `Roboto`, `Helvetica`,
 * `Arial`, `sans-serif`) with the widest widths any face it resolves to was measured at: macOS's system face, Segoe UI,
 * Roboto, Liberation Sans for Helvetica and Arial on Linux, and DejaVu Sans. What the cells are drawn in unless a
 * font is given. */
export const mantineDefaultFont: MantineExGridFont = Object.freeze({
    family: '-apple-system, BlinkMacSystemFont, "Segoe UI", Roboto, Helvetica, Arial, sans-serif',
    widths: Object.freeze({
        ...mantineClassWidths,
        get glyphWidths() { return mantineGlyphWidths(); },
    }),
});

/** What Mantine's own words are on the Wrapper: its Table's `striped` and `highlightOnHover`. */
export interface MantinePresentationWords {
    /** The preset the grid takes where it is given none. */
    readonly density?: GridDensity;
    /** Row Stripes, Mantine's Table's `striped` (ADR-0038). */
    readonly striped?: boolean;
    /** The hover band, Mantine's Table's `highlightOnHover` (ADR-0029). */
    readonly highlightOnHover?: boolean;
}

/** What the Wrapper hands a grid: `font`'s widths, and the Density, hover band and Row Stripes its words map onto. A
 * value the grid is given beats these, and these beat the preset (ADR-0028). */
export function mantinePresentationDefaults(font: MantineExGridFont, words: MantinePresentationWords = {}): GridPresentationDefaults {
    return new GridPresentationDefaults({
        ...font.widths,
        ...(words.density === undefined ? {} : { density: words.density }),
        ...(words.striped === undefined ? {} : { stripeRows: words.striped }),
        ...(words.highlightOnHover === undefined ? {} : { highlightHoverRow: words.highlightOnHover }),
    });
}

/** The size Mantine's components are drawn at in a grid of `density` (ADR-0390): Mantine has no word like Dense, so
 * the preset picks a size, `xs` for the compact ones and `sm`, Mantine's own default, for Standard. */
export function mantineSizeOf(density: GridDensity): MantineSize {
    switch (density) {
        case 'compact':
        case 'excel':
            return 'xs';
        case 'standard':
            return 'sm';
        case 'comfortable':
            return 'md';
        default:
            throw new RangeError(`Unknown density preset '${String(density satisfies never)}' (ADR-0028).`);
    }
}
