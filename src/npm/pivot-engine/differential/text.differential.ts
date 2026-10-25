// Text compared as the C# engine compares it, held to .NET: OrdinalIgnoreCase, ToUpperInvariant,
// Uri.EscapeDataString and each culture's collation ignoring case.

import { afterAll, expect, test } from 'vitest';
import { pivotLocales } from '../src/culture/culture-data';
import { compareOrdinal, escapeDataString, foldedText, upperInvariant } from '../src/text/casing';
import { compareInCulture } from '../src/text/collation';
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

const alphabet = [...Array.from('aAbBzZ09 -_.~!*()\'%éÉèeEàÀçÇñÑüÜößẞæÆøØåÅıIiİſsSŉǅǆǄΣσςЖжあアｱかがカガー漢字한글'), 'ﬀ', '\u0301', '\uD801\uDC00', '\uD801\uDC28', '\uD83D\uDE00', '\u00A0', '\u2212'];

// What a culture orders is held on text a report holds: Latin with its accents and cases, Greek and Cyrillic,
// and Japanese, whose katakana and width .NET tailors (collation.ts), with full-width Latin. A combining mark
// standing alone, and a full-width letter beside a contraction (Danish aa), are left out: the tailoring is
// found character by character.
const pools = [
    Array.from('aAbBzZ09 -_.~!()\'%éÉèeEàÀçÇñÑüÜößæÆøØåÅIiSs'),
    Array.from('ΣσςΑαЖжЯяёЁ -1'),
    Array.from('あいうかがきくけこさアイウカガキクケコサァぁヶヵーｰｱｶｷｸﾅ漢字月１２1 ・'),
    Array.from('bBdD1１ＢＤｂｄ-ー漢'),
];

function randomText(): string {
    if (next() < 0.1) {
        // Any code point but a lone surrogate, which JSON cannot carry to .NET.
        const codePoint = int(0x2FFFF);
        return codePoint >= 0xD800 && codePoint <= 0xDFFF ? 'x' : String.fromCodePoint(codePoint);
    }
    return Array.from({ length: int(6) }, () => pick(alphabet)).join('');
}

function collatedText(pool: readonly string[]): string {
    return Array.from({ length: 1 + int(5) }, () => pick(pool)).join('');
}

function variant(text: string): string {
    return Array.from(text).map((ch) => (next() < 0.5 ? ch.toUpperCase() : ch.toLowerCase())).join('');
}

test(`casing, escaping and collation, seed ${seed}`, async () => {
    expect.hasAssertions();
    const locales = pivotLocales();
    const requests: Record<string, unknown>[] = [];
    const mine: unknown[] = [];
    for (let i = 0; i < 20_000; i++) {
        const kind = int(4);
        const pool = pick(pools);
        const a = kind === 3 ? collatedText(pool) : randomText();
        const b = next() < 0.5 ? variant(a) : kind === 3 ? collatedText(pool) : randomText();
        switch (kind) {
            case 0:
                requests.push({ op: 'fold', a, b });
                mine.push({ equal: foldedText(a) === foldedText(b) });
                break;
            case 1:
                requests.push({ op: 'upper', text: a });
                mine.push({ text: upperInvariant(a) });
                break;
            case 2:
                requests.push({ op: 'escape', text: a });
                mine.push({ text: escapeDataString(a) });
                break;
            default: {
                const culture = pick(locales);
                requests.push({ op: 'compare', a, b, culture });
                mine.push({ ignoreCase: compareInCulture(culture, a, b) });
                break;
            }
        }
    }
    void compareOrdinal;
    const answers = await oracle.askAll(requests);
    const differences: string[] = [];
    answers.forEach((answer, i) => {
        if (JSON.stringify(answer) !== JSON.stringify(mine[i])) differences.push(`${JSON.stringify(requests[i])}: .NET ${JSON.stringify(answer)}, ours ${JSON.stringify(mine[i])}`);
    });
    expect({ differences: differences.length, first: differences.slice(0, 60) }).toEqual({ differences: 0, first: [] });
});
