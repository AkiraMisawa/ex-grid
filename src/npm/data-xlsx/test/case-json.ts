// What the shared .xlsx cases' runner reads from a case's JSON (tests/cases/README.md). A key a case must write and leaves
// out fails the case; a key the format lets a case leave out takes the value the format names, through `optional`.

/** A value of a case, as JSON holds it. */
export type Json = null | boolean | number | string | readonly Json[] | { readonly [key: string]: Json };
export type JsonObject = { readonly [key: string]: Json };

export function isObject(value: Json | undefined): value is JsonObject {
    return typeof value === 'object' && value !== null && !Array.isArray(value);
}

export function object(value: Json): JsonObject {
    if (!isObject(value)) throw new Error(`Expected an object, not ${JSON.stringify(value)}.`);
    return value;
}

/** The key's value, which the case must write. */
export function key(value: Json, name: string): Json {
    const found = object(value)[name];
    if (found === undefined) throw new Error(`The case leaves out '${name}'.`);
    return found;
}

/** The key's value read, or `otherwise` where the case leaves the key out, as the format allows it to. */
export function optional<T>(value: JsonObject, name: string, read: (value: Json) => T, otherwise: T): T {
    const found = value[name];
    return found === undefined ? otherwise : read(found);
}

/** Fails a case whose object names a key the cases do not. */
export function known(value: JsonObject, ...keys: string[]): void {
    const unknown = Object.keys(value).find(name => !keys.includes(name));
    if (unknown !== undefined) throw new Error(`'${unknown}' is not a key the cases name here.`);
}

export function list(value: Json): readonly Json[] {
    if (!Array.isArray(value)) throw new Error(`Expected a list, not ${JSON.stringify(value)}.`);
    return value as readonly Json[];
}

export function string(value: Json): string {
    if (typeof value !== 'string') throw new Error(`Expected a string, not ${JSON.stringify(value)}.`);
    return value;
}

export function number(value: Json): number {
    if (typeof value !== 'number') throw new Error(`Expected a number, not ${JSON.stringify(value)}.`);
    return value;
}

export function flag(value: Json): boolean {
    if (typeof value !== 'boolean') throw new Error(`Expected a boolean, not ${JSON.stringify(value)}.`);
    return value;
}

/** `\uXXXX` escapes undone. */
function utf16(escaped: string): string {
    return escaped.replace(/\\u([0-9a-fA-F]{4})/g, (_, digits: string) => String.fromCharCode(Number.parseInt(digits, 16)));
}

const main = 'http://schemas.openxmlformats.org/spreadsheetml/2006/main';
const rel = 'http://schemas.openxmlformats.org/officeDocument/2006/relationships';

/**
 * Text as a case writes it: a string, `{"utf16": …}` with `\uXXXX` escapes for text JSON cannot hold, or a list of
 * pieces, each one of those or `{"each": …, "from": m, "to": n}`, text written once for each number from `m` to `n` with
 * `{i}` standing for it. `{Main}` and `{Rel}` stand for SpreadsheetML's two namespaces.
 */
export function text(value: Json): string {
    let written: string;
    if (typeof value === 'string') written = value;
    else if (Array.isArray(value)) written = (value as readonly Json[]).map(piece).join('');
    else if (isObject(value) && value['utf16'] !== undefined) written = utf16(string(value['utf16']));
    else throw new Error(`${JSON.stringify(value)} is not text.`);
    return written.replaceAll('{Main}', main).replaceAll('{Rel}', rel);
}

function piece(value: Json): string {
    if (isObject(value) && value['each'] !== undefined) {
        const once = string(value['each']);
        const pieces: string[] = [];
        for (let i = number(key(value, 'from')); i <= number(key(value, 'to')); i++) pieces.push(once.replaceAll('{i}', String(i)));
        return pieces.join('');
    }
    return text(value);
}

/** A value a case writes as `null` where the package answers `undefined` (tests/cases/README.md). */
export function orNull<T>(value: T | undefined): T | null {
    return value === undefined ? null : value;
}

export function hex(argb: number): string {
    return (argb >>> 0).toString(16).toUpperCase().padStart(8, '0');
}

export function parseHex(value: Json): number {
    return Number.parseInt(string(value), 16) >>> 0;
}
