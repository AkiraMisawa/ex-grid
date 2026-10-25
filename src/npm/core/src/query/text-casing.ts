// Text compares as .NET's OrdinalIgnoreCase does (ADR-0023): character by character, each upper-cased by
// the invariant simple mapping, never by the reader's culture, so the machine a Consumer runs on cannot
// reorder a result. Two of .NET's rules are kept that JavaScript's toUpperCase does not: a character
// whose upper case is several characters (ß, ŉ) stays as it is, and a character outside ASCII never folds
// into ASCII (the dotless ı is not I, the long ſ is not S), so ASCII text compares as ASCII always has.

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
    if (upper === character || !single || point < 0x80) return character;
    return upper;
}

/** Orders two texts as OrdinalIgnoreCase does: by their upper-cased UTF-16 code units. */
export function compareIgnoringCase(a: string, b: string): number {
    const x = foldCase(a);
    const y = foldCase(b);
    if (x === y) return 0;
    return x < y ? -1 : 1;
}
