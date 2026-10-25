// Text compared as the C# engine compares it: two spellings that differ only in case are one Item
// (ADR-0060), as .NET's OrdinalIgnoreCase folds them, a column's name upper-cases an Item invariantly and
// escapes it as Uri.EscapeDataString does, and a label orders by the culture's collation, ignoring case,
// ties broken ordinally. tools/pivot-oracle holds each to .NET.

import { elementAt } from '../must';
import { upperRuns } from './upper-table';

/** A way .NET upper-cases text: the code points it leaves as they are, beyond those .NET's table leaves. */
interface Casing {
    readonly kept: ReadonlySet<number>;
}

// ToUpperInvariant is the table itself; OrdinalIgnoreCase keeps the long s as it is.
const invariantCasing: Casing = { kept: new Set() };
const ordinalCasing: Casing = { kept: new Set([0x17F]) };

/** A code point's simple upper case, as .NET's table holds it (upper-table.ts), never the JavaScript
 * engine's: its full mapping writes ᾳ as two code points, and its Unicode is its own version. */
function upperOf(codePoint: number, casing: Casing): number {
    if (codePoint < 0x80) return codePoint >= 0x61 && codePoint <= 0x7A ? codePoint - 0x20 : codePoint;
    if (casing.kept.has(codePoint)) return codePoint;
    let low = 0;
    let high = upperRuns.length - 1;
    while (low <= high) {
        const middle = (low + high) >> 1;
        const [first, last, delta, step] = elementAt(upperRuns, middle);
        if (codePoint < first) {
            high = middle - 1;
        } else if (codePoint > last) {
            low = middle + 1;
        } else {
            return (codePoint - first) % step === 0 ? codePoint + delta : codePoint;
        }
    }
    return codePoint;
}

function upperCased(text: string, casing: Casing): string {
    // oxlint-disable-next-line no-control-regex -- an all-ASCII text upper-cases as JavaScript's own
    if (/^[\x00-\x7F]*$/.test(text)) return text.toUpperCase();
    let result = '';
    for (const ch of text) {
        const codePoint = ch.codePointAt(0);
        if (codePoint === undefined) throw new Error('Internal: an empty character.');
        result += String.fromCodePoint(upperOf(codePoint, casing));
    }
    return result;
}

/** The text upper-cased one code point at a time, as `ToUpperInvariant` maps it. */
export function upperInvariant(text: string): string {
    return upperCased(text, invariantCasing);
}

/** What two texts equal under OrdinalIgnoreCase share: a Map key for an Item's text. */
export function foldedText(text: string): string {
    return upperCased(text, ordinalCasing);
}

/** -1, 0 or 1 as `a` sorts before, with or after `b` ordinally, by UTF-16 code unit. */
export function compareOrdinal(a: string, b: string): number {
    if (a === b) return 0;
    return a < b ? -1 : 1;
}

/** `string.Compare(a, b, StringComparison.OrdinalIgnoreCase)`'s sign. */
export function compareOrdinalIgnoringCase(a: string, b: string): number {
    return compareOrdinal(foldedText(a), foldedText(b));
}

/** `Uri.EscapeDataString`: every UTF-8 byte but the unreserved letters, digits and `-._~` as `%XX`. */
export function escapeDataString(text: string): string {
    // A lone surrogate has no UTF-8; .NET writes the replacement character in its place.
    const wellFormed = text.replace(/[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/g, '\uFFFD');
    return encodeURIComponent(wellFormed).replace(/[!'()*]/g, (ch) => `%${ch.charCodeAt(0).toString(16).toUpperCase()}`);
}

// What .NET's char.IsWhiteSpace holds a space: NEL is one, and the byte order mark, which JavaScript's trim
// takes away, is not.
const netWhiteSpace = /^[\t\n\v\f\r \u0085\u00A0\u1680\u2000-\u200A\u2028\u2029\u202F\u205F\u3000]*$/u;

/** Whether nothing is left of the text once .NET's `Trim` has taken its white space away. */
export function isWhiteSpaceOnly(text: string): boolean {
    return netWhiteSpace.test(text);
}
