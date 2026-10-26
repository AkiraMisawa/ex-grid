import type { Evaluator } from './evaluator';
import { MissingNode, type Node } from './nodes';
import { Operand } from './operand';

/**
 * What a function makes of a 3D Reference given as one of its arguments (ADR-0331 §7), as Excel answered for
 * each function ExSheet declares: read as one Value (`#REF!`, or `#VALUE!` handed on); read as one range across
 * its span, Sheet by Sheet in tab order (`SUM` and its kin); handed on as it is (`IF`'s and `CHOOSE`'s values);
 * `#VALUE!` where the function reads it (`IFS`'s and `SWITCH`'s values); or `#VALUE!` before anything of the
 * call is read, where the function asks for a range of one Sheet.
 */
export type ThreeDArgument = 'value' | 'range' | 'handOn' | 'valueError' | 'oneSheet';

/** One of a fixed list of values an argument takes, as completion lists it (ADR-0058): the value, and Excel's text for it. */
export interface ArgumentValue {
    /** What choosing it writes into the Formula: `-1`. */
    readonly value: string;
    /** The text the list shows, Excel's: `-1 - Exact match or next smaller item`. */
    readonly text: string;
}

/** What a function's declaration says beyond its name, arguments and evaluation. */
export interface FunctionTraits {
    /** The arguments that take one of a fixed list of values, by index, each list in Excel's order with Excel's texts (ADR-0058). */
    readonly values?: Readonly<Record<number, readonly ArgumentValue[]>>;
    /** Whether arguments after `pairOffset` come in pairs: an incomplete pair is refused on entry. */
    readonly inPairs?: boolean;
    /** Arguments before the repeating pairs: one for the result range of SUMIFS and its partners. */
    readonly pairOffset?: number;
    /** The arguments, by index from `first` to `last`, that Excel takes only as a Reference (ADR-0047). */
    readonly referenceOnly?: { readonly first: number; readonly last: number };
    /** Whether a call of it can give a Reference (`INDIRECT`, `OFFSET`, `INDEX`, `CHOOSE`, `IF` and their kin). */
    readonly returnsReference?: boolean;
    /** What a 3D Reference given as each argument is to it, by the argument's index and the count of arguments (ADR-0331 §7). */
    readonly threeD?: (index: number, count: number) => ThreeDArgument;
}

/** A function of the declared set (ADR-0047): its name, how many arguments it takes, and how it evaluates. */
export class FunctionDefinition {
    readonly name: string;
    readonly minimumArguments: number;
    /** The most arguments it takes; Excel's limit of 255 for the open-ended ones. */
    readonly maximumArguments: number;
    /** The argument list as a hint shows it, such as `number1, [number2], ...`. */
    readonly arguments: string;
    readonly description: string;
    readonly values: Readonly<Record<number, readonly ArgumentValue[]>>;
    readonly inPairs: boolean;
    readonly pairOffset: number;
    readonly referenceOnly: { readonly first: number; readonly last: number } | undefined;
    readonly returnsReference: boolean;
    readonly threeD: ((index: number, count: number) => ThreeDArgument) | undefined;
    readonly #invoke: (call: FunctionCall) => Operand;

    constructor(name: string, minimum: number, maximum: number, args: string, description: string, invoke: (call: FunctionCall) => Operand,
        traits: FunctionTraits = {}) {
        this.name = name;
        this.minimumArguments = minimum;
        this.maximumArguments = maximum;
        this.arguments = args;
        this.description = description;
        this.#invoke = invoke;
        this.values = traits.values ?? {}; // oxlint-disable-line ex-grid/no-stand-in-default -- a function with no fixed lists of values
        this.inPairs = traits.inPairs === true;
        this.pairOffset = traits.pairOffset ?? 0; // oxlint-disable-line ex-grid/no-stand-in-default -- C#'s init default: no arguments before the pairs
        this.referenceOnly = traits.referenceOnly;
        this.returnsReference = traits.returnsReference === true;
        this.threeD = traits.threeD;
    }

    invoke(call: FunctionCall): Operand {
        return this.#invoke(call);
    }
}

/** One call of a function: its argument nodes, evaluated only when the function asks (so `IF` is lazy, as in Excel). */
export class FunctionCall {
    readonly evaluator: Evaluator;
    readonly #arguments: readonly Node[];
    /** For `RAND` and `RANDBETWEEN`: which of the Formula's draws this call is, counted in its text (ADR-0180). */
    readonly place: number;
    /** What a 3D Reference is to the function called, by argument. */
    readonly threeD: ((index: number, count: number) => ThreeDArgument) | undefined;

    constructor(evaluator: Evaluator, args: readonly Node[], place = 0, threeD?: (index: number, count: number) => ThreeDArgument) {
        this.evaluator = evaluator;
        this.#arguments = args;
        this.place = place;
        this.threeD = threeD;
    }

    get count(): number {
        return this.#arguments.length;
    }

    /**
     * Argument `index`, evaluated. One that holds a kept blank refuses the Formula (`Evaluator.refuse`): what
     * this function reads it as is a reading no run has settled (ADR-0125).
     */
    operand(index: number): Operand {
        const operand = this.#admitted(index, this.evaluator.operand(this.argument(index)));
        return operand.keptBlank ? this.evaluator.refuse() : operand;
    }

    /** Argument `index`, evaluated, a kept blank included (ADR-0125), for the functions that read it the one way. */
    keepingBlanks(index: number): Operand {
        return this.#admitted(index, this.evaluator.operand(this.argument(index)));
    }

    /**
     * What a 3D Reference given as argument `index` is to the function called, as Excel answered (ADR-0331 §7);
     * any other operand as it is. Every door an argument comes in by passes through here.
     */
    #admitted(index: number, operand: Operand): Operand {
        if (operand.kind !== 'threeD') return operand;
        const policy = this.threeD === undefined ? 'value' : this.threeD(index, this.#arguments.length);
        switch (policy) {
            case 'range': return operand;
            case 'handOn': return operand.handOn();
            case 'valueError':
            case 'oneSheet':
                return Operand.ofError('#VALUE!');
            case 'value': {
                const value = this.evaluator.scalarOf(operand);
                if (value === undefined) throw new Error('Internal: a 3D Reference read as one Value is an Error Value.');
                return Operand.of(value);
            }
            default: throw new Error(`Internal: ${String(policy)} is no ThreeDArgument.`);
        }
    }

    /**
     * Argument `index`, evaluated, for a function that reads one Value of it. A kept blank given as that one
     * Value is read as a blank cell's (ADR-0125). An array that holds one refuses the Formula.
     */
    oneValue(index: number): Operand {
        const operand = this.#admitted(index, this.evaluator.operand(this.argument(index)));
        return operand.keptBlank && operand.kind === 'array' ? this.evaluator.refuse() : operand;
    }

    /** The argument's syntax, when a function must distinguish a literal Reference from a computed one. */
    argument(index: number): Node {
        const node = this.#arguments[index];
        if (node === undefined) throw new Error(`Internal: argument ${String(index)} of ${String(this.#arguments.length)} is read.`);
        return node;
    }

    /**
     * The argument evaluated for its extent alone, as `ROWS` and `COLUMNS` read it: no Value of it is read, so a
     * kept blank is counted in the extent, never refused.
     */
    measured(index: number): Operand {
        return this.#admitted(index, this.evaluator.operand(this.argument(index)));
    }

    /** Whether argument `index` was given and not left empty (`MissingNode.isEmpty`). */
    has(index: number): boolean {
        const node = this.#arguments[index];
        return node !== undefined && !MissingNode.isEmpty(node);
    }
}
