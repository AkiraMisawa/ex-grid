// General, held to the C# engine's PivotGeneral: random doubles and decimals under every culture.

import { afterAll, expect, test } from 'vitest';
import { cultureOf, pivotLocales } from '../src/culture/culture-data';
import { generalOfDecimal, generalOfDouble } from '../src/text/general';
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
    switch (int(5)) {
        case 0: {
            const bits = new DataView(new ArrayBuffer(8));
            bits.setUint32(0, int(2 ** 32));
            bits.setUint32(4, int(2 ** 32));
            return bits.getFloat64(0);
        }
        case 1: return (next() - 0.5) * 10 ** (int(30) - 15);
        case 2: return pick([0, -0, Infinity, -Infinity, NaN, 1 / 3, 2 / 3, Math.SQRT2, 123456789012, 99999999999.5, 0.000012345678, 1e-5, 5e-324]);
        case 3: return int(1_000_000_000_000) / 10 ** int(16);
        default: return Number((next() * 10 ** int(14)).toPrecision(1 + int(17)));
    }
}

function randomDecimal(): string {
    const digits = Array.from({ length: 1 + int(28) }, () => int(10)).join('').replace(/^0+(?=\d)/, '');
    const scale = int(Math.min(28, digits.length) + 1);
    const text = scale === 0 ? digits : `${digits.length === scale ? '0' : digits.slice(0, digits.length - scale)}.${digits.slice(digits.length - scale)}`;
    return (next() < 0.4 ? '-' : '') + text;
}

function hex(value: number): string {
    const bits = new DataView(new ArrayBuffer(8));
    bits.setFloat64(0, value);
    return bits.getBigUint64(0).toString(16).padStart(16, '0');
}

test(`General, seed ${seed}`, async () => {
    expect.hasAssertions();
    const locales = pivotLocales();
    const requests: Record<string, unknown>[] = [];
    const mine: string[] = [];
    for (let i = 0; i < 40_000; i++) {
        const culture = pick(locales);
        const data = cultureOf(culture).data.number;
        if (next() < 0.5) {
            const value = randomDouble();
            requests.push({ op: 'general', kind: 'double', value: hex(value), culture });
            mine.push(generalOfDouble(value, data));
        } else {
            const text = randomDecimal();
            const exact = { mantissa: BigInt(text.replace('.', '')), scale: text.includes('.') ? text.length - text.indexOf('.') - 1 : 0 };
            requests.push({ op: 'general', kind: 'decimal', value: text, culture });
            mine.push(generalOfDecimal(exact, data));
        }
    }
    const answers = await oracle.askAll(requests);
    const differences: string[] = [];
    answers.forEach((answer, i) => {
        if (answer['text'] !== mine[i]) differences.push(`${JSON.stringify(requests[i])}: C# ${JSON.stringify(answer)}, ours ${JSON.stringify(mine[i])}`);
    });
    expect({ differences: differences.length, first: differences.slice(0, 40) }).toEqual({ differences: 0, first: [] });
});
