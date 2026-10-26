// The functions built for arrays (ADR-0125; C#'s Functions.Arrays.cs): FILTER, UNIQUE, SORT, SORTBY, SEQUENCE,
// TRANSPOSE, which spill, and SUMPRODUCT, which reduces its arrays to one Value.
import { compareIgnoreCase } from '../../dotnet';
import { elementAt } from '../../must';
import { errorValues, Value, type ErrorValue } from '../../value';
import { compareValues } from '../evaluator';
import type { FunctionCall } from '../function-definition';
import { Operand } from '../operand';
import { ValueArray } from '../value-array';
import { numberArgument, numberResult, scalarArgument } from './shared';

/**
 * An argument read whole as an array, or its Error Value as the failure. A kept blank refuses the Formula
 * (`FunctionCall.operand`), unless `keepingBlanks` says this function gives the same answer for every reading of
 * it.
 */
function arrayArgument(call: FunctionCall, index: number, keepingBlanks = false): ValueArray | Operand {
    const operand = keepingBlanks ? call.keepingBlanks(index) : call.operand(index);
    if (operand.isError) return operand;
    const array = call.evaluator.toArray(operand);
    const single = array.isSingle ? array.at(0, 0) : undefined;
    if (single?.isError === true && !operand.isRange) return Operand.of(single);
    return array;
}

/**
 * The array `UNIQUE`, `SORT`, `SORTBY`, `FILTER` or `TRANSPOSE` made (ADR-0125), a blank it kept from its array
 * marked as a kept blank (`Operand.keptBlank`): it spills as 0 (SORT-012, UNIQUE-010, TRANSPOSE-004), `COUNTA`
 * counts it in the array (SORT-021, UNIQUE-017), and taken out by `INDEX` it is blank to `ISBLANK` (SORT-022).
 */
function made(result: ValueArray): Operand {
    return Operand.ofArray(result).withKeptBlank();
}

/**
 * FILTER: the rows (or columns) of `array` whose `include` is TRUE. `include` is one column as tall as the
 * array, or one row as wide; any other shape is `#VALUE!`. A number counts as TRUE when it is not 0, a blank as
 * FALSE, text is `#VALUE!`, and an Error Value is the result. Nothing included is `if_empty`, or `#CALC!`.
 */
export function filter(call: FunctionCall): Operand {
    const array = arrayArgument(call, 0);
    if (array instanceof Operand) return array;
    const include = arrayArgument(call, 1);
    if (include instanceof Operand) return include;
    let byRows: boolean;
    if (include.columns === 1 && include.rows === array.rows) byRows = true;
    else if (include.rows === 1 && include.columns === array.columns) byRows = false;
    else return Operand.ofError('#VALUE!');

    const count = byRows ? array.rows : array.columns;
    const kept: number[] = [];
    for (let i = 0; i < count; i++) {
        const flag = byRows ? include.at(i, 0) : include.at(0, i);
        if (flag === undefined) continue;
        if (flag.isError) return Operand.of(flag);
        if (flag.kind === 'boolean') {
            if (flag.boolean) kept.push(i);
        } else if (flag.kind === 'number') {
            if (flag.number !== 0) kept.push(i);
        } else return Operand.ofError('#VALUE!');
    }
    if (kept.length === 0) return call.has(2) ? call.operand(2) : Operand.ofError('#CALC!');

    const result = byRows ? new ValueArray(kept.length, array.columns) : new ValueArray(array.rows, kept.length);
    kept.forEach((line, k) => {
        if (byRows) {
            for (let c = 0; c < array.columns; c++) result.set(k, c, array.at(line, c));
        } else {
            for (let r = 0; r < array.rows; r++) result.set(r, k, array.at(r, line));
        }
    });
    return made(result);
}

/**
 * UNIQUE: the distinct rows of `array` (its columns when `by_col` is TRUE), in the order they first appear; with
 * `exactly_once` TRUE, only those that appear once. Text is compared without regard to case; a number and text
 * that reads as one are different. A blank is one of a kind only with another blank (UNIQUE-010 to 015). Nothing
 * left is `#CALC!`.
 */
export function unique(call: FunctionCall): Operand {
    const array = arrayArgument(call, 0);
    if (array instanceof Operand) return array;
    const byColumn = flagArgument(call, 1);
    if (byColumn instanceof Operand) return byColumn;
    const exactlyOnce = flagArgument(call, 2);
    if (exactlyOnce instanceof Operand) return exactlyOnce;

    const count = byColumn ? array.columns : array.rows;
    const width = byColumn ? array.rows : array.columns;
    const item = (line: number, at: number): Value | undefined => byColumn ? array.at(at, line) : array.at(line, at);
    const same = (a: number, b: number): boolean => {
        for (let k = 0; k < width; k++) {
            if (!sameItem(item(a, k), item(b, k))) return false;
        }
        return true;
    };

    // Each line's first equal, then how many there are of it.
    const first: number[] = [];
    const occurrences = new Map<number, number>();
    for (let i = 0; i < count; i++) {
        first.push(i);
        for (let j = 0; j < i; j++) {
            if (first[j] === j && same(i, j)) {
                first[i] = j;
                break;
            }
        }
        const head = elementAt(first, i);
        occurrences.set(head, (occurrences.get(head) ?? 0) + 1); // oxlint-disable-line ex-grid/no-stand-in-default -- a line not yet counted has been seen 0 times
    }
    const kept: number[] = [];
    for (let i = 0; i < count; i++) {
        if (first[i] === i && (!exactlyOnce || occurrences.get(i) === 1)) kept.push(i);
    }
    if (kept.length === 0) return Operand.ofError('#CALC!');

    const result = byColumn ? new ValueArray(width, kept.length) : new ValueArray(kept.length, width);
    kept.forEach((line, k) => {
        for (let at = 0; at < width; at++) {
            if (byColumn) result.set(at, k, item(line, at));
            else result.set(k, at, item(line, at));
        }
    });
    return made(result);
}

function sameItem(x: Value | undefined, y: Value | undefined): boolean {
    if (x === undefined || y === undefined) return x === y;
    return x.kind === y.kind && (x.isError ? x.error === y.error : compareValues(x, y) === 0);
}

/**
 * SORT: `array`'s rows ordered by the column `sort_index` (from 1, the first when left out), ascending for
 * `sort_order` 1 and descending for −1; its columns, by a row, when `by_col` is TRUE. Equal keys keep their
 * order. Keys order as `orderLines` says, a blank last. An index outside the array, or an order other than 1 and
 * −1, is `#VALUE!` (SORT-007, 008). An index that is not a whole number is truncated (SORT-042).
 */
export function sort(call: FunctionCall): Operand {
    // A kept blank in the array is a blank key, sorted last as a blank cell's (SORT-038).
    const array = arrayArgument(call, 0, true);
    if (array instanceof Operand) return array;
    let index = 1;
    if (call.has(1)) {
        const given = numberArgument(call, 1);
        if (given instanceof Operand) return given;
        index = given;
    }
    const descending = orderArgument(call, 2);
    if (descending instanceof Operand) return descending;
    const byColumn = flagArgument(call, 3);
    if (byColumn instanceof Operand) return byColumn;
    index = Math.trunc(index);
    const lines = byColumn ? array.columns : array.rows;
    const across = byColumn ? array.rows : array.columns;
    if (index < 1 || index > across) return Operand.ofError('#VALUE!');
    const at = index - 1;
    const keys: (Value | undefined)[] = [];
    for (let i = 0; i < lines; i++) keys.push(byColumn ? array.at(at, i) : array.at(i, at));
    const order = orderLines([{ keys, descending }], lines);
    if (order === undefined) return call.evaluator.refuse();
    return made(reorder(array, order, byColumn));
}

/**
 * SORTBY: `array` ordered by one or more `by_array`s, each with its `sort_order` (1 when left out), the first
 * deciding first. Each `by_array` is one column as tall as the array, or one row as wide, and all of them run the
 * same way; any other shape is `#VALUE!`. Keys order as in `sort` (SORTBY-007 to 010).
 */
export function sortBy(call: FunctionCall): Operand {
    const array = arrayArgument(call, 0);
    if (array instanceof Operand) return array;
    let byColumn: boolean | undefined;
    const keys: SortKey[] = [];
    for (let i = 1; i < call.count; i += 2) {
        const by = arrayArgument(call, i);
        if (by instanceof Operand) return by;
        let columns: boolean;
        if (by.columns === 1 && by.rows === array.rows && (by.rows > 1 || array.columns === 1)) columns = false;
        else if (by.rows === 1 && by.columns === array.columns && by.columns > 1) columns = true;
        else return Operand.ofError('#VALUE!');
        if (byColumn !== undefined && byColumn !== columns) return Operand.ofError('#VALUE!');
        byColumn = columns;
        const descending = orderArgument(call, i + 1);
        if (descending instanceof Operand) return descending;
        const length = columns ? by.columns : by.rows;
        const values: (Value | undefined)[] = [];
        for (let k = 0; k < length; k++) values.push(columns ? by.at(0, k) : by.at(k, 0));
        keys.push({ keys: values, descending });
    }
    const lines = byColumn === true ? array.columns : array.rows;
    const order = orderLines(keys, lines);
    if (order === undefined) return call.evaluator.refuse();
    return made(reorder(array, order, byColumn === true));
}

interface SortKey {
    readonly keys: readonly (Value | undefined)[];
    readonly descending: boolean;
}

/**
 * The order of `lines` lines by their keys, stable. A blank key comes after numbers, text, booleans and Error
 * Values whichever the order (SORT-011 to 014, 016 to 019, 024, 025, 031). An Error Value key sorts after every
 * other kind but the blank ascending, and before them descending (SORT-026, 028 to 030; SORTBY-010). Two
 * different Error Values are ordered where the readings their one observation leaves agree (`errorKeysSettled`);
 * `undefined` where they part.
 */
function orderLines(keys: readonly SortKey[], lines: number): number[] | undefined {
    for (const { keys: values, descending } of keys) {
        if (!errorKeysSettled(values, descending)) return undefined;
    }
    const order: number[] = [];
    for (let i = 0; i < lines; i++) order.push(i);
    return order.toSorted((a, b) => {
        for (const { keys: values, descending } of keys) {
            const x = values[a];
            const y = values[b];
            let compared: number;
            if (x === undefined && y === undefined) compared = 0;
            else if (x === undefined) compared = 1;
            else if (y === undefined) compared = -1;
            else compared = descending ? -compareKeys(x, y) : compareKeys(x, y);
            if (compared !== 0) return compared;
        }
        return a - b;
    });
}

function errorOrdinal(error: ErrorValue): number {
    return errorValues.indexOf(error);
}

/** Two keys that are not blank, ascending: numbers, then text, then booleans, and then Error Values, two Error Values in `ERROR.TYPE`'s order. */
function compareKeys(x: Value, y: Value): number {
    if (x.isError && y.isError) return Math.sign(errorOrdinal(x.error) - errorOrdinal(y.error));
    if (x.isError) return 1;
    if (y.isError) return -1;
    return compareValues(x, y);
}

/**
 * Whether a key's Error Values are ordered as every reading run 18a left orders them (SORT-032): different Error
 * Values are ordered ascending where `ERROR.TYPE`'s numbers and their texts give the one order, each of them one
 * of the seven `ERROR.TYPE` numbers; anything else, or two in a descending sort, refuses.
 */
function errorKeysSettled(values: readonly (Value | undefined)[], descending: boolean): boolean {
    const errors: ErrorValue[] = [];
    for (const value of values) {
        if (value?.isError === true && !errors.includes(value.error)) errors.push(value.error);
    }
    if (errors.length < 2) return true;
    if (descending || errors.some(e => errorOrdinal(e) > errorOrdinal('#N/A'))) return false;
    const byNumber = errors.toSorted((a, b) => errorOrdinal(a) - errorOrdinal(b));
    const byText = errors.toSorted(compareIgnoreCase);
    return byNumber.every((e, i) => e === byText[i]);
}

function reorder(array: ValueArray, order: readonly number[], byColumn: boolean): ValueArray {
    const result = new ValueArray(array.rows, array.columns);
    order.forEach((line, k) => {
        if (byColumn) {
            for (let r = 0; r < array.rows; r++) result.set(r, k, array.at(r, line));
        } else {
            for (let c = 0; c < array.columns; c++) result.set(k, c, array.at(line, c));
        }
    });
    return result;
}

/** A sort order argument: 1 or left out is ascending (`false`), −1 descending, anything else `#VALUE!`. */
function orderArgument(call: FunctionCall, index: number): boolean | Operand {
    if (!call.has(index)) return false;
    const order = numberArgument(call, index);
    if (order instanceof Operand) return order;
    if (order !== 1 && order !== -1) return Operand.ofError('#VALUE!');
    return order === -1;
}

/** A TRUE or FALSE argument, FALSE when left out; a number is TRUE when it is not 0, other text `#VALUE!`. */
function flagArgument(call: FunctionCall, index: number): boolean | Operand {
    if (!call.has(index)) return false;
    const value = scalarArgument(call, index);
    if (value instanceof Operand) return value;
    if (value === undefined) return false;
    if (value.kind === 'boolean') return value.boolean;
    if (value.kind === 'number') return value.number !== 0;
    return Operand.ofError('#VALUE!');
}

/**
 * SEQUENCE: `rows` by `columns` numbers, row by row, from `start` by `step`, each 1 when left out and the sizes
 * truncated. A size of 0 is `#CALC!`, below 0 `#VALUE!`; an array larger than the engine can hold is `#NUM!`.
 */
export function sequence(call: FunctionCall): Operand {
    const rowsGiven = numberArgument(call, 0);
    if (rowsGiven instanceof Operand) return rowsGiven;
    let columns = 1;
    if (call.has(1)) {
        const given = numberArgument(call, 1);
        if (given instanceof Operand) return given;
        columns = given;
    }
    let start = 1;
    if (call.has(2)) {
        const given = numberArgument(call, 2);
        if (given instanceof Operand) return given;
        start = given;
    }
    let step = 1;
    if (call.has(3)) {
        const given = numberArgument(call, 3);
        if (given instanceof Operand) return given;
        step = given;
    }
    const rows = Math.trunc(rowsGiven);
    columns = Math.trunc(columns);
    if (rows < 0 || columns < 0) return Operand.ofError('#VALUE!');
    if (rows === 0 || columns === 0) return Operand.ofError('#CALC!');
    if (rows * columns > ValueArray.mostCells) return Operand.ofError('#NUM!');
    const result = new ValueArray(rows, columns);
    let k = 0;
    for (let r = 0; r < result.rows; r++) {
        for (let c = 0; c < result.columns; c++) result.set(r, c, Value.fromNumber(start + step * k++));
    }
    return Operand.ofArray(result);
}

/** TRANSPOSE: the array's rows as columns, a blank kept as `SORT`'s is (TRANSPOSE-004 to 006). */
export function transpose(call: FunctionCall): Operand {
    const array = arrayArgument(call, 0);
    if (array instanceof Operand) return array;
    const result = new ValueArray(array.columns, array.rows);
    for (let r = 0; r < array.rows; r++) {
        for (let c = 0; c < array.columns; c++) result.set(c, r, array.at(r, c));
    }
    return made(result);
}

/**
 * SUMPRODUCT: the sum of the products of its arrays' corresponding Values, the arrays all of one size, or
 * `#VALUE!`. Inside an array anything but a number counts as 0, as Microsoft documents, so a kept blank is 0
 * whichever way it is read; an Error Value is the result, and a sum past the largest double is `#NUM!`.
 */
export function sumProduct(call: FunctionCall): Operand {
    const arrays: ValueArray[] = [];
    for (let i = 0; i < call.count; i++) {
        if (!call.has(i)) return Operand.ofError('#VALUE!');
        const array = arrayArgument(call, i, true);
        if (array instanceof Operand) return array;
        const head = arrays[0];
        if (head !== undefined && (array.rows !== head.rows || array.columns !== head.columns)) return Operand.ofError('#VALUE!');
        arrays.push(array);
    }
    const head = elementAt(arrays, 0);
    let sum = 0;
    for (let r = 0; r < head.rows; r++) {
        for (let c = 0; c < head.columns; c++) {
            let product = 1;
            for (const array of arrays) {
                const value = array.at(r, c);
                if (value?.isError === true) return Operand.of(value);
                product *= value?.kind === 'number' ? value.number : 0;
            }
            sum += product;
        }
    }
    return numberResult(sum);
}
