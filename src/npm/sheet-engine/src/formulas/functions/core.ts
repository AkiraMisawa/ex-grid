// SUM, AVERAGE, MIN, MAX, COUNT, COUNTA, IF, IFERROR, ISERROR and ROUND (C#'s Functions.cs).
import { Value } from '../../value';
import { finalAdd } from '../arithmetic';
import type { FunctionCall } from '../function-definition';
import { Operand } from '../operand';
import { roundAt } from '../rounding';
import { booleanResult, collectNumbers, isArray, numberArgument, numberResult, readsAsNumber, truthOf, unlifted } from './shared';

/**
 * SUM of the numbers taken (`collectNumbers`). A kept blank (`Operand.keptBlank`) is passed over, as over a
 * blank cell: where it would be the last number, reading it as 0 would make another last addition, and Excel's
 * `=SUM(SORT(A1:A4))` over 0.1, 0.2, -0.3 and a blank is 0, the last addition of 0.2 cancelling, not 2.78E-17
 * (SORT-037, run 18a).
 */
export function sum(call: FunctionCall): Operand {
    const numbers: number[] = [];
    const error = collectNumbers(call, numbers);
    return error !== undefined ? Operand.ofError(error) : sumOf(numbers);
}

/**
 * SUM of the numbers taken. Its last addition cancels as a Formula's final addition does (ADR-0047, second run:
 * `=SUM(0.1,0.2,-0.3)` is 0), wherever SUM stands in the Formula.
 */
export function sumOf(numbers: readonly number[]): Operand {
    let total = 0;
    for (let i = 0; i < numbers.length; i++) {
        const n = numbers[i] as number; // oxlint-disable-line typescript/no-unsafe-type-assertion -- i is within the list
        total = i === numbers.length - 1 ? finalAdd(total, n) : total + n;
    }
    return numberResult(total);
}

export function average(call: FunctionCall): Operand {
    const numbers: number[] = [];
    const error = collectNumbers(call, numbers);
    return error !== undefined ? Operand.ofError(error) : averageOf(numbers);
}

/** AVERAGE of the numbers taken; with none, `#DIV/0!`. */
export function averageOf(numbers: readonly number[]): Operand {
    if (numbers.length === 0) return Operand.ofError('#DIV/0!');
    let total = 0;
    for (const n of numbers) total += n;
    return numberResult(total / numbers.length);
}

export function min(call: FunctionCall): Operand {
    const numbers: number[] = [];
    const error = collectNumbers(call, numbers);
    return error !== undefined ? Operand.ofError(error) : minOf(numbers);
}

/** MIN of the numbers taken; with none, 0. */
export function minOf(numbers: readonly number[]): Operand {
    let least = numbers.length === 0 ? 0 : Infinity;
    for (const n of numbers) least = Math.min(least, n);
    return Operand.of(Value.fromNumber(least));
}

export function max(call: FunctionCall): Operand {
    const numbers: number[] = [];
    const error = collectNumbers(call, numbers);
    return error !== undefined ? Operand.ofError(error) : maxOf(numbers);
}

/** MAX of the numbers taken; with none, 0. */
export function maxOf(numbers: readonly number[]): Operand {
    let greatest = numbers.length === 0 ? 0 : -Infinity;
    for (const n of numbers) greatest = Math.max(greatest, n);
    return Operand.of(Value.fromNumber(greatest));
}

/**
 * Inside a range, only numbers are counted. Typed directly, numbers, booleans and text that reads as a number
 * are counted; Error Values and other text are not, and are not errors. A kept blank (`Operand.keptBlank`) is
 * no number, and is not counted, as Excel's `COUNT` did not count `SORT`'s (SORT-034, run 18a).
 */
export function count(call: FunctionCall): Operand {
    const evaluator = call.evaluator;
    let counted = 0;
    for (let i = 0; i < call.count; i++) {
        const operand = call.keepingBlanks(i);
        switch (operand.kind) {
            case 'area':
            case 'column':
            case 'array':
            case 'threeD':
                for (const value of evaluator.rangeValues(operand)) {
                    if (value.kind === 'number') counted++;
                }
                break;
            case 'missing':
                counted++;
                break;
            case 'scalar': {
                const scalar = operand.scalar;
                if (scalar === undefined) break;
                if (scalar.kind === 'number' || scalar.kind === 'boolean') counted++;
                else if (scalar.kind === 'text' && readsAsNumber(scalar.text, evaluator)) counted++;
                break;
            }
            default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
        }
    }
    return Operand.of(Value.fromNumber(counted));
}

/**
 * Counts every value that is not blank: Error Values and empty text included. A kept blank in its array
 * (`Operand.keptBlank`) is counted, as Excel counted `SORT`'s and `UNIQUE`'s (SORT-021, UNIQUE-017; ADR-0125),
 * and `SORTBY`'s, `FILTER`'s and `TRANSPOSE`'s are counted as those are; and given as one Value it is counted
 * too, as Excel counted the one `INDEX` took out of `SORT`'s array and `UNIQUE`'s of one blank cell (SORT-035,
 * UNIQUE-023, run 18a).
 */
export function countA(call: FunctionCall): Operand {
    const evaluator = call.evaluator;
    let counted = 0;
    for (let i = 0; i < call.count; i++) {
        const operand = call.keepingBlanks(i);
        if (operand.keptBlank && operand.kind !== 'array') {
            counted++;
            continue;
        }
        switch (operand.kind) {
            case 'array':
                if (operand.keptBlank) {
                    const array = operand.arrayOf();
                    counted += array.rows * array.columns;
                    break;
                }
                for (const _ of evaluator.rangeValues(operand)) counted++;
                break;
            case 'area':
            case 'column':
            case 'threeD':
                for (const _ of evaluator.rangeValues(operand)) counted++;
                break;
            case 'missing':
                counted++;
                break;
            case 'scalar':
                if (operand.scalar !== undefined) counted++;
                break;
            default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
        }
    }
    return Operand.of(Value.fromNumber(counted));
}

export function ifFunction(call: FunctionCall): Operand {
    const test = call.operand(0);
    if (isArray(test)) return unlifted(call);
    const condition = call.evaluator.scalarOf(test);
    let truth = false;
    if (condition !== undefined) {
        const read = truthOf(condition);
        if (read === undefined) return Operand.ofError(condition.isError ? condition.error : '#VALUE!');
        truth = read;
    }
    // The branch taken is handed on as it is, a kept blank still marked: Excel's ISBLANK read SORT's kept blank
    // through IF as blank (SORT-039, run 18a).
    if (truth) return call.has(1) ? call.keepingBlanks(1) : Operand.of(Value.fromNumber(0));
    if (call.count < 3) return booleanResult(false);
    return call.has(2) ? call.keepingBlanks(2) : Operand.of(Value.fromNumber(0));
}

/**
 * An empty cell as either argument gives an empty value: a Formula whose result it is shows 0, as Excel was
 * observed to show, and joined to text it is "" (IFERROR-013). A value_if_error left out is empty too, not 0
 * (IFERROR-016). An Error Value is caught — but not `#GETTING_DATA`, which waits (ADR-0049), nor `#CIRC!`,
 * which a Formula reading a cycle shows whatever it computes (ADR-0047).
 */
export function ifError(call: FunctionCall): Operand {
    const operand = call.operand(0);
    if (isArray(operand)) return unlifted(call);
    const value = call.evaluator.scalarOf(operand);
    if (value?.isError === true && value.error !== '#GETTING_DATA' && value.error !== '#CIRC!') return fallbackOf(call);
    return Operand.ofScalar(value);
}

/** IFERROR's and IFNA's fallback: blank when left out, a 3D Reference handed on, as Excel's fallback was (ADR-0331 §7). */
export function fallbackOf(call: FunctionCall): Operand {
    if (!call.has(1)) return Operand.blank;
    const fallback = call.operand(1);
    if (fallback.kind === 'threeD') return fallback;
    if (isArray(fallback)) return unlifted(call);
    return Operand.ofScalar(call.evaluator.scalarOf(fallback));
}

export function isError(call: FunctionCall): Operand {
    const operand = call.oneValue(0);
    if (isArray(operand)) return unlifted(call);
    const value = call.evaluator.scalarOf(operand);
    if (value?.isError === true && (value.error === '#GETTING_DATA' || value.error === '#CIRC!')) return Operand.of(value);
    return booleanResult(value?.isError === true);
}

export function round(call: FunctionCall): Operand {
    const evaluator = call.evaluator;
    const first = call.operand(0);
    const second = call.operand(1);
    if (isArray(first) || isArray(second)) return unlifted(call);
    const number = evaluator.toNumber(evaluator.scalarOf(first));
    if (typeof number !== 'number') return Operand.ofError(number);
    const digits = evaluator.toNumber(evaluator.scalarOf(second));
    if (typeof digits !== 'number') return Operand.ofError(digits);
    return numberResult(roundAt(number, Math.trunc(digits), 'halfAwayFromZero'));
}

/** ROUNDUP and ROUNDDOWN. */
export function roundBy(call: FunctionCall, rounding: 'awayFromZero' | 'towardZero'): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    const digits = numberArgument(call, 1);
    if (digits instanceof Operand) return digits;
    return numberResult(roundAt(number, Math.trunc(digits), rounding));
}
