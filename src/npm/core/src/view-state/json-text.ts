// JSON written as the C# core's System.Text.Json writes it under its default encoder, so that a document both
// cores write is one text, byte for byte (ADR-0207, ADR-0370 section 5). The pivot engine's writer, copied:
// the packages share no code.

const shortEscapes: Readonly<Record<string, string>> = Object.freeze({ '\\': '\\\\', '\n': '\\n', '\r': '\\r', '\t': '\\t', '\b': '\\b', '\f': '\\f' });

/** A string as Utf8JsonWriter writes it under the default encoder: `"` as `"`, `+` as `+`, every
 * character past printable ASCII as its UTF-16 units. A lone surrogate, which it refuses, is refused. */
export function jsonString(text: string): string {
    let written = '"';
    for (let i = 0; i < text.length; i++) {
        const ch = text.charAt(i);
        const code = text.charCodeAt(i);
        const high = isHigh(code);
        const low = isLow(code);
        const paired = high ? isLow(text.charCodeAt(i + 1)) : low && i > 0 && isHigh(text.charCodeAt(i - 1));
        if ((high || low) && !paired) throw new TypeError(`Cannot write invalid UTF-16 text: a lone surrogate at ${i} of ${JSON.stringify(text)}.`);
        const short = shortEscapes[ch];
        if (short !== undefined) written += short;
        else if (code < 0x20 || code > 0x7e || '"&\'+<>`'.includes(ch)) written += `\\u${code.toString(16).toUpperCase().padStart(4, '0')}`;
        else written += ch;
    }
    return `${written}"`;
}

function isHigh(code: number): boolean {
    return code >= 0xd800 && code <= 0xdbff;
}

function isLow(code: number): boolean {
    return code >= 0xdc00 && code <= 0xdfff;
}

/** An object written from members already written, in order, as `{"name":value,…}`. */
export function jsonObject(members: readonly (readonly [string, string])[]): string {
    return `{${members.map(([name, value]) => `${jsonString(name)}:${value}`).join(',')}}`;
}

/** An array written from entries already written. */
export function jsonArray(entries: readonly string[]): string {
    return `[${entries.join(',')}]`;
}
