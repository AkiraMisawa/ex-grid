import { CellAddress, CellFormatChange, CellRange, Culture, NumberFormat, SheetEdit, Workbook, type WorkbookDocument } from '@ex-grid/sheet-engine';

/** Ten rolls of two dice, and a uniform draw beside each, summarised: RAND and RANDBETWEEN over a Sheet Seed. It holds
 * the Formulas only: no seed and no draw. */
export function diceRolls(): WorkbookDocument {
    const sheet = 'Sheet1';
    const workbook = new Workbook(Culture.of('en-US'), [sheet]);
    const enter = (address: string, typed: string): void => { workbook.do(sheet, SheetEdit.enter(CellAddress.parse(address), typed)); };
    ['Roll', 'Die 1', 'Die 2', 'Sum', 'Uniform'].forEach((heading, c) => { workbook.do(sheet, SheetEdit.enter(new CellAddress(0, c), heading)); });
    for (let roll = 1; roll <= 10; roll++) {
        const row = String(roll + 1);
        enter(`A${row}`, String(roll));
        enter(`B${row}`, '=RANDBETWEEN(1,6)');
        enter(`C${row}`, '=RANDBETWEEN(1,6)');
        enter(`D${row}`, `=B${row}+C${row}`);
        enter(`E${row}`, '=RAND()');
    }
    enter('G1', 'Average sum');
    enter('H1', '=AVERAGE(D2:D11)');
    enter('G2', 'Sevens');
    enter('H2', '=COUNTIF(D2:D11,7)');
    enter('G3', 'Average uniform');
    enter('H3', '=AVERAGE(E2:E11)');

    workbook.do(sheet, SheetEdit.setColumnWidth(CellRange.parse('G:G'), 16));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('E2:E11'), NumberFormat.parse('0.000')));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('H1'), NumberFormat.parse('0.0')));
    workbook.do(sheet, SheetEdit.setNumberFormat(CellRange.parse('H3'), NumberFormat.parse('0.000')));
    workbook.do(sheet, SheetEdit.setCellFormat([CellRange.parse('A1:E1'), CellRange.parse('G1:G3')], new CellFormatChange({ bold: true })));
    return workbook.toDocument();
}
