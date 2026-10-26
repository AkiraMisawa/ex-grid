// Whole Sheets, held to the C# engine: random texts typed under random cultures — numbers in the culture's
// spelling, dates, times, percentages, currencies, booleans, Error Values, words — and random Formulas over
// them, each cell's Value compared exactly and its display as shown, and every refused entry by its reason.
// The platform's transcendental functions are left to math.differential.ts.

import { afterAll, expect, test } from 'vitest';
import { cultures, draw, literal, type Draw } from './draw';
import { startOracle } from './oracle';
import { differences, type SheetRequest } from './sheet';

const oracle = startOracle();
afterAll(() => { oracle.close(); });

const d = draw();

const separators: Record<string, { readonly decimal: string; readonly group: string }> = {
    'invariant': { decimal: '.', group: ',' },
    'en-US': { decimal: '.', group: ',' },
    'en-GB': { decimal: '.', group: ',' },
    'de-DE': { decimal: ',', group: '.' },
    'fr-FR': { decimal: ',', group: ' ' },
    'ja-JP': { decimal: '.', group: ',' },
};

function grouped(r: Draw, whole: string, group: string): string {
    if (!r.chance(0.4)) return whole;
    return whole.replace(/\B(?=(\d{3})+(?!\d))/gu, group);
}

/** A text as a user types it into a cell under `culture`. */
function typed(r: Draw, culture: string): string {
    const { decimal, group } = separators[culture] ?? { decimal: '.', group: ',' };
    const whole = String(r.int(r.pick([10, 1000, 10_000_000, 1_000_000_000_000])));
    const fraction = String(r.int(1000)).padStart(1 + r.int(3), '0');
    const day = 1 + r.int(28);
    const month = 1 + r.int(12);
    const year = r.pick([1900, 1904, 1999, 2026, 2029, 30, 99, 9999]);
    switch (r.int(16)) {
        case 0:
        case 1: return `${r.pick(['', '-', '+'])}${grouped(r, whole, group)}${r.chance(0.6) ? `${decimal}${fraction}` : ''}`;
        case 2: return `${grouped(r, whole, group)}${decimal}${fraction}${r.pick(['%', ' %', 'E+3', 'e-2', ''])}`;
        case 3: return r.pick([`(${whole})`, ` ${whole} `, `${whole}-`, `$${grouped(r, whole, group)}`, `€${whole}`, `${whole} €`, `¥${whole}`, `£${whole}${decimal}${fraction}`, `-$${whole}`, `$-${whole}`]);
        case 4: return r.pick([`${String(year)}-${String(month)}-${String(day)}`, `${String(year)}/${String(month)}/${String(day)}`]);
        case 5: return r.pick([`${String(month)}/${String(day)}/${String(year)}`, `${String(day)}/${String(month)}/${String(year)}`, `${String(day)}.${String(month)}.${String(year)}`, `${String(month)}/${String(day)}`]);
        case 6: return r.pick([`${String(day)}-${r.pick(['Jan', 'feb', 'MAR', 'Dec', 'Mai', 'janv.'])}`, `${r.pick(['Jan', 'March', 'Oct'])} ${String(day)}, ${String(year)}`, `${String(day)} ${r.pick(['Jan', 'März', 'déc.'])} ${String(year)}`]);
        case 7: return r.pick([`${String(r.int(30))}:${String(r.int(60)).padStart(2, '0')}`, `${String(r.int(24))}:${String(r.int(60))}:${String(r.int(60))}`, `${String(1 + r.int(12))}:${String(r.int(60)).padStart(2, '0')} ${r.pick(['AM', 'pm', 'a', 'P'])}`, `${String(r.int(24))}:00:00${decimal}5`]);
        case 8: return `${String(year)}-${String(month)}-${String(day)} ${String(r.int(24))}:${String(r.int(60)).padStart(2, '0')}`;
        case 9: return r.pick(['TRUE', 'false', 'True', 'WAHR', 'VRAI', '#N/A', '#DIV/0!', '#ref!', '#VALUE!', '#NAME?', '#NUM!', '#NULL!']);
        case 10: return r.pick(['', ' ', 'abc', 'ABC', 'Ab c', "'123", "'", 'x1', '1 1/2', '3/4', '0 1/8', '1/0', '١٢٣', 'ｘ', 'ß', 'İ']);
        case 11: return literal(r).replace('.', decimal);
        case 12: return `${whole}${decimal}${'9'.repeat(10 + r.int(8))}`;
        case 13: return `${r.pick(['', '-'])}0${decimal}${'0'.repeat(r.int(12))}${fraction}`;
        case 14: return `${grouped(r, whole, r.pick([group, ',', '.', ' ']))}${r.pick([decimal, '.', ','])}${fraction}`;
        default: return `${whole}${r.pick(['', decimal])}${r.pick(['', fraction])}`;
    }
}

const rangeFunctions = ['SUM', 'AVERAGE', 'COUNT', 'COUNTA', 'COUNTBLANK', 'MAX', 'MIN', 'MEDIAN', 'PRODUCT', 'STDEV.S', 'VAR.P', 'CONCAT', 'ROWS', 'COLUMNS', 'SUMPRODUCT'];
const textFunctions = ['LEN', 'UPPER', 'LOWER', 'PROPER', 'TRIM', 'VALUE', 'NUMBERVALUE', 'ISNUMBER', 'ISTEXT', 'ISBLANK', 'ISERROR', 'ISNA', 'ERROR.TYPE', 'NOT', 'INT', 'ABS', 'SIGN', 'DAY', 'MONTH', 'YEAR', 'WEEKDAY', 'HOUR', 'MINUTE', 'SECOND'];
const criteria = ['">0"', '"<=5"', '"=abc"', '"a*"', '"?b*"', '""', '"<>"', '"TRUE"', '1', '"1"', '">=1/1/2026"', '"#N/A"'];

/** A cell of the grid A1:E8, or a range in it. */
function cell(r: Draw): string {
    return `${r.pick(['A', 'B', 'C', 'D', 'E'])}${String(1 + r.int(8))}`;
}
function range(r: Draw): string {
    const top = 1 + r.int(8);
    return `${r.pick(['A', 'B', 'C'])}${String(top)}:${r.pick(['C', 'D', 'E'])}${String(top + r.int(9 - top))}`;
}
function wholeColumn(r: Draw): string {
    const letter = r.pick(['A', 'B', 'C', 'D', 'E']);
    return `${letter}1:${letter}8`;
}

/** A Formula over the grid, `depth` calls deep at most. */
function formula(r: Draw, depth: number): string {
    if (depth === 0) return r.chance(0.6) ? cell(r) : r.pick([literal(r), '"abc"', '""', 'TRUE', '0', '1', '"1"', '-1', '2.5']);
    const inner = (): string => formula(r, depth - 1);
    switch (r.int(25)) {
        case 0: return `${r.pick(rangeFunctions)}(${range(r)})`;
        case 1: return `${r.pick(textFunctions)}(${inner()})`;
        case 2: return `(${inner()}${r.pick(['+', '-', '*', '/', '&', '=', '<>', '<', '>=', '^'])}${inner()})`;
        case 3: return `IF(${inner()},${inner()},${inner()})`;
        case 4: return `IFERROR(${inner()},${inner()})`;
        case 5: return `COUNTIF(${range(r)},${r.pick(criteria)})`;
        case 6: return `SUMIF(${wholeColumn(r)},${r.pick(criteria)},${wholeColumn(r)})`;
        case 7: {
            const [a, b] = [wholeColumn(r), wholeColumn(r)];
            const criterion = r.pick(criteria);
            return r.pick([`AVERAGEIF(${a},${criterion},${b})`, `MAXIFS(${a},${b},${criterion})`, `MINIFS(${a},${b},${criterion})`, `SUMIFS(${a},${b},${criterion})`, `COUNTIFS(${a},${criterion})`]);
        }
        case 8: return `${r.pick(['LEFT', 'RIGHT'])}(${inner()},${String(r.int(5))})`;
        case 9: return `MID(${inner()},${String(1 + r.int(4))},${String(r.int(5))})`;
        case 10: return `${r.pick(['FIND', 'SEARCH'])}(${inner()},${inner()})`;
        case 11: return `SUBSTITUTE(${inner()},${inner()},${inner()})`;
        case 12: return `TEXT(${inner()},${r.pick(['"0.00"', '"#,##0"', '"0%"', '"yyyy-mm-dd"', '"h:mm"', '"General"', '"@"', '"0.0E+0"', '"dddd"'])})`;
        case 13: return `${r.pick(['ROUND', 'ROUNDUP', 'ROUNDDOWN', 'TRUNC', 'MOD', 'MROUND', 'CEILING.MATH', 'FLOOR.MATH'])}(${inner()},${r.pick(['0', '1', '2', '-1', '0.5', '3'])})`;
        case 14: return `${r.pick(['MATCH', 'XMATCH'])}(${inner()},${wholeColumn(r)},${r.pick(['0', '1', '-1'])})`;
        case 15: return `XLOOKUP(${inner()},${wholeColumn(r)},${wholeColumn(r)},"none")`;
        case 16: return `VLOOKUP(${inner()},A1:E8,${String(1 + r.int(5))},${r.pick(['FALSE', 'TRUE'])})`;
        case 17: return `INDEX(${range(r)},${String(1 + r.int(3))},${String(1 + r.int(3))})`;
        case 18: return `${r.pick(['DATE', 'TIME'])}(${inner()},${inner()},${inner()})`;
        case 19: return `${r.pick(['EDATE', 'EOMONTH', 'DAYS', 'YEARFRAC', 'NETWORKDAYS', 'WORKDAY'])}(${inner()},${inner()})`;
        case 20: {
            const area = range(r);
            return r.pick([`TEXTJOIN(",",TRUE,${area})`, `SORT(${area})`, `UNIQUE(${area})`, `TRANSPOSE(${area})`, `FILTER(${area},${area}<>"")`, `SEQUENCE(${String(1 + r.int(3))},${String(1 + r.int(3))})`]);
        }
        case 21: return `${r.pick(['LARGE', 'SMALL'])}(${range(r)},${String(1 + r.int(4))})`;
        case 22: return `RANK.EQ(${inner()},${wholeColumn(r)},${r.pick(['0', '1'])})`;
        case 23: return `SWITCH(${inner()},${inner()},${inner()},${inner()})`;
        default: return `${r.pick(['AND', 'OR', 'XOR', 'CHOOSE'])}(${inner()},${inner()})`;
    }
}

/** A grid of typed texts in A1:E8 and Formulas over it in G1:G30, each read back with every spilled cell. */
function request(r: Draw): SheetRequest {
    const culture = r.pick(cultures);
    const entries: [string, string][] = [];
    for (const letter of ['A', 'B', 'C', 'D', 'E']) {
        for (let row = 1; row <= 8; row++) if (r.chance(0.85)) entries.push([`${letter}${String(row)}`, typed(r, culture)]);
    }
    for (let row = 1; row <= 30; row++) entries.push([`G${String(row * 3)}`, `=${formula(r, 1 + r.int(3))}`]);
    const read: string[] = [];
    for (const letter of ['A', 'B', 'C', 'D', 'E', 'G', 'H', 'I', 'J']) for (let row = 1; row <= 92; row++) {
        if (letter <= 'E' && row > 8) break;
        read.push(`${letter}${String(row)}`);
    }
    return { culture, entries, read, ...(r.chance(0.3) ? { width: 1 + r.int(14) } : {}) };
}

test(`whole Sheets against the C# engine, seed ${String(d.seed)}`, async () => {
    expect.hasAssertions();
    const requests = Array.from({ length: 1500 }, () => request(d));
    const found = differences(requests, await oracle.askAll(requests));
    expect({ differences: found.length, first: found.slice(0, 20) }).toEqual({ differences: 0, first: [] });
});
