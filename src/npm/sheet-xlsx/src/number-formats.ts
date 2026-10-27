import { NumberFormat, type Culture } from '@ex-grid/sheet-engine';
import { firstCustomNumberFormat } from '@ex-grid/data-xlsx';
import { equalsIgnoreCase } from './text-casing';

// A Number Format between an .xlsx's id and code and the engine's NumberFormat, both ways (ADR-0330 §4, ADR-0071), as
// ExSheet.Xlsx's XlsxNumberFormats crosses it. A built-in id is the engine's format of the same meaning: the dates and the
// time Excel shows in the culture's own form (the short date 14 and 22, 15, 16, 20) are the engine's built-ins of the same
// invariant code, which it shows in the Workbook's culture as Excel shows them in the computer's; the rest are their fixed
// codes (ECMA-376 Part 1, §18.8.30). The currencies 5 to 8 are read from the code the file spells, as every other id whose
// code follows the computer's locale: an amount is never shown in the reader's currency where the file meant another.
// Written, the engine's built-in currency is a code of the file's own that spells the Workbook culture's currency, so that
// it means the same amount under every locale Excel opens it in. A format of the file's own is read as written. A code the
// engine does not read, or a built-in id whose code follows the computer's locale and that the file does not spell out,
// cannot cross, and the file is refused by name: a cell shown in General instead would show a date as `45000`, or drop a
// currency symbol, and look plausible.

/**
 * The built-ins whose meaning is fixed, by id, in the engine's invariant codes. 14 and 22 are the engine's built-in short
 * dates (ECMA-376 spells them `mm-dd-yy` and `m/d/yy h:mm`, and Excel shows both in the computer's short-date pattern). The
 * currencies 5 to 8 are not here: Excel shows them in the computer's currency.
 */
const builtIns: ReadonlyMap<number, string> = new Map([
    [0, 'General'],
    [1, '0'],
    [2, '0.00'],
    [3, '#,##0'],
    [4, '#,##0.00'],
    [9, '0%'],
    [10, '0.00%'],
    [11, '0.00E+00'],
    [12, '# ?/?'],
    [13, '# ??/??'],
    [14, 'm/d/yyyy'],
    [15, 'd-mmm-yy'],
    [16, 'd-mmm'],
    [17, 'mmm-yy'],
    [18, 'h:mm AM/PM'],
    [19, 'h:mm:ss AM/PM'],
    [20, 'h:mm'],
    [21, 'h:mm:ss'],
    [22, 'm/d/yyyy h:mm'],
    [37, '#,##0 ;(#,##0)'],
    [38, '#,##0 ;[Red](#,##0)'],
    [39, '#,##0.00;(#,##0.00)'],
    [40, '#,##0.00;[Red](#,##0.00)'],
    [45, 'mm:ss'],
    [46, '[h]:mm:ss'],
    [47, 'mmss.0'],
    [48, '##0.0E+0'],
    [49, '@'],
]);

/** The built-ins the engine shows in the culture's own form; a code of the engine's that spells one, in any case, is it. */
const localised: ReadonlySet<number> = new Set([14, 15, 16, 20, 22]);

/** The engine's built-in currency, in its invariant codes, with and without decimals (`NumberFormat.builtInCurrency`). */
const builtInCurrencies = ['$#,##0.00_);[Red]($#,##0.00)', '$#,##0_);[Red]($#,##0)'];

/**
 * The code a format of the file's own writes for `format` under the Workbook's `culture`: the engine's built-in currency
 * spelled in the culture's currency (`NumberFormat.localCode`), so that the file says which currency it is; any other format
 * its own code.
 */
export function writtenCode(format: NumberFormat, culture: Culture): string {
    return builtInCurrencies.some(c => equalsIgnoreCase(c, format.code)) ? format.localCode(culture) : format.code;
}

/** What reading a cell format's Number Format gives: the format, or why it cannot cross and what the refusal names as held. */
export type NumberFormatRead =
    | { readonly format: NumberFormat; readonly reason?: undefined; readonly held?: undefined }
    | { readonly format?: undefined; readonly reason: string; readonly held: string };

/**
 * The format a cell format's `id` names, with the `code` the styles part writes for it, or the reason it cannot cross and
 * what to name. A built-in the engine knows is that built-in, whatever code the part writes beside it; any other id is the
 * code the part writes.
 */
export function readNumberFormat(id: number, code: string | undefined): NumberFormatRead {
    const builtIn = builtIns.get(id);
    if (builtIn !== undefined) {
        const reading = NumberFormat.tryParse(builtIn);
        if (reading.format !== undefined) return { format: reading.format };
        return { reason: `Excel's built-in Number Format ${String(id)} (${builtIn}) is not one the engine reads: ${reading.reason}`, held: builtIn };
    }
    if (code === undefined) {
        return {
            held: `Number Format ${String(id)}`,
            reason: id < firstCustomNumberFormat
                ? `The cell takes Excel's built-in Number Format ${String(id)}, whose code follows the computer's locale, and the file does not spell it out.`
                : `The cell takes the Number Format ${String(id)}, which the file does not hold.`,
        };
    }
    const reading = NumberFormat.tryParseAsWritten(code);
    if (reading.format !== undefined) return { format: reading.format };
    return { reason: `The Number Format '${code}' is not one the engine reads: ${reading.reason}`, held: code };
}

/**
 * The built-in id `format` is written as, or `undefined` for one written as a format of the file's own. A built-in the engine
 * shows in the culture's form is its id whatever the case of its code, as the engine reads it; any other is its id only when
 * its code is the built-in's exactly.
 */
export function builtInId(format: NumberFormat): number | undefined {
    for (const [id, code] of builtIns) {
        const same = localised.has(id) ? equalsIgnoreCase(code, format.code) : code === format.code;
        if (same) return id;
    }
    return format.isGeneral ? 0 : undefined;
}
