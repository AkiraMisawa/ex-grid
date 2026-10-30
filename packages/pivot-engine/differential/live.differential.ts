// Live data, held to the C# engine (ADR-0067): a random keyed Snapshot, a random question over it, and a run
// of random Change Batches — records changed in place, removed, added, keys that come back, now and then a
// batch the Snapshot refuses — each followed by the question again and a field's Items, asked as a Pivot Source
// case (tests/cases/README.md, kind "pivotSource"). The C# side is the shared cases' own runner, which
// dotnet/tools/pivot-oracle compiles.
//
// After a batch that changes records, ExGrid.Data reads its rows in its storage's order and the Script Core in
// the Snapshot's, so the answers are read in any order ("order": "any"), and the parts asked for are those no
// order changes: counts, sums and extremes, over numbers whose sums are exact in a double — multiples of an
// eighth well inside 2^53 — and Decimals no sum takes past 96 bits. No double is NaN or infinite: a leaf that
// holds one shows #NUM!, and the sum and extremes it carries beside the flag are those of the numbers met
// before it, which the order decides. The parts a double's order does change are the TypeScript live test's,
// held to a fresh pass over the same Snapshot (test/live.test.ts).

import fs from 'node:fs';
import { afterAll, expect, test } from 'vitest';
import { type Json, type JsonObject } from '../test/cases';
import { sourceAnswer } from '../test/source-cases';
import { firstDifference, normal, random, seedOfRun, startOracle } from './oracle';

const oracle = startOracle();
afterAll(() => { oracle.close(); });

const seed = seedOfRun();
const next = random(seed);
const int = (below: number): number => Math.floor(next() * below);
const chance = (p: number): boolean => next() < p;
function pick<T>(list: readonly T[]): T {
    const chosen = list[int(list.length)];
    if (chosen === undefined) throw new Error('Internal: picked from an empty list.');
    return chosen;
}

// ---- The data -----------------------------------------------------------------------------------

const regions = ['east', 'East', 'EAST', 'West', 'west', '', 'North', 'Ä', 'ä', 'ｱ', 'ア', 'Zeta'];

function region(): Json {
    if (chance(0.1)) return null;
    // Now and then a Region no record had, which a batch brings.
    return chance(0.05) ? `Desk ${int(30)}` : pick(regions);
}

function decimal(): Json {
    if (chance(0.1)) return null;
    const digits = String(int(10 ** (1 + int(8))));
    const scale = int(Math.min(4, digits.length) + 1);
    const written = scale === 0 ? digits : `${digits.length <= scale ? '0' : digits.slice(0, digits.length - scale)}.${digits.slice(-scale).padStart(scale, '0')}`;
    return { decimal: (chance(0.3) ? '-' : '') + written };
}

function double(): Json {
    if (chance(0.1)) return null;
    return (int(1 << 20) - (1 << 18)) / 8;
}

function integer(): Json {
    if (chance(0.1)) return null;
    return int(200) - 60;
}

function date(): Json {
    if (chance(0.1)) return null;
    return { date: new Date(Date.UTC(2025, 0, 1) + int(800) * 86_400_000).toISOString().slice(0, 10) };
}

function boolean(): Json {
    return chance(0.12) ? null : chance(0.5);
}

const columns: readonly (readonly [string, string, () => Json])[] = [
    ['Region', 'text', region], ['Product', 'text', () => (chance(0.1) ? null : pick(['Apples', 'Pears', 'plums', 'Plums']))],
    ['Amount', 'decimal', decimal], ['Risk', 'double', double], ['Units', 'integer', integer], ['When', 'date', date], ['Live', 'boolean', boolean],
];
const fields = columns.map(([name]) => name);

function record(id: number): Json[] {
    return [id, ...columns.map(([, , make]) => make())];
}

// ---- The question -------------------------------------------------------------------------------

function question(): JsonObject {
    const free = [...fields];
    const take = (): string => {
        const field = pick(free);
        free.splice(free.indexOf(field), 1);
        return field;
    };
    const placed = (): Json => {
        const field = take();
        if (!chance(0.25)) return field;
        return { field, hiddenItems: field === 'Region' || field === 'Product' ? [{ kind: 'text', value: pick(['East', 'Apples', 'West', 'Zeta']) }] : [{ kind: 'blank' }] };
    };
    const values = Array.from({ length: 1 + int(3) }, () => {
        const field = pick(fields);
        const parts = ['sum', 'extremes'].filter(() => chance(0.6));
        return parts.length === 0 && chance(0.5) ? field : { field, parts };
    });
    return {
        rows: Array.from({ length: int(3) }, placed),
        columns: Array.from({ length: int(2) }, placed),
        filters: Array.from({ length: int(2) }, placed),
        values,
        ...(chance(0.1) ? { maxLeaves: 1 + int(12) } : {}),
    };
}

// ---- The batches --------------------------------------------------------------------------------

// A batch over the records held, which `held` is brought to: some changed, some removed, some keys that come
// back, some new; now and then one the Snapshot refuses, which changes nothing.
function batch(held: Map<number, Json[]>, fresh: () => number): JsonObject {
    const keys = [...held.keys()];
    if (keys.length > 0 && chance(0.05)) return pick([{ removedKeys: [100_000 + int(10)] }, { added: [record(pick(keys))] }]);
    const touched = new Set(Array.from({ length: Math.min(keys.length, int(12)) }, () => pick(keys)));
    const removed = [...touched].filter(() => chance(0.4));
    const changed = [...touched].filter(key => !removed.includes(key)).map(key => record(key));
    const again = removed.filter(() => chance(0.3));
    const added = [...again.map(key => record(key)), ...Array.from({ length: int(8) }, () => record(fresh()))];
    for (const key of removed) held.delete(key);
    for (const row of [...changed, ...added]) held.set(Number(row[0]), row);
    return {
        ...(added.length === 0 ? {} : { added }),
        ...(changed.length === 0 ? {} : { changed }),
        ...(removed.length === 0 ? {} : { removedKeys: removed }),
    };
}

test(`Live data, seed ${seed}`, async () => {
    expect.hasAssertions();
    const file: JsonObject = { locale: 'en-US', words: 'english' };
    const requests: Record<string, unknown>[] = [];
    const mine: Json[] = [];
    for (let i = 0; i < 200; i++) {
        let id = 0;
        const fresh = (): number => ++id;
        const rows = Array.from({ length: int(80) }, () => record(fresh()));
        const held = new Map(rows.map(row => [Number(row[0]), row]));
        let asked = question();
        const asks: Json[] = [{ aggregate: asked }];
        for (let b = 0; b < 1 + int(8); b++) {
            asks.push({ apply: batch(held, fresh) });
            if (chance(0.15)) asked = question();
            asks.push({ aggregate: asked, order: 'any' });
            if (chance(0.3)) asks.push({ items: pick(fields), under: 'current' });
        }
        const entry: JsonObject = {
            name: `random ${i}`,
            data: { snapshot: { recordKey: 'Id', columns: [{ name: 'Id', kind: 'integer' }, ...columns.map(([name, kind]) => ({ name, kind }))] }, rows },
            asks,
        };
        requests.push({ op: 'case', kind: 'pivotSource', file, case: entry });
        mine.push(await sourceAnswer(file, entry));
    }
    const answers = await oracle.askAll(requests);
    const differences: string[] = [];
    answers.forEach((answer, i) => {
        if (JSON.stringify(normal(answer)) !== JSON.stringify(normal(mine[i]))) {
            differences.push(`${firstDifference(normal(answer), normal(mine[i]), '')}\n  in ${JSON.stringify(requests[i])}`);
        }
    });
    // EXGRID_PIVOT_DIFF_OUT names a file each differing case is written to, a line each, to be made a case of.
    const out = process.env['EXGRID_PIVOT_DIFF_OUT'];
    if (out !== undefined) fs.writeFileSync(out, answers.flatMap((answer, i) => (JSON.stringify(normal(answer)) === JSON.stringify(normal(mine[i])) ? [] : [JSON.stringify(requests[i])])).join('\n'));
    expect({ differences: differences.length, first: differences.slice(0, 5) }).toEqual({ differences: 0, first: [] });
});
