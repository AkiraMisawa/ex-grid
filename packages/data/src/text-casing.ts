// Text compares as .NET's OrdinalIgnoreCase does (ADR-0023), as the Script Core's query/text-casing.ts says it;
// the packages share no code, so each carries its own: character by character, each upper-cased by
// the invariant simple mapping, never by the reader's culture, so the machine a Consumer runs on cannot
// reorder a result. JavaScript's toUpperCase applies the full mapping instead, and .NET keeps two rules it
// does not: a character whose full upper case is several characters takes its simple mapping, which is
// itself (ß, ŉ) except for the Greek letters with ypogegrammeni (ᾳ is ᾼ), and a character outside ASCII
// never folds into ASCII (the dotless ı is not I, the long ſ is not S), so ASCII text compares as ASCII
// always has. Every code point was compared against .NET 10 when this was written.

/** `text` upper-cased as OrdinalIgnoreCase compares it. */
export function foldCase(text: string): string {
    let folded = '';
    let changed = false;
    for (const character of text) {
        const upper = upperOf(character);
        if (upper !== character) changed = true;
        folded += upper;
    }
    return changed ? folded : text;
}

function upperOf(character: string): string {
    const code = character.charCodeAt(0);
    if (code < 0x80) return code >= 0x61 && code <= 0x7a ? String.fromCharCode(code - 0x20) : character;
    const upper = character.toUpperCase();
    // One code point to one code point, and never from outside ASCII into it.
    const point = upper.codePointAt(0);
    const single = point !== undefined && upper.length === (point > 0xffff ? 2 : 1);
    if (!single) return simpleUpperOf(code, character);
    if (upper === character || point < 0x80) return character;
    return upper;
}

/** The simple upper case of a character whose full upper case is several characters. Only the Greek
 * letters with ypogegrammeni have one that is not themselves: each takes the prosgegrammeni form. */
function simpleUpperOf(code: number, character: string): string {
    const pairs = (code >= 0x1f80 && code <= 0x1f87) || (code >= 0x1f90 && code <= 0x1f97) || (code >= 0x1fa0 && code <= 0x1fa7);
    if (pairs) return String.fromCharCode(code + 8);
    if (code === 0x1fb3 || code === 0x1fc3 || code === 0x1ff3) return String.fromCharCode(code + 9);
    return character;
}
