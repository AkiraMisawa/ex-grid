// The label geometry a report source estimates its label columns' widths under (ADR-0151, ADR-0153): the
// grid's resolved width of each character class, and of glyphs outside them, already scaled to the client's
// font. The source never reads a browser's layout; it is told the geometry and estimates. The C# engine's
// PivotReportLabelMetrics and PivotLabelSizing.

import type { PivotReport, PivotReportRow } from '../report/report';
import { elementAt } from '../must';
import { pieceUnits } from '../source/slicing';

/** The widths of the grid's character classes and of single glyphs, in pixels. Plain data, so it crosses
 * to a Worker as it is. */
export interface PivotLabelMetrics {
    /** `%`, `€`, `−`, `+`, `#`. */
    readonly wideWidthPx: number;
    /** Digits, currency signs, `E`, `-`, the apostrophes and spaces numbers are grouped with, and the marks of
     * direction. */
    readonly digitWidthPx: number;
    /** `.`, `,`, `(`, `)`, `/`, `:` and the space. */
    readonly narrowWidthPx: number;
    /** A full-width character: CJK, kana, Hangul, full-width forms. */
    readonly fullWidthPx: number;
    /** Any other character. */
    readonly otherWidthPx: number;
    /** The padding on one side of a cell. */
    readonly cellHorizontalPaddingPx: number;
    /** The width of single glyphs outside the classes, by code point. */
    readonly glyphWidths?: Readonly<Record<number, number>>;
}

// The glyphs a geometry gives a width of its own; none when it gives none.
function glyphsOf(metrics: PivotLabelMetrics): Readonly<Record<number, number>> {
    return metrics.glyphWidths ?? {}; // oxlint-disable-line ex-grid/no-stand-in-default -- no glyph widths are none
}

function positive(width: unknown): boolean {
    return typeof width === 'number' && Number.isFinite(width) && width > 0;
}

/** Refuses label geometry a width cannot be estimated under: a class width that is not finite and positive,
 * a negative padding, or a glyph that is no Unicode scalar or has no finite positive width. */
export function checkLabelMetrics(metrics: PivotLabelMetrics): void {
    for (const width of [metrics.wideWidthPx, metrics.digitWidthPx, metrics.narrowWidthPx, metrics.fullWidthPx, metrics.otherWidthPx]) {
        if (!positive(width)) throw new RangeError('Glyph widths must be finite and positive.');
    }
    const padding = metrics.cellHorizontalPaddingPx;
    if (typeof padding !== 'number' || !Number.isFinite(padding) || padding < 0) {
        throw new RangeError('A cell\'s horizontal padding is finite and not negative.');
    }
    for (const [point, width] of Object.entries(glyphsOf(metrics))) {
        const code = Number(point);
        const scalar = Number.isInteger(code) && code >= 0 && code <= 0x10FFFF && (code < 0xD800 || code > 0xDFFF);
        if (!scalar || !positive(width)) throw new TypeError('A glyph needs a Unicode scalar and a finite positive width.');
    }
}

/** Whether two geometries estimate alike. */
export function sameLabelMetrics(a: PivotLabelMetrics | undefined, b: PivotLabelMetrics | undefined): boolean {
    if (a === undefined || b === undefined) return a === b;
    const glyphs = Object.entries(glyphsOf(a));
    const others = glyphsOf(b);
    return a.wideWidthPx === b.wideWidthPx && a.digitWidthPx === b.digitWidthPx && a.narrowWidthPx === b.narrowWidthPx
        && a.fullWidthPx === b.fullWidthPx && a.otherWidthPx === b.otherWidthPx && a.cellHorizontalPaddingPx === b.cellHorizontalPaddingPx
        && glyphs.length === Object.keys(others).length && glyphs.every(([point, width]) => others[Number(point)] === width);
}

const wide = new Set(['%', '€', '−', '+', '#'].map(c => c.codePointAt(0)));
const narrow = new Set(['.', ',', '(', ')', '/', ':', ' '].map(c => c.codePointAt(0)));
const digitSigns = new Set(['$', '£', '¥', '₹', '₺', '₫', 'E', '-', '\'', '’', ' ', ' ', '‎', '‏', '؜']
    .map(c => c.codePointAt(0)));

function isDigitClass(c: number): boolean {
    return (c >= 0x30 && c <= 0x39) || digitSigns.has(c);
}

function isFullWidth(c: number): boolean {
    return (c >= 0x1100 && c <= 0x115F) // Hangul Jamo initials
        || (c >= 0x2E80 && c <= 0x303E) // CJK radicals, Kangxi, ideographic space and punctuation
        || (c >= 0x3041 && c <= 0x33FF) // kana, Bopomofo, Hangul compatibility, enclosed and compatibility CJK
        || (c >= 0x3400 && c <= 0x4DBF) // CJK Extension A
        || (c >= 0x4E00 && c <= 0x9FFF) // CJK Unified Ideographs
        || (c >= 0xA000 && c <= 0xA4CF) // Yi
        || (c >= 0xA960 && c <= 0xA97F) // Hangul Jamo Extended-A
        || (c >= 0xAC00 && c <= 0xD7A3) // Hangul syllables
        || (c >= 0xF900 && c <= 0xFAFF) // CJK Compatibility Ideographs
        || (c >= 0xFE10 && c <= 0xFE19) // vertical forms
        || (c >= 0xFE30 && c <= 0xFE6F) // CJK compatibility and small forms
        || (c >= 0xFF01 && c <= 0xFF60) // fullwidth forms
        || (c >= 0xFFE0 && c <= 0xFFE6) // fullwidth signs
        || (c >= 0x1F300 && c <= 0x1F64F) // pictographs and emoticons
        || (c >= 0x1F900 && c <= 0x1F9FF) // supplemental pictographs
        || (c >= 0x20000 && c <= 0x3FFFD); // CJK Extensions B onward
}

function widthOf(metrics: PivotLabelMetrics, point: number): number {
    if (wide.has(point)) return metrics.wideWidthPx;
    if (narrow.has(point)) return metrics.narrowWidthPx;
    if (isFullWidth(point)) return metrics.fullWidthPx;
    if (isDigitClass(point)) return metrics.digitWidthPx;
    const glyph = metrics.glyphWidths?.[point];
    return glyph === undefined ? metrics.otherWidthPx : glyph;
}

/** A label's text estimated under the geometry, both sides' padding included, without reading a browser's
 * layout. A lone surrogate is a character of the other class, as .NET reads it. */
export function estimateLabelPx(metrics: PivotLabelMetrics, text: string): number {
    let width = 2 * metrics.cellHorizontalPaddingPx;
    for (const character of text) width += widthOf(metrics, defined(character.codePointAt(0)));
    return width;
}

function defined(point: number | undefined): number {
    if (point === undefined) throw new Error('Internal: a character has no code point.');
    // .NET enumerates a lone surrogate as U+FFFD, which is of the other class.
    return point >= 0xD800 && point <= 0xDFFF ? 0xFFFD : point;
}

// .NET's Math.Round, which sends a tie to the even number (CONVENTIONS.md).
function roundHalfToEven(value: number): number {
    const floor = Math.floor(value);
    const diff = value - floor;
    if (diff < 0.5) return floor;
    if (diff > 0.5) return floor + 1;
    return floor % 2 === 0 ? floor : floor + 1;
}

function rowWidth(row: PivotReportRow, column: number, metrics: PivotLabelMetrics): number {
    const label = elementAt(row.labels, column);
    if (label.text === undefined && label.toggle === undefined) return 0;
    const levels = label.indent + (label.toggle === undefined ? 0 : 1);
    return estimateLabelPx(metrics, label.text ?? '') + (roundHalfToEven(metrics.fullWidthPx) * levels); // oxlint-disable-line ex-grid/no-stand-in-default -- a label with a button and no text is the button's width alone, as the C# engine measures it
}

/** The widest label of each label column, every row measured, yielding between pieces of rows. */
export function* labelWidthSteps(report: PivotReport, metrics: PivotLabelMetrics): Generator<void, readonly number[]> {
    const widths = report.labelColumns.map(() => 0);
    const size = Math.max(1, Math.floor(pieceUnits / 4));
    for (let from = 0; from < report.rows.length; from += size) {
        if (from > 0) yield;
        const to = Math.min(report.rows.length, from + size);
        for (let r = from; r < to; r++) {
            const row = elementAt(report.rows, r);
            for (let column = 0; column < widths.length; column++) {
                widths[column] = Math.max(elementAt(widths, column), rowWidth(row, column, metrics));
            }
        }
    }
    return Object.freeze(widths);
}
