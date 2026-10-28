// The widths a Blazor Consumer hands a grid, as they cross from .NET (ADR-0016, ADR-0030): a component's
// `CellMetrics` and a Wrapper's cascaded `GridPresentationDefaults`, each written whole as numbers, made here into
// the core's `CellTextMetrics` and `GridPresentationDefaults`. Whoever sets the font owes its widths (ADR-0027), so a
// Script grid in a Wrapper that sets one judges `####` by them, as the C# core does.

import { CellTextMetrics, GlyphWidthTable, GridPresentationDefaults, type GridDensity } from '@ex-grid/core';

/** A glyph table as .NET writes it: the size it was measured at and each glyph's widths. */
export interface WireGlyphWidths {
    readonly measuredAtPx: number;
    readonly glyphs: readonly (readonly [glyph: string, regularPx: number, boldPx: number])[];
}

/** `CellTextMetrics` as .NET writes them: every class, and the glyph table with the size the metrics are true at. */
export interface WireCellMetrics {
    readonly wideWidthPx: number;
    readonly digitWidthPx: number;
    readonly narrowWidthPx: number;
    readonly fullWidthPx: number;
    readonly cellHorizontalPaddingPx: number;
    readonly boldWideWidthPx: number;
    readonly boldDigitWidthPx: number;
    readonly boldNarrowWidthPx: number;
    readonly otherWidthPx: number;
    readonly boldOtherWidthPx: number;
    readonly glyphWidths?: WireGlyphWidths;
    /** The size the glyph table is scaled to: present with the table. */
    readonly fontSizePx?: number;
}

/** `GridPresentationDefaults` as .NET writes them: the widths of the Wrapper's font, and its words. */
export interface WirePresentationDefaults {
    readonly wideWidthPx: number;
    readonly digitWidthPx: number;
    readonly narrowWidthPx: number;
    readonly fontSizePx: number;
    readonly boldWideWidthPx: number;
    readonly boldDigitWidthPx: number;
    readonly boldNarrowWidthPx: number;
    readonly otherWidthPx: number;
    readonly boldOtherWidthPx: number;
    readonly glyphWidths?: WireGlyphWidths;
    readonly density?: GridDensity;
    readonly highlightHoverRow?: boolean;
    readonly stripeRows?: boolean;
}

function tableOf(wire: WireGlyphWidths): GlyphWidthTable {
    return new GlyphWidthTable(wire.measuredAtPx, wire.glyphs);
}

/**
 * The metrics and defaults one component crossed last, made again only when .NET writes other numbers: the core
 * tells a glyph table apart by identity, so the same numbers handed again on each render are the same objects, and
 * the grid resolves nothing anew.
 */
export class WireMetrics {
    #metrics: { readonly text: string; readonly made: CellTextMetrics } | undefined;
    #defaults: { readonly text: string; readonly made: GridPresentationDefaults } | undefined;

    /** The Cell Metrics .NET wrote, or none. */
    cellMetrics(wire: WireCellMetrics | undefined): CellTextMetrics | undefined {
        if (wire === undefined) return undefined;
        const text = JSON.stringify(wire);
        if (this.#metrics?.text === text) return this.#metrics.made;
        const { glyphWidths, fontSizePx, ...widths } = wire;
        let made = new CellTextMetrics(widths);
        if (glyphWidths !== undefined) {
            if (fontSizePx === undefined) throw new TypeError('Internal: .NET wrote a glyph table without the size it is scaled to.');
            made = made.withGlyphWidths(tableOf(glyphWidths), fontSizePx);
        }
        this.#metrics = { text, made };
        return made;
    }

    /** A Wrapper's defaults .NET wrote, or none. */
    presentationDefaults(wire: WirePresentationDefaults | undefined): GridPresentationDefaults | undefined {
        if (wire === undefined) return undefined;
        const text = JSON.stringify(wire);
        if (this.#defaults?.text === text) return this.#defaults.made;
        const { glyphWidths, ...rest } = wire;
        const made = new GridPresentationDefaults({ ...rest, ...(glyphWidths === undefined ? {} : { glyphWidths: tableOf(glyphWidths) }) });
        this.#defaults = { text, made };
        return made;
    }
}
