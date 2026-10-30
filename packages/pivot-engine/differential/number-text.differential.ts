// .NET's number formats, held to .NET: random doubles and decimals in random standard and custom formats,
// under every culture of the table, formatted by number-text.ts and by dotnet/tools/pivot-oracle.

import { afterAll, expect, test } from 'vitest';
import { cultureOf, pivotLocales } from '../src/culture/culture-data';
import { formatDecimal, formatDouble } from '../src/text/number-text';
import { PivotFormatError } from '../src/errors';
import { random, seedOfRun, startOracle } from './oracle';

const oracle = startOracle();
afterAll(() => { oracle.close(); });

const seed = seedOfRun();
const next = random(seed);
const pick = <T>(list: readonly T[]): T => {
    const chosen = list[Math.floor(next() * list.length)];
    if (chosen === undefined) throw new Error('Internal: picked from an empty list.');
    return chosen;
};
const int = (below: number): number => Math.floor(next() * below);

function randomDouble(): number {
    switch (int(8)) {
        case 0: {
            const bits = new DataView(new ArrayBuffer(8));
            bits.setUint32(0, int(2 ** 32));
            bits.setUint32(4, int(2 ** 32));
            const value = bits.getFloat64(0);
            return Number.isFinite(value) ? value : 1.5;
        }
        case 1: return (int(2_000_001) - 1_000_000) / 8;
        case 2: return (int(2_000_001) - 1_000_000) / 1000;
        case 3: return pick([0, -0, 0.5, 1.5, 2.5, -2.5, 0.125, 0.375, 1e15, 1e16, 1e17, 123456789012345680, 1e-5, 0.0001, 5e-324, 1.7976931348623157e308]);
        case 4: return (next() - 0.5) * 10 ** (int(40) - 20);
        case 5: return Number((next() * 1000).toFixed(int(6)));
        case 6: return int(1_000_000_000) * pick([1, -1]) / 10 ** int(10);
        default: return (int(20001) - 10000) / 100 + 0.005;
    }
}

function randomDecimal(): string {
    const digits = Array.from({ length: 1 + int(28) }, () => int(10)).join('').replace(/^0+(?=\d)/, '');
    const scale = int(Math.min(28, digits.length) + 1);
    const text = scale === 0 ? digits : `${digits.length === scale ? '0' : digits.slice(0, digits.length - scale)}.${digits.slice(digits.length - scale)}`;
    return (next() < 0.4 ? '-' : '') + text;
}

const standardLetters = ['C', 'c', 'E', 'e', 'F', 'f', 'G', 'g', 'N', 'n', 'P', 'p', 'R', 'r', 'D', 'X', 'Z'];
const customPieces = ['#', '0', '0', '#', ',', '.', '%', '‰', 'E+0', 'E-00', 'e0', ';', "'x'", '"a;b"', '\\#', ' ', '-', '(', ')', '$', 'E', 'E+', ','];

function randomFormat(): string | undefined {
    switch (int(6)) {
        case 0: return undefined;
        case 1:
        case 2: {
            const letter = pick(standardLetters);
            const precision = int(4);
            return precision === 0 ? letter : `${letter}${int(precision === 1 ? 10 : 31)}`;
        }
        case 3: return pick(['#,##0', '#,##0.00', '0.00', '0.0%', '0', '0%', '#,##0,', '0.000000000000000000', '#,##0.00;(#,##0.00)', '0.00;-0.00;"zero"', '#,#', '00.##', '#.##E+00', '0E0', '###,###,##0.###']);
        default: return Array.from({ length: 1 + int(8) }, () => pick(customPieces)).join('');
    }
}

function hex(value: number): string {
    const bits = new DataView(new ArrayBuffer(8));
    bits.setFloat64(0, value);
    return bits.getBigUint64(0).toString(16).padStart(16, '0');
}

function ours(run: () => string): unknown {
    try {
        return { text: run() };
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- .NET's FormatException is an answer the oracle gives too
        if (error instanceof PivotFormatError) return { error: 'FormatException' };
        throw error;
    }
}

test(`.NET's number formats, seed ${seed}`, async () => {
    expect.hasAssertions();
    const locales = pivotLocales();
    const requests: Record<string, unknown>[] = [];
    const mine: unknown[] = [];
    for (let i = 0; i < 40_000; i++) {
        const culture = pick(locales);
        const format = randomFormat();
        const data = cultureOf(culture).data.number;
        if (next() < 0.5) {
            const value = randomDouble();
            requests.push({ op: 'number', kind: 'double', value: hex(value), format: format === undefined ? null : format, culture });
            mine.push(ours(() => formatDouble(value, format, data)));
        } else {
            const text = randomDecimal();
            const exact = { mantissa: BigInt(text.replace('.', '')), scale: text.includes('.') ? text.length - text.indexOf('.') - 1 : 0 };
            requests.push({ op: 'number', kind: 'decimal', value: text, format: format === undefined ? null : format, culture });
            mine.push(ours(() => formatDecimal(exact, format, data)));
        }
    }
    const answers = await oracle.askAll(requests);
    const differences: string[] = [];
    answers.forEach((answer, i) => {
        const expected = 'error' in answer ? { error: answer['error'] } : { text: answer['text'] };
        if (JSON.stringify(expected) !== JSON.stringify(mine[i])) {
            differences.push(`${JSON.stringify(requests[i])}: .NET ${JSON.stringify(expected)}, ours ${JSON.stringify(mine[i])}`);
        }
    });
    expect({ differences: differences.length, first: differences.slice(0, 40) }).toEqual({ differences: 0, first: [] });
});
