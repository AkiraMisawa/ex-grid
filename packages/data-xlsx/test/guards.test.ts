// What only TypeScript can be handed, and so no shared case can hold: a fraction where .NET's `int` stands, and a change
// made to the default styles every workbook shares.
import { describe, expect, it } from 'vitest';
import { createXlsxWriter, defaultStyles, type XlsxStyles } from '../src/index';

describe('ADR-0330 §6: the writer refuses a fraction where the file writes a whole number', () => {
    it('ADR-0330 §6: a fractional active sheet, defined name\'s sheet or cell format index is refused as the writer is made', () => {
        const format = defaultStyles.cellFormats[0];
        if (format === undefined) throw new Error('The default styles hold a cell format.');
        const styles: XlsxStyles = { ...defaultStyles, cellFormats: [{ ...format, fontId: 0.5 }] };

        expect(() => createXlsxWriter({ activeSheet: 0.5 })).toThrow('The active sheet is a whole number, not 0.5.');
        expect(() => createXlsxWriter({ definedNames: [{ name: 'N', text: 'Sheet1!$A$1', localSheet: 1.5 }] }))
            .toThrow("The defined name 'N''s sheet is a whole number, not 1.5.");
        expect(() => createXlsxWriter({ styles })).toThrow('An index of the cell format 0 is a whole number, not 0.5.');
    });

    it('ADR-0330 §6: a shared Formula\'s fractional group is refused as the cell is written', async () => {
        const writer = createXlsxWriter();
        const sheet = writer.addSheet('Sheet1');

        expect(() => sheet.write({ row: 0, column: 0, type: 'number', value: '1', formula: { text: 'B1', kind: 'shared', sharedIndex: 0.5 } }))
            .toThrow(TypeError);
        await writer.abandon();
    });
});

describe('ADR-0330 §4: the default styles are shared, so nobody changes them', () => {
    it('ADR-0330 §4: the default Number Formats cannot be added to, removed from or cleared', () => {
        const formats = defaultStyles.numberFormats as Map<number, string>;

        expect(() => formats.set(164, '0.0')).toThrow(TypeError);
        expect(() => formats.delete(164)).toThrow(TypeError);
        expect(() => { formats.clear(); }).toThrow(TypeError);
        expect(defaultStyles.numberFormats.size).toBe(0);
        expect(Object.isFrozen(defaultStyles.fonts)).toBe(true);
    });
});
