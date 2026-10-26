// AND, OR, NOT, IFNA, ISBLANK, ISNUMBER, ISTEXT, ISNA, NA, ERROR.TYPE, IFS, SWITCH and XOR, each to Microsoft's
// documentation of it (C#'s Functions.Logic.cs).
import { Value } from '../../value';
import { approximatelyEqual } from '../arithmetic';
import { compareValues } from '../evaluator';
import type { FunctionCall } from '../function-definition';
import { Operand } from '../operand';
import { fallbackOf } from './core';
import { booleanResult, isArray, scalarArgument, truthOf, unlifted } from './shared';

export function and(call: FunctionCall): Operand {
    return logical(call, true);
}

export function or(call: FunctionCall): Operand {
    return logical(call, false);
}

/** AND and OR, over the logical values `readLogicals` reads. With nothing that counts, the result is `#VALUE!`. */
function logical(call: FunctionCall, all: boolean): Operand {
    let result: boolean | undefined;
    const failure = readLogicals(call, truth => {
        result = all ? (result === undefined || result) && truth : (result !== undefined && result) || truth;
    });
    if (failure !== undefined) return failure;
    return result === undefined ? Operand.ofError('#VALUE!') : booleanResult(result);
}

/**
 * The logical values AND, OR and XOR take, left to right, each handed to `take`. Inside a range or a Reference,
 * numbers and booleans count, a number being TRUE unless it is 0, and text and blanks are passed over. Typed as
 * an argument, a number or a boolean counts, and so does text that spells TRUE or FALSE; other text is passed
 * over, as text inside a range is (AND-011: Excel's `AND("abc",TRUE)` is TRUE). An argument left empty is
 * FALSE. An Error Value anywhere is the failure, the first one found left to right; no value decides the result
 * early, so an error after a FALSE is still the result.
 */
function readLogicals(call: FunctionCall, take: (truth: boolean) => void): Operand | undefined {
    const evaluator = call.evaluator;
    for (let i = 0; i < call.count; i++) {
        const operand = call.operand(i);
        switch (operand.kind) {
            case 'area':
            case 'column':
            case 'array':
            case 'threeD':
                for (const value of evaluator.rangeValues(operand)) {
                    if (value.isError) return Operand.of(value);
                    if (value.kind === 'number') take(value.number !== 0);
                    else if (value.kind === 'boolean') take(value.boolean);
                }
                break;
            case 'missing':
                take(false);
                break;
            case 'scalar': {
                const scalar = operand.scalar;
                if (scalar === undefined) break;
                if (scalar.isError) return Operand.of(scalar);
                const truth = truthOf(scalar);
                if (truth !== undefined) take(truth);
                break;
            }
            default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
        }
    }
    return undefined;
}

/** NOT: a blank is FALSE, so its NOT is TRUE; otherwise the test `IF` applies. */
export function not(call: FunctionCall): Operand {
    const value = scalarArgument(call, 0);
    if (value instanceof Operand) return value;
    if (value === undefined) return booleanResult(true);
    const truth = truthOf(value);
    return truth === undefined ? Operand.ofError('#VALUE!') : booleanResult(!truth);
}

/** IFNA: as `IFERROR`, for `#N/A` alone. So it catches neither `#GETTING_DATA` (ADR-0049) nor `#CIRC!` (ADR-0047). */
export function ifNa(call: FunctionCall): Operand {
    const operand = call.operand(0);
    if (isArray(operand)) return unlifted(call);
    const value = call.evaluator.scalarOf(operand);
    if (value?.isError === true && value.error === '#N/A') return fallbackOf(call);
    return Operand.ofScalar(value);
}

/** ISBLANK: TRUE for an empty cell only; a Formula that returns `""` is not blank. */
export function isBlank(call: FunctionCall): Operand {
    return inspect(call, value => value === undefined);
}

/** ISNUMBER: TRUE for a number only; text that reads as one is not a number. */
export function isNumber(call: FunctionCall): Operand {
    return inspect(call, value => value?.kind === 'number');
}

/**
 * An IS function: it answers TRUE or FALSE for any one Value, an Error Value included, and a kept blank as a
 * blank cell's, as Excel's `ISBLANK` read `SORT`'s (SORT-022) — but `#GETTING_DATA` and `#CIRC!` are its result,
 * as `ISERROR`'s (ADR-0047, ADR-0049): FALSE would be an answer about data that has not arrived, or about a
 * cycle.
 */
function inspect(call: FunctionCall, test: (value: Value | undefined) => boolean): Operand {
    const operand = call.oneValue(0);
    if (isArray(operand)) return unlifted(call);
    const value = call.evaluator.scalarOf(operand);
    if (value?.isError === true && (value.error === '#GETTING_DATA' || value.error === '#CIRC!')) return Operand.of(value);
    return booleanResult(test(value));
}

/** ISTEXT: TRUE for text only, empty text included; a number is not text. */
export function isText(call: FunctionCall): Operand {
    return inspect(call, value => value?.kind === 'text');
}

/** ISNA: TRUE for `#N/A` only. */
export function isNa(call: FunctionCall): Operand {
    return inspect(call, value => value?.isError === true && value.error === '#N/A');
}

/**
 * LET is read into a `LetNode` of its own, with its names in scope (ADR-0181), and never called as a function:
 * declared so that completion, the argument hint and the catalogue know it.
 */
export function letFunction(): Operand {
    throw new Error('Internal: LET is evaluated as a LetNode, never as a call.');
}

/** NA: the Error Value `#N/A`. */
export function na(): Operand {
    return Operand.ofError('#N/A');
}

/**
 * ERROR.TYPE (ADR-0047's note of 2026-10-10): Microsoft's number for an Error Value — `#NULL!` 1, `#DIV/0!` 2,
 * `#VALUE!` 3, `#REF!` 4, `#NAME?` 5, `#NUM!` 6, `#N/A` 7 — and `#N/A` for a Value that is no error. Excel's
 * newer Error Values take the numbers Excel gave them (ERRORTYPE-013 to 019): `#SPILL!` 9, `#CONNECT!` 10,
 * `#BLOCKED!` 11, `#UNKNOWN!` 12, `#FIELD!` 13, `#CALC!` 14. `#GETTING_DATA` and `#CIRC!` are given back, never
 * numbered (ADR-0049).
 */
export function errorType(call: FunctionCall): Operand {
    const operand = call.operand(0);
    if (isArray(operand)) return unlifted(call);
    const value = call.evaluator.scalarOf(operand);
    if (value?.isError !== true) return Operand.ofError('#N/A');
    switch (value.error) {
        case '#NULL!': return Operand.of(Value.fromNumber(1));
        case '#DIV/0!': return Operand.of(Value.fromNumber(2));
        case '#VALUE!': return Operand.of(Value.fromNumber(3));
        case '#REF!': return Operand.of(Value.fromNumber(4));
        case '#NAME?': return Operand.of(Value.fromNumber(5));
        case '#NUM!': return Operand.of(Value.fromNumber(6));
        case '#N/A': return Operand.of(Value.fromNumber(7));
        case '#SPILL!': return Operand.of(Value.fromNumber(9));
        case '#CONNECT!': return Operand.of(Value.fromNumber(10));
        case '#BLOCKED!': return Operand.of(Value.fromNumber(11));
        case '#UNKNOWN!': return Operand.of(Value.fromNumber(12));
        case '#FIELD!': return Operand.of(Value.fromNumber(13));
        case '#CALC!': return Operand.of(Value.fromNumber(14));
        case '#GETTING_DATA':
        case '#CIRC!':
            return Operand.of(value);
        default: return Operand.ofError('#VALUE!');
    }
}

/**
 * IFS: the value beside the first test that is TRUE, each test read as `IF` reads its own; a test's Error Value
 * is the result; with none TRUE, `#N/A`. Only the tests up to the first TRUE, and its value, are evaluated.
 */
export function ifs(call: FunctionCall): Operand {
    for (let i = 0; i + 1 < call.count; i += 2) {
        const test = call.operand(i);
        if (isArray(test)) return unlifted(call);
        const value = call.evaluator.scalarOf(test);
        let truth = false;
        if (value !== undefined) {
            const read = truthOf(value);
            if (read === undefined) return Operand.ofError(value.isError ? value.error : '#VALUE!');
            truth = read;
        }
        if (truth) return call.has(i + 1) ? call.operand(i + 1) : Operand.of(Value.fromNumber(0));
    }
    return Operand.ofError('#N/A');
}

/**
 * SWITCH: the result beside the first value equal to the expression, compared as `=` compares; with none, the
 * default when one is given, else `#N/A`. An Error Value in the expression, or in a value reached before a
 * match, is the result.
 */
export function switchFunction(call: FunctionCall): Operand {
    const expression = scalarArgument(call, 0);
    if (expression instanceof Operand) return expression;
    const pairs = Math.trunc((call.count - 1) / 2);
    for (let p = 0; p < pairs; p++) {
        const at = 1 + 2 * p;
        const candidate = scalarArgument(call, at);
        if (candidate instanceof Operand) return candidate;
        if (compareValues(expression, candidate, approximatelyEqual) === 0) {
            return call.has(at + 1) ? call.operand(at + 1) : Operand.of(Value.fromNumber(0));
        }
    }
    const hasDefault = (call.count - 1) % 2 === 1;
    return hasDefault ? call.operand(call.count - 1) : Operand.ofError('#N/A');
}

/** XOR: TRUE when an odd number of the logical values AND and OR take are TRUE; with none, `#VALUE!`. */
export function xor(call: FunctionCall): Operand {
    const read = { any: false, odd: false };
    const failure = readLogicals(call, truth => {
        read.any = true;
        if (truth) read.odd = !read.odd;
    });
    if (failure !== undefined) return failure;
    return read.any ? booleanResult(read.odd) : Operand.ofError('#VALUE!');
}
