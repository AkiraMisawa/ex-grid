// The words ExSheet shows (ADR-0370, section 10): English by default, character for character the C#
// package's; Japanese chosen in one line; a Consumer's label over both; every id worded in both languages.
import { describe, expect, it } from 'vitest';
import { CellAddress, FormulaSyntaxError } from '@ex-grid/sheet-engine';
import { formatCellsOffer, sheetWordIdList, sheetWords, SheetWords, type SheetWordId } from '../src/index';

function consumerLabel(id: SheetWordId, english: string): string | undefined {
    if (id === 'opened') expect(english).toBe('The file was opened.');
    return id === 'summaryRefused' ? '集計なし: {0} の数式は Excel と同じ答えを出せないため、ExSheet が {1} で拒否しました。' : undefined;
}

// A label from an untyped caller, which answers something that is not a word.
const untypedLabel: unknown = () => 42;

describe('SheetWords', () => {
    it('ADR-0370 §10: English by default, as the C# package says it', () => {
        expect.hasAssertions();
        const words = SheetWords.english;
        expect(words.editIsOpen).toBe(
            'Nothing was changed: a cell is being edited. Press Enter to commit the edit or Escape to cancel it, then try again.');
        expect(words.sheetMoved('Q1', 2, 3)).toBe("Sheet 'Q1' was moved to position 2 of 3.");
        expect(words.formulaUnreadable(new FormulaSyntaxError('=1+', 3, 'An operand is missing.')))
            .toBe('This Formula cannot be read at character 4: An operand is missing.');
        expect(words.summaryRefused(CellAddress.parse('A3'), '#VALUE!')).toBe(
            "No figures: ExSheet refused the Formula in A3 with #VALUE! because it cannot answer it as Excel does, so a figure over it would not be Excel's.");
        expect(words.pasteTooNarrowToShow('####', CellAddress.parse('B2'))).toBe(
            "Nothing was pasted: the field for B2 is '####', which is how the source shows a value too wide for its column. "
            + 'The source column was too narrow to show the value, so the value itself was not copied. Widen the column there and copy again.');
        expect(words.structureKeyWhileEditing(false)).toMatch(/^Nothing was deleted: /u);
    });

    it('ADR-0058 / ADR-0370 §10: a Pointing refusal names its table and column, and a drag says it took back', () => {
        expect.hasAssertions();
        const words = SheetWords.english;
        expect(words.pointingRefused('columnNotInTable', 'Positions', 'Note', false))
            .toBe("Nothing was written: the column 'Note' is not a column of the Linked Table 'Positions'.");
        expect(words.pointingRefused('severalCells', 'Positions', undefined, true)).toMatch(/^What the press wrote was taken back: /u);
        expect(words.pointingRefused('cellNotHeld', 'Positions', undefined, false)).toContain("the cell of 'Positions'");
        expect(words.pointingRefused('cellNotHeld', 'Positions', 'PV', false)).toContain("the column 'PV' of 'Positions'");
        expect(() => words.pointingRefused('columnNotInTable', 'Positions', undefined, false)).toThrow(/Internal/u);
    });

    it('ADR-0370 §10 / ADR-0060: Japanese is chosen in one line, with Excel Japanese edition\'s words', () => {
        expect.hasAssertions();
        const words = sheetWords({ japanese: true });
        expect(words.formatCellsTitle).toBe('セルの書式設定');
        expect(words.categoryName('custom')).toBe('ユーザー定義');
        expect(words.sheetMoved('Q1', 2, 3)).toBe("シート 'Q1' を 3 枚中 2 番目に移動しました。");
        const swatch = formatCellsOffer.themeColours[1]?.[0];
        expect(swatch === undefined ? undefined : words.swatchName(swatch)).toBe('白、背景 1、黒 + 基本色 5%');
    });

    it('ADR-0370 §10: a Consumer\'s label wins over both, and English stands where it says nothing', () => {
        expect.hasAssertions();
        const words = sheetWords({ label: consumerLabel });
        expect(words.summaryRefused(CellAddress.parse('A2'), '#VALUE!'))
            .toBe('集計なし: A2 の数式は Excel と同じ答えを出せないため、ExSheet が #VALUE! で拒否しました。');
        expect(words.opened).toBe('The file was opened.');
        expect(sheetWords({ japanese: true, label: consumerLabel }).opened).toBe('ファイルを開きました。');
    });

    it('ADR-0370 §10: a label that names an argument the sentence lacks, or answers something not a word, is refused', () => {
        expect.hasAssertions();
        expect(() => sheetWords({ label: () => 'Sheet {3}' }).sheetAdded('A')).toThrow(RangeError);
        expect(() => sheetWords({ label: untypedLabel as never }).opened).toThrow(TypeError);
    });

    it('ADR-0370 §10: every id has its English and its Japanese, and the English names each palette colour as Excel does', () => {
        expect.hasAssertions();
        const ids = sheetWordIdList();
        const japanese = sheetWords({ japanese: true });
        expect(ids.length).toBeGreaterThan(150);
        for (const id of ids) {
            expect(SheetWords.english.word(id)).not.toBe('');
            expect(japanese.word(id)).not.toBe('');
        }
        expect(formatCellsOffer.themeColours.flatMap(row => row.map(swatch => SheetWords.english.swatchName(swatch))).slice(30, 34))
            .toEqual(['White, Background 1, Darker 25%', 'Black, Text 1, Lighter 25%', 'Light Grey, Background 2, Darker 50%', 'Dark Blue, Text 2, Lighter 50%']);
    });
});
