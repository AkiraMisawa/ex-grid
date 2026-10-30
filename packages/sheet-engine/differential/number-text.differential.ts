// Number to text, held to the C# engine: random numbers under random Number Formats, read at no width and at
// random column widths under every culture the Script Core holds, and the same numbers through TEXT and `&`.

import { afterAll, expect, test } from 'vitest';
import { cultures, draw, exactish, literal, type Draw } from './draw';
import { startOracle } from './oracle';
import { differences, type SheetRequest } from './sheet';

const oracle = startOracle();
afterAll(() => { oracle.close(); });

const d = draw();

const known = [
    'General', '0', '0.0', '0.00', '0.000', '#,##0', '#,##0.00', '0%', '0.00%', '0.00E+00', '##0.0E+0', '0.0E-0',
    '# ?/?', '# ??/??', '?/8', '0.0,,"M"', '#,##0,', '[Red]0.00;[Blue]-0.00', '0.00;-0.00;"zero";@', '"$"#,##0.00_);("$"#,##0.00)',
    '$#,##0.00_);[Red]($#,##0.00)', '0.000000000000000', '#.##############', '000000', '@', 'yyyy-mm-dd', 'dd/mm/yyyy hh:mm:ss',
    'h:mm AM/PM', '[h]:mm:ss', 'mmm d, yyyy', 'dddd', 'mm:ss.0', 'd-mmm-yy', 'yyyy"年"m"月"d"日"', '[$-409]mmmm d, yyyy',
    '#,##0.00 [$€-407]', '0.00\\ "kg"', '* #,##0', '#,##0_-;\\-#,##0_-', '[<1000]0;[>=1000]#,##0,"K"', '0.0#', '#.#', '.00', '#,###',
];
const pieces = ['0', '0', '#', '?', ',', '.', '%', 'E+', 'E-', '00', '"x"', '\\-', ' ', '_)', '*', ';', '[Red]', '/', 'y', 'm', 'd', 'h', 's', 'AM/PM', '[h]', '.0', '@', '(', ')', '$'];

function format(r: Draw): string {
    return r.chance(0.6) ? r.pick(known) : Array.from({ length: 1 + r.int(7) }, () => r.pick(pieces)).join('');
}

/** A number as typed into a cell: a Formula, so every double is reachable, and now and then a date or time. */
function number(r: Draw): string {
    switch (r.int(5)) {
        case 0: return `=${r.pick(['', '-'])}${literal(r)}`;
        case 1: return `=${r.pick(['', '-'])}${exactish(r)}`;
        case 2: return `=${String(r.int(2_958_466))}${r.chance(0.5) ? `+${String(r.int(86_400))}/86400` : ''}`;
        case 3: return `=${String(r.int(1000))}/${String(1 + r.int(1000))}`;
        default: return `=${r.pick(['', '-'])}${literal(r)}*10^${String(r.int(41) - 20)}`;
    }
}

/** A Sheet of numbers in A, each with a format of its own, and TEXT and `&` of each beside it. */
function request(r: Draw): SheetRequest {
    const entries: [string, string][] = [];
    const formats: [string, string][] = [];
    const rows = 1 + r.int(30);
    for (let row = 1; row <= rows; row++) {
        const code = format(r);
        entries.push([`A${String(row)}`, number(r)]);
        formats.push([`A${String(row)}`, code]);
        entries.push([`B${String(row)}`, `=TEXT(A${String(row)},"${code.replaceAll('"', '""')}")`]);
        entries.push([`C${String(row)}`, `=A${String(row)}&""`]);
    }
    const read = entries.map(([address]) => address);
    return { culture: r.pick(cultures), entries, formats, read, ...(r.chance(0.5) ? { width: 1 + r.int(16) + r.int(4) / 4 } : {}) };
}

test(`number to text against the C# engine, seed ${String(d.seed)}`, async () => {
    expect.hasAssertions();
    const requests = Array.from({ length: 2000 }, () => request(d));
    const found = differences(requests, await oracle.askAll(requests));
    expect({ differences: found.length, first: found.slice(0, 20) }).toEqual({ differences: 0, first: [] });
});
