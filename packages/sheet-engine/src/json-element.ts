// The JSON a Sheet Document and a Workbook Document are written in (ADR-0048), read and written as C#'s
// System.Text.Json does, so a document written by either engine reads back in the other unchanged. JSON.parse
// will not do: it keeps the last of two properties of one name, where JsonDocument keeps both and the reader
// refuses a cell holding two Entries, and it reads `9.0` as a version, which TryGetInt32 does not.
import { formatRoundTrip, hex } from './dotnet';

/** A JSON value as JsonDocument holds it: an object's properties in order, two of one name both kept, and a number's text. @internal */
export type JsonElement =
    | { readonly kind: 'object'; readonly properties: readonly (readonly [string, JsonElement])[]; readonly raw: string }
    | { readonly kind: 'array'; readonly items: readonly JsonElement[]; readonly raw: string }
    | { readonly kind: 'string'; readonly value: string }
    | { readonly kind: 'number'; readonly raw: string }
    | { readonly kind: 'true' }
    | { readonly kind: 'false' }
    | { readonly kind: 'null' };

/** JsonDocument's default deepest nesting. */
const maxDepth = 64;

/** Text that is not JSON as JsonDocument reads it. @internal */
export class JsonSyntaxError extends Error {
    constructor(message: string) {
        super(message);
        this.name = 'JsonSyntaxError';
    }
}

/** Parses `text` as JsonDocument.Parse does with its default options: no comments, no trailing commas, at most 64 deep. @internal */
export function parseJson(text: string): JsonElement {
    let at = 0;
    const fail = (what: string): never => {
        throw new JsonSyntaxError(`${what} at position ${String(at)}.`);
    };
    const skip = (): void => {
        while (at < text.length) {
            const c = text.charCodeAt(at);
            if (c !== 0x20 && c !== 0x09 && c !== 0x0a && c !== 0x0d) return;
            at++;
        }
    };
    const literal = (word: string): void => {
        if (!text.startsWith(word, at)) fail('An unexpected character');
        at += word.length;
    };
    const string = (): string => {
        at++;
        let out = '';
        for (;;) {
            if (at >= text.length) fail('An unterminated string');
            const c = text.charCodeAt(at);
            if (c === 0x22) {
                at++;
                return out;
            }
            if (c < 0x20) fail('A control character in a string');
            if (c !== 0x5c) {
                out += text[at];
                at++;
                continue;
            }
            const e = text[at + 1];
            at += 2;
            switch (e) {
                case '"': out += '"'; break;
                case '\\': out += '\\'; break;
                case '/': out += '/'; break;
                case 'b': out += '\b'; break;
                case 'f': out += '\f'; break;
                case 'n': out += '\n'; break;
                case 'r': out += '\r'; break;
                case 't': out += '\t'; break;
                case 'u': {
                    const digits = text.slice(at, at + 4);
                    if (!/^[0-9A-Fa-f]{4}$/u.test(digits)) fail('An invalid \\u escape');
                    out += String.fromCharCode(parseInt(digits, 16));
                    at += 4;
                    break;
                }
                case undefined:
                default: fail('An invalid escape');
            }
        }
    };
    const value = (depth: number): JsonElement => {
        skip();
        const start = at;
        const c = text[at];
        switch (c) {
            case '{': {
                if (depth >= maxDepth) fail('Nesting deeper than 64');
                at++;
                const properties: (readonly [string, JsonElement])[] = [];
                skip();
                if (text[at] === '}') {
                    at++;
                    return { kind: 'object', properties, raw: text.slice(start, at) };
                }
                for (;;) {
                    skip();
                    if (text[at] !== '"') fail('A property name expected');
                    const name = string();
                    skip();
                    if (text[at] !== ':') fail('\':\' expected');
                    at++;
                    properties.push([name, value(depth + 1)]);
                    skip();
                    if (text[at] === ',') {
                        at++;
                        continue;
                    }
                    if (text[at] === '}') {
                        at++;
                        return { kind: 'object', properties, raw: text.slice(start, at) };
                    }
                    fail('\',\' or \'}\' expected');
                }
            }
            case '[': {
                if (depth >= maxDepth) fail('Nesting deeper than 64');
                at++;
                const items: JsonElement[] = [];
                skip();
                if (text[at] === ']') {
                    at++;
                    return { kind: 'array', items, raw: text.slice(start, at) };
                }
                for (;;) {
                    items.push(value(depth + 1));
                    skip();
                    if (text[at] === ',') {
                        at++;
                        continue;
                    }
                    if (text[at] === ']') {
                        at++;
                        return { kind: 'array', items, raw: text.slice(start, at) };
                    }
                    fail('\',\' or \']\' expected');
                }
            }
            case '"':
                return { kind: 'string', value: string() };
            case 't':
                literal('true');
                return { kind: 'true' };
            case 'f':
                literal('false');
                return { kind: 'false' };
            case 'n':
                literal('null');
                return { kind: 'null' };
            case undefined:
                return fail('An unexpected end');
            default: {
                const match = /^-?(?:0|[1-9][0-9]*)(?:\.[0-9]+)?(?:[eE][+-]?[0-9]+)?/u.exec(text.slice(at));
                if (match === null) return fail('An unexpected character');
                at += match[0].length;
                return { kind: 'number', raw: match[0] };
            }
        }
    };
    const root = value(0);
    skip();
    if (at < text.length) fail('Text after the JSON value');
    return root;
}

/** `JsonElement.TryGetInt32`: a whole number written without a fraction or an exponent, inside Int32. @internal */
export function jsonInt32(element: JsonElement): number | undefined {
    if (element.kind !== 'number' || !/^-?[0-9]+$/u.test(element.raw)) return undefined;
    const n = Number(element.raw);
    return n >= -2147483648 && n <= 2147483647 ? n : undefined;
}

/** `JsonElement.TryGetDouble`: the number, or `undefined` beyond a double's range, which TryGetDouble refuses. @internal */
export function jsonDouble(element: JsonElement): number | undefined {
    if (element.kind !== 'number') return undefined;
    const n = Number(element.raw);
    return Number.isFinite(n) ? n : undefined;
}

/** `JsonElement.ToString()`, as a refusal's message quotes a value: a string's text, a number's or a container's JSON, `True` or `False`. @internal */
export function jsonText(element: JsonElement): string {
    switch (element.kind) {
        case 'object':
        case 'array':
        case 'number':
            return element.raw;
        case 'string':
            return element.value;
        case 'true':
            return 'True';
        case 'false':
            return 'False';
        case 'null':
            return '';
        default:
            throw new Error(`Internal: ${String(element satisfies never)} is not a JSON element.`);
    }
}

/** The characters Utf8JsonWriter's default encoder writes as they are: printable ASCII but for these. */
const escapedAscii = new Set(['"', '&', '\'', '+', '<', '>', '\\', '`']);

/** A string as Utf8JsonWriter writes it with its default encoder: everything outside printable ASCII, and HTML's specials, escaped. */
function writeString(text: string): string {
    let out = '"';
    for (let i = 0; i < text.length; i++) {
        const ch = text.charAt(i);
        const c = text.charCodeAt(i);
        if (c >= 0x20 && c < 0x7f && !escapedAscii.has(ch)) {
            out += ch;
            continue;
        }
        switch (ch) {
            case '\\': out += '\\\\'; break;
            case '\b': out += '\\b'; break;
            case '\f': out += '\\f'; break;
            case '\n': out += '\\n'; break;
            case '\r': out += '\\r'; break;
            case '\t': out += '\\t'; break;
            default: out += `\\u${hex(c, 4)}`;
        }
    }
    return `${out}"`;
}

/** Writes JSON as Utf8JsonWriter does without indenting: properties in the order written, numbers as .NET writes a double. @internal */
export class JsonWriter {
    #out = '';
    /** Whether the container open now has had a value yet, one entry per open container. */
    readonly #started: boolean[] = [];

    #separate(): void {
        const last = this.#started.length - 1;
        if (last < 0) return;
        if (this.#started[last] === true) this.#out += ',';
        this.#started[last] = true;
    }

    #name(name: string | undefined): void {
        this.#separate();
        if (name !== undefined) this.#out += `${writeString(name)}:`;
    }

    startObject(name?: string): void {
        this.#name(name);
        this.#out += '{';
        this.#started.push(false);
    }

    endObject(): void {
        this.#started.pop();
        this.#out += '}';
    }

    startArray(name?: string): void {
        this.#name(name);
        this.#out += '[';
        this.#started.push(false);
    }

    endArray(): void {
        this.#started.pop();
        this.#out += ']';
    }

    string(name: string | undefined, value: string): void {
        this.#name(name);
        this.#out += writeString(value);
    }

    number(name: string | undefined, value: number): void {
        if (!Number.isFinite(value)) throw new RangeError(`${String(value)} cannot be written as JSON.`);
        this.#name(name);
        this.#out += formatRoundTrip(value);
    }

    boolean(name: string | undefined, value: boolean): void {
        this.#name(name);
        this.#out += value ? 'true' : 'false';
    }

    toString(): string {
        return this.#out;
    }
}
