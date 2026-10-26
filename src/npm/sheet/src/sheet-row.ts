// One row of a Sheet as ExGrid is handed it: a position, not a thing (ADR-0046). Row 5 stays row 5 whatever is
// put into it, so the grid's Row Sequence Version never moves; what changes is the instance. The Sheet hands the
// grid a new instance for every row a change to the Sheet names, and the same instance for every other row, so
// the others are not painted again (ADR-0003, SH-4).
//
// A row reads its cells from the Sheet the first time the grid asks for each one and keeps the answer. That is
// safe because an instance is retired the moment the Sheet reports a change on its row: while it is current,
// what it would read has not changed since it was made.

import type { CellAlign, CellAppearance, CellBorder, CellTextMeasure, EditVerdict } from '@ex-grid/core';
import {
    CellAddress, Entry, FormulaSyntaxError, numberFormatColourRgb, Sheet,
    type BorderLine, type CellColour, type CellDisplay, type HorizontalAlignment, type NumberFormatColour, type Value,
} from '@ex-grid/sheet-engine';
import { unreachable } from './must';

/**
 * What a number shows when it cannot be shown in its format at any width: a run of `#` no column is wide enough
 * for, which ExGrid's rule for a number that does not fit turns into the `####` that fills the cell (ADR-0016).
 */
export const unshowable = '#'.repeat(256);

/**
 * What one cell hands ExGrid as its value: the engine's formatted text, whether it is a number, the unformatted
 * Value as the engine writes it for its own copy, its alignment, and the colour its Number Format paints it in.
 */
export interface SheetCellText {
    readonly text: string;
    /** The Value unformatted, which the grid's own copy writes in its table, as `raw` is. */
    toString(): string;
    readonly isNumber: boolean;
    readonly raw: string;
    readonly align: CellAlign;
    readonly colour: NumberFormatColour | undefined;
}

/**
 * The cell's alignment as the grid takes it (ADR-0050, item 7): a user's own setting as it is; General as `auto`,
 * so the cell's kind decides, except where the engine resolves General to the centre, which is Excel's place for
 * booleans and Error Values and one no kind gives.
 */
function alignOf(display: CellDisplay, setting: HorizontalAlignment): CellAlign {
    switch (setting) {
        case 'left': return 'left';
        case 'center': return 'center';
        case 'right': return 'right';
        case 'general': return display.alignment === 'center' ? 'center' : 'auto';
        default: return unreachable(setting);
    }
}

/** The cell's text from the engine's display, Value and alignment setting, or undefined for a blank cell. A
 * `####` keeps its section's colour, as Excel's does (ADR-0071). */
function cellTextOf(display: CellDisplay, value: Value | undefined, setting: HorizontalAlignment): SheetCellText | undefined {
    const raw = value === undefined ? '' : value.toString();
    const align = alignOf(display, setting);
    const toString = (): string => raw;
    if (display.cannotShow) return Object.freeze({ text: unshowable, isNumber: true, raw, align, colour: display.colour, toString });
    if (display.text === '') return undefined;
    return Object.freeze({ text: display.text, isNumber: display.isNumber, raw, align, colour: display.colour, toString });
}

/** A content width in pixels as Excel's column width: how many of the grid's digits fit in it. */
export function charactersIn(contentWidthPx: number, metrics: CellTextMeasure): number {
    return contentWidthPx <= 0 || metrics.digitWidthPx <= 0 ? 0 : contentWidthPx / metrics.digitWidthPx;
}

/** The words a refused Formula is rejected with in the editor (ADR-0034). */
export type FormulaUnreadableWords = (error: FormulaSyntaxError) => string;

/** One row of a Sheet. */
export class SheetRow {
    readonly #sheet: Sheet;
    readonly #index: number;
    readonly #cells = new Map<number, SheetCellText | undefined>();
    readonly #painted = new Map<number, { readonly widthPx: number; readonly metrics: CellTextMeasure; readonly text: string | undefined }>();
    #appearances = new Map<number, CellAppearance>();
    #appearancesRead = -1;

    constructor(sheet: Sheet, index: number) {
        this.#sheet = sheet;
        this.#index = index;
    }

    /** The Sheet the row is of: an edit committed while another Sheet is shown lands on it (ADR-0331 §5). */
    get sheet(): Sheet { return this.#sheet; }

    /** The row's position on the Sheet, from 0 (row `1` is 0). */
    get index(): number { return this.#index; }

    /** What the cell in `column` shows, or undefined for a blank cell. */
    at(column: number): SheetCellText | undefined {
        if (this.#cells.has(column)) return this.#cells.get(column);
        const address = new CellAddress(this.#index, column);
        const text = cellTextOf(this.#sheet.getDisplay(address), this.#sheet.getValue(address), this.#sheet.getAlignment(address));
        this.#cells.set(column, text);
        return text;
    }

    /**
     * What the cell paints in a column whose content is `contentWidthPx` wide (ADR-0047, third round; ADR-0050 item
     * 11; ticket 91): for a number in General, the widest text the engine fits to a width in characters that
     * `metrics` charge no wider than the content, so `=1/3` reads `0.333333` in a default column; or the run of `#`
     * that becomes `####` where not even the shortest form fits. Undefined where the cell paints its value's own text.
     */
    paintedAt(column: number, contentWidthPx: number, metrics: CellTextMeasure): string | undefined {
        const shown = this.at(column);
        if (shown?.isNumber !== true || shown.text === unshowable) return undefined;
        const cached = this.#painted.get(column);
        if (cached?.widthPx === contentWidthPx && cached.metrics === metrics) return cached.text;
        const address = new CellAddress(this.#index, column);
        let painted: string | undefined;
        if (this.#sheet.getNumberFormat(address).isGeneral) {
            const text = this.#fittedGeneral(address, contentWidthPx, metrics);
            painted = text === shown.text ? undefined : text;
        }
        this.#painted.set(column, { widthPx: contentWidthPx, metrics, text: painted });
        return painted;
    }

    // The widest text the engine writes for a General number at a width in characters that `metrics` charge no
    // wider than the content, each glyph at its own width (ADR-0016; ticket 91). The search starts one character
    // past the digits that fit: General's text holds at most one glyph narrower than a digit.
    #fittedGeneral(address: CellAddress, contentWidthPx: number, metrics: CellTextMeasure): string {
        const digits = Math.min(Sheet.maxColumnWidth, Math.max(0, charactersIn(contentWidthPx, metrics)));
        // The engine counts whole characters; a width that is 8 may arrive as 7.9999999.
        for (let characters = Math.floor(digits + 1e-9) + 1; characters > 0; characters--) {
            const display = this.#sheet.getDisplay(address, characters);
            if (!display.cannotShow && metrics.textWidthPx(display.text) <= contentWidthPx) return display.text;
        }
        return unshowable;
    }

    /**
     * The cell's Font, Fill and Borders as the core paints them (ADR-0050 item 15, ADR-0071), for one `reading` of
     * the Sheet's formatting. Within a reading the answer is kept: a change the engine names retires the row, and
     * a change that reaches rows the engine does not name starts a new reading.
     */
    appearanceAt(column: number, reading: number): CellAppearance {
        if (this.#appearancesRead !== reading) {
            this.#appearances = new Map();
            this.#appearancesRead = reading;
        }
        const cached = this.#appearances.get(column);
        if (cached !== undefined) return cached;
        const appearance = appearanceOf(this.#sheet, new CellAddress(this.#index, column), this.at(column)?.colour);
        this.#appearances.set(column, appearance);
        return appearance;
    }

    /** The cell's Font and Fill as the Cell Editor takes them: the Font's own colour, never a Number Format's
     * (ticket 88). */
    editorAppearanceAt(column: number): CellAppearance {
        return appearanceOf(this.#sheet, new CellAddress(this.#index, column), undefined);
    }

    /** The cell's Entry as the Cell Editor opens on it (ADR-0051). */
    entryTextAt(column: number): string {
        return this.#sheet.getEntryText(new CellAddress(this.#index, column));
    }

    /** For a cell an array spills into, its Anchor's Formula, which the Formula Bar shows dimmed (ADR-0125). */
    spilledFormulaAt(column: number): string | undefined {
        const anchor = this.#sheet.spilledFrom(new CellAddress(this.#index, column));
        return anchor === undefined ? undefined : this.#sheet.getEntryText(anchor);
    }

    /**
     * The verdict on a commit into this row (ADR-0034): a Formula the engine cannot read is rejected with the
     * engine's reason, and the editor holds the text so it can be corrected. Text with a leading `+` or `-` is read
     * as the engine reads it, since Excel may read it as a Formula. Anything else is a constant, which always reads.
     */
    judge(typed: string, unreadable: FormulaUnreadableWords): EditVerdict {
        const first = typed.charAt(0);
        if (first !== '=' && first !== '+' && first !== '-') return { kind: 'accept' };
        try {
            Entry.parse(typed, this.#sheet.culture);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- an unreadable Formula is the verdict's answer (ADR-0034); anything else is rethrown
            if (error instanceof FormulaSyntaxError) return { kind: 'reject', message: unreadable(error) };
            throw error;
        }
        return { kind: 'accept' };
    }
}

/**
 * The appearance of the cell at `address`: its Font, Fill and Borders as the Sheet shows them, with the colour of
 * the Number Format's section that shows its Value, where it names one, in place of the Font's colour (the
 * eleventh Windows run, case 2). Each side is the edge as shown from both cells, so the core is never left to
 * draw one cell's record of it alone.
 */
export function appearanceOf(sheet: Sheet, address: CellAddress, numberFormatColour: NumberFormatColour | undefined): CellAppearance {
    const font = sheet.getFont(address);
    const fill = sheet.getFill(address).colour;
    const borders = sheet.getBorders(address);
    const fontColour = numberFormatColour === undefined ? rgbOf(font.colour) : numberFormatColourRgb(numberFormatColour);
    const fillRgb = fill === undefined ? undefined : rgbOf(fill);
    return Object.freeze({
        ...(fontColour === undefined ? {} : { fontColour }),
        ...(font.bold ? { bold: true } : {}),
        ...(font.italic ? { italic: true } : {}),
        ...(font.underline ? { underline: true } : {}),
        ...(font.strikethrough ? { strikethrough: true } : {}),
        ...(fillRgb === undefined ? {} : { fill: fillRgb }),
        ...sideOf('top', borders.top),
        ...sideOf('right', borders.right),
        ...sideOf('bottom', borders.bottom),
        ...sideOf('left', borders.left),
    });
}

/** A recorded colour as the core paints it; undefined for Automatic. */
function rgbOf(colour: CellColour): number | undefined {
    return colour.isAutomatic ? undefined : colour.rgb;
}

/** A Border side as the core draws it: Automatic is Excel's black. */
function sideOf(side: 'top' | 'right' | 'bottom' | 'left', line: BorderLine): Partial<Record<typeof side, CellBorder>> {
    if (line.isNone) return {};
    return { [side]: { style: line.style, colour: line.colour.isAutomatic ? 0x00_00_00 : line.colour.rgb } };
}
