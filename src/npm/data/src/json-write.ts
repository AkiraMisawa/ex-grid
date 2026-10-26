// JSON written as System.Text.Json writes it under its default encoder, so that a document both platforms write is
// one text, byte for byte: a Board Diff (ADR-0340, Q7) is written in C# and in TypeScript alike, its parts read back
// and written again. The core's View State writer holds its own copy of the string and the double rules.

import { isJsonArray, jsonMember, jsonMemberNames, JsonNumber, type JsonValue } from './json';

const shortEscapes: Readonly<Record<string, string>> = Object.freeze({ '\\': '\\\\', '\n': '\\n', '\r': '\\r', '\t': '\\t', '\b': '\\b', '\f': '\\f' });

/** A string as Utf8JsonWriter writes it under the default encoder: the quote, the ampersand, the apostrophe, the plus,
 * the angle brackets and the backtick as `\uXXXX`, and every character past printable ASCII as its UTF-16 units. A lone surrogate, which it refuses, is
 * refused. */
export function jsonStringText(text: string): string {
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

/** A finite double as Utf8JsonWriter writes it: .NET's shortest text that reads back as the same value, laid out
 * as its general format lays it out (`1E+21`, `-1.5E-07`). A NaN or an infinity, which JSON has no number for, is
 * refused. */
export function jsonDoubleText(value: number): string {
    if (!Number.isFinite(value)) throw new RangeError(`${value} is no number JSON writes.`);
    const negative = value < 0 || Object.is(value, -0);
    if (value === 0) return negative ? '-0' : '0';
    // JavaScript's toExponential() with no argument writes the shortest digits that read back as the double.
    const [mantissa = '', exponent = ''] = Math.abs(value).toExponential().split('e');
    const digits = mantissa.replace('.', '').replace(/0+$/u, '');
    const scale = Number(exponent) + 1;
    // .NET's FormatGeneral under the round-trip format: an exponent where the scale lies past 17 digits (or the
    // digits' own count, if more) or below -3.
    const scientific = scale > Math.max(digits.length, 17) || scale < -3;
    const point = scientific ? 1 : scale;
    let text = point > 0 ? digits.slice(0, point).padEnd(point, '0') : '0';
    const rest = point > 0 ? digits.slice(point) : digits;
    if (rest.length > 0 || point < 0) text += `.${'0'.repeat(Math.max(0, -point))}${rest}`;
    if (scientific) {
        const power = scale - 1;
        text += `E${power < 0 ? '-' : '+'}${String(Math.abs(power)).padStart(2, '0')}`;
    }
    return (negative ? '-' : '') + text;
}

/** `value` as `JsonNode.ToJsonString()` writes a node read from it: no space between tokens, each member in the
 * order it was read, a number as it is spelt, and each string as {@link jsonStringText} writes it. */
export function writeJsonText(value: JsonValue): string {
    if (value === null) return 'null';
    if (typeof value === 'boolean') return value ? 'true' : 'false';
    if (typeof value === 'string') return jsonStringText(value);
    if (value instanceof JsonNumber) return value.raw;
    if (isJsonArray(value)) return `[${value.map(writeJsonText).join(',')}]`;
    return `{${jsonMemberNames(value).map(name => {
        const member = jsonMember(value, name);
        if (member === undefined) throw new Error(`Internal: member '${name}' is named and missing.`);
        return `${jsonStringText(name)}:${writeJsonText(member)}`;
    }).join(',')}}`;
}
