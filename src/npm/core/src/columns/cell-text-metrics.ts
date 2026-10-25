import { defined } from '../must';
import type { GlyphWidthTable } from './glyph-width-table';
import { type ColumnType, hashesWhenOverflowing } from './overflow-rules';

/**
 * The widths a theme supplies, in one of the forms the C# core's constructors take (ADR-0016). Each
 * form adds a group to the one before, and a group is given whole:
 *
 * - **uniform**: `digitWidthPx` and `cellHorizontalPaddingPx` alone; every character is charged the digit.
 * - **three classes**: with `wideWidthPx` and `narrowWidthPx`; the full-width class is twice the digit.
 * - **four classes**: with `fullWidthPx`, the em.
 * - **bold**: with `boldWideWidthPx`, `boldDigitWidthPx` and `boldNarrowWidthPx` (ADR-0050, item 15).
 * - **every class**: with `otherWidthPx` and `boldOtherWidthPx` (ticket 83).
 *
 * A width a form leaves out is derived by {@link CellTextMetrics.boldWidthAllowance} or
 * {@link CellTextMetrics.otherWidthAllowance}, each erring toward an early `####`.
 */
export type CellTextMetricsOptions =
    | Uniform
    | ThreeClasses
    | FourClasses
    | WithBold
    | EveryClass;

interface Uniform {
    readonly digitWidthPx: number;
    readonly cellHorizontalPaddingPx: number;
}

interface ThreeClasses extends Uniform {
    readonly wideWidthPx: number;
    readonly narrowWidthPx: number;
}

interface FourClasses extends ThreeClasses {
    readonly fullWidthPx: number;
}

interface WithBold extends FourClasses {
    readonly boldWideWidthPx: number;
    readonly boldDigitWidthPx: number;
    readonly boldNarrowWidthPx: number;
}

interface EveryClass extends WithBold {
    readonly otherWidthPx: number;
    readonly boldOtherWidthPx: number;
}

// Each group, after the uniform pair, in the order a form adds them.
const groups = [
    ['wideWidthPx', 'narrowWidthPx'],
    ['fullWidthPx'],
    ['boldWideWidthPx', 'boldDigitWidthPx', 'boldNarrowWidthPx'],
    ['otherWidthPx', 'boldOtherWidthPx'],
] as const;

const boldWidthAllowance = 1.04;
const otherWidthAllowance = 2;

/** Every width resolved: what the metrics hold, whichever form gave them. */
type Widths = Readonly<Required<EveryClass>>;

// The form the options take, by the groups they give: a group given in part, or after one left out,
// matches no form. Only an untyped caller can pass one.
function widthsOf(options: CellTextMetricsOptions): Widths {
    const given = groups.map(group => group.filter(name => name in options).length);
    const first = given.indexOf(0);
    const depth = first === -1 ? groups.length : first;
    if (!groups.every((group, i) => given[i] === (i < depth ? group.length : 0))) {
        throw new TypeError(
            'Cell metrics are given in one of the forms ADR-0016 names: the digit and the padding, then the wide '
            + 'and narrow widths, the full width, the three bold widths and the two other widths, each group whole '
            + `and none after one left out. (given: ${Object.keys(options).join(', ')})`);
    }
    const { digitWidthPx: digit, cellHorizontalPaddingPx } = options;
    if (!('wideWidthPx' in options)) {
        return {
            wideWidthPx: digit, digitWidthPx: digit, narrowWidthPx: digit, fullWidthPx: digit, cellHorizontalPaddingPx,
            boldWideWidthPx: digit * boldWidthAllowance, boldDigitWidthPx: digit * boldWidthAllowance,
            boldNarrowWidthPx: digit * boldWidthAllowance, otherWidthPx: digit, boldOtherWidthPx: digit * boldWidthAllowance,
        };
    }
    const { wideWidthPx: wide, narrowWidthPx: narrow } = options;
    const fullWidthPx = 'fullWidthPx' in options ? options.fullWidthPx : 2 * digit;
    if (!('boldDigitWidthPx' in options)) {
        return {
            wideWidthPx: wide, digitWidthPx: digit, narrowWidthPx: narrow, fullWidthPx, cellHorizontalPaddingPx,
            boldWideWidthPx: wide * boldWidthAllowance, boldDigitWidthPx: digit * boldWidthAllowance,
            boldNarrowWidthPx: narrow * boldWidthAllowance,
            otherWidthPx: digit * otherWidthAllowance, boldOtherWidthPx: digit * boldWidthAllowance * otherWidthAllowance,
        };
    }
    const { boldWideWidthPx, boldDigitWidthPx, boldNarrowWidthPx } = options;
    return {
        wideWidthPx: wide, digitWidthPx: digit, narrowWidthPx: narrow, fullWidthPx, cellHorizontalPaddingPx,
        boldWideWidthPx, boldDigitWidthPx, boldNarrowWidthPx,
        otherWidthPx: 'otherWidthPx' in options ? options.otherWidthPx : digit * otherWidthAllowance,
        boldOtherWidthPx: 'boldOtherWidthPx' in options ? options.boldOtherWidthPx : boldDigitWidthPx * otherWidthAllowance,
    };
}

// The refusals, in the C# core's order and words, so the shared cases see one message.
function check(w: Widths): void {
    const refuse = (name: keyof Widths, why: string): never => {
        throw new RangeError(`${why} (${name}: ${w[name]})`);
    };
    const { isFinite } = Number;
    if (!isFinite(w.digitWidthPx) || w.digitWidthPx <= 0) refuse('digitWidthPx', 'A digit width is a finite, positive number of pixels.');
    if (!isFinite(w.narrowWidthPx) || w.narrowWidthPx <= 0 || w.narrowWidthPx > w.digitWidthPx) {
        refuse('narrowWidthPx', 'The narrow width is a finite, positive number of pixels, no wider than the digit.');
    }
    if (!isFinite(w.wideWidthPx) || w.wideWidthPx < w.digitWidthPx) refuse('wideWidthPx', 'The wide width is finite and at least the digit\'s.');
    if (!isFinite(w.fullWidthPx) || w.fullWidthPx < w.digitWidthPx) {
        refuse('fullWidthPx', 'The full-width width is finite and at least the digit\'s.');
    }
    if (!isFinite(w.cellHorizontalPaddingPx) || w.cellHorizontalPaddingPx < 0) {
        refuse('cellHorizontalPaddingPx', 'Cell padding is a finite, non-negative number of pixels.');
    }
    if (!isFinite(w.boldDigitWidthPx) || w.boldDigitWidthPx <= 0) {
        refuse('boldDigitWidthPx', 'A bold digit width is a finite, positive number of pixels.');
    }
    if (!isFinite(w.boldNarrowWidthPx) || w.boldNarrowWidthPx <= 0 || w.boldNarrowWidthPx > w.boldDigitWidthPx) {
        refuse('boldNarrowWidthPx', 'The bold narrow width is a finite, positive number of pixels, no wider than the bold digit.');
    }
    if (!isFinite(w.boldWideWidthPx) || w.boldWideWidthPx < w.boldDigitWidthPx) {
        refuse('boldWideWidthPx', 'The bold wide width is finite and at least the bold digit\'s.');
    }
    if (!isFinite(w.otherWidthPx) || w.otherWidthPx < w.digitWidthPx) {
        refuse('otherWidthPx', 'The other width is finite and at least the digit\'s.');
    }
    if (!isFinite(w.boldOtherWidthPx) || w.boldOtherWidthPx < w.boldDigitWidthPx) {
        refuse('boldOtherWidthPx', 'The bold other width is finite and at least the bold digit\'s.');
    }
}

/**
 * The numbers the overflow estimate needs, supplied by the theme as numbers: geometry truth is the
 * core's (ADR-0013), and text measurement makes no layout read (ADR-0021).
 *
 * The estimate charges **per character class** (ADR-0016): wide, digit, narrow, full-width and other,
 * because one number cannot serve a twelve-digit amount column and a percent column at once. Each width
 * is contractually **at least as wide as any glyph of its class the column's formats emit**, at every
 * weight the grid paints, on every platform it supports: overshooting errs toward an early `####`, the
 * safe direction.
 *
 * **The other class** (ticket 83) is every glyph outside the four measured ones. A letter or currency
 * sign the face draws itself is charged its own width from a {@link GlyphWidthTable}; anything else the
 * widest glyph measured. Only a value that can become `####` is charged either: a Text value or a label is
 * charged them at the digit ({@link CellTextMetrics.for}), because what never hashes is cut visibly.
 *
 * **Bold widths** (ADR-0050, item 15; ADR-0071) are the classes measured at the bold weight, and a bold
 * cell is judged by them ({@link CellTextMetrics.bold}).
 */
export class CellTextMetrics {
    /**
     * What metrics built without bold widths charge a bold character, over its regular class width: 4%
     * more. The widest growth measured from weight 600 to bold was 3.9%, `(` in `system-ui` on macOS.
     */
    static readonly boldWidthAllowance = boldWidthAllowance;

    /**
     * What metrics built without the other class charge one of its glyphs, over their digit: twice it. The
     * widest such glyph measured is 1.59 digits, `W` and `₩` in DejaVu Sans Bold (ticket 83).
     */
    static readonly otherWidthAllowance = otherWidthAllowance;

    readonly #widths: Widths;
    // The table, what its widths are multiplied by (the metrics' font size over the size it was measured
    // at), and whether its bold widths are read.
    readonly #glyphs: GlyphWidthTable | undefined;
    readonly #glyphScale: number;
    readonly #glyphBold: boolean;
    // The bold metrics and the Text metrics, made on first asking and kept: the paint path asks for them
    // per bold cell and per label (ADR-0027 P5). A memo of a value derived from readonly state, so the
    // metrics stay immutable as anyone can observe them.
    #bold: CellTextMetrics | undefined;
    #asText: CellTextMetrics | undefined;

    /** Metrics in one of the forms {@link CellTextMetricsOptions} names. Refuses a narrow width wider than
     * its digit, a wide, full-width or other width narrower than it, and a width that is not finite. */
    constructor(options: CellTextMetricsOptions) {
        const widths = widthsOf(options);
        check(widths);
        this.#widths = Object.freeze(widths);
        const glyphs = carried.get(options);
        this.#glyphs = glyphs?.table;
        this.#glyphScale = glyphs === undefined ? 1 : glyphs.scale;
        this.#glyphBold = glyphs !== undefined && glyphs.bold;
    }

    // A copy with every width given and the glyph table carried: how each derived metrics is made.
    static #copy(widths: Widths, table: GlyphWidthTable | undefined, scale: number, bold: boolean): CellTextMetrics {
        const options = { ...widths };
        carried.set(options, { table, scale, bold });
        return new CellTextMetrics(options);
    }

    /** What `%` costs, and `€ − + #`, each measured past the digit on some platform. One `#` of an
     * overflowing cell is charged at it too, so the run fits the cell it stands in. */
    get wideWidthPx(): number { return this.#widths.wideWidthPx; }
    /** What a tabular digit costs, and the glyphs measured at a digit's width or under in every face: `$ £ ¥
     * ₹ ₺ ₫`, the exponent's `E`, the hyphen-minus, the apostrophes and spaces that group digits, and the
     * direction marks (ticket 83). */
    get digitWidthPx(): number { return this.#widths.digitWidthPx; }
    /** What the separators cost: `. , ( ) / :` and the space. */
    get narrowWidthPx(): number { return this.#widths.narrowWidthPx; }
    /** What a full-width character costs (East Asian Width Wide or Fullwidth): an em, whatever the family. */
    get fullWidthPx(): number { return this.#widths.fullWidthPx; }
    /** Padding on one side; a cell pays it twice. */
    get cellHorizontalPaddingPx(): number { return this.#widths.cellHorizontalPaddingPx; }
    /** The wide class at the bold weight. */
    get boldWideWidthPx(): number { return this.#widths.boldWideWidthPx; }
    /** The digit class at the bold weight. */
    get boldDigitWidthPx(): number { return this.#widths.boldDigitWidthPx; }
    /** The separators at the bold weight. */
    get boldNarrowWidthPx(): number { return this.#widths.boldNarrowWidthPx; }
    /** What every glyph outside the four measured classes, and outside the glyph table, costs: the widest
     * of them, so a glyph nobody listed is charged over its width (ticket 83). */
    get otherWidthPx(): number { return this.#widths.otherWidthPx; }
    /** The other class at the bold weight. */
    get boldOtherWidthPx(): number { return this.#widths.boldOtherWidthPx; }

    /** Single glyphs charged their own measured width instead of their class's, or `undefined` (ticket 83).
     * The classes come first: a glyph a class names is charged the class, whatever the table holds. */
    get glyphWidths(): GlyphWidthTable | undefined { return this.#glyphs; }

    /**
     * The metrics a bold cell is judged by (ADR-0050, item 15): the bold widths in the regular ones' place,
     * with the same full-width class (no narrower than the bold digit) and the same padding. Bold however
     * often it is asked.
     */
    get bold(): CellTextMetrics {
        this.#bold ??= this.#makeBold();
        return this.#bold;
    }

    #makeBold(): CellTextMetrics {
        const w = this.#widths;
        return CellTextMetrics.#copy({
            wideWidthPx: w.boldWideWidthPx, digitWidthPx: w.boldDigitWidthPx, narrowWidthPx: w.boldNarrowWidthPx,
            fullWidthPx: Math.max(w.fullWidthPx, w.boldDigitWidthPx), cellHorizontalPaddingPx: w.cellHorizontalPaddingPx,
            boldWideWidthPx: w.boldWideWidthPx, boldDigitWidthPx: w.boldDigitWidthPx, boldNarrowWidthPx: w.boldNarrowWidthPx,
            otherWidthPx: w.boldOtherWidthPx, boldOtherWidthPx: w.boldOtherWidthPx,
        }, this.#glyphs, this.#glyphScale, true);
    }

    /**
     * These metrics with measured bold widths in place of whatever they carried (ADR-0050, item 15). The
     * bold other class goes with them, charged its allowance over the new bold digit. Internal: how a
     * Wrapper's bold widths reach the resolved metrics.
     *
     * @internal
     */
    withBoldWidths(boldWideWidthPx: number, boldDigitWidthPx: number, boldNarrowWidthPx: number): CellTextMetrics {
        return CellTextMetrics.#copy({
            ...this.#widths, boldWideWidthPx, boldDigitWidthPx, boldNarrowWidthPx,
            boldOtherWidthPx: boldDigitWidthPx * otherWidthAllowance,
        }, this.#glyphs, this.#glyphScale, this.#glyphBold);
    }

    /**
     * These metrics charging the glyphs `table` holds their own widths (ticket 83), scaled from the size the
     * table was measured at to `fontSizePx`, the size these metrics are true at. `undefined` takes a table
     * away. The bold widths are read when the metrics are {@link CellTextMetrics.bold}.
     */
    withGlyphWidths(table: GlyphWidthTable | undefined, fontSizePx: number): CellTextMetrics {
        if (!Number.isFinite(fontSizePx) || fontSizePx <= 0) {
            throw new RangeError(`A font size is a finite, positive number of pixels. (fontSizePx: ${fontSizePx})`);
        }
        return CellTextMetrics.#copy(this.#widths, table, table === undefined ? 1 : fontSizePx / table.measuredAtPx, this.#glyphBold);
    }

    /**
     * The metrics a value of `type` is estimated with (ADR-0016; ticket 83). A Number or Date becomes
     * `####` when it does not fit, so it is charged every class: these metrics. A Text or Boolean value
     * never does, so it, and a label, is charged every glyph outside the measured classes at the digit,
     * with no glyph table. The bold widths follow, so {@link CellTextMetrics.bold} composes either way round.
     */
    for(type: ColumnType): CellTextMetrics {
        if (hashesWhenOverflowing(type)) {
            return this;
        }
        const w = this.#widths;
        this.#asText ??= CellTextMetrics.#copy({ ...w, otherWidthPx: w.digitWidthPx, boldOtherWidthPx: w.boldDigitWidthPx },
            undefined, 1, this.#glyphBold);
        return this.#asText;
    }

    // One character's charge (ADR-0016): its class's, or its own width where the table holds it. Anything
    // no class names and no table holds is the other class. A lone surrogate is charged as U+FFFD, the
    // character C#'s rune enumeration reads it as, so both cores charge the same text alike.
    #widthOf(character: number): number {
        const codePoint = character >= 0xD800 && character <= 0xDFFF ? 0xFFFD : character;
        const w = this.#widths;
        if (wide.has(codePoint)) return w.wideWidthPx;
        if (narrow.has(codePoint)) return w.narrowWidthPx;
        if (isFullWidth(codePoint)) return w.fullWidthPx;
        if (isDigitClass(codePoint)) return w.digitWidthPx;
        const own = this.#glyphs?.widthPxOf(codePoint, this.#glyphBold);
        return own === undefined ? w.otherWidthPx : own * this.#glyphScale;
    }

    /** The width of the text alone, no padding: what an action label's box is measured from (ADR-0020).
     * O(n) over the text, and no layout read (ADR-0021). */
    textWidthPx(text: string): number {
        let sum = 0;
        for (const character of text) {
            sum += this.#widthOf(defined(character.codePointAt(0), 'the code point of a character'));
        }
        return sum;
    }

    /** The full cell width a value requires: the text charged per class, plus the padding on both sides.
     * The unit Auto width and Size to fit take, comparable to a resolved column width (ADR-0016). */
    estimatePx(text: string): number {
        return this.textWidthPx(text) + 2 * this.#widths.cellHorizontalPaddingPx;
    }

    /**
     * These metrics with the full-width class charged at least `emPx`. The grid knows the em, since it
     * emits the font size, so it lifts whatever a theme or the three-class form supplied (ADR-0016).
     *
     * @internal
     */
    withFullWidthAtLeast(emPx: number): CellTextMetrics {
        return this.#widths.fullWidthPx >= emPx
            ? this
            : CellTextMetrics.#copy({ ...this.#widths, fullWidthPx: emPx }, this.#glyphs, this.#glyphScale, this.#glyphBold);
    }

    /** The width left for content after padding; zero or negative in a crushed column. */
    contentWidthPx(columnWidthPx: number): number {
        return columnWidthPx - 2 * this.#widths.cellHorizontalPaddingPx;
    }
}

// The glyph table a copy carries, keyed by the options object #copy makes and hands straight to the
// constructor. Module-private, so a caller cannot pass a table that way: a table is added with
// withGlyphWidths, which checks the size it is scaled to.
const carried = new WeakMap<object, { readonly table: GlyphWidthTable | undefined; readonly scale: number; readonly bold: boolean }>();

const codePoints = (glyphs: string): ReadonlySet<number> =>
    new Set(Array.from(glyphs, glyph => defined(glyph.codePointAt(0), 'the code point of a glyph')));

const wide = codePoints('%€−+#');
const narrow = codePoints('.,()/: ');

/**
 * The digit class (ticket 83; `tests/GlyphWidths` measures every glyph listed): the digits, the currency
 * signs drawn on a digit's advance, the exponent's `E`, the hyphen-minus, the apostrophes and spaces
 * cultures group digits with, and the zero-width direction marks. Listing a glyph here is a claim about
 * every face, so one joins only once it is measured in each.
 */
const digitClass = codePoints('$£¥₹₺₫E-\'’  ‎‏؜');

function isDigitClass(c: number): boolean {
    return (c >= 0x30 && c <= 0x39) || digitClass.has(c);
}

/**
 * East Asian Width Wide or Fullwidth (Unicode Standard Annex #11), by the blocks that carry them: what a
 * CJK face draws on the em square. Ambiguous-width characters are not here: they are an em only in a CJK
 * face, and the other class covers what the text faces draw them at.
 */
function isFullWidth(c: number): boolean {
    return (c >= 0x1100 && c <= 0x115F)       // Hangul Jamo initials
        || (c >= 0x2E80 && c <= 0x303E)       // CJK radicals, Kangxi, ideographic space and punctuation
        || (c >= 0x3041 && c <= 0x33FF)       // kana, Bopomofo, Hangul compatibility, enclosed and compatibility CJK
        || (c >= 0x3400 && c <= 0x4DBF)       // CJK Extension A
        || (c >= 0x4E00 && c <= 0x9FFF)       // CJK Unified Ideographs
        || (c >= 0xA000 && c <= 0xA4CF)       // Yi
        || (c >= 0xA960 && c <= 0xA97F)       // Hangul Jamo Extended-A
        || (c >= 0xAC00 && c <= 0xD7A3)       // Hangul syllables
        || (c >= 0xF900 && c <= 0xFAFF)       // CJK Compatibility Ideographs
        || (c >= 0xFE10 && c <= 0xFE19)       // vertical forms
        || (c >= 0xFE30 && c <= 0xFE6F)       // CJK compatibility and small forms
        || (c >= 0xFF01 && c <= 0xFF60)       // fullwidth forms
        || (c >= 0xFFE0 && c <= 0xFFE6)       // fullwidth signs
        || (c >= 0x1F300 && c <= 0x1F64F)     // pictographs and emoticons
        || (c >= 0x1F900 && c <= 0x1F9FF)     // supplemental pictographs
        || (c >= 0x20000 && c <= 0x3FFFD);    // CJK Extensions B onward
}
