// The order in which Excel's comparison operators, and XLOOKUP's approximate matches, put text (ADR-0047). It
// is a collation, not code points: Excel puts `é` beside `e`, so `="é">"z"` is FALSE, and case is ignored, so
// `="a"="A"` is TRUE. The engine's own, so that it gives the same answer on every platform and in the browser.
//
// Text is compared first on each character's primary weight — punctuation, symbols and spaces before digits,
// digits before letters, and a letter by its base letter, its accents removed and its case ignored — with a
// hyphen and an apostrophe passed over and `ß` read as `ss`; where every primary weight is equal, on the
// accents (an unaccented letter before an accented one); and where those are equal too, on the hyphens and
// apostrophes passed over (the text with fewer first).
import { compareOrdinal, upperInvariant } from '../dotnet';
import { elementAt } from '../must';

interface Weight {
    readonly weightClass: number;
    readonly base: number;
    readonly accents: string;
}

/** Excel's order of two texts: negative, 0 or positive. */
export function compareText(left: string, right: string): number {
    const a = weigh(left);
    const b = weigh(right);
    const length = Math.min(a.weights.length, b.weights.length);
    for (let i = 0; i < length; i++) {
        const x = elementAt(a.weights, i);
        const y = elementAt(b.weights, i);
        const primary = x.weightClass !== y.weightClass ? Math.sign(x.weightClass - y.weightClass) : Math.sign(x.base - y.base);
        if (primary !== 0) return primary;
    }
    if (a.weights.length !== b.weights.length) return Math.sign(a.weights.length - b.weights.length);
    for (let i = 0; i < length; i++) {
        const accents = compareOrdinal(elementAt(a.weights, i).accents, elementAt(b.weights, i).accents);
        if (accents !== 0) return Math.sign(accents);
    }
    return Math.sign(a.hyphens - b.hyphens);
}

/**
 * A text two texts share exactly when `compareText` gives 0 for them: their primary weights, their accents,
 * and how many hyphens and apostrophes were passed over (ADR-0049, 2026-09-30).
 */
export function textEqualityKey(text: string): string {
    const { weights, hyphens } = weigh(text);
    let key = '';
    // Each weight is its class, its base character and its accents behind their length, so no two lists of
    // weights are written alike.
    for (const w of weights) {
        key += String.fromCharCode(0x30 + w.weightClass) + String.fromCharCode(w.base) + String.fromCharCode(w.accents.length) + w.accents;
    }
    return `${key}|${String(hyphens)}`;
}

const sharpS: Weight = { weightClass: 2, base: 0x53, accents: '' };

/** The primary weights and accents of the text's characters, a hyphen or an apostrophe passed over and counted, `ß` as `ss`. */
function weigh(text: string): { weights: Weight[]; hyphens: number } {
    let hyphens = 0;
    const weights: Weight[] = [];
    for (let i = 0; i < text.length; i++) {
        const c = text.charCodeAt(i);
        const pair = text.codePointAt(i);
        if (pair !== undefined && pair > 0xffff) {
            // A character outside the Basic Multilingual Plane is two halves with no decomposition: each is
            // weighed as itself, in the class of the whole character (an emoji among the symbols, 𠮷 among the
            // letters), as a character inside the plane with no accents is.
            const weightClass = classOfCharacter(String.fromCodePoint(pair));
            weights.push({ weightClass, base: c, accents: '' }, { weightClass, base: text.charCodeAt(i + 1), accents: '' });
            i++;
            continue;
        }
        if (c === 0x2d || c === 0x27) {
            hyphens++;
            continue;
        }
        if (c === 0xdf || c === 0x1e9e) {
            weights.push(sharpS, sharpS);
            continue;
        }
        weights.push(weighCharacter(c));
    }
    return { weights, hyphens };
}

/** A character's primary weight — its class and its base character — and its accents. */
function weighCharacter(c: number): Weight {
    if (c < 128) return { weightClass: classOf(c), base: upperInvariant(c), accents: '' };
    // A half with no other half is no character: it is weighed as itself, among the symbols.
    if (c >= 0xd800 && c <= 0xdfff) return { weightClass: 0, base: c, accents: '' };
    const decomposed = String.fromCharCode(c).normalize('NFD');
    const base = decomposed.charCodeAt(0);
    return { weightClass: classOf(base), base: upperInvariant(base), accents: decomposed.slice(1) };
}

const decimalDigit = /^\p{Nd}$/u;
const letter = /^\p{L}$/u;

function classOf(c: number): number {
    return classOfCharacter(String.fromCharCode(c));
}

function classOfCharacter(character: string): number {
    if (decimalDigit.test(character)) return 1;
    if (letter.test(character)) return 2;
    return 0;
}
