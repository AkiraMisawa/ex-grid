// JSON as System.Text.Json reads it, for the family's readers of a document a person or an AI wrote: a Board
// (ADR-0340), a grid's View State (ADR-0207), a Chart Spec (ADR-0320). JSON.parse loses what those readers
// refuse by: a number's spelling, from which C# reads an integer (1.0 is not one), and a member written twice.
// (The pivot engine's reader of its own documents is the same reading, kept in its package for now.)

/** A number as the document spells it: C# reads an integer from its spelling, so 1.0 is not one. */
export class JsonNumber {
    /** The number as the document spells it. */
    readonly raw: string;

    /** A number spelt `raw`, which is JSON's spelling of a number. */
    constructor(raw: string) {
        this.raw = raw;
    }

    /** The spelling's value as a 32-bit integer, or `undefined` where it has a fraction, an exponent or a value
     * outside 32 bits, as `JsonElement.TryGetInt32` refuses it. */
    get int32(): number | undefined {
        const value = this.#whole();
        return value !== undefined && value >= -(2 ** 31) && value < 2 ** 31 ? value : undefined;
    }

    /** The spelling's value as a whole number a double holds exactly, or `undefined` where it has a fraction or an
     * exponent, as `JsonElement.TryGetInt64` refuses it, or lies past 2^53. */
    get integer(): number | undefined {
        const value = this.#whole();
        return value !== undefined && Number.isSafeInteger(value) ? value : undefined;
    }

    /** The spelling's value as a double, which is infinite where the spelling is past a double's range, as
     * `JsonElement.TryGetDouble` answers it on .NET Core 3.0 and later. */
    get double(): number {
        return Number(this.raw);
    }

    #whole(): number | undefined {
        if (!/^-?(?:0|[1-9]\d*)$/u.test(this.raw)) return undefined;
        return Number(this.raw) + 0;
    }
}

/** A JSON value, each number kept as it is spelt. */
export type JsonValue = null | boolean | JsonNumber | string | readonly JsonValue[] | JsonObject;

/** A JSON object. Its members are read with {@link jsonMember} and walked with {@link jsonMemberNames}. */
export type JsonObject = { readonly [key: string]: JsonValue };

/** Text that is not JSON, or a JSON object that writes a member twice, which System.Text.Json refuses under
 * `AllowDuplicateProperties = false`. Its message says where, in the platform's words. */
export class JsonTextError extends Error {
    /** Text refused, said by `message`. */
    constructor(message: string, options?: ErrorOptions) {
        super(message, options);
        this.name = 'JsonTextError';
    }
}

// The names of an object in the order the document wrote them, which an object's own keys do not keep where a
// name is a whole number.
const writtenOrder = new WeakMap<JsonObject, readonly string[]>();

/** The names `object` was written with, in the order the document wrote them, as System.Text.Json enumerates
 * an object; an object {@link readJsonText} did not read gives its own keys. */
export function jsonMemberNames(object: JsonObject): readonly string[] {
    return writtenOrder.get(object) ?? Object.keys(object);
}

/** An object's own member, or `undefined` where it has none: `__proto__` is a member like any other. */
export function jsonMember(object: JsonObject, name: string): JsonValue | undefined {
    return Object.hasOwn(object, name) ? object[name] : undefined;
}

function memberOf(object: JsonObject, name: string): JsonValue {
    const value = jsonMember(object, name);
    if (value === undefined) throw new Error(`Internal: member '${name}' is named and missing.`);
    return value;
}

/** Whether a value is an array. */
export function isJsonArray(value: JsonValue | undefined): value is readonly JsonValue[] {
    return Array.isArray(value);
}

/** Whether a value is an object, and not an array, a number or null. */
export function isJsonObject(value: JsonValue | undefined): value is JsonObject {
    return typeof value === 'object' && value !== null && !Array.isArray(value) && !(value instanceof JsonNumber);
}

/** A text that reads back as `value`, its numbers as spelt and no space between tokens: the value written
 * again, where `JsonElement.GetRawText` would keep the document's own spaces and escapes. */
export function jsonRawText(value: JsonValue): string {
    if (value === null) return 'null';
    if (typeof value === 'boolean') return value ? 'true' : 'false';
    if (typeof value === 'string') return JSON.stringify(value);
    if (value instanceof JsonNumber) return value.raw;
    if (isJsonArray(value)) return `[${value.map(jsonRawText).join(',')}]`;
    return `{${jsonMemberNames(value).map(name => `${JSON.stringify(name)}:${jsonRawText(memberOf(value, name))}`).join(',')}}`;
}

/**
 * Reads `text` as System.Text.Json reads it with duplicate members refused: text that is not JSON, and an
 * object that writes a member twice, throw a {@link JsonTextError}; each number is kept as it is spelt.
 */
export function readJsonText(text: string): JsonValue {
    try {
        JSON.parse(text);
    } catch (error) {
        throw new JsonTextError(error instanceof Error ? error.message : String(error), { cause: error });
    }
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
    const valueAt = (): JsonValue => {
        skip();
        const ch = text.charAt(at);
        if (ch === '{') {
            at++;
            const object: { [key: string]: JsonValue } = {};
            const order: string[] = [];
            skip();
            if (text.charAt(at) === '}') {
                at++;
                writtenOrder.set(object, Object.freeze(order));
                return Object.freeze(object);
            }
            for (;;) {
                skip();
                const name = stringAt();
                skip();
                at++; // the colon
                if (Object.hasOwn(object, name)) throw new JsonTextError(`The member '${name}' is written twice in one object.`);
                order.push(name);
                Object.defineProperty(object, name, { value: valueAt(), enumerable: true, writable: false, configurable: false });
                skip();
                if (text.charAt(at++) === '}') {
                    writtenOrder.set(object, Object.freeze(order));
                    return Object.freeze(object);
                }
            }
        }
        if (ch === '[') {
            at++;
            const entries: JsonValue[] = [];
            skip();
            if (text.charAt(at) === ']') {
                at++;
                return Object.freeze(entries);
            }
            for (;;) {
                entries.push(valueAt());
                skip();
                if (text.charAt(at++) === ']') return Object.freeze(entries);
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
