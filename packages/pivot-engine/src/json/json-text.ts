// JSON as the C# engine's System.Text.Json reads and writes it: a reader that keeps each number as it is
// spelt, since C# reads an integer or a decimal from its spelling and not from a double, and a writer that
// spells strings and numbers as Utf8JsonWriter does, so that a document both engines write is one text.

import { decimalFromText, type SnapshotDecimal } from '@ex-grid/data';
import { doubleRaw } from '../text/invariant';

/** A number as the document spells it: C# reads an integer from its spelling, so 1.0 is not one. */
export class JsonNumber {
    readonly raw: string;

    constructor(raw: string) {
        this.raw = raw;
    }

    /** The spelling's value as a 32-bit integer, or `undefined` where it has a fraction, an exponent or
     * a value outside 32 bits, as `JsonElement.TryGetInt32` refuses it. */
    get int32(): number | undefined {
        const value = this.#whole();
        return value !== undefined && value >= -(2 ** 31) && value < 2 ** 31 ? value : undefined;
    }

    /** The spelling's value as a whole number a double holds exactly, or `undefined` where it has a
     * fraction or an exponent, as `JsonElement.TryGetInt64` refuses it, or lies past 2^53. */
    get integer(): number | undefined {
        const value = this.#whole();
        return value !== undefined && Number.isSafeInteger(value) ? value : undefined;
    }

    /** The decimal the spelling writes exactly, or `undefined` where no decimal holds it: more than 28
     * places that are not trailing zeros, or digits past 2^96. An exponent is read. */
    get decimal(): SnapshotDecimal | undefined {
        const match = /^(-?)(\d+)(?:\.(\d+))?(?:[eE]([+-]?\d+))?$/u.exec(this.raw);
        if (match === null) return undefined;
        const [, sign = '', whole = '', fraction = '', exponent = '0'] = match;
        let digits = (whole + fraction).replace(/^0+(?=\d)/u, '');
        let places = fraction.length - Number(exponent);
        if (/^0+$/u.test(digits)) return decimalFromText('0');
        while (places > 0 && digits.endsWith('0')) {
            digits = digits.slice(0, -1);
            places--;
        }
        // A decimal has at most 29 digits, so a value past them is not one, however its exponent is spelt.
        if (places > 28 || digits.length - places > 29) return undefined;
        const text = places <= 0
            ? digits + '0'.repeat(-places)
            : `${digits.slice(0, -places).padStart(1, '0')}.${digits.slice(-places).padStart(places, '0')}`;
        if (BigInt(text.replace('.', '')) >= 1n << 96n) return undefined;
        return decimalFromText(sign + text);
    }

    /** The spelling's value as a double. */
    get double(): number {
        return Number(this.raw);
    }

    #whole(): number | undefined {
        if (!/^-?(?:0|[1-9]\d*)$/u.test(this.raw)) return undefined;
        return Number(this.raw) + 0;
    }
}

/** A JSON value, each number kept as it is spelt. */
export type Json = null | boolean | JsonNumber | string | readonly Json[] | { readonly [key: string]: Json };

/** A JSON object. */
export type JsonObject = { readonly [key: string]: Json };

// The names an object read was written with more than once, which only a reader that walks its members sees,
// and its names in the order written, which an object's own keys do not keep where a name is a whole number.
const writtenTwice = new WeakMap<JsonObject, readonly string[]>();
const writtenOrder = new WeakMap<JsonObject, readonly string[]>();

/** The names `object` was written with, each once, in the order the document first wrote them, as
 * System.Text.Json enumerates an object; an object {@link readJson} did not read gives its own keys. */
export function memberNames(object: JsonObject): readonly string[] {
    return writtenOrder.get(object) ?? Object.keys(object);
}

/** The names `object` was written with more than once, in the order the second of each was met; none for an
 * object {@link readJson} did not read. */
export function namesWrittenTwice(object: JsonObject): readonly string[] {
    return writtenTwice.get(object) ?? []; // oxlint-disable-line ex-grid/no-stand-in-default -- an object read with no name written twice is not recorded
}

/**
 * The value of a text JSON.parse reads without complaint, each number kept as it is spelt; a name written
 * twice is the last one written, as JSON.parse and JsonElement both take it. Text that is not JSON is the
 * caller's to refuse first: this reader assumes JSON.parse has read it.
 */
export function readJson(text: string): Json {
    let at = 0;
    const skip = (): void => {
        while (at < text.length && ' \t\n\r'.includes(text.charAt(at))) at++;
    };
    const stringAt = (): string => {
        const start = at;
        at++;
        while (text.charAt(at) !== '"') at += text.charAt(at) === '\\' ? 2 : 1;
        at++;
        const decoded: unknown = JSON.parse(text.slice(start, at));
        if (typeof decoded !== 'string') throw new Error('Internal: a JSON string read as another value.');
        return decoded;
    };
    const valueAt = (): Json => {
        skip();
        const ch = text.charAt(at);
        if (ch === '{') {
            at++;
            const object: { [key: string]: Json } = {};
            const twice: string[] = [];
            const order: string[] = [];
            skip();
            if (text.charAt(at) === '}') {
                at++;
                return object;
            }
            for (;;) {
                skip();
                const name = stringAt();
                skip();
                at++; // the colon
                if (Object.hasOwn(object, name)) twice.push(name);
                else order.push(name);
                Object.defineProperty(object, name, { value: valueAt(), enumerable: true, writable: true, configurable: true });
                skip();
                if (text.charAt(at++) === '}') {
                    if (twice.length > 0) writtenTwice.set(object, Object.freeze(twice));
                    writtenOrder.set(object, Object.freeze(order));
                    return object;
                }
            }
        }
        if (ch === '[') {
            at++;
            const entries: Json[] = [];
            skip();
            if (text.charAt(at) === ']') {
                at++;
                return entries;
            }
            for (;;) {
                entries.push(valueAt());
                skip();
                if (text.charAt(at++) === ']') return entries;
            }
        }
        if (ch === '"') return stringAt();
        for (const [word, value] of [['true', true], ['false', false], ['null', null]] as const) {
            if (text.startsWith(word, at)) {
                at += word.length;
                return value;
            }
        }
        const number = /-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?/uy;
        number.lastIndex = at;
        const spelled = number.exec(text);
        if (spelled === null) throw new Error('Internal: JSON.parse read a value this reader does not.');
        at += spelled[0].length;
        return new JsonNumber(spelled[0]);
    };
    return valueAt();
}

/** Whether a value is an array. */
export function isArray(value: Json | undefined): value is readonly Json[] {
    return Array.isArray(value);
}

/** Whether a value is an object, and not an array, a number or null. */
export function isObject(value: Json | undefined): value is JsonObject {
    return typeof value === 'object' && value !== null && !Array.isArray(value) && !(value instanceof JsonNumber);
}

/** An object's own member, or `undefined` where it has none: `__proto__` is a member like any other. */
export function member(element: JsonObject, name: string): Json | undefined {
    return Object.hasOwn(element, name) ? element[name] : undefined;
}

// ---- writing -------------------------------------------------------------------------------------

// What System.Text.Json's default encoder spells by its code: every character outside printable ASCII, and
// those HTML gives a meaning to.
const shortEscapes: Readonly<Record<string, string>> = Object.freeze({ '\\': '\\\\', '\n': '\\n', '\r': '\\r', '\t': '\\t', '\b': '\\b', '\f': '\\f' });

/** A string as Utf8JsonWriter writes it under the default encoder: `"` as `"`, `<` as `<`, every
 * character past ASCII as its UTF-16 units. */
export function jsonString(text: string): string {
    let written = '"';
    for (let i = 0; i < text.length; i++) {
        const ch = text.charAt(i);
        const code = text.charCodeAt(i);
        // Utf8JsonWriter refuses text that is not UTF-16: a surrogate without its other half.
        const high = code >= 0xd800 && code <= 0xdbff;
        const low = code >= 0xdc00 && code <= 0xdfff;
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

/** A finite double as Utf8JsonWriter writes it: its shortest round-trip text, `1E+300`, `-0`. */
export function jsonDouble(value: number): string {
    if (!Number.isFinite(value)) throw new RangeError(`${value} is not a number JSON writes; it is written by name, as a string.`);
    // Between 10^-4 and 10^15 both platforms write the shortest digits without an exponent, so JavaScript's own
    // text is .NET's; outside it, and for -0, .NET's spelling is made by hand.
    const magnitude = Math.abs(value);
    return magnitude >= 1e-4 && magnitude < 1e15 ? String(value) : doubleRaw(value);
}

/** An object written from members already written, in order, as `{"name":value,…}`. */
export function jsonObject(members: readonly (readonly [string, string])[]): string {
    return `{${members.map(([name, value]) => `${jsonString(name)}:${value}`).join(',')}}`;
}

/** An array written from entries already written. */
export function jsonArray(entries: readonly string[]): string {
    return `[${entries.join(',')}]`;
}
