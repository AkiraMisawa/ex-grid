// .NET's date formats, held to .NET: random clock values in random standard and custom formats, under
// every culture of the table, formatted by date-text.ts and by dotnet/tools/pivot-oracle.

import { afterAll, expect, test } from 'vitest';
import { cultureOf, pivotLocales } from '../src/culture/culture-data';
import { formatDate } from '../src/text/date-text';
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
const maxTicks = 3_155_378_975_999_999_999n;

function randomTicks(): bigint {
    switch (int(4)) {
        case 0: return BigInt(int(2 ** 31)) * BigInt(int(2 ** 31)) % maxTicks;
        case 1: return 638_000_000_000_000_000n + BigInt(int(2 ** 30)) * 864_000_000n;
        case 2: return (638_000_000_000_000_000n + BigInt(int(100_000)) * 864_000_000_000n);
        default: return pick([0n, maxTicks, 864_000_000_000n * 59n, 630_822_816_000_000_000n]);
    }
}

const standards = ['d', 'D', 'f', 'F', 'g', 'G', 'm', 'M', 'o', 'O', 'r', 'R', 's', 't', 'T', 'u', 'U', 'y', 'Y', 'x', 'Q'];
const pieces = ['d', 'dd', 'ddd', 'dddd', 'M', 'MM', 'MMM', 'MMMM', 'y', 'yy', 'yyy', 'yyyy', 'yyyyy', 'h', 'hh', 'H', 'HH', 'm', 'mm',
    's', 'ss', 'f', 'ff', 'fff', 'fffffff', 'ffffffff', 'F', 'FF', 'FFFFFFF', 't', 'tt', 'g', 'gg', 'z', 'zz', 'zzz', 'K', ':', '/', '-', ' ',
    '.', "'at'", '"x\\"y"', '\\d', '%d', '%', "'open", 'T', ','];

function randomFormat(): string | undefined {
    switch (int(5)) {
        case 0: return undefined;
        case 1: return pick(standards);
        case 2: return pick(['yyyy-MM-dd', 'yyyy-MM', 'dd MMMM yyyy', 'd MMMM', 'MMMM yyyy', 'HH:mm:ss.FFF', 'ss.FFFFFFF', 'h:mm tt', 'dddd, d MMMM yyyy']);
        default: return Array.from({ length: 1 + int(6) }, () => pick(pieces)).join('');
    }
}

function ours(run: () => string): unknown {
    try {
        return { text: run() };
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- .NET's FormatException is an answer the oracle gives too
        if (error instanceof PivotFormatError) return { error: 'FormatException' };
        throw error;
    }
}

test(`.NET's date formats, seed ${seed}`, async () => {
    expect.hasAssertions();
    const locales = pivotLocales();
    const requests: Record<string, unknown>[] = [];
    const mine: unknown[] = [];
    for (let i = 0; i < 30_000; i++) {
        const culture = pick(locales);
        const format = randomFormat();
        const ticks = randomTicks();
        requests.push({ op: 'date', ticks: ticks.toString(), format: format === undefined ? null : format, culture });
        mine.push(ours(() => formatDate(ticks, format, cultureOf(culture).data.date)));
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
