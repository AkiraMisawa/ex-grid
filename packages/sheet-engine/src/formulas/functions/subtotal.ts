// SUBTOTAL and AGGREGATE (C#'s Functions.Subtotal.cs; ADR-0047's note of 2026-10-10), admitted while a Sheet
// cannot hide a row (ADR-0046): the function numbers and options that pass over hidden rows give what the
// others give, as they do in Excel over such a sheet. A cell is nested when its Formula, or for a cell of a
// Spill Range its Anchor's, calls SUBTOTAL or AGGREGATE anywhere in its text, as the seventeenth Windows run
// read Excel's answers (SH-68): SUBTOTAL and AGGREGATE's options 0 to 3 pass over it, and AGGREGATE's options
// 4 to 7 count its Value.
import { Value } from '../../value';
import type { FunctionCall } from '../function-definition';
import type { Node } from '../nodes';
import { Operand } from '../operand';
import { averageOf, maxOf, minOf, sumOf } from './core';
import { kthOf, medianOf, productOf, spreadOf } from './math';
import { numberArgument } from './shared';

/**
 * Whether a cell holding `formula` is a nested SUBTOTAL or AGGREGATE: its Formula calls either anywhere in its
 * text — the whole Formula, inside a larger expression, in parentheses, under `IF` — as Excel counts one
 * (SUBTOTAL-020, 021, 035 to 038, AGGREGATE-026, 052 to 054). `false` for a constant or a blank.
 */
function isNested(formula: Node | undefined): boolean {
    if (formula === undefined) return false;
    for (const call of formula.calls()) {
        if (call.function !== undefined && (call.name === 'SUBTOTAL' || call.name === 'AGGREGATE')) return true;
    }
    return false;
}

/**
 * SUBTOTAL: function numbers 1 to 11 — AVERAGE, COUNT, COUNTA, MAX, MIN, PRODUCT, STDEV.S, STDEV.P, SUM, VAR.S,
 * VAR.P — over its references, each function as it reads a range; 101 to 111 give the same while no row can be
 * hidden. A function number that is not whole is truncated toward zero, as Excel's is (SUBTOTAL-027: 9.5 is 9).
 * A nested cell (`isNested`) is passed over. `#VALUE!`, as Excel's was for 12 and 0 (SUBTOTAL-025, 026):
 * another function number; and an argument that is no reference when it is computed.
 */
export function subtotal(call: FunctionCall): Operand {
    const number = truncatedArgument(call, 0);
    if (number instanceof Operand) return number;
    // No row can be hidden yet (ADR-0046), so 101 to 111 skip nothing 1 to 11 do not.
    const fn = number >= 101 && number <= 111 ? number - 100 : number;
    if (fn < 1 || fn > 11) return Operand.ofError('#VALUE!');
    const values: Value[] = [];
    for (let i = 1; i < call.count; i++) {
        const failure = collect(call.operand(i), false, true, false, values);
        if (failure !== undefined) return failure;
    }
    return reduce(fn, values, undefined);
}

/**
 * AGGREGATE: function numbers 1 to 12 over references (the reference form) and 14 and 15 — LARGE and SMALL —
 * over an array and its k (the array form), with options 0 to 7. Options 0 to 3 pass over a nested cell
 * (`isNested`), and options 4 to 7 count its Value, as Excel does (AGGREGATE-025, 042 to 051); options 2, 3, 6
 * and 7 pass over Error Values; options 1, 3, 5 and 7, which pass over hidden rows, give what 0, 2, 4 and 6
 * give, since no row can be hidden. A function number or option that is not whole is truncated toward zero, as
 * SUBTOTAL's is. The array form takes a Value given directly as an array of that one Value: a number is its one
 * number (AGGREGATE-039), and text or a boolean leaves no number, so `#NUM!` (AGGREGATE-061). Its k is read as
 * LARGE's and SMALL's (`kthOf`). Refused (`Evaluator.refuse`): the function numbers of functions the engine does
 * not support (13, MODE.SNGL; 16 to 19, PERCENTILE and QUARTILE), where Excel answers (AGGREGATE-027 to 031).
 * `#VALUE!`, as Excel's: a number or option outside Excel's, LARGE or SMALL without its k or with an argument
 * after it, and an argument of the reference form that is no reference when it is computed.
 */
export function aggregate(call: FunctionCall): Operand {
    const fn = truncatedArgument(call, 0);
    if (fn instanceof Operand) return fn;
    let option = 0;
    if (call.has(1)) {
        const given = truncatedArgument(call, 1);
        if (given instanceof Operand) return given;
        option = given;
    }
    if (fn < 1 || fn > 19 || option < 0 || option > 7) return Operand.ofError('#VALUE!');
    // MODE.SNGL, PERCENTILE.INC, QUARTILE.INC, PERCENTILE.EXC and QUARTILE.EXC are not declared: refused.
    if (fn === 13 || fn > 15) return call.evaluator.refuse();
    const skipNested = option <= 3;
    const passOverErrors = option === 2 || option === 3 || option === 6 || option === 7;
    const values: Value[] = [];
    if (fn <= 12) {
        for (let i = 2; i < call.count; i++) {
            const failure = collect(call.operand(i), false, skipNested, passOverErrors, values);
            if (failure !== undefined) return failure;
        }
        return reduce(fn, values, undefined);
    }

    // The array form: the array, and k.
    if (call.count !== 4) return Operand.ofError('#VALUE!');
    const failure = collect(call.operand(2), true, skipNested, passOverErrors, values);
    if (failure !== undefined) return failure;
    const error = values.find(v => v.isError);
    if (error !== undefined) return Operand.of(error);
    const k = numberArgument(call, 3);
    if (k instanceof Operand) return k;
    return reduce(fn, values, k);
}

/**
 * Argument `index` truncated toward zero to a whole number, as Excel reads SUBTOTAL's function number
 * (SUBTOTAL-027) and, by the same rule, AGGREGATE's function number and option; one past what a 32-bit integer
 * holds is refused with `#VALUE!`, outside every range either takes.
 */
function truncatedArgument(call: FunctionCall, index: number): number | Operand {
    const number = numberArgument(call, index);
    if (number instanceof Operand) return number;
    const truncated = Math.trunc(number);
    if (truncated < -2147483648 || truncated > 2147483647) return Operand.ofError('#VALUE!');
    return truncated === 0 ? 0 : truncated;
}

/**
 * The Values of one argument that SUBTOTAL or AGGREGATE reduces, added in order to `values`: a reference's cells
 * that are not blank, a Linked Table's column, or, where `arrays` allows it, a computed array or a number, text
 * or boolean given directly, which is an array of that one Value (`reduce` counts only its numbers). A nested
 * cell (`isNested`) is passed over when `skipNested`, and its Value counted otherwise; an Error Value is passed
 * over when `passOverErrors`. The failure when the argument is refused, or is an Error Value itself.
 */
function collect(operand: Operand, arrays: boolean, skipNested: boolean, passOverErrors: boolean, values: Value[]): Operand | undefined {
    const add = (value: Value): void => {
        if (!(value.isError && passOverErrors)) values.push(value);
    };
    switch (operand.kind) {
        case 'area': {
            const cells = operand.cellsOf();
            for (const address of cells.nonBlankIn(operand.area)) {
                if (skipNested && isNested(cells.formulaAt(address))) continue;
                const value = cells.read(address);
                if (value === undefined) throw new Error(`Internal: ${address.toString()} was listed as not blank.`);
                add(value);
            }
            return undefined;
        }
        case 'column':
            for (const value of operand.columnOf()) if (value !== undefined) add(value);
            return undefined;
        case 'array':
            if (!arrays) break;
            for (const value of operand.arrayOf().nonBlank()) add(value);
            return undefined;
        case 'scalar': {
            // A Value given directly is an array of that one Value: a number is taken (AGGREGATE-039), and
            // text or a boolean is no number, not coerced, so LARGE and SMALL pass over it (AGGREGATE-061:
            // AGGREGATE(14,6,"5",1) is #NUM!, not 5 and not #VALUE!).
            const given = operand.scalar;
            if (arrays && given !== undefined && !given.isError) {
                add(given);
                return undefined;
            }
            break;
        }
        case 'missing':
        case 'threeD':
            break;
        default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
    }
    return operand.isError ? operand : Operand.ofError('#VALUE!');
}

/**
 * The function a SUBTOTAL or AGGREGATE number names, over `values`, each as it reads a range: only numbers
 * count, COUNT passes over an Error Value and COUNTA counts it, and for the others the first Error Value is the
 * result.
 */
function reduce(fn: number, values: readonly Value[], k: number | undefined): Operand {
    if (fn === 2) return Operand.of(Value.fromNumber(values.filter(v => v.kind === 'number').length));
    if (fn === 3) return Operand.of(Value.fromNumber(values.length));
    const numbers: number[] = [];
    for (const value of values) {
        if (value.isError) return Operand.of(value);
        if (value.kind === 'number') numbers.push(value.number);
    }
    switch (fn) {
        case 1: return averageOf(numbers);
        case 4: return maxOf(numbers);
        case 5: return minOf(numbers);
        case 6: return productOf(numbers);
        case 7: return spreadOf(numbers, true, true);
        case 8: return spreadOf(numbers, false, true);
        case 9: return sumOf(numbers);
        case 10: return spreadOf(numbers, true, false);
        case 11: return spreadOf(numbers, false, false);
        case 12: return medianOf(numbers);
        case 14: return kthOf(numbers, kOf(k), true);
        case 15: return kthOf(numbers, kOf(k), false);
        default: throw new Error(`Internal: ${String(fn)} is no function number.`);
    }
}

function kOf(k: number | undefined): number {
    if (k === undefined) throw new Error('Internal: LARGE and SMALL are reduced with their k.');
    return k;
}
