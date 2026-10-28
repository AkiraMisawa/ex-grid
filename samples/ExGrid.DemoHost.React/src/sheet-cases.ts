// The Sheets /sheet?case= opens in place of the page's own: SheetCases.cs's, the set-ups of the eleventh Windows run's
// Part A (docs/specs/exsheet/verify-on-windows-11.md) and the cases that file lists beside them, built the same way so
// the specs written against the Blazor hosts read the same cells here.
import type { CellTextMetrics } from '@ex-grid/core';
import {
    BorderChange, BorderLine, CellAddress, CellColour, CellFill, CellFormatChange, CellRange, Culture, NumberFormat, Sheet, SheetEdit, Workbook,
    type BorderLineStyle, type WorkbookDocument,
} from '@ex-grid/sheet-engine';

const red = CellColour.fromRgb(0xFF0000);
const blue = CellColour.fromRgb(0x0000FF);
const yellow = CellFill.solid(CellColour.fromRgb(0xFFFF00));
const lightBlue = CellFill.solid(CellColour.fromRgb(0x00B0F0));
const thin = new BorderLine('thin');
const thickBlack = new BorderLine('thick');
const doubleBlack = new BorderLine('double');

/** Excel's thirteen line styles, in the order case 9 set them on B2:B14, with the names C#'s enum gives them. */
const lineStyles: readonly (readonly [BorderLineStyle, string])[] = [
    ['hair', 'Hair'], ['thin', 'Thin'], ['medium', 'Medium'], ['thick', 'Thick'], ['double', 'Double'], ['dotted', 'Dotted'], ['dashed', 'Dashed'],
    ['dashDot', 'DashDot'], ['dashDotDot', 'DashDotDot'], ['mediumDashed', 'MediumDashed'], ['mediumDashDot', 'MediumDashDot'],
    ['mediumDashDotDot', 'MediumDashDotDot'], ['slantedDashDot', 'SlantedDashDot'],
];

/** The names {@link sheetCase} knows, in the order the run numbers them. */
export const sheetCaseNames: readonly string[] = [
    '1', '2', '3b', '3c', '4', '5', '6', '7', '8', '9', '10', '11', '12', '13', '14', '16', '17', '18', 'paper', 'lines', '12-1', '12-14',
    '14-16', '14-17', '14-18', 'fills', '18a-2', 'today', 'spill', 'bold', 'fit',
];

/** Case `name`'s Workbook, in `culture`. `metrics` are the Cell Metrics the Sheet will paint it with, which `bold` and
 * `fit` compute their columns' widths from, a hundredth of a pixel from where a number stops fitting (SH-41; ADR-0071,
 * "Bold, and what fits"; ADR-0016). Throws for a name that is not one of {@link sheetCaseNames}: refused by name
 * rather than opened as the page's own Sheet, which a comparison would then be made against. */
export function sheetCase(name: string, culture: Culture, metrics: CellTextMetrics): WorkbookDocument {
    const workbook = new Workbook(culture);
    const sheet = workbook.sheets[0];
    if (sheet === undefined) throw new Error('A new Workbook holds a Sheet.');
    const enter = (address: string, typed: string): void => { workbook.do(sheet.name, SheetEdit.enter(CellAddress.parse(address), typed)); };
    const format = (change: CellFormatChange, range: string): void => {
        workbook.do(sheet.name, SheetEdit.setCellFormat([CellRange.parse(range)], change));
    };
    const borders = (parts: ConstructorParameters<typeof BorderChange>[0]): CellFormatChange => new CellFormatChange({ borders: new BorderChange(parts) });
    // A column's width is in characters, a digit's width each, so its content box is the width asked.
    const setContentWidth = (first: number, last: number, contentPx: number): void => {
        workbook.do(sheet.name, SheetEdit.setColumnWidth(CellRange.wholeColumns(first, last), contentPx / metrics.digitWidthPx));
    };
    const shown = (address: string, width?: number): string => sheet.getDisplay(CellAddress.parse(address), width).text;
    switch (name) {
        case 'bold': {
            // ####, at the bold widths: shown a hundredth over the edge, and #### a hundredth under it where the regular
            // widths still fit it (SH-41).
            enter('A1', '0.12');
            enter('A2', '0.12');
            for (const column of 'BCDEFGHI') enter(`${column}2`, '0.12');
            format(new CellFormatChange({ numberFormat: NumberFormat.parse('0%') }), 'A1:I2');
            format(new CellFormatChange({ bold: true }), 'A2:I2');
            const boldPx = metrics.bold.textWidthPx(shown('A2'));
            setContentWidth(0, 0, boldPx - 0.01);
            setContentWidth(1, 8, boldPx + 0.01);
            break;
        }
        case 'fit': {
            // Italic is judged by the regular widths (ADR-0071), and General fits a bold number to the bold ones (SH-41).
            enter('A1', '0.12');
            enter('A2', '0.12');
            format(new CellFormatChange({ numberFormat: NumberFormat.parse('0%'), italic: true }), 'A1:A2');
            format(new CellFormatChange({ alignment: 'left' }), 'A2');
            setContentWidth(0, 0, metrics.textWidthPx(shown('A1')) + 0.01);
            enter('B1', '123456789012345678901');
            enter('B2', '123456789012345678901');
            format(new CellFormatChange({ bold: true }), 'B2');
            const whole = shown('B1', Sheet.maxColumnWidth);
            setContentWidth(1, 1, (metrics.textWidthPx(whole) + metrics.bold.textWidthPx(whole)) / 2);
            break;
        }
        case 'today':
            enter('A1', '=TEXT(TODAY(),"yyyy-mm-dd")');
            enter('A2', '=TODAY()');
            enter('A3', '=TEXT(NOW(),"yyyy-mm-dd")');
            // To the minute, which moves on as each minute turns (ADR-0124, SH-60).
            enter('A4', '=TEXT(NOW(),"yyyy-mm-dd hh:mm")');
            break;
        case 'spill':
            // ADR-0125: C1's Formula spills down C1:C3.
            enter('A1', '1');
            enter('A2', '2');
            enter('A3', '3');
            enter('C1', '=A1:A3*10');
            break;
        case '1': {
            // Rows 1–8: -5 and 5 in each of the eight named colours; [White]'s row filled black.
            const names = ['Black', 'Blue', 'Cyan', 'Green', 'Magenta', 'Red', 'White', 'Yellow'];
            names.forEach((colour, row) => {
                enter(`A${String(row + 1)}`, '-5');
                enter(`B${String(row + 1)}`, '5');
                format(new CellFormatChange({ numberFormat: NumberFormat.parse(`[${colour}]0`) }), `A${String(row + 1)}:B${String(row + 1)}`);
            });
            format(new CellFormatChange({ fill: CellFill.solid(CellColour.fromRgb(0x000000)) }), 'A7:B7');
            break;
        }
        case '2':
            // The Number Format's colour wins over the Font's.
            enter('A1', '-5');
            enter('B1', '5');
            format(new CellFormatChange({ numberFormat: NumberFormat.parse('0;[Red]-0'), fontColour: blue }), 'A1:B1');
            break;
        case '3b':
            // No section shows them, so no colour; and 5 in 0;[Red]@ is shown by its first section, which names none.
            enter('A1', 'abc');
            enter('A2', 'TRUE');
            enter('A3', '=1/0');
            enter('A4', '5');
            format(new CellFormatChange({ numberFormat: NumberFormat.parse('[Red]0') }), 'A1:A3');
            format(new CellFormatChange({ numberFormat: NumberFormat.parse('0;[Red]@') }), 'A4');
            break;
        case '3c':
            // A #### keeps its section's colour: column A at the width Excel showed it at.
            enter('A1', '-123456789');
            format(new CellFormatChange({ numberFormat: NumberFormat.parse('0;[Red]-0') }), 'A1');
            workbook.do(sheet.name, SheetEdit.setColumnWidth(CellRange.wholeColumns(0, 0), 9.5));
            break;
        case '4':
            format(new CellFormatChange({ fill: yellow }), 'B2');
            break;
        case '5':
            format(new CellFormatChange({ fill: CellFill.solid(CellColour.fromRgb(0xFFFFFF)) }), 'B2');
            break;
        case '6':
            format(new CellFormatChange({ fill: yellow }), 'B2:C2');
            break;
        case '7':
            // The first of the case's two settings; Part C sets C2's left thin blue itself.
            format(borders({ right: new BorderLine('thick', red) }), 'B2');
            break;
        case '8':
            format(borders({ bottom: thickBlack }), 'B2');
            break;
        case '9':
            lineStyles.forEach(([style], i) => { format(borders({ bottom: new BorderLine(style) }), `B${String(i + 2)}`); });
            break;
        case 'lines':
            lineStyles.forEach(([style, written], i) => {
                enter(`A${String(i + 2)}`, written);
                format(borders({ bottom: new BorderLine(style) }), `B${String(i + 2)}`);
                format(borders({ right: new BorderLine(style) }), `D${String(i + 2)}`);
            });
            break;
        case '10':
            format(borders({ bottom: thickBlack }), 'B2');
            format(new CellFormatChange({ fill: yellow }), 'B3');
            break;
        case '11':
            // Every edge of B2:C3, outside and in; Part C selects B2:C3 with keys.
            format(borders({ top: thin, bottom: thin, left: thin, right: thin, insideHorizontal: thin, insideVertical: thin }), 'B2:C3');
            break;
        case '12':
            // Part C selects row 3 and inserts a row with keys.
            format(new CellFormatChange({ fill: yellow, borders: new BorderChange({ top: thin, bottom: thickBlack }) }), 'B2');
            break;
        case '13':
        case '14':
            // Nothing is set up: the case is its keys.
            break;
        case '12-1': {
            format(borders({ left: new BorderLine('thin', blue) }), 'C2');
            enter('E5', '1');
            format(borders({ right: new BorderLine('thick', red) }), 'E5');
            const block = sheet.copy(CellRange.parse('E5')).block;
            if (block === undefined) throw new Error('E5 holds a cell, so its copy is a block.');
            workbook.do(sheet.name, SheetEdit.paste(block, CellAddress.parse('B2')));
            break;
        }
        case '12-14':
            format(new CellFormatChange({ borders: BorderChange.outline(thin) }), '3:3');
            break;
        case '14-16':
            format(new CellFormatChange({ fill: yellow }), 'B2');
            format(new CellFormatChange({ fill: lightBlue }), 'B3');
            break;
        case '14-17':
            format(new CellFormatChange({ fill: yellow, borders: new BorderChange({ bottom: doubleBlack }) }), 'B2');
            break;
        case '14-18':
            format(borders({ bottom: new BorderLine('mediumDashed') }), 'B2');
            format(borders({ bottom: new BorderLine('dashed') }), 'B4');
            break;
        case 'fills':
            format(new CellFormatChange({ fill: yellow }), 'B2');
            format(new CellFormatChange({ fill: lightBlue }), 'C2');
            format(borders({ bottom: doubleBlack }), 'E2');
            format(new CellFormatChange({ fill: yellow }), 'E3');
            format(new CellFormatChange({ fill: yellow, borders: new BorderChange({ right: doubleBlack }) }), 'B5');
            format(new CellFormatChange({ fill: lightBlue }), 'C5');
            format(borders({ right: doubleBlack }), 'E5');
            format(new CellFormatChange({ fill: yellow }), 'F5');
            format(new CellFormatChange({ fill: yellow, borders: new BorderChange({ bottom: doubleBlack }) }), 'B8');
            format(new CellFormatChange({ fill: lightBlue }), 'B9');
            break;
        case '18a-2':
            format(new CellFormatChange({ fill: yellow }), 'D4');
            break;
        case '16':
            enter('A1', 'abc');
            enter('A2', 'def');
            break;
        case '17':
            enter('A1', 'abc');
            enter('A2', 'def');
            format(new CellFormatChange({ bold: true }), 'A1');
            break;
        case '18':
            enter('A1', '1234.5');
            break;
        case 'paper':
            enter('A1', 'red');
            enter('B1', '-5');
            enter('C1', 'bold');
            enter('D1', 'italic');
            enter('A2', 'underline');
            enter('B2', 'strike');
            enter('C2', 'automatic');
            format(new CellFormatChange({ fontColour: red }), 'A1');
            format(new CellFormatChange({ numberFormat: NumberFormat.parse('0;[Red]-0'), fontColour: blue }), 'B1');
            format(new CellFormatChange({ bold: true }), 'C1');
            format(new CellFormatChange({ italic: true }), 'D1');
            format(new CellFormatChange({ underline: true }), 'A2');
            format(new CellFormatChange({ strikethrough: true }), 'B2');
            format(new CellFormatChange({ fill: yellow }), 'C3');
            // A row and a column record a Fill, so cells that hold nothing are painted.
            format(new CellFormatChange({ fill: lightBlue }), '5:5');
            format(new CellFormatChange({ fill: yellow }), 'F:F');
            break;
        default:
            throw new RangeError(`Unknown case '${name}': /sheet?case= opens one of ${sheetCaseNames.join(', ')}.`);
    }
    return workbook.toDocument();
}

/** The culture every Sheet page opens in, as the Blazor pages' en-US. */
export const enUs = Culture.of('en-US');
