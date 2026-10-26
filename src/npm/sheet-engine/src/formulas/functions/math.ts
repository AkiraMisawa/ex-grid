// ROUNDUP, ROUNDDOWN, ABS, INT, MOD, PRODUCT, MEDIAN, LARGE, SMALL, TRUNC, POWER, SQRT, RANK.EQ, the spreads,
// SIGN, EXP, LN, LOG10, PI (C#'s Functions.Math.cs); MROUND, CEILING.MATH and FLOOR.MATH
// (Functions.ObservedRounding.cs); RAND and RANDBETWEEN (Functions.Random.cs).
import { bitDecrement, bitIncrement, lowestBitClear, roundHalfAwayFromZero, roundHalfToEven } from '../../dotnet';
import { Value } from '../../value';
import { approximatelyEqual, atFifteenDigits, power as powerOf } from '../arithmetic';
import { between, unitOf } from '../draws';
import type { FunctionCall } from '../function-definition';
import { Operand } from '../operand';
import { roundAt } from '../rounding';
import { collectNumbers, numberArgument, numberOrFailure, numberResult, scalarArgument, sortNumbers } from './shared';

/**
 * The quotient at and above which MOD is `#NUM!`. Excel 365 answers `MOD(2^27,1)` (MOD-008); a Microsoft 365
 * user reported that `MOD(1125899999999,1)` is 0 and `MOD(1125900000000,1)` is `#NUM!`, and the area pass of
 * 2026-10-12 read Excel on both sides of it (MOD-010 to MOD-016).
 */
const modQuotientLimit = 1_125_900_000_000;

/** ABS. */
export function abs(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    return number instanceof Operand ? number : Operand.of(Value.fromNumber(Math.abs(number)));
}

/** INT rounds down, toward negative infinity: INT(-8.9) is -9. */
export function int(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    return number instanceof Operand ? number : Operand.of(Value.fromNumber(Math.floor(number)));
}

/**
 * MOD: `number - divisor*INT(number/divisor)`, as Microsoft defines it, so the result takes the divisor's sign,
 * computed as the one evaluation at the hardware's widths (a double's 53 bits, an x87 register's 64) that gives
 * every answer Excel was seen to give (ADR-0047, ADR-0202). The remainder is exact; where it has the number's
 * sign, the divisor is added as `turnedRemainder` says. A divisor of 0 is `#DIV/0!`, and a quotient of
 * `modQuotientLimit` or more `#NUM!`.
 */
export function mod(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    const divisor = numberArgument(call, 1);
    if (divisor instanceof Operand) return divisor;
    if (divisor === 0) return Operand.ofError('#DIV/0!');
    if (Math.abs(number / divisor) >= modQuotientLimit) return Operand.ofError('#NUM!');
    // % on doubles is exact, and carries the number's sign.
    const remainder = number % divisor;
    if (remainder === 0 || (remainder < 0) === (divisor < 0)) return numberResult(remainder);
    return numberResult(turnedRemainder(remainder, divisor));
}

/**
 * `remainder` + `divisor`, where the remainder, exact, has the sign opposite the divisor's, as Excel adds it:
 * held at 64 bits, rounded to nearest as an x87 register adds, then stored as a double, rounded to nearest; and
 * 0 where the double stored is the divisor itself. The exact sum is the sum rounded once and its error
 * (Fast2Sum); the register's last place is 2^-11 of the double's between the sum and its neighbour on the
 * error's side, so the 64-bit sum is the sum plus a whole number of those steps, and a sum 1024 steps away is a
 * tie, which the double stored breaks to the even neighbour.
 */
function turnedRemainder(remainder: number, divisor: number): number {
    // MOD(-n,-d) is -MOD(n,d): work with a positive divisor.
    const sign = divisor < 0 ? -1 : 1;
    const d = sign * divisor;
    const r = sign * remainder;
    const sum = r + d;
    const error = r - (sum - d);
    const gap = error < 0 ? sum - bitDecrement(sum) : bitIncrement(sum) - sum;
    const steps = roundHalfToEven(error / gap * 2048);
    const stored = Math.abs(steps) < 1024 || lowestBitClear(sum) ? sum
        : steps > 0 ? bitIncrement(sum) : bitDecrement(sum);
    return sign * (stored === d ? 0 : stored);
}

/** PRODUCT: the numbers SUM takes, multiplied; with none, 0, as Excel's is. */
export function product(call: FunctionCall): Operand {
    const numbers: number[] = [];
    const error = collectNumbers(call, numbers);
    return error !== undefined ? Operand.ofError(error) : productOf(numbers);
}

/** PRODUCT of the numbers taken; with none, 0. */
export function productOf(numbers: readonly number[]): Operand {
    if (numbers.length === 0) return Operand.of(Value.fromNumber(0));
    let result = 1;
    for (const n of numbers) result *= n;
    return numberResult(result);
}

/** MEDIAN: the middle of the numbers SUM takes, or the mean of the middle two; with none, `#NUM!`. */
export function median(call: FunctionCall): Operand {
    const numbers: number[] = [];
    const error = collectNumbers(call, numbers);
    return error !== undefined ? Operand.ofError(error) : medianOf(numbers);
}

/** MEDIAN of the numbers taken, which it sorts; with none, `#NUM!`. */
export function medianOf(numbers: number[]): Operand {
    if (numbers.length === 0) return Operand.ofError('#NUM!');
    sortNumbers(numbers);
    const middle = Math.trunc(numbers.length / 2);
    const upper = numbers[middle] as number; // oxlint-disable-line typescript/no-unsafe-type-assertion -- the middle of a list of one or more
    return numberResult(numbers.length % 2 === 1 ? upper : ((numbers[middle - 1] as number) + upper) / 2); // oxlint-disable-line typescript/no-unsafe-type-assertion -- an even count has two middles
}

export function large(call: FunctionCall): Operand {
    return ranked(call, true);
}

export function small(call: FunctionCall): Operand {
    return ranked(call, false);
}

/**
 * LARGE and SMALL: the numbers of the array (a range's numbers only; a typed value coerced), and the k-th of
 * them (`kthOf`). A k below 1, or above their count, is `#NUM!`.
 */
function ranked(call: FunctionCall, largest: boolean): Operand {
    const numbers: number[] = [];
    const array = call.operand(0);
    if (array.isRange) {
        for (const value of call.evaluator.rangeValues(array)) {
            if (value.isError) return Operand.of(value);
            if (value.kind === 'number') numbers.push(value.number);
        }
    } else {
        const single = numberArgument(call, 0);
        if (single instanceof Operand) return single;
        numbers.push(single);
    }
    const k = numberArgument(call, 1);
    if (k instanceof Operand) return k;
    return kthOf(numbers, k, largest);
}

/**
 * The k-th largest or smallest of the numbers taken, which it sorts, as Excel reads k. k is read at Excel's 15
 * significant digits. A k outside 1 to the count of the numbers is `#NUM!`, whole or not, before it is rounded
 * (LARGE-011, LARGE-013, SMALL-007, SMALL-008). A k within it that is not whole is rounded up for the k-th
 * largest (LARGE-007 to 009) and truncated for the k-th smallest (SMALL-004, 005, 009).
 */
export function kthOf(numbers: number[], kAsGiven: number, largest: boolean): Operand {
    let k = atFifteenDigits(kAsGiven);
    if (k < 1 || k > numbers.length) return Operand.ofError('#NUM!');
    k = largest ? Math.ceil(k) : Math.floor(k);
    sortNumbers(numbers);
    const at = k - 1;
    const picked = numbers[largest ? numbers.length - 1 - at : at] as number; // oxlint-disable-line typescript/no-unsafe-type-assertion -- k is within 1 to the count
    return Operand.of(Value.fromNumber(picked));
}

/** TRUNC: toward zero at `num_digits`, 0 when left out — ROUNDDOWN with an optional second argument. */
export function trunc(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    let digits = 0;
    if (call.has(1)) {
        const given = numberArgument(call, 1);
        if (given instanceof Operand) return given;
        digits = given;
    }
    return numberResult(roundAt(number, Math.trunc(digits), 'towardZero'));
}

/** POWER: the `^` operator's answer, its Error Values included (0^0 is `#NUM!`, 0 to a negative power `#DIV/0!`). */
export function power(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    const exponent = numberArgument(call, 1);
    if (exponent instanceof Operand) return exponent;
    if (number === 0 && exponent === 0) return Operand.ofError('#NUM!');
    if (number === 0 && exponent < 0) return Operand.ofError('#DIV/0!');
    const result = powerOf(number, exponent);
    return result === undefined ? Operand.ofError('#NUM!') : numberResult(result);
}

/** SQRT: a negative number is `#NUM!`. */
export function sqrt(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    return number < 0 ? Operand.ofError('#NUM!') : Operand.of(Value.fromNumber(Math.sqrt(number)));
}

/**
 * RANK.EQ: the number's rank among the numbers of `ref`, from the largest when `order` is 0 or left out and from
 * the smallest otherwise; equal numbers share the top rank. A number not among them is `#N/A`, and a `ref` that
 * is no range is `#VALUE!`.
 */
export function rankEq(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    const reference = call.operand(1);
    if (!reference.isRange) return reference.isError ? reference : Operand.ofError('#VALUE!');
    let order = 0;
    if (call.has(2)) {
        const given = numberArgument(call, 2);
        if (given instanceof Operand) return given;
        order = given;
    }
    let found = false;
    let ahead = 0;
    for (const value of call.evaluator.rangeValues(reference)) {
        if (value.isError) return Operand.of(value);
        if (value.kind !== 'number') continue;
        if (value.number === number) found = true;
        else if (order === 0 ? value.number > number : value.number < number) ahead++;
    }
    return found ? Operand.of(Value.fromNumber(ahead + 1)) : Operand.ofError('#N/A');
}

export function stdevS(call: FunctionCall): Operand {
    return spread(call, true, true);
}

export function stdevP(call: FunctionCall): Operand {
    return spread(call, false, true);
}

export function varS(call: FunctionCall): Operand {
    return spread(call, true, false);
}

export function varP(call: FunctionCall): Operand {
    return spread(call, false, false);
}

/**
 * VAR.S, VAR.P and their square roots STDEV.S and STDEV.P, over the numbers SUM takes, the squared deviations
 * taken from their mean. Fewer than two numbers for a sample, or none for a population, is `#DIV/0!`.
 */
function spread(call: FunctionCall, sample: boolean, root: boolean): Operand {
    const numbers: number[] = [];
    const error = collectNumbers(call, numbers);
    return error !== undefined ? Operand.ofError(error) : spreadOf(numbers, sample, root);
}

/** VAR.S, VAR.P, STDEV.S or STDEV.P of the numbers taken (`spread`). */
export function spreadOf(numbers: readonly number[], sample: boolean, root: boolean): Operand {
    const divisor = sample ? numbers.length - 1 : numbers.length;
    if (divisor < 1) return Operand.ofError('#DIV/0!');
    let total = 0;
    for (const n of numbers) total += n;
    const mean = total / numbers.length;
    let squares = 0;
    for (const n of numbers) squares += (n - mean) * (n - mean);
    const variance = squares / divisor;
    return numberResult(root ? Math.sqrt(variance) : variance);
}

export function signFunction(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    return number instanceof Operand ? number : Operand.of(Value.fromNumber(Math.sign(number)));
}

/** EXP: e to the number; past what a double holds, `#NUM!`. */
export function exp(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    return number instanceof Operand ? number : numberResult(Math.exp(number));
}

/** LN: a number not above 0 is `#NUM!`. */
export function ln(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    return number <= 0 ? Operand.ofError('#NUM!') : Operand.of(Value.fromNumber(Math.log(number)));
}

/** LOG10: a number not above 0 is `#NUM!`. */
export function log10(call: FunctionCall): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    return number <= 0 ? Operand.ofError('#NUM!') : Operand.of(Value.fromNumber(Math.log10(number)));
}

export function pi(): Operand {
    return Operand.of(Value.fromNumber(Math.PI));
}

/**
 * MROUND for whole-number multiples. Decimal multiples are refused: the Windows run's 6.05, 7.05, 0.15 and 0.35
 * midpoints cannot all be reproduced by one ordinary binary rounding operation, and Microsoft's MROUND
 * documentation leaves their direction undefined. ADR-0047 permits this refusal, never an invented epsilon.
 */
export function mRound(call: FunctionCall): Operand {
    const number = mroundNumber(call, 0);
    if (number instanceof Operand) return number;
    const multiple = mroundNumber(call, 1);
    if (multiple instanceof Operand) return multiple;
    if (number === 0 || multiple === 0) return Operand.of(Value.fromNumber(0));
    if (Math.sign(number) !== Math.sign(multiple)) return Operand.ofError('#NUM!');
    if (multiple !== Math.trunc(multiple)) return call.evaluator.refuse();
    return numberResult(roundHalfAwayFromZero(number / multiple) * multiple);
}

function mroundNumber(call: FunctionCall, index: number): number | Operand {
    const value = scalarArgument(call, index);
    if (value instanceof Operand) return value;
    if (value?.kind === 'boolean') return Operand.ofError('#VALUE!');
    return numberArgument(call, index);
}

export function ceilingMath(call: FunctionCall): Operand {
    return directionalMultiple(call, true);
}

export function floorMath(call: FunctionCall): Operand {
    return directionalMultiple(call, false);
}

function directionalMultiple(call: FunctionCall, ceilingAsked: boolean): Operand {
    const number = numberArgument(call, 0);
    if (number instanceof Operand) return number;
    let significance = 1;
    if (call.has(1)) {
        const given = numberArgument(call, 1);
        if (given instanceof Operand) return given;
        significance = given;
    }
    let mode = 0;
    if (call.has(2)) {
        const given = numberArgument(call, 2);
        if (given instanceof Operand) return given;
        mode = given;
    }
    if (significance === 0 || number === 0) return Operand.of(Value.fromNumber(0));
    significance = Math.abs(significance);
    const quotient = number / significance;
    if (quotient === 0) return Operand.ofError('#NUM!');
    // The run pins half-multiples, not which side Excel takes when division is almost integral. Refuse that
    // boundary at the existing 15-digit precision; never use that precision as an epsilon to choose an answer.
    const nearest = roundHalfToEven(quotient);
    if (quotient !== nearest && approximatelyEqual(quotient, nearest)) return call.evaluator.refuse();
    const ceiling = number < 0 && mode !== 0 ? !ceilingAsked : ceilingAsked;
    return numberResult((ceiling ? Math.ceil(quotient) : Math.floor(quotient)) * significance);
}

/** The largest bound RANDBETWEEN takes: 2^53, past which a double no longer holds every whole number (ADR-0180). */
const largestBound = 9_007_199_254_740_992;

/** RAND: a number from 0 up to 1, evenly spread; `#GETTING_DATA` while the Sheet has no seed. */
export function rand(call: FunctionCall): Operand {
    const draw = drawFor(call);
    return draw === undefined ? Operand.ofError('#GETTING_DATA') : Operand.of(Value.fromNumber(unitOf(draw)));
}

/**
 * RANDBETWEEN: a whole number from `bottom` to `top`, both included, every one equally likely. As Excel was
 * observed to answer: a bottom above the top is `#NUM!`, a boolean and text that is not a number are `#VALUE!`,
 * a numeric string is read as its number, a blank Reference is 0, and an Error Value passes through
 * (RANDBETWEEN-007 to 014). Refused by decision: a fractional bound (RANDBETWEEN-004 to 006), and a bound above
 * 2^53 in magnitude.
 */
export function randBetween(call: FunctionCall): Operand {
    const bottom = boundOf(call, 0);
    if (bottom instanceof Operand) return bottom;
    const top = boundOf(call, 1);
    if (top instanceof Operand) return top;
    if (!isWhole(bottom) || !isWhole(top)) return call.evaluator.refuse();
    if (bottom > top) return Operand.ofError('#NUM!');
    const draw = drawFor(call);
    if (draw === undefined) return Operand.ofError('#GETTING_DATA');
    return Operand.of(Value.fromNumber(between(draw, bottom, top)));
}

function isWhole(bound: number): boolean {
    return bound === Math.floor(bound) && Math.abs(bound) <= largestBound;
}

/** A bound of RANDBETWEEN: one Value, coerced as a number except that a boolean is `#VALUE!`, as Excel answered `RANDBETWEEN(TRUE,3)`. */
function boundOf(call: FunctionCall, index: number): number | Operand {
    const value = scalarArgument(call, index);
    if (value instanceof Operand) return value;
    if (value?.kind === 'boolean') return Operand.ofError('#VALUE!');
    return numberOrFailure(call.evaluator.toNumber(value));
}

/** This call's draw, from the cell it is computed in; `undefined` while the Sheet has no seed. */
function drawFor(call: FunctionCall): bigint | undefined {
    const self = call.evaluator.self;
    return self === undefined ? undefined : call.evaluator.cells.draw(self, call.place);
}
