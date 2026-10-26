// What the functions of the declared set share (C#'s FunctionLibrary partials Functions.cs and
// Functions.Arguments.cs): how an argument that takes one Value is read, how the aggregates take their
// numbers, and the refusal of an array where one Value is wanted.
import { parseNumber } from '../../constant-parser';
import { equalsIgnoreCase } from '../../dotnet';
import { Value, type ErrorValue } from '../../value';
import type { Evaluator } from '../evaluator';
import type { FunctionCall } from '../function-definition';
import { Operand } from '../operand';

/** A function of the declared set, as a declaration names it. */
export type FunctionBody = (call: FunctionCall) => Operand;

/**
 * Whether an argument where one Value is wanted holds several: a range of several cells, a Linked Table's
 * column of several, an array, or a 3D Reference that reached a function reading it across its span
 * (`TEXTJOIN`'s delimiter, which Excel cycles through, as through a range's). Excel 365 applies the function
 * to each Value and spills the results; the engine does not yet (ADR-0125, "Not yet: functions lifted over
 * arrays"), so the function refuses the Formula (`unlifted`).
 */
export function isArray(operand: Operand): boolean {
    return (operand.kind === 'area' && !operand.area.isSingleCell)
        || (operand.kind === 'column' && operand.columnOf().length !== 1)
        || operand.kind === 'array' || operand.kind === 'threeD';
}

/**
 * The refusal of an array where a function wants one Value (`isArray`): `#VALUE!`, and the Formula's
 * (`Evaluator.refuse`), as a Formula that meets `#CIRC!` is `#CIRC!` (ADR-0047). ADR-0125 keeps it "an Error
 * Value, never another answer", and IFERROR-009 records that `IFERROR` does not turn it into a fallback.
 */
export function unlifted(call: FunctionCall): Operand {
    return call.evaluator.refuse();
}

/**
 * Argument `index` as one Value, blank as `undefined`; an `Operand` (the failure) when it is an Error Value, or
 * the Formula's refusal when it is a range of several cells or an array (`unlifted`). A kept blank given as one
 * Value is read as a blank cell's (`FunctionCall.oneValue`; ADR-0125).
 */
export function scalarArgument(call: FunctionCall, index: number): Value | undefined | Operand {
    const operand = call.oneValue(index);
    if (isArray(operand)) return unlifted(call);
    const value = call.evaluator.scalarOf(operand);
    if (value?.isError === true) return Operand.of(value);
    return value;
}

/** Argument `index` coerced to a number as the operators coerce it: a blank is 0, a boolean 1 or 0, text that reads as a number is that number; or the failure. */
export function numberArgument(call: FunctionCall, index: number): number | Operand {
    const value = scalarArgument(call, index);
    if (value instanceof Operand) return value;
    return numberOrFailure(call.evaluator.toNumber(value));
}

/** Argument `index` coerced to text as `&` coerces it; or the failure. */
export function textArgument(call: FunctionCall, index: number): string | Operand {
    const value = scalarArgument(call, index);
    if (value instanceof Operand) return value;
    return call.evaluator.toText(value);
}

/** Argument `index` when it is given (`FunctionCall.has`), as a number; `fallback` when it is left out. */
export function optionalNumber(call: FunctionCall, index: number, fallback: number): number | Operand {
    return call.has(index) ? numberArgument(call, index) : fallback;
}

/** A coercion's number, or its Error Value as the failure. */
export function numberOrFailure(number: number | ErrorValue): number | Operand {
    return typeof number === 'number' ? number : Operand.ofError(number);
}

/** One number as a Value; past what a double holds, `#NUM!` (C#'s `Operand.Of(Evaluator.Number(x))`). */
export function numberResult(number: number): Operand {
    return Operand.of(Number.isFinite(number) ? Value.fromNumber(number) : Value.fromError('#NUM!'));
}

/** A boolean as a Value. */
export function booleanResult(value: boolean): Operand {
    return Operand.of(Value.fromBoolean(value));
}

/**
 * A Value read as a logical one, as `IF`'s test and `NOT` read it: a number is TRUE unless it is 0, and text is
 * TRUE or FALSE only when it spells one, without regard to case. `undefined` for any other text and for an
 * Error Value.
 */
export function truthOf(value: Value): boolean | undefined {
    switch (value.kind) {
        case 'number': return value.number !== 0;
        case 'boolean': return value.boolean;
        case 'text':
            if (equalsIgnoreCase(value.text, 'TRUE')) return true;
            if (equalsIgnoreCase(value.text, 'FALSE')) return false;
            return undefined;
        case 'error': return undefined;
        default: throw new Error(`Internal: ${String(value.kind)} is no kind of Value.`);
    }
}

/**
 * The numbers the aggregates take, per Microsoft's documentation: inside a range or Reference only numbers
 * count, and blanks, text and booleans are ignored; typed directly as arguments, booleans and text that reads
 * as a number count too, and other text is `#VALUE!`. An Error Value anywhere is the result, the first one
 * found left to right. A kept blank (`Operand.keptBlank`) is passed over as a blank cell is: Excel's `AVERAGE`
 * and `MIN` passed over the one `FILTER`, `TRANSPOSE` and `UNIQUE` kept (FILTER-014, TRANSPOSE-007,
 * UNIQUE-021, 022, run 18a), and `MAX`, `PRODUCT`, `MEDIAN` and the spreads that take their numbers here pass
 * over it too, by generalisation.
 */
export function collectNumbers(call: FunctionCall, numbers: number[], from = 0): ErrorValue | undefined {
    for (let i = from; i < call.count; i++) {
        const error = addNumbers(call.evaluator, call.keepingBlanks(i), numbers, false);
        if (error !== undefined) return error;
    }
    return undefined;
}

/**
 * The numbers one argument gives (`collectNumbers`), or its first Error Value. A kept blank
 * (`Operand.keptBlank`) is passed over as a blank cell is, or, with `keptBlankAsZero`, read as 0 where it
 * stands (ADR-0125).
 */
export function addNumbers(evaluator: Evaluator, operand: Operand, numbers: number[], keptBlankAsZero: boolean): ErrorValue | undefined {
    switch (operand.kind) {
        case 'array':
            if (operand.keptBlank && keptBlankAsZero) {
                for (const item of operand.arrayOf().all()) {
                    if (item === undefined) numbers.push(0);
                    else if (item.kind === 'number') numbers.push(item.number);
                    else if (item.isError) return item.error;
                }
                return undefined;
            }
            return addRange(evaluator, operand, numbers);
        case 'area':
        case 'column':
        case 'threeD':
            return addRange(evaluator, operand, numbers);
        case 'missing':
            numbers.push(0);
            return undefined;
        case 'scalar': {
            const scalar = operand.scalar;
            if (scalar === undefined) {
                if (!operand.keptBlank || keptBlankAsZero) numbers.push(0);
                return undefined;
            }
            const number = evaluator.toNumber(scalar);
            if (typeof number !== 'number') return number;
            numbers.push(number);
            return undefined;
        }
        default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
    }
}

function addRange(evaluator: Evaluator, operand: Operand, numbers: number[]): ErrorValue | undefined {
    for (const value of evaluator.rangeValues(operand)) {
        if (value.kind === 'number') numbers.push(value.number);
        else if (value.isError) return value.error;
    }
    return undefined;
}

/** Whether text reads as a number under the culture, as `COUNT` counts typed text. */
export function readsAsNumber(text: string, evaluator: Evaluator): boolean {
    return parseNumber(text, evaluator.culture) !== undefined;
}

/**
 * An argument that takes an array, where a computation gave one Value that is not an Error Value: that Value as
 * an array of one, as Excel's array of one row. A blank argument, a Reference or a column stays as it is.
 */
export function arrayOrValue(operand: Operand): Operand {
    return operand.kind === 'scalar' && operand.scalar !== undefined && !operand.scalar.isError ? Operand.asArray(operand.scalar) : operand;
}

/** The numbers sorted ascending, as C#'s `List<double>.Sort` sorts finite doubles. */
export function sortNumbers(numbers: number[]): void {
    numbers.sort((a, b) => a - b);
}

/** The Value of an Error Value. */
export function errorResult(error: ErrorValue): Operand {
    return Operand.ofError(error);
}
