import { equalsIgnoreCase, formatRoundTrip } from './dotnet';

/**
 * An Error Value, written as a cell shows it: Excel's set, `#GETTING_DATA` with the stricter meaning
 * ADR-0049 gives it, and `#CIRC!`, the one Error Value ExSheet adds (ADR-0047). Error Values are data; they
 * propagate through every Formula that uses them.
 */
export type ErrorValue =
    | '#NULL!' | '#DIV/0!' | '#VALUE!' | '#REF!' | '#NAME?' | '#NUM!' | '#N/A' | '#GETTING_DATA' | '#CIRC!'
    | '#SPILL!' | '#CALC!' | '#FIELD!' | '#BLOCKED!' | '#CONNECT!' | '#UNKNOWN!';

/** Every Error Value, in the C# enum's order, which the lexer reads Excel's first seven in. */
export const errorValues: readonly ErrorValue[] = [
    '#NULL!', '#DIV/0!', '#VALUE!', '#REF!', '#NAME?', '#NUM!', '#N/A', '#GETTING_DATA', '#CIRC!',
    '#SPILL!', '#CALC!', '#FIELD!', '#BLOCKED!', '#CONNECT!', '#UNKNOWN!',
];

/**
 * Reads the text of one of Excel's Error Values, in either case, as Excel reads one typed with real keys:
 * `#spill!` is `#SPILL!`. `#GETTING_DATA` and `#CIRC!` are not read: they describe the state of a
 * computation, and a user cannot type one in (ADR-0047, ADR-0049). `#BUSY!` is not one: Excel keeps it as text.
 */
export function parseTypedError(text: string): ErrorValue | undefined {
    return readable(text, true);
}

/** Reads the text of an Error Value a cell can hold exactly as Excel writes it in a file, case and all. */
export function parseWrittenError(text: string): ErrorValue | undefined {
    return readable(text, false);
}

function readable(text: string, ignoreCase: boolean): ErrorValue | undefined {
    for (const error of errorValues) {
        if (error === '#GETTING_DATA' || error === '#CIRC!') continue;
        if (ignoreCase ? equalsIgnoreCase(error, text) : error === text) return error;
    }
    return undefined;
}

/** The four kinds of Value, as in Excel (ADR-0047). A blank cell is not a fifth kind: it has no Value. */
export type ValueKind = 'number' | 'text' | 'boolean' | 'error';

/**
 * What a cell evaluates to: a number, text, a boolean or an Error Value (CONTEXT.md). A constant Entry is
 * its own Value; a Formula's Value is its result. Values are never recorded in a Sheet Document (ADR-0048).
 * A blank is no Value: where one may stand, the engine says `undefined`.
 */
export class Value {
    readonly #kind: ValueKind;
    readonly #number: number;
    readonly #text: string | undefined;
    readonly #error: ErrorValue | undefined;
    readonly #refused: boolean;

    private constructor(kind: ValueKind, number: number, text: string | undefined, error: ErrorValue | undefined, refused: boolean) {
        this.#kind = kind;
        this.#number = number;
        this.#text = text;
        this.#error = error;
        this.#refused = refused;
    }

    /** A number. NaN and the infinities are not numbers a cell can hold; Excel's answer to them is `#NUM!`. */
    static fromNumber(number: number): Value {
        if (!Number.isFinite(number)) throw new RangeError(`A Value is a finite number; Excel's answer to ${String(number)} is #NUM!.`);
        return new Value('number', number === 0 ? 0 : number, undefined, undefined, false);
    }

    /** Text, possibly empty. */
    static fromText(text: string): Value {
        return new Value('text', 0, text, undefined, false);
    }

    /** `TRUE` or `FALSE`. */
    static fromBoolean(value: boolean): Value {
        return value ? Value.#true : Value.#false;
    }

    /** An Error Value. */
    static fromError(error: ErrorValue): Value {
        return Value.#errors.get(error) ?? new Value('error', 0, undefined, error, false);
    }

    static readonly #true = new Value('boolean', 1, undefined, undefined, false);
    static readonly #false = new Value('boolean', 0, undefined, undefined, false);
    static readonly #errors = new Map<ErrorValue, Value>(errorValues.map(e => [e, new Value('error', 0, undefined, e, false)]));

    /**
     * A refusal (ADR-0047; 2026-10-12, decided with the user: a refusal is never caught, anywhere): the Error
     * Value `#VALUE!` where ExSheet cannot answer as Excel. It is `#VALUE!` to everything outside the engine,
     * and the engine carries it as `#CIRC!` is carried. Internal to the engine.
     */
    static readonly refusal: Value = new Value('error', 0, undefined, '#VALUE!', true);

    /** A refusal shown as `#NAME?`: a call of a function Excel has and ExSheet does not declare, or a declared Linked Table's bare name. Internal. */
    static readonly refusedName: Value = new Value('error', 0, undefined, '#NAME?', true);

    /** Which of the four kinds this Value is. */
    get kind(): ValueKind {
        return this.#kind;
    }

    /**
     * Whether this Error Value is a Refused Formula's: `#VALUE!`, or `#NAME?` for a function Excel has and
     * ExSheet does not declare, which no Formula can catch, in its own cell or in any cell that reads it
     * (ADR-0047). It is equal to the Error Value it shows.
     */
    get isRefused(): boolean {
        return this.#refused;
    }

    /** Whether this Value is an Error Value. */
    get isError(): boolean {
        return this.#kind === 'error';
    }

    /** The number; throws unless the kind is `number`. */
    get number(): number {
        if (this.#kind !== 'number') throw this.#wrong('number');
        return this.#number;
    }

    /** The text; throws unless the kind is `text`. */
    get text(): string {
        if (this.#text === undefined) throw this.#wrong('text');
        return this.#text;
    }

    /** The boolean; throws unless the kind is `boolean`. */
    get boolean(): boolean {
        if (this.#kind !== 'boolean') throw this.#wrong('boolean');
        return this.#number !== 0;
    }

    /** The Error Value; throws unless the kind is `error`. */
    get error(): ErrorValue {
        if (this.#error === undefined) throw this.#wrong('error');
        return this.#error;
    }

    #wrong(asked: ValueKind): Error {
        return new Error(`This Value is ${this.#kind}, not ${asked}.`);
    }

    /**
     * Exact equality: the same kind and the same number, text (ordinal, case-sensitive), boolean or Error
     * Value. This is "did the Value change", not Excel's `=`, which ignores case. A refusal equals the Error
     * Value it shows.
     */
    equals(other: Value): boolean {
        return this.#kind === other.#kind && this.#number === other.#number && this.#text === other.#text && this.#error === other.#error;
    }

    /**
     * Whether a cell's Value did not change: equal and both refused or neither, or both absent. A `#VALUE!`
     * that turns from Excel's own into a refusal changes what every Formula reading the cell answers.
     */
    static unchanged(before: Value | undefined, after: Value | undefined): boolean {
        if (before === undefined || after === undefined) return before === after;
        return before.equals(after) && before.#refused === after.#refused;
    }

    /** Whether two Values that may be blank are equal: both blank, or equal (`Nullable.Equals`). */
    static same(a: Value | undefined, b: Value | undefined): boolean {
        if (a === undefined || b === undefined) return a === b;
        return a.equals(b);
    }

    /** A diagnostic form, culture-invariant: a number round-trips, text is as held. What a cell shows is the Sheet's display, not this. */
    toString(): string {
        switch (this.#kind) {
            case 'number': return formatRoundTrip(this.#number);
            case 'text': return this.text;
            case 'boolean': return this.#number !== 0 ? 'TRUE' : 'FALSE';
            case 'error': return this.error;
            default: return unreachableKind(this.#kind);
        }
    }
}

function unreachableKind(kind: never): never {
    throw new Error(`Internal: ${String(kind)} is no kind of Value.`);
}
