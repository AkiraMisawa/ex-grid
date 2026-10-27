// The escaping ECMA-376 gives text (ST_Xstring, Part 1, §22.9.2.19), as ExGrid.Data.Xlsx's XString does it: a
// character XML cannot carry is written `_xHHHH_`, and a literal `_x` that would read as one is written `_x005F_`
// first. Excel writes a carriage return in a cell as `_x000D_`.

function hexAt(text: string, at: number): number | undefined {
    const digits = text.slice(at, at + 4);
    return /^[0-9A-Fa-f]{4}$/.test(digits) ? Number.parseInt(digits, 16) : undefined;
}

/** The text an escaped one stands for: each whole `_xHHHH_` its character. */
export function decodeXString(text: string): string {
    let at = text.indexOf('_x');
    if (at < 0) return text;
    let out = '';
    let start = 0;
    while (at >= 0) {
        const code = at + 7 <= text.length && text.charAt(at + 6) === '_' ? hexAt(text, at + 2) : undefined;
        if (code !== undefined) {
            out += text.slice(start, at) + String.fromCharCode(code);
            start = at + 7;
            at = text.indexOf('_x', start);
        } else {
            at = text.indexOf('_x', at + 2);
        }
    }
    return out + text.slice(start);
}

/** A character XML 1.0 cannot carry, or a carriage return, which XML would turn into a line feed. */
function escaped(c: number): boolean {
    return (c < 0x20 && c !== 0x09 && c !== 0x0a) || c === 0xfffe || c === 0xffff;
}

function looksEscaped(text: string, at: number): boolean {
    return at + 7 <= text.length && text.charAt(at + 1) === 'x' && text.charAt(at + 6) === '_' && hexAt(text, at + 2) !== undefined;
}

/** The text escaped as a file writes it. */
export function encodeXString(text: string): string {
    let needs = false;
    for (let i = 0; i < text.length; i++) {
        const c = text.charCodeAt(i);
        if (c === 0x5f || escaped(c)) {
            needs = true;
            break;
        }
    }
    if (!needs) return text;
    let out = '';
    for (let i = 0; i < text.length; i++) {
        const c = text.charCodeAt(i);
        if (escaped(c)) out += `_x${c.toString(16).toUpperCase().padStart(4, '0')}_`;
        else if (c === 0x5f && looksEscaped(text, i)) out += '_x005F_';
        else out += text.charAt(i);
    }
    return out;
}

/**
 * Whether XML 1.0 carries `text`: as it is, or, when `encoded`, once `encodeXString` has escaped it. Escaping carries
 * every control character, but no half of a surrogate pair without the other.
 */
export function carries(text: string, encoded: boolean): boolean {
    for (let i = 0; i < text.length; i++) {
        const c = text.charCodeAt(i);
        if (c >= 0xd800 && c <= 0xdbff) {
            const next = i + 1 < text.length ? text.charCodeAt(i + 1) : 0;
            if (next >= 0xdc00 && next <= 0xdfff) {
                i++;
                continue;
            }
            return false;
        }
        if (c >= 0xdc00 && c <= 0xdfff) return false;
        if (!encoded && ((c < 0x20 && c !== 0x09 && c !== 0x0a && c !== 0x0d) || c === 0xfffe || c === 0xffff)) return false;
    }
    return true;
}
