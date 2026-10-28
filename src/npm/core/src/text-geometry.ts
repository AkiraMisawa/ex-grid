// The text geometry a grid charges text with (ADR-0016), handed out as plain data, so a Consumer that lays text
// out beside the grid's, or has it estimated elsewhere, charges it alike: ExPivot tells its report source the
// geometry its label widths are estimated under, as the Razor component tells it the grid's Cell Metrics
// (ADR-0151). Nothing here reads a browser's layout (ADR-0021).

import type { CellTextMetrics } from './columns/cell-text-metrics';
import { GridMetrics, type GridDensity } from './grid-metrics';
import type { GridPresentationDefaults } from './grid-presentation-defaults';

/** The widths a grid charges each character class, its cells' padding, and single glyphs, in pixels. */
export interface GridTextGeometry {
    /** `%`, `€`, `−`, `+`, `#`. */
    readonly wideWidthPx: number;
    /** Digits, currency signs and the characters numbers are written with. */
    readonly digitWidthPx: number;
    /** `.`, `,`, `(`, `)`, `/`, `:` and the space. */
    readonly narrowWidthPx: number;
    /** A full-width character, the em of the grid's font. */
    readonly fullWidthPx: number;
    /** Any other character. */
    readonly otherWidthPx: number;
    /** The padding on one side of a cell. */
    readonly cellHorizontalPaddingPx: number;
    /** Single glyphs charged at their own width, by code point. */
    readonly glyphWidths: Readonly<Record<number, number>>;
}

/** What a grid's text is charged by, as `createGrid` takes it (ADR-0028): its Density, its Cell Metrics, and a
 * Wrapper's defaults, each beating the next. */
export interface GridTextOptions {
    /** The preset, beating a Wrapper's; compact where neither gives one. */
    readonly density?: GridDensity;
    /** The Cell Metrics, beating a Wrapper's widths and the preset's. */
    readonly cellMetrics?: CellTextMetrics;
    /** A Wrapper's widths and Density (ADR-0030). */
    readonly presentationDefaults?: GridPresentationDefaults;
}

// Each grid's metrics and geometry, made once: resolving builds the glyph table. Metrics and defaults given are
// told apart by identity, as a grid tells a Wrapper's defaults apart.
const presets = new Map<GridDensity, GridMetrics>();
const given = new WeakMap<object, Map<GridDensity, GridMetrics>>();
const geometries = new WeakMap<GridMetrics, GridTextGeometry>();

function metricsAt(options: GridTextOptions): GridMetrics {
    const { cellMetrics, presentationDefaults: defaults } = options;
    // oxlint-disable-next-line ex-grid/no-stand-in-default -- ADR-0028: an untouched grid is compact, as createGrid's is
    const density = options.density ?? defaults?.density ?? 'compact';
    const keyed = cellMetrics ?? defaults;
    if (keyed === undefined) {
        let metrics = presets.get(density);
        if (metrics === undefined) {
            metrics = GridMetrics.resolve({ density });
            presets.set(density, metrics);
        }
        return metrics;
    }
    let byDensity = given.get(keyed);
    if (byDensity === undefined) {
        byDensity = new Map();
        given.set(keyed, byDensity);
    }
    // Metrics given beat the defaults' widths, so the defaults count only for their Density, which the key names.
    let metrics = byDensity.get(density);
    if (metrics === undefined) {
        metrics = GridMetrics.resolve({
            density,
            ...(cellMetrics === undefined ? {} : { cellMetrics }),
            ...(defaults === undefined ? {} : { defaults }),
        });
        byDensity.set(density, metrics);
    }
    return metrics;
}

/** The text geometry of a grid drawn with `options`, as `createGrid` resolves it: plain data, which crosses to a
 * Worker as it is. A row height does not move it. */
export function gridTextGeometry(options: GridTextOptions = {}): GridTextGeometry {
    const resolved = metricsAt(options);
    const held = geometries.get(resolved);
    if (held !== undefined) return held;
    const metrics = resolved.cellMetrics;
    const geometry: GridTextGeometry = Object.freeze({
        wideWidthPx: metrics.wideWidthPx,
        digitWidthPx: metrics.digitWidthPx,
        narrowWidthPx: metrics.narrowWidthPx,
        fullWidthPx: metrics.fullWidthPx,
        otherWidthPx: metrics.otherWidthPx,
        cellHorizontalPaddingPx: metrics.cellHorizontalPaddingPx,
        glyphWidths: metrics.exportGlyphWidths(),
    });
    geometries.set(resolved, geometry);
    return geometry;
}

/** The cell width a header needs in a grid drawn with `options`, as an Auto width charges it (ADR-0016): its label,
 * an em of slack, the column-menu button's band when `menuButton`, and the sort mark's box when `sortable`. A
 * Consumer that fixes a column's width beside the grid's own charges its header alike. */
export function gridHeaderRequiredPx(label: string, options: GridTextOptions & { readonly menuButton: boolean; readonly sortable: boolean }): number {
    const { menuButton, sortable } = options;
    return metricsAt(options).headerRequiredPx({ label, menuButton, sortable });
}
