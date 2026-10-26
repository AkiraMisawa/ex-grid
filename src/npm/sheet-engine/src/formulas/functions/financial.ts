// PMT, PV, FV and XNPV (C#'s Functions.Financial.cs): the annuity identity
// pv*(1+rate)^nper + pmt*(1+rate*type)*((1+rate)^nper-1)/rate + fv = 0, and its form at a rate of 0, each
// evaluated as the C# engine's documentation records the evaluation Excel's observed doubles leave.
import { CellAddress } from '../../cell-address';
import type { ErrorValue, Value } from '../../value';
import type { CellReader } from '../cell-reader';
import { exp as correctlyRoundedExp, log as correctlyRoundedLog } from '../correctly-rounded';
import { exactPayment } from '../exact-payment';
import type { FunctionCall } from '../function-definition';
import { Operand } from '../operand';
import type { Area } from '../reference';
import { numberArgument, numberResult } from './shared';

/** The largest `nper` raised by squaring: 2^53, past which a double holds no odd whole number. */
const wholePeriods = 9007199254740992;

interface Annuity {
    readonly rate: number;
    readonly periods: number;
    readonly third: number;
    readonly fourth: number;
    readonly atStart: boolean;
}

/**
 * The annuity's arguments: `rate`, `nper`, the third, and the optional fourth (0 when left out) and `type`.
 * `type` is read as a truth value, as Excel reads it: 0 is payment at the end of each period, and any other
 * number at the start (PMT-008 to PMT-010). At a rate other than 0, an `nper` whose absolute value is past 2^53
 * is refused: how Excel raises 1+rate to one has not been observed.
 */
function annuityOf(call: FunctionCall): Annuity | Operand {
    const rate = numberArgument(call, 0);
    if (rate instanceof Operand) return rate;
    const periods = numberArgument(call, 1);
    if (periods instanceof Operand) return periods;
    const third = numberArgument(call, 2);
    if (third instanceof Operand) return third;
    let fourth = 0;
    if (call.has(3)) {
        const given = numberArgument(call, 3);
        if (given instanceof Operand) return given;
        fourth = given;
    }
    let type = 0;
    if (call.has(4)) {
        const given = numberArgument(call, 4);
        if (given instanceof Operand) return given;
        type = given;
    }
    if (rate !== 0 && Math.abs(periods) > wholePeriods) return call.evaluator.refuse();
    return { rate, periods, third, fourth, atStart: type !== 0 };
}

/**
 * `onePlusRate` raised to `periods` in PV, FV and XNPV, or the Error Value instead: a whole number by squaring
 * (`raise`), a negative number as 1 over the power to its absolute value, 0.5 by the square root, any other
 * number as e^(n ln b) with the exponential and the logarithm each correctly rounded, a negative 1+rate to a
 * fraction whose reciprocal is an odd whole number as its odd root, and 1+rate of 0 as the `^` operator takes
 * it (FV-001 to FV-023, PV-001 to PV-016, XNPV-005, 006, 013).
 */
function raiseOrError(onePlusRate: number, periods: number): number | ErrorValue {
    const refused = powerError(onePlusRate, periods);
    if (refused !== undefined) return refused;
    const magnitude = Math.abs(periods);
    let power: number;
    if (magnitude === Math.floor(magnitude)) power = raise(onePlusRate, magnitude);
    else if (onePlusRate === 0) power = 0;
    else if (onePlusRate > 0) power = magnitude === 0.5 ? Math.sqrt(onePlusRate) : correctlyRoundedExp(magnitude * correctlyRoundedLog(onePlusRate));
    else power = -correctlyRoundedExp(correctlyRoundedLog(-onePlusRate) * magnitude);
    return periods < 0 ? 1 / power : power;
}

/** The Error Value the financial power gives instead of raising (FV-017, FV-018, FV-020, FV-021, PV-011, PV-012), or `undefined`. */
function powerError(onePlusRate: number, periods: number): ErrorValue | undefined {
    if (onePlusRate === 0 && periods === 0) return '#NUM!';
    if (onePlusRate === 0 && periods < 0) return '#DIV/0!';
    if (onePlusRate >= 0 || periods === Math.floor(periods)) return undefined;
    const root = 1 / Math.abs(periods);
    return Number.isFinite(root) && root === Math.floor(root) && Math.abs(root % 2) === 1 ? undefined : '#NUM!';
}

/**
 * `value` to the power `exponent` as PV, FV and XNPV raise 1+rate to a whole number: by squaring, from the
 * exponent's lowest bit, each product rounded to a double (FV-001, FV-003, PV-001, PV-003, XNPV-006).
 */
function raise(value: number, exponent: number): number {
    let result = 1;
    let square = value;
    let k = exponent;
    while (k > 0) {
        if (k % 2 === 1) result *= square;
        k = Math.floor(k / 2);
        if (k > 0) square *= square;
    }
    return result;
}

/**
 * PMT: the payment that pays `pv` down to `fv` over `nper` periods; `nper` of 0 is `#NUM!` (PMT-005, PMT-046),
 * and a rate of -1 or below is `#NUM!` (PMT-034 to PMT-037). At a rate above -1 other than 0, the exact payment
 * rounded once, or a refusal (`exactPayment`): ADR-0047's exception for PMT alone (decided with the user,
 * 2026-10-12), Excel's double differing from the engine's by 1 to 11 units in the last place where it differs.
 */
export function pmt(call: FunctionCall): Operand {
    const annuity = annuityOf(call);
    if (annuity instanceof Operand) return annuity;
    const { rate, periods, third: present, fourth: future, atStart } = annuity;
    if (periods === 0 || rate <= -1) return Operand.ofError('#NUM!');
    if (rate === 0) return numberResult(-(present + future) / periods);
    const payment = exactPayment(rate, periods, present, future, atStart);
    return payment === undefined ? call.evaluator.refuse() : numberResult(payment);
}

/**
 * PV: what a series of payments and a future value are worth now. The payment multiplies the product of
 * 1+rate*type and ((1+rate)^nper-1)/rate, the grouping that gives Excel's doubles; 1+rate is raised as FV raises
 * it. At a rate of -1 with an `nper` above 0 the growth is 0, and PV is `#DIV/0!` (PV-013).
 */
export function pv(call: FunctionCall): Operand {
    const annuity = annuityOf(call);
    if (annuity instanceof Operand) return annuity;
    const { rate, periods, third: payment, fourth: future, atStart } = annuity;
    if (rate === 0) return numberResult(-(future + payment * periods));
    const growth = raiseOrError(1 + rate, periods);
    if (typeof growth !== 'number') return Operand.ofError(growth);
    if (1 + rate === 0) return Operand.ofError('#DIV/0!');
    const due = 1 + rate * (atStart ? 1 : 0);
    return numberResult(-(future + payment * (due * ((growth - 1) / rate))) / growth);
}

/**
 * FV: what a present value and a series of payments are worth after `nper` periods,
 * `-(pv*g + pmt*((1+rate*type)*((g-1)/rate)))` in doubles, g raised by `raiseOrError` (FV-009, FV-014, FV-015,
 * FV-019).
 */
export function fv(call: FunctionCall): Operand {
    const annuity = annuityOf(call);
    if (annuity instanceof Operand) return annuity;
    const { rate, periods, third: payment, fourth: present, atStart } = annuity;
    if (rate === 0) return numberResult(-(present + payment * periods));
    const growth = raiseOrError(1 + rate, periods);
    if (typeof growth !== 'number') return Operand.ofError(growth);
    const due = 1 + rate * (atStart ? 1 : 0);
    return numberResult(-(present * growth + payment * (due * ((growth - 1) / rate))));
}

/** One check of XNPV's, or a reading of its ranges (`xnpvOrders`). */
type XnpvStep = 'rate' | 'counts' | 'dates' | 'values' | 'order' | 'pairs' | 'pairsOrderAfterDate' | 'pairsOrderAfterValue';

/** The orders of XNPV's checks that run 18a's answers leave (XNPV-021 to XNPV-030); XNPV answers only where they all agree. */
const xnpvOrders: readonly (readonly XnpvStep[])[] = [
    ['rate', 'counts', 'dates', 'values', 'order'],
    ['rate', 'dates', 'counts', 'values', 'order'],
    ['dates', 'rate', 'counts', 'values', 'order'],
    ['rate', 'counts', 'pairs', 'order'],
    ['rate', 'counts', 'pairsOrderAfterDate'],
    ['rate', 'counts', 'pairsOrderAfterValue'],
];

/** What one order of XNPV's checks answers: an Error Value, a refusal, or neither, the sum. */
interface XnpvOutcome {
    readonly error: ErrorValue | undefined;
    readonly refused: boolean;
}

const sumOutcome: XnpvOutcome = { error: undefined, refused: false };
const numOutcome: XnpvOutcome = { error: '#NUM!', refused: false };

function sameOutcome(a: XnpvOutcome, b: XnpvOutcome): boolean {
    return a.error === b.error && a.refused === b.refused;
}

/**
 * XNPV: each value discounted by the years from the first date, the days divided by 365, and the discounted
 * values summed from the first, the plain sum (XNPV-018). The values and the dates are ranges of the same count,
 * numbers only. A rate of 0 or below is `#NUM!`, checked before XNPV evaluates (XNPV-007 to 012, 017, 019, 020).
 * The order of the checks is as run 18a read it; where the orders it leaves part, XNPV refuses.
 */
export function xNpv(call: FunctionCall): Operand {
    const rateRead = numberArgument(call, 0);
    let rate = 0;
    let rateError: ErrorValue | undefined;
    if (rateRead instanceof Operand) {
        const scalar = rateRead.scalar;
        if (scalar === undefined || !scalar.isError || scalar.isRefused) return rateRead;
        rateError = scalar.error;
    } else rate = rateRead;
    const values = cellsOf(call, 1);
    const dates = cellsOf(call, 2);
    const countsDiffer = cellCount(call.operand(1)) !== cellCount(call.operand(2));
    const outcome = checked(xnpvOrders[0] as readonly XnpvStep[], rateError, rate, countsDiffer, values, dates); // oxlint-disable-line typescript/no-unsafe-type-assertion -- six orders
    for (const order of xnpvOrders.slice(1)) {
        if (!sameOutcome(checked(order, rateError, rate, countsDiffer, values, dates), outcome)) return call.evaluator.refuse();
    }
    if (outcome.refused) return call.evaluator.refuse();
    if (outcome.error !== undefined) return Operand.ofError(outcome.error);
    const onePlusRate = 1 + rate;
    const origin = Math.floor(numberAt(dates, 0));
    let total = 0;
    for (let i = 0; i < values.length; i++) {
        const discount = raiseOrError(onePlusRate, (Math.floor(numberAt(dates, i)) - origin) / 365);
        if (typeof discount !== 'number') return Operand.ofError(discount);
        total += numberAt(values, i) / discount;
    }
    return numberResult(total);
}

function numberAt(cells: readonly (Value | undefined)[], index: number): number {
    const cell = cells[index];
    if (cell === undefined) throw new Error('Internal: XNPV reads a cell its checks found blank.');
    return cell.number;
}

/** The answer of XNPV's checks made in `order`: the first that fails, or the sum. */
function checked(order: readonly XnpvStep[], rateError: ErrorValue | undefined, rate: number, countsDiffer: boolean,
    values: readonly (Value | undefined)[], dates: readonly (Value | undefined)[]): XnpvOutcome {
    for (const step of order) {
        let outcome: XnpvOutcome | undefined;
        switch (step) {
            case 'rate':
                outcome = rateError !== undefined ? { error: rateError, refused: false } : rate <= 0 ? numOutcome : undefined;
                break;
            case 'counts':
                outcome = countsDiffer ? numOutcome : undefined;
                break;
            case 'dates':
                outcome = firstFailure(dates);
                break;
            case 'values':
                outcome = firstFailure(values);
                break;
            case 'order':
                for (let i = 1; i < dates.length && outcome === undefined; i++) {
                    if (beforeFirst(dates, i)) outcome = numOutcome;
                }
                break;
            case 'pairs':
            case 'pairsOrderAfterDate':
            case 'pairsOrderAfterValue':
                outcome = paired(step, values, dates);
                break;
            default: throw new Error(`Internal: ${String(step)} is no step of XNPV's.`);
        }
        if (outcome !== undefined) return outcome;
    }
    return sumOutcome;
}

function firstFailure(cells: readonly (Value | undefined)[]): XnpvOutcome | undefined {
    for (const cell of cells) {
        const outcome = failureOf(cell);
        if (outcome !== undefined) return outcome;
    }
    return undefined;
}

/** The dates and the values read pair by pair, a date before its value, in one of the pair orders. */
function paired(step: XnpvStep, values: readonly (Value | undefined)[], dates: readonly (Value | undefined)[]): XnpvOutcome | undefined {
    for (let i = 0; i < Math.min(values.length, dates.length); i++) {
        const date = failureOf(dates[i]);
        if (date !== undefined) return date;
        if (step === 'pairsOrderAfterDate' && beforeFirst(dates, i)) return numOutcome;
        const value = failureOf(values[i]);
        if (value !== undefined) return value;
        if (step === 'pairsOrderAfterValue' && beforeFirst(dates, i)) return numOutcome;
    }
    return undefined;
}

/** A cell's failure: its Error Value, a refusal where it is not a number, or `undefined` for a number. */
function failureOf(cell: Value | undefined): XnpvOutcome | undefined {
    if (cell?.isError === true) return { error: cell.error, refused: false };
    if (cell?.kind === 'number') return undefined;
    return { error: undefined, refused: true };
}

/** Whether date `index`, read and a number, as the first is, lies before the first's day. */
function beforeFirst(dates: readonly (Value | undefined)[], index: number): boolean {
    return Math.floor(numberAt(dates, index)) < Math.floor(numberAt(dates, 0));
}

/** How many cells a range argument holds, whatever they hold. */
function cellCount(operand: Operand): number {
    switch (operand.kind) {
        case 'column': return operand.columnOf().length;
        case 'array': {
            const array = operand.arrayOf();
            return array.rows * array.columns;
        }
        case 'area': return operand.area.rows * operand.area.columns;
        case 'scalar':
        case 'missing':
        case 'threeD':
            return 1;
        default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
    }
}

/** Every cell of a range, blank as `undefined`, read in order. A range of more than 1,000,000 cells is one blank, which XNPV refuses. */
function cellsOf(call: FunctionCall, index: number): (Value | undefined)[] {
    const operand = call.operand(index);
    switch (operand.kind) {
        case 'scalar': return [operand.scalar];
        case 'column': return [...operand.columnOf()];
        case 'array': return [...operand.arrayOf().all()];
        case 'area': return areaCells(operand.area, operand.cellsOf());
        case 'missing':
        case 'threeD':
            return [undefined];
        default: throw new Error(`Internal: ${String(operand.kind)} is no kind of operand.`);
    }
}

function areaCells(area: Area, reader: CellReader): (Value | undefined)[] {
    if (area.rows * area.columns > 1_000_000) return [undefined];
    const cells: (Value | undefined)[] = [];
    for (let row = area.row1; row <= area.row2; row++) {
        for (let column = area.column1; column <= area.column2; column++) cells.push(reader.read(new CellAddress(row, column)));
    }
    return cells;
}
