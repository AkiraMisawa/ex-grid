/**
 * Measured widths for single glyphs, regular and bold, at the size they were measured at (ADR-0016;
 * ticket 83). The Cell Metrics charge a glyph the table holds its own width instead of its class's: a
 * letter or a currency sign costs what it paints. A table speaks for one face, so it holds only glyphs
 * that face draws itself; whoever sets the face owes the table, as they owe the class widths
 * (ADR-0027). `tests/GlyphWidths` measures one.
 */
export class GlyphWidthTable {
    /** The font size the widths are true at; the Cell Metrics scale them to theirs. */
    readonly measuredAtPx: number;
    readonly #widths: ReadonlyMap<number, readonly [regular: number, bold: number]>;

    /**
     * A table of `glyphs` measured at `measuredAtPx`: each a single character (one code point), with its
     * width at the regular weights the grid paints and at the bold one. Refuses a glyph given twice, a
     * string that is not one character, and a width that is not a finite, positive number of pixels.
     */
    constructor(measuredAtPx: number, glyphs: Iterable<readonly [glyph: string, regularPx: number, boldPx: number]>) {
        if (!Number.isFinite(measuredAtPx) || measuredAtPx <= 0) {
            throw new RangeError(`The size a table was measured at is a finite, positive number of pixels. (measuredAtPx: ${measuredAtPx})`);
        }
        const widths = new Map<number, readonly [number, number]>();
        for (const [glyph, regularPx, boldPx] of glyphs) {
            const point = glyph.codePointAt(0);
            if (point === undefined || String.fromCodePoint(point) !== glyph || (point >= 0xD800 && point <= 0xDFFF)) {
                throw new TypeError(`'${glyph}' is not one character.`);
            }
            if (!Number.isFinite(regularPx) || regularPx <= 0 || !Number.isFinite(boldPx) || boldPx <= 0) {
                throw new RangeError(`'${glyph}' needs finite, positive widths.`);
            }
            if (widths.has(point)) {
                throw new TypeError(`'${glyph}' is in the table twice.`);
            }
            widths.set(point, Object.freeze([regularPx, boldPx] as const));
        }
        this.measuredAtPx = measuredAtPx;
        this.#widths = widths;
    }

    /** How many glyphs the table holds. */
    get count(): number {
        return this.#widths.size;
    }

    /** The code points the table holds. */
    get codePoints(): Iterable<number> {
        return this.#widths.keys();
    }

    /** The width of `codePoint` at {@link GlyphWidthTable.measuredAtPx}, at the bold weight or the
     * regular ones, or `undefined` when the table does not hold it. */
    widthPxOf(codePoint: number, bold: boolean): number | undefined {
        const widths = this.#widths.get(codePoint);
        if (widths === undefined) {
            return undefined;
        }
        return bold ? widths[1] : widths[0];
    }
}
