// Excel's formatting keys, as ExSheet declares them to its grid (ADR-0071, "Keys"; ADR-0050 item 14) and what
// each applies, the counterpart of C#'s SheetFormatKeys. Only the keys Excel has, each only as Excel has it, as
// the eleventh Windows run found them (verification/2026-10-01-windows-excel-11/cell-format.md, cases 16 to 20).

import { BorderChange, BorderLine, CellFormatChange, NumberFormat, type CellFormat, type Culture } from '@ex-grid/sheet-engine';
import { hasTwelveHourTime } from './culture-facts';
import { unreachable } from './must';

/** What a formatting key does. Ctrl+1 opens Format Cells (ticket 52) and sets nothing itself. */
export type SheetFormatKey =
    | 'bold' | 'italic' | 'underline' | 'strikethrough' | 'general' | 'number' | 'time' | 'date' | 'currency' | 'percent'
    | 'scientific' | 'outline' | 'noBorders' | 'formatCells';

// Each key is the character it types with Ctrl, as Excel reads it — by the character, not by where the key lies
// (case 19) — so a layout that types `#` without Shift (UK) reaches the date format as one that needs Shift does.
// A letter is claimed in both cases for CapsLock, and without Shift: Ctrl+Shift+U is another key in Excel. Every
// other character is claimed with Shift and without it, whichever its layout needs.
const keys: readonly (readonly [string, SheetFormatKey])[] = [
    ['b', 'bold'], ['2', 'bold'],
    ['i', 'italic'], ['3', 'italic'],
    ['u', 'underline'], ['4', 'underline'],
    ['5', 'strikethrough'],
    ['~', 'general'],
    ['!', 'number'],
    ['@', 'time'],
    ['#', 'date'],
    ['$', 'currency'],
    ['%', 'percent'],
    ['^', 'scientific'],
    ['&', 'outline'],
    ['_', 'noBorders'],
    ['1', 'formatCells'],
];

function declaredKeys(): ReadonlyMap<string, SheetFormatKey> {
    const byKey = new Map<string, SheetFormatKey>();
    for (const [character, kind] of keys) {
        if (/^[a-z]$/u.test(character)) {
            byKey.set(`Control+${character}`, kind);
            byKey.set(`Control+${character.toUpperCase()}`, kind);
        } else {
            byKey.set(`Control+${character}`, kind);
            byKey.set(`Control+Shift+${character}`, kind);
        }
    }
    return byKey;
}

/** The formatting keys ExSheet declares to its grid, in the grid's canonical form. */
export function sheetFormatKeysDeclared(): readonly string[] {
    return Object.freeze([...declaredKeys().keys()]);
}

/** What a declared key does; `undefined` for a key that is not one of these. */
export function sheetFormatKeyOf(key: string): SheetFormatKey | undefined {
    return declaredKeys().get(key);
}

/**
 * What a key sets on the Selection, under the Sheet's culture (cases 16 to 20). A toggle's direction is the
 * Focus cell's (case 17): over a Focus that is bold, Ctrl+B takes bold off every cell, and otherwise puts it on
 * every cell. The Number Formats are the ones Excel applied under en-GB, en-US and ja-JP alike, but for two: the
 * time is 12-hour where the culture's own time is (`h:mm AM/PM` under en-US, `h:mm` under en-GB and ja-JP), and
 * the currency is Excel's built-in, shown in the culture's own currency (`NumberFormat.builtInCurrency`). The
 * date and the time are Excel's built-ins 15 and 20 (or the AM/PM one), recorded in their codes and shown in the
 * culture's own form, as Excel shows them (the twelfth run, case 19). A Number Format widens the columns it no
 * longer fits, as every Number Format set on the Selection does (case 17), and so do strikethrough and underline
 * (`widensAsAKeyDoes`). The outline is thin and Automatic on each range's four edges; no borders clears every
 * edge of each range. Ctrl+1 sets nothing, and is refused here.
 */
export function sheetFormatChangeFor(kind: SheetFormatKey, focus: CellFormat, culture: Culture): CellFormatChange {
    switch (kind) {
        case 'bold': return new CellFormatChange({ bold: !focus.font.bold });
        case 'italic': return new CellFormatChange({ italic: !focus.font.italic });
        case 'underline': return new CellFormatChange({ underline: !focus.font.underline });
        case 'strikethrough': return new CellFormatChange({ strikethrough: !focus.font.strikethrough });
        case 'general': return new CellFormatChange({ numberFormat: NumberFormat.general });
        case 'number': return new CellFormatChange({ numberFormat: NumberFormat.parse('#,##0.00') });
        case 'time': return new CellFormatChange({ numberFormat: NumberFormat.parse(hasTwelveHourTime(culture) ? 'h:mm AM/PM' : 'h:mm') });
        case 'date': return new CellFormatChange({ numberFormat: NumberFormat.parse('d-mmm-yy') });
        case 'currency': return new CellFormatChange({ numberFormat: NumberFormat.builtInCurrency(culture) });
        case 'percent': return new CellFormatChange({ numberFormat: NumberFormat.parse('0%') });
        case 'scientific': return new CellFormatChange({ numberFormat: NumberFormat.parse('0.00E+00') });
        case 'outline': return new CellFormatChange({ borders: BorderChange.outline(new BorderLine('thin')) });
        case 'noBorders': return new CellFormatChange({ borders: BorderChange.none });
        case 'formatCells': throw new RangeError('Ctrl+1 opens Format Cells, and sets no format of its own.');
        default: return unreachable(kind);
    }
}
