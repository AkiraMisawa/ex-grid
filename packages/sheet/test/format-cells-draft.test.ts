// Format Cells' draft (ADR-0071, SH-45): what it opens on, what each choice means, and the change OK sets — the
// rules every Chrome's Format Cells sets its controls by. And the codes the Number tab writes: each one the
// engine reads, under every culture ExSheet supports. Ported from dotnet/tests/ExSheet.Components/FormatCellsDraftTests.cs.
import { describe, expect, it } from 'vitest';
import {
    BorderChange, BorderLine, CellAddress, CellFill, CellFont, CellFormatChange, CellRange, Culture, NumberFormat, SheetEdit,
    Workbook, type BorderLineStyle, type Sheet,
} from '@ex-grid/sheet-engine';
import {
    currencyCode, currencyNegativeStyles, customTypes, dateTypes, FormatCellsDraft, mostDecimalPlaces, numberCode,
    numberNegativeStyles, percentageCode, recogniseNumberFormat, scientificCode, SheetWords, timeTypes, type NumberFormatCategory,
} from '../src/index';
import { colourFromRgb } from '../src/cell-colour';
import { defaultCodeOf } from '../src/number-format-codes';

const enUs = Culture.of('en-US');
const thin = new BorderLine('thin');
const thick = new BorderLine('thick');
const red = colourFromRgb(0xFF0000);
const black = colourFromRgb(0x000000);

type Prepare = (sheet: Sheet) => void;

function sheetIn(culture: Culture = enUs): Sheet {
    const workbook = new Workbook(culture);
    return workbook.shown;
}

function format(sheet: Sheet, range: string, change: CellFormatChange): void {
    sheet.workbook.do(sheet.name, SheetEdit.setCellFormat([CellRange.parse(range)], change));
}

function open(prepare: Prepare | undefined, focus: string, ...ranges: string[]): FormatCellsDraft {
    const sheet = sheetIn();
    prepare?.(sheet);
    return FormatCellsDraft.open(sheet, CellAddress.parse(focus), ranges.map(range => CellRange.parse(range)));
}

function borders(parts: ConstructorParameters<typeof BorderChange>[0]): CellFormatChange {
    return new CellFormatChange({ borders: new BorderChange(parts) });
}

const thickBottomOnA1: Prepare = sheet => format(sheet, 'A1', borders({ bottom: thick }));

/** A line as a test compares it: its style and colour, or `undefined` for a line that differs. */
function line(value: BorderLine | undefined): string | undefined {
    return value === undefined ? undefined : `${value.style} ${value.colour.toString()}`;
}

/** The edges a change sets, each as `line` writes it; absent where the change leaves the edge. */
function edgesOf(change: CellFormatChange): Record<string, string | undefined> | undefined {
    const set = change.borders;
    if (set === undefined) return undefined;
    const sides = ['top', 'bottom', 'left', 'right', 'insideHorizontal', 'insideVertical'] as const;
    return Object.fromEntries(sides.flatMap(side => (set[side] === undefined ? [] : [[side, line(set[side])]])));
}

function penOf(draft: FormatCellsDraft): [BorderLineStyle, string] {
    return [draft.lineStyle, draft.lineColour.toString()];
}

describe('number format codes', () => {
    it.each(['en-US', 'en-GB', 'ja-JP', 'de-DE', 'fr-FR'])(
        'ADR-0071 / SH-45: every code the Number tab writes under %s is read, and opens again on the category that wrote it',
        name => {
            expect.hasAssertions();
            const culture = Culture.of(name);
            const written: (readonly [string, NumberFormatCategory])[] = [];
            for (let places = 0; places <= mostDecimalPlaces; places++) {
                for (let negative = 0; negative < numberNegativeStyles; negative++) {
                    written.push([numberCode(places, false, negative), 'number'], [numberCode(places, true, negative), 'number']);
                }
                for (let negative = 0; negative < currencyNegativeStyles; negative++) written.push([currencyCode(places, negative, culture), 'currency']);
                written.push([percentageCode(places), 'percentage'], [scientificCode(places), 'scientific']);
            }
            written.push(...dateTypes.map(code => [code, 'date'] as const), ...timeTypes.map(code => [code, 'time'] as const), ['@', 'text']);
            for (const [code, category] of written) {
                const reading = NumberFormat.tryParse(code);
                expect(reading.reason, code).toBeUndefined();
                if (reading.format !== undefined) expect(recogniseNumberFormat(reading.format, culture).category, code).toBe(category);
            }
            for (const code of customTypes) expect(NumberFormat.tryParse(code).reason, code).toBeUndefined();
        });

    it('ADR-0071 / SH-45: Currency writes the culture\'s symbol where the culture puts it, and the eleventh run\'s en-GB key format opens as Currency', () => {
        expect.hasAssertions();
        expect(currencyCode(2, 4, enUs)).toBe('$#,##0.00_);[Red]($#,##0.00)');
        const enGb = Culture.of('en-GB');
        expect(currencyCode(2, 2, enGb)).toBe('£#,##0.00;[Red]-£#,##0.00');
        expect(recogniseNumberFormat(NumberFormat.parse('£#,##0.00;[Red]-£#,##0.00'), enGb).category).toBe('currency');
        expect(currencyCode(2, 0, Culture.of('de-DE'))).toBe('#,##0.00 €');
    });
});

describe('FormatCellsDraft', () => {
    it('ADR-0071: a code no category writes opens under Custom, as it is', () => {
        expect.hasAssertions();
        const draft = open(sheet => format(sheet, 'A1', new CellFormatChange({ numberFormat: NumberFormat.parse('0.0" kg"') })), 'A1', 'A1');
        expect(draft.category).toBe('custom');
        expect(draft.typeCode).toBe('0.0" kg"');
        expect(draft.types[0]).toBe('0.0" kg"');
    });

    it('ADR-0071 / SH-45: a disabled category is refused with its reason', () => {
        expect.hasAssertions();
        const draft = open(undefined, 'A1', 'A1');
        expect(() => draft.selectCategory('fraction')).toThrow(/^Fractions are not/u);
        expect(draft.change.isEmpty).toBe(true);
    });

    it('ADR-0100: a category chosen writes the code the Sheet Toolbar\'s Number Format list sets for it', () => {
        expect.hasAssertions();
        const categories = ['general', 'number', 'currency', 'date', 'time', 'percentage', 'scientific', 'text'] as const;
        for (const culture of ['en-US', 'ja-JP', 'de-DE']) {
            for (const category of categories) {
                const draft = openIn(culture, undefined);
                draft.selectCategory(category);
                expect(draft.numberFormatCode, `${category} under ${culture}`).toBe(defaultCodeOf(category, Culture.of(culture)));
            }
        }
    });

    it('ADR-0071 / SH-45: Custom starts from the code shown, as Excel\'s does', () => {
        expect.hasAssertions();
        const draft = open(undefined, 'A1', 'A1');
        draft.selectCategory('percentage');
        draft.setDecimalPlaces(1);
        draft.selectCategory('custom');
        expect(draft.typeCode).toBe('0.0%');
        expect(draft.change.numberFormat?.code).toBe('0.0%');
    });

    it('ADR-0071 / SH-45, case 25: a font style sets bold and italic together, and nothing else of the Font', () => {
        expect.hasAssertions();
        const draft = open(undefined, 'A1', 'A1');
        draft.setFontStyle('boldItalic');
        const change = draft.change;
        expect([change.bold, change.italic, change.underline, change.strikethrough, change.fontColour, change.borders])
            .toEqual([true, true, undefined, undefined, undefined, undefined]);
    });

    it('ADR-0071 / SH-45: an edge\'s button takes the chosen line, and takes it away when the edge already shows it', () => {
        expect.hasAssertions();
        const draft = open(undefined, 'A1', 'A1');
        draft.toggleEdge('top');
        expect(line(draft.edgeLine('top'))).toBe(line(thin));
        draft.toggleEdge('top');
        expect(line(draft.edgeLine('top'))).toBe(line(BorderLine.none));
        expect(edgesOf(draft.change)).toEqual({ top: line(BorderLine.none) });
    });

    it('ADR-0071 / SH-45, case 22: None over one cell takes its outline away and touches no inside edge it has none of', () => {
        expect.hasAssertions();
        const draft = open(undefined, 'A1', 'A1');
        draft.applyPreset('none');
        const none = line(BorderLine.none);
        expect(edgesOf(draft.change)).toEqual({ top: none, bottom: none, left: none, right: none });
        expect(() => draft.applyPreset('inside')).toThrow(Error);
        expect(() => draft.toggleEdge('insideVertical')).toThrow(/Vertical edge lies inside a range/u);
    });

    it('ADR-0071 / SH-45: Inside over a block sets both inside edges, in the chosen style and colour', () => {
        expect.hasAssertions();
        const draft = open(undefined, 'B2', 'B2:C3');
        const blue = colourFromRgb(0x0070C0);
        draft.setLineStyle('dashed');
        draft.setLineColour(blue);
        draft.applyPreset('inside');
        const dashed = line(new BorderLine('dashed', blue));
        expect(edgesOf(draft.change)).toEqual({ insideHorizontal: dashed, insideVertical: dashed });
    });

    it('ADR-0071 / SH-45: choosing a colour from the palette replaces More Colours\' text, and its refusal with it', () => {
        expect.hasAssertions();
        const draft = open(undefined, 'A1', 'A1');
        draft.setColourText('fill', '#12');
        expect(draft.refusal).toEqual({ kind: 'notAColour', target: 'fill', text: '#12' });
        expect(SheetWords.english.formatCellsRefusal(draft.refusal ?? { kind: 'noNumberFormatCode' }))
            .toBe('\'#12\' is not a colour. Type # and six hex digits, such as #1F4E79.');
        draft.setFill(CellFill.solid(colourFromRgb(0xFFFF00)));
        expect(draft.refusal).toBeUndefined();
        expect(draft.colourText('fill')).toBe('');
    });

    it('ADR-0071 / SH-45: More Colours text that is a colour sets it, in either case and with or without #', () => {
        expect.hasAssertions();
        const draft = open(undefined, 'A1', 'A1');
        draft.setColourText('font', ' 1f4e79 ');
        expect(draft.change.fontColour?.rgb).toBe(0x1F4E79);
        expect(draft.colourText('font')).toBe(' 1f4e79 ');
        expect(FormatCellsDraft.tryReadHex('#GGGGGG')).toBeUndefined();
    });

    it('ADR-0071 / SH-45: the case 24 Selection — bold, a red Fill and a thick bottom over a plain cell — opens with the Font style empty and No Colour', () => {
        expect.hasAssertions();
        const draft = open(sheet => format(sheet, 'A1', new CellFormatChange({
            bold: true, fill: CellFill.solid(red), borders: new BorderChange({ bottom: thick }),
        })), 'A1', 'A1:A2');
        expect(draft.fontStyle).toBeUndefined();
        expect(draft.fill.equals(CellFill.none)).toBe(true);
        expect(line(draft.edgeLine('top'))).toBe(line(BorderLine.none));
        expect(line(draft.edgeLine('bottom'))).toBe(line(BorderLine.none));
        expect(draft.change.isEmpty).toBe(true);
    });

    it('ADR-0071 / SH-45: what is shared across the Selection opens as it is shared', () => {
        expect.hasAssertions();
        const draft = open(sheet => format(sheet, 'A1:B2', new CellFormatChange({ italic: true, borders: BorderChange.outline(thin) })), 'A1', 'A1:B2');
        expect(draft.fontStyle).toBe('italic');
        expect(line(draft.edgeLine('top'))).toBe(line(thin));
        expect(line(draft.edgeLine('right'))).toBe(line(thin));
        expect(line(draft.edgeLine('insideVertical'))).toBe(line(BorderLine.none));
    });

    it('ADR-0071 / SH-45, case 11-24: a thick bottom over a plain cell is an inside edge that differs', () => {
        expect.hasAssertions();
        expect(open(thickBottomOnA1, 'A1', 'A1:A2').edgeLine('insideHorizontal')).toBeUndefined();
    });

    it('ADR-0071 / SH-45, case 14-13: A2 under A1\'s thick bottom opens with a thick top, though A2 records nothing; A1 opens with its own thick bottom', () => {
        expect.hasAssertions();
        const a2 = open(thickBottomOnA1, 'A2', 'A2');
        expect(line(a2.edgeLine('top'))).toBe(line(thick));
        expect(line(a2.edgeLine('bottom'))).toBe(line(BorderLine.none));
        expect(a2.change.isEmpty).toBe(true);
        expect(line(open(thickBottomOnA1, 'A1', 'A1').edgeLine('bottom'))).toBe(line(thick));
    });

    it('ADR-0071 / SH-45, case 14-13: over several ranges, each range\'s outer edges show as drawn, and differ where the lines drawn there do', () => {
        expect.hasAssertions();
        const prepare: Prepare = sheet => {
            thickBottomOnA1(sheet);
            format(sheet, 'C1', borders({ bottom: thick }));
        };
        expect(line(open(prepare, 'A2', 'A2', 'C2').edgeLine('top'))).toBe(line(thick));
        expect(open(prepare, 'A2', 'A2', 'E2').edgeLine('top')).toBeUndefined();
        // A2:B2's top is thick over A2 and drawn nowhere over B2.
        expect(open(prepare, 'A2', 'A2:B2').edgeLine('top')).toBeUndefined();
    });

    it('ADR-0071 / SH-45, case 14-13: an edge shown from the neighbour and left alone is not touched; the bottom takes the line the tab opened on (run 17, h10)', () => {
        expect.hasAssertions();
        const draft = open(thickBottomOnA1, 'A2', 'A2');
        draft.toggleEdge('bottom');
        expect(edgesOf(draft.change)).toEqual({ bottom: line(thick) });
    });

    it('ADR-0071 / SH-45, case 14-13 (run 17, h10): over A2, the Top button alone takes the shown top away, and OK clears that edge on both sides', () => {
        expect.hasAssertions();
        const sheet = sheetIn();
        thickBottomOnA1(sheet);
        const draft = FormatCellsDraft.open(sheet, CellAddress.parse('A2'), [CellRange.parse('A2')]);
        draft.toggleEdge('top');
        expect(edgesOf(draft.change)).toEqual({ top: line(BorderLine.none) });
        format(sheet, 'A2', draft.change);
        expect(sheet.getBorders(CellAddress.parse('A1')).bottom.isNone).toBe(true);
        expect(sheet.getBorders(CellAddress.parse('A2')).top.isNone).toBe(true);
    });

    it('ADR-0071 / SH-45, run 17 h10: over A2 under A1\'s thick bottom, the Border tab opens on Thick and Automatic, as the line is', () => {
        expect.hasAssertions();
        const draft = open(thickBottomOnA1, 'A2', 'A2');
        expect(penOf(draft)).toEqual(['thick', 'Automatic']);
        expect(draft.change.isEmpty).toBe(true);
    });

    it.each(['A2', 'A1'])('ADR-0071 / SH-45, case 14-13: A1\'s bottom thick black opens the tab on Thick and black, over %s', focus => {
        expect.hasAssertions();
        const draft = open(sheet => format(sheet, 'A1', borders({ bottom: new BorderLine('thick', black) })), focus, focus);
        expect(penOf(draft)).toEqual(['thick', black.toString()]);
    });

    it('ADR-0071 / SH-45, run 17 h10\'s second case: A2\'s top set thin after A1\'s thick bottom opens the tab on Thin and Automatic', () => {
        expect.hasAssertions();
        const draft = open(sheet => {
            thickBottomOnA1(sheet);
            format(sheet, 'A2', borders({ top: thin }));
        }, 'A2', 'A2');
        expect(penOf(draft)).toEqual(['thin', 'Automatic']);
    });

    it.each([
        [1, 'B2', 'B2'],
        [2, 'A2', 'A2:B3'],
        [3, 'D5', 'D5'],
    ] as const)('ADR-0071 / SH-45, the readings of 2026-10-12 (R8 (%i)): Thin and Automatic over %s in %s', (reading, focus, range) => {
        expect.hasAssertions();
        const draft = open(sheet => {
            if (reading === 1) format(sheet, 'B2', borders({ top: thin, right: thick }));
            if (reading === 2) {
                format(sheet, 'A2', borders({ top: thin }));
                format(sheet, 'B2', borders({ right: new BorderLine('medium') }));
                format(sheet, 'A3', borders({ bottom: thick }));
                format(sheet, 'B3', borders({ left: new BorderLine('dashed') }));
            }
        }, focus, range);
        expect(penOf(draft)).toEqual(['thin', 'Automatic']);
    });

    it.each([
        ['B2', ['B2']],
        ['A2', ['A2:B2']],
        ['A2', ['A2', 'C2']],
    ] as const)('ADR-0071 / SH-45 (run 18a, format-cells 2, 4 and 5): the tab opens on the Thick the top shows, over %s in %j', (focus, ranges) => {
        expect.hasAssertions();
        const draft = open(sheet => {
            format(sheet, 'B2', borders({ top: thick, right: new BorderLine('medium') }));
            for (const cell of ['A1', 'B1', 'C1']) format(sheet, cell, borders({ bottom: thick }));
        }, focus, ...ranges);
        expect(penOf(draft)).toEqual(['thick', 'Automatic']);
    });

    it('ADR-0071 / SH-45 (run 18a, format-cells 3): B2 with a thick top and a thick bottom opens on Thick', () => {
        expect.hasAssertions();
        expect(penOf(open(sheet => format(sheet, 'B2', borders({ top: thick, bottom: thick })), 'B2', 'B2'))).toEqual(['thick', 'Automatic']);
    });

    it.each(['C2', 'B2'])('ADR-0071 / SH-45, ticket 102 (run 18a, format-cells 7): the tab opens on the drawn line, Thick and red, over %s', cell => {
        expect.hasAssertions();
        const draft = open(sheet => {
            format(sheet, 'C2', borders({ left: new BorderLine('thin', colourFromRgb(0x0000FF)) }));
            format(sheet, 'B2', borders({ right: new BorderLine('thick', red) }));
        }, cell, cell);
        expect(penOf(draft)).toEqual(['thick', red.toString()]);
    });

    it.each([
        ['B2', { left: thick }, 'B2', thick],
        ['B2', { right: thick }, 'B2', thick],
        ['C2', { left: thick }, 'B2', thick],
        ['A1', { bottom: new BorderLine('mediumDashed') }, 'A2', new BorderLine('mediumDashed')],
        ['A1', { bottom: new BorderLine('thick', red) }, 'A2', new BorderLine('thick', red)],
    ] as const)('ADR-0071 / SH-45 (run 18a, format-cells 1 and 6): one line shown on %s\'s edge %j opens the tab on it over %s', (at, sides, cell, expected) => {
        expect.hasAssertions();
        const draft = open(sheet => format(sheet, at, borders(sides)), cell, cell);
        expect(penOf(draft)).toEqual([expected.style, expected.colour.toString()]);
        expect(draft.change.isEmpty).toBe(true);
    });
});

function openIn(culture: string, numberFormat: NumberFormat | undefined): FormatCellsDraft {
    const sheet = sheetIn(Culture.of(culture));
    if (numberFormat !== undefined) sheet.workbook.do(sheet.name, SheetEdit.setNumberFormat(CellRange.parse('A1'), numberFormat));
    return FormatCellsDraft.open(sheet, CellAddress.parse('A1'), [CellRange.parse('A1')]);
}

function typed(culture: string, code: string): NumberFormat | undefined {
    const draft = openIn(culture, undefined);
    draft.selectCategory('custom');
    draft.setType(code);
    return draft.change.numberFormat;
}

function same(a: NumberFormat | undefined, b: NumberFormat | undefined): boolean {
    return a !== undefined && b !== undefined && a.equals(b);
}

describe('FormatCellsDraft under Custom (the fourteenth Windows run, case 11)', () => {
    it('ADR-0071 / SH-45, case 14-11: under ja-JP, dd-mmm-yy typed under Custom is built-in 15, and d-mmm-yy is a code of its own', () => {
        expect.hasAssertions();
        expect(typed('ja-JP', 'dd-mmm-yy')?.code).toBe('d-mmm-yy');
        const own = typed('ja-JP', 'd-mmm-yy');
        expect(own?.code).not.toBe('d-mmm-yy');
        expect(own?.localCode(Culture.of('ja-JP'))).toBe('d-mmm-yy');
        // Under en-US the built-in spells d-mmm-yy, so typed there it is the built-in.
        expect(typed('en-US', 'd-mmm-yy')?.code).toBe('d-mmm-yy');
    });

    it('ADR-0071 / SH-45, case 14-11: a code of its own opens under Custom spelled as it was typed, and OK without a change keeps it', () => {
        expect.hasAssertions();
        const draft = openIn('ja-JP', typed('ja-JP', 'd-mmm-yy'));
        expect(draft.category).toBe('custom');
        expect(draft.typeCode).toBe('d-mmm-yy');
        expect(draft.change.isEmpty).toBe(true);
        draft.selectCategory('custom');
        expect(same(draft.change.numberFormat, typed('ja-JP', 'd-mmm-yy'))).toBe(true);
    });

    it('ADR-0071 / SH-45, case 14-11: Custom starts from the built-in shown as the culture spells it, so OK keeps the built-in', () => {
        expect.hasAssertions();
        const draft = openIn('ja-JP', NumberFormat.parse('d-mmm-yy'));
        expect(draft.category).toBe('date');
        draft.selectCategory('custom');
        expect(draft.typeCode).toBe('dd-mmm-yy');
        expect(draft.change.numberFormat?.code).toBe('d-mmm-yy');
    });

    it('ADR-0071 / SH-45, case 14-11: a code typed under Custom stays as typed when Custom is chosen again', () => {
        expect.hasAssertions();
        const draft = openIn('ja-JP', undefined);
        draft.selectCategory('custom');
        draft.setType('d-mmm-yy');
        draft.selectCategory('custom');
        expect(draft.typeCode).toBe('d-mmm-yy');
        expect(same(draft.change.numberFormat, typed('ja-JP', 'd-mmm-yy'))).toBe(true);
    });

    it('ADR-0071 / SH-45, case 14-11: the currency key\'s built-in under en-GB opens under Custom in en-GB\'s spelling, and set again it stays the built-in', () => {
        expect.hasAssertions();
        const enGb = Culture.of('en-GB');
        const draft = openIn('en-GB', NumberFormat.builtInCurrency(enGb));
        expect(draft.category).toBe('custom');
        expect(draft.typeCode).toBe('£#,##0.00;[Red]-£#,##0.00');
        draft.selectCategory('custom');
        expect(same(draft.change.numberFormat, NumberFormat.builtInCurrency(enGb))).toBe(true);
    });

    it('ADR-0071 / SH-45, case 14-11: Custom lists the codes as the culture spells them, so the built-in short date is listed in en-GB\'s spelling, once', () => {
        expect.hasAssertions();
        const draft = openIn('en-GB', undefined);
        draft.selectCategory('custom');
        expect(draft.types).toContain('dd/mm/yyyy');
        expect(draft.types).toContain('dd-mmm-yy');
        expect(draft.types).toContain('hh:mm');
        expect(draft.types).not.toContain('m/d/yyyy');
        expect(new Set(draft.types).size).toBe(draft.types.length);
        draft.setType('dd/mm/yyyy');
        expect(draft.change.numberFormat?.code).toBe('m/d/yyyy');
        // Under en-US the list is spelled as it always was.
        const usDraft = openIn('en-US', undefined);
        usDraft.selectCategory('custom');
        expect(usDraft.types).toEqual(customTypes);
    });

    it('ADR-0071: a Custom code ExSheet does not read is refused by name, and OK sets no Number Format', () => {
        expect.hasAssertions();
        const draft = openIn('en-US', undefined);
        draft.selectCategory('custom');
        draft.setType('');
        expect(draft.refusal).toEqual({ kind: 'noNumberFormatCode' });
        draft.setType('0.0 [');
        expect(draft.refusal?.kind).toBe('numberFormatNotRead');
        expect(draft.change.numberFormat).toBeUndefined();
    });
});

function fontOf(change: CellFormatChange): readonly unknown[] {
    return [change.bold, change.italic, change.underline, change.strikethrough, change.fontColour?.isAutomatic];
}

describe('Normal font (ADR-0071, 2026-10-02; Part C, case 24)', () => {
    it('SH-55: the box shows checked exactly while every part of the Font is the default', () => {
        expect.hasAssertions();
        expect(open(undefined, 'A1', 'A1').isNormalFont).toBe(true);
        expect(open(sheet => format(sheet, 'A1', new CellFormatChange({ bold: true })), 'A1', 'A1').isNormalFont).toBe(false);
        expect(open(sheet => format(sheet, 'A1', new CellFormatChange({ fontColour: red })), 'A1', 'A1').isNormalFont).toBe(false);
        // Bold differing across the Selection shows the Font style empty, which is not the default.
        expect(open(sheet => format(sheet, 'A2', new CellFormatChange({ bold: true })), 'A1', 'A1:A2').isNormalFont).toBe(false);
        const draft = open(undefined, 'A1', 'A1');
        draft.setUnderline(true);
        expect(draft.isNormalFont).toBe(false);
    });

    it('SH-55: checking it sets every part of the Font to its default, and OK records each one', () => {
        expect.hasAssertions();
        const draft = open(sheet => format(sheet, 'A1', new CellFormatChange({
            bold: true, italic: true, underline: true, strikethrough: true, fontColour: colourFromRgb(0x0000FF),
        })), 'A1', 'A1');
        draft.setNormalFont();
        expect(draft.isNormalFont).toBe(true);
        const change = draft.change;
        expect(fontOf(change)).toEqual([false, false, false, false, true]);
        expect(change.fill).toBeUndefined();
        expect(change.numberFormat).toBeUndefined();
    });

    it('SH-55: OK records the default Font on the cell, over its row\'s Font', () => {
        expect.hasAssertions();
        const sheet = sheetIn();
        format(sheet, '2:2', new CellFormatChange({ bold: true, fontColour: red }));
        const draft = FormatCellsDraft.open(sheet, CellAddress.parse('B2'), [CellRange.parse('B2')]);
        draft.setNormalFont();
        format(sheet, 'B2', draft.change);
        expect(sheet.getCellFormat(CellAddress.parse('B2')).font.equals(CellFont.default)).toBe(true);
        expect(sheet.getCellFormat(CellAddress.parse('C2')).font.bold).toBe(true);
    });
});
