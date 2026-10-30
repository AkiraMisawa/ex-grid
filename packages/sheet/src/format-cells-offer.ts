// What Format Cells offers, in Excel's order (ADR-0071; the eleventh Windows run, cases 22 and 23): the same
// under every Chrome. The C# counterpart is FormatCellsOffer and the enums beside it in FormatCells.cs; what
// each offer is called is a word (sheet-words.ts), resolved from its name as ADR-0370 section 10 has it, so a
// Chrome in another language names the same offers.

import type { BorderLineStyle, CellColour, HorizontalAlignment } from '@ex-grid/sheet-engine';
import { colourFromRgb } from './cell-colour';

/** Format Cells' tabs, in Excel's order: five of Excel's six, Protection left out (ADR-0071). */
export type FormatCellsTab = 'number' | 'alignment' | 'font' | 'border' | 'fill';

/**
 * The Number tab's categories, in Excel's order (ADR-0071; the eleventh Windows run, case 22). Accounting,
 * Fraction and Special are offered disabled ({@link DisabledNumberFormatCategory}).
 */
export type NumberFormatCategory =
    | 'general' | 'number' | 'currency' | 'accounting' | 'date' | 'time' | 'percentage' | 'fraction'
    | 'scientific' | 'text' | 'special' | 'custom';

/**
 * The categories Format Cells shows disabled, each with its reason: Accounting pads with `*`, Fraction is not
 * among the codes ExSheet reads, and Special's formats depend on the locale (ADR-0071).
 */
export type DisabledNumberFormatCategory = 'accounting' | 'fraction' | 'special';

/** The Font tab's styles, as Excel lists them: emphasis as one choice of four. */
export type FontStyle = 'regular' | 'italic' | 'bold' | 'boldItalic';

/** An edge the Border tab sets, relative to each selected range, as Excel's dialog sets it (ADR-0071). The
 * inside edges are Excel's Horizontal and Vertical. */
export type BorderEdge = 'top' | 'insideHorizontal' | 'bottom' | 'left' | 'insideVertical' | 'right';

/** The Border tab's presets, as Excel's dialog offers them (the eleventh Windows run, case 22). */
export type BorderPreset = 'none' | 'outline' | 'inside';

/** Which colour a More Colours entry is for: the Font's, the Fill, or the Border's line. */
export type ColourTarget = 'font' | 'fill' | 'border';

/** Excel's ten standard colours (case 23), by name. */
export type StandardColourName =
    | 'darkRed' | 'red' | 'orange' | 'yellow' | 'lightGreen' | 'green' | 'lightBlue' | 'blue' | 'darkBlue' | 'purple';

/** The Office theme's ten colours (case 23), by the name of their column. */
export type ThemeColourName =
    | 'background1' | 'text1' | 'background2' | 'text2' | 'accent1' | 'accent2' | 'accent3' | 'accent4' | 'accent5' | 'accent6';

/** A tint or a shade of a theme colour, as Excel's tooltip names it: `Darker 5%`, `Lighter 80%`. */
export interface SwatchStep {
    /** Whether the theme colour is darkened or lightened. */
    readonly shade: 'darker' | 'lighter';
    /** By how much, in percent. */
    readonly percent: number;
}

/**
 * One colour of the palette, with what Excel names it (ADR-0071; the eleventh Windows run, case 23): a theme
 * colour, with its step for a tint or a shade, or a standard colour. Its words are
 * {@link SheetWords.swatchName}'s.
 */
export interface PaletteSwatch {
    /** The RGB value Excel names it with in Format Cells' Fill tab, not the colour sampled from the screen. */
    readonly colour: CellColour;
    /** The colour's name: a theme colour's column, or a standard colour. */
    readonly name: ThemeColourName | StandardColourName;
    /** The tint or the shade of a theme colour, or `undefined` for the colour itself and a standard colour. */
    readonly step: SwatchStep | undefined;
}

/** A Number Format category as Format Cells offers it: available, or shown disabled with its reason. */
export interface NumberFormatCategoryOffer {
    /** The category. */
    readonly category: NumberFormatCategory;
    /** Whether it can be chosen; one that cannot is shown with {@link SheetWords.categoryDisabled}'s reason. */
    readonly enabled: boolean;
}

/** Whether a category is one Format Cells shows disabled. */
export function isDisabledCategory(category: NumberFormatCategory): category is DisabledNumberFormatCategory {
    return category === 'accounting' || category === 'fraction' || category === 'special';
}

function swatch(name: ThemeColourName | StandardColourName, rgb: number, shade?: SwatchStep): PaletteSwatch {
    return Object.freeze({ colour: colourFromRgb(rgb), name, step: shade });
}

function step(shade: 'darker' | 'lighter', percent: number): SwatchStep {
    return Object.freeze({ shade, percent });
}

// The column's name, then its RGB values from the theme row down, as the Fill tab names them (case 22's
// "tab-fill"), and the steps its tooltips name (case 23).
const themeColumns: readonly (readonly [ThemeColourName, readonly number[], readonly SwatchStep[]])[] = [
    ['background1', [0xFFFFFF, 0xF2F2F2, 0xD9D9D9, 0xBFBFBF, 0xA6A6A6, 0x7F7F7F],
        [step('darker', 5), step('darker', 15), step('darker', 25), step('darker', 35), step('darker', 50)]],
    ['text1', [0x000000, 0x7F7F7F, 0x595959, 0x404040, 0x262626, 0x0D0D0D],
        [step('lighter', 50), step('lighter', 35), step('lighter', 25), step('lighter', 15), step('lighter', 5)]],
    ['background2', [0xE8E8E8, 0xD0D0D0, 0xADADAD, 0x747474, 0x393939, 0x161616],
        [step('darker', 10), step('darker', 25), step('darker', 50), step('darker', 75), step('darker', 90)]],
    ['text2', [0x0E2841, 0xDAE9F8, 0xA6C9EC, 0x4D93D9, 0x215C98, 0x153D64],
        [step('lighter', 90), step('lighter', 75), step('lighter', 50), step('lighter', 25), step('lighter', 10)]],
    ['accent1', [0x156082, 0xC0E6F5, 0x83CCEB, 0x44B3E1, 0x104861, 0x0B3040], accentSteps()],
    ['accent2', [0xE97132, 0xFBE2D5, 0xF7C7AC, 0xF1A983, 0xBE5014, 0x7E350E], accentSteps()],
    ['accent3', [0x196B24, 0xC1F0C8, 0x83E28E, 0x47D359, 0x12501A, 0x0D3512], accentSteps()],
    ['accent4', [0x0F9ED5, 0xCAEDFB, 0x94DCF8, 0x61CBF3, 0x0C769E, 0x074F69], accentSteps()],
    ['accent5', [0xA02B93, 0xF2CEEF, 0xE49EDD, 0xD86DCD, 0x782170, 0x50154A], accentSteps()],
    ['accent6', [0x4EA72E, 0xDAF2D0, 0xB5E6A2, 0x8ED973, 0x3C7D22, 0x275317], accentSteps()],
];

function accentSteps(): readonly SwatchStep[] {
    return [step('lighter', 80), step('lighter', 60), step('lighter', 40), step('darker', 25), step('darker', 50)];
}

function themeRows(): readonly (readonly PaletteSwatch[])[] {
    const rows: (readonly PaletteSwatch[])[] = [];
    for (let row = 0; row < 6; row++) {
        rows.push(Object.freeze(themeColumns.map(([name, rgb, steps]) => {
            const value = rgb[row];
            if (value === undefined) throw new Error(`Internal: the theme column '${name}' has no row ${row}.`);
            return swatch(name, value, row === 0 ? undefined : steps[row - 1]);
        })));
    }
    return Object.freeze(rows);
}

/** What Format Cells offers, in Excel's order (ADR-0071; the eleventh Windows run, cases 22 and 23). */
export const formatCellsOffer = Object.freeze({
    /** The tabs, in Excel's order. */
    tabs: Object.freeze<FormatCellsTab[]>(['number', 'alignment', 'font', 'border', 'fill']),
    /** The Number tab's categories, in Excel's order, each available or disabled. */
    categories: Object.freeze((
        ['general', 'number', 'currency', 'accounting', 'date', 'time', 'percentage', 'fraction', 'scientific', 'text', 'special', 'custom'] as const
    ).map((category): NumberFormatCategoryOffer => Object.freeze({ category, enabled: !isDisabledCategory(category) }))),
    /** The Alignment tab's horizontal alignments, as Excel lists them. */
    alignments: Object.freeze<HorizontalAlignment[]>(['general', 'left', 'center', 'right']),
    /** The Font tab's styles, as Excel lists them. */
    fontStyles: Object.freeze<FontStyle[]>(['regular', 'italic', 'bold', 'boldItalic']),
    /**
     * The Border tab's line styles in Excel's two columns, each from the top: None, Hair, Dotted, Dash-dot-dot,
     * Dash-dot, Dashed and Thin; then Medium Dash-dot-dot, Slanted Dash-dot, Medium Dash-dot, Medium Dashed,
     * Medium, Thick and Double (case 22).
     */
    lineStyleColumns: Object.freeze([
        Object.freeze<BorderLineStyle[]>(['none', 'hair', 'dotted', 'dashDotDot', 'dashDot', 'dashed', 'thin']),
        Object.freeze<BorderLineStyle[]>(['mediumDashDotDot', 'slantedDashDot', 'mediumDashDot', 'mediumDashed', 'medium', 'thick', 'double']),
    ]),
    /** The Border tab's presets, in Excel's order. */
    presets: Object.freeze<BorderPreset[]>(['none', 'outline', 'inside']),
    /** The Border tab's edges, in Excel's order, without its diagonals (ADR-0071). */
    edges: Object.freeze<BorderEdge[]>(['top', 'insideHorizontal', 'bottom', 'left', 'insideVertical', 'right']),
    /**
     * The Office theme's colours (case 23): ten across, in six rows — the theme colours, then five rows of their
     * tints and shades. Each records the RGB value Excel names it with in Format Cells' Fill tab; the two named
     * "Grey" are `#7F7F7F`.
     */
    themeColours: themeRows(),
    /** Excel's ten standard colours (case 23). */
    standardColours: Object.freeze([
        swatch('darkRed', 0xC00000), swatch('red', 0xFF0000), swatch('orange', 0xFFC000), swatch('yellow', 0xFFFF00),
        swatch('lightGreen', 0x92D050), swatch('green', 0x00B050), swatch('lightBlue', 0x00B0F0), swatch('blue', 0x0070C0),
        swatch('darkBlue', 0x002060), swatch('purple', 0x7030A0),
    ]),
});
