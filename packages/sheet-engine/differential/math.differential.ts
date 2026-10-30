// Excel's arithmetic, held to the C# engine bit for bit: random nests of the numeric functions and operators
// over numbers crowded near the places the last bit or a rounding goes wrong, each cell's Value compared by
// its 64 bits and its display as General shows it.

import { afterAll, expect, test } from 'vitest';
import { cultures, draw, exactish, literal, type Draw } from './draw';
import { startOracle } from './oracle';
import { differences, type SheetRequest } from './sheet';

const oracle = startOracle();
afterAll(() => { oracle.close(); });

const d = draw();

const unary = [
    'ABS', 'ACOS', 'ACOSH', 'ACOT', 'ACOTH', 'ASIN', 'ASINH', 'ATAN', 'ATANH', 'COS', 'COSH', 'COT', 'COTH', 'CSC', 'CSCH',
    'DEGREES', 'EXP', 'INT', 'LN', 'LOG10', 'RADIANS', 'SEC', 'SECH', 'SIGN', 'SIN', 'SINH', 'SQRT', 'TAN', 'TANH',
];
const binary = ['ATAN2', 'POWER', 'MOD', 'MROUND', 'LOG', 'CEILING.MATH', 'FLOOR.MATH', 'TRUNC'];
const rounding = ['ROUND', 'ROUNDUP', 'ROUNDDOWN', 'TRUNC'];
const aggregates = ['SUM', 'AVERAGE', 'PRODUCT', 'MEDIAN', 'STDEV.S', 'STDEV.P', 'VAR.S', 'VAR.P', 'MAX', 'MIN', 'SUMPRODUCT'];
const operators = ['+', '-', '*', '/', '^'];

/** A number expression `depth` calls deep at most, reading the cells A1 to A`rows` when there are any. */
function expression(r: Draw, depth: number, rows = 0): string {
    if (depth === 0 || r.chance(0.25)) {
        if (rows > 0 && r.chance(0.4)) return `A${String(1 + r.int(rows))}`;
        return r.chance(0.6) ? literal(r) : exactish(r);
    }
    switch (r.int(7)) {
        case 0:
        case 1: return `${r.pick(unary)}(${expression(r, depth - 1, rows)})`;
        case 2: return `${r.pick(binary)}(${expression(r, depth - 1, rows)},${expression(r, depth - 1, rows)})`;
        case 3: return `${r.pick(rounding)}(${expression(r, depth - 1, rows)},${String(r.int(31) - 15)})`;
        case 4: return `(${expression(r, depth - 1, rows)}${r.pick(operators)}${expression(r, depth - 1, rows)})`;
        case 5: return `${r.pick(['-', '+'])}${expression(r, depth - 1, rows)}${r.chance(0.2) ? '%' : ''}`;
        default: return `${r.pick(aggregates)}(${Array.from({ length: 1 + r.int(5) }, () => expression(r, depth - 1, rows)).join(',')})`;
    }
}

/** A Sheet of random number cells in A, and Formulas in B over them, reading every cell back. */
function request(r: Draw): SheetRequest {
    const entries: [string, string][] = [];
    const rows = 1 + r.int(40);
    for (let row = 1; row <= rows; row++) entries.push([`A${String(row)}`, `=${expression(r, 0)}`]);
    for (let row = 1; row <= 40; row++) {
        const formula = r.chance(0.2)
            ? `${r.pick(aggregates)}(A${String(1 + r.int(rows))}:A${String(1 + r.int(rows))})`
            : expression(r, 1 + r.int(3), rows);
        entries.push([`B${String(row)}`, `=${formula}`]);
    }
    const read = entries.map(([address]) => address);
    return { culture: r.pick(cultures), entries, read, ...(r.chance(0.3) ? { width: 1 + r.int(20) } : {}) };
}

test(`Excel's arithmetic against the C# engine, seed ${String(d.seed)}`, async () => {
    expect.hasAssertions();
    const requests = Array.from({ length: 1500 }, () => request(d));
    const found = differences(requests, await oracle.askAll(requests));
    expect({ differences: found.length, first: found.slice(0, 20) }).toEqual({ differences: 0, first: [] });
});
