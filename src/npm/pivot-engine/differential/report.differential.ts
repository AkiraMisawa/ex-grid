// Whole reports, held to the C# engine: random data — a Snapshot of every kind of column, or records whose
// values are of any kind — and random layouts over it, each asked as a Pivot Source case (tests/cases/README.md,
// kind "pivotSource"): the Leaf Aggregates of the layout's question, the report read back with every reading,
// and a field's Items. The C# side is the shared cases' own runner, which tools/pivot-oracle compiles, so a
// difference here is one a written case would show.

import fs from 'node:fs';
import { afterAll, expect, test } from 'vitest';
import { pivotAggregations, type PivotAggregation } from '../src/index';
import { array, must, object, type Json, type JsonObject } from '../test/cases';
import { sourceAnswer } from '../test/source-cases';
import { random, seedOfRun, startOracle } from './oracle';

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
function some<T>(list: readonly T[], most: number): T[] {
    const chosen = list.filter(() => chance(0.5));
    return chosen.slice(0, most);
}

// ---- The data -----------------------------------------------------------------------------------

const texts = ['east', 'East', 'West', 'west', '', 'North', 'Ä', 'ä', 'ｱ', 'ア', 'a b', '10', '9', 'Zeta'];

function text(): Json {
    return chance(0.12) ? null : pick(texts);
}

function decimal(): Json {
    if (chance(0.1)) return null;
    if (chance(0.05)) return { decimal: pick(['79228162514264337593543950335', '-79228162514264337593543950335', '12345678901234567890.123']) };
    const digits = String(int(10 ** (1 + int(9))));
    const scale = int(Math.min(6, digits.length) + 1);
    const written = scale === 0 ? digits : `${digits.length <= scale ? '0' : digits.slice(0, digits.length - scale)}.${digits.slice(-scale).padStart(scale, '0')}`;
    return { decimal: (chance(0.3) ? '-' : '') + written };
}

function double(): Json {
    if (chance(0.1)) return null;
    if (chance(0.06)) return { double: pick(['NaN', 'Infinity', '-Infinity', '-0']) };
    if (chance(0.05)) return pick([5e-324, 1e300, -1e300, 0.1, 1 / 3]);
    // A -0 is written as JSON keeps it: JSON.stringify writes -0 as 0, which the oracle would read.
    const number = Math.round((next() - 0.4) * 10 ** int(6)) / 10 ** int(4);
    return Object.is(number, -0) ? { double: '-0' } : number;
}

function integer(): Json {
    if (chance(0.1)) return null;
    if (chance(0.05)) return { integer: pick(['9223372036854775807', '-9223372036854775808']) };
    return int(200) - 60;
}

function date(): Json {
    if (chance(0.1)) return null;
    const day = new Date(Date.UTC(2025, 0, 1) + int(800) * 86_400_000).toISOString().slice(0, 10);
    return { date: chance(0.15) ? `${day}T${String(int(24)).padStart(2, '0')}:${pick(['00', '30', '45'])}:00` : day };
}

function boolean(): Json {
    return chance(0.12) ? null : chance(0.5);
}

// A value of any kind, as a record whose fields hold what they are given carries it (ADR-0060).
function anything(): Json {
    return pick([text, decimal, double, integer, date, boolean])();
}

interface Shape {
    readonly data: JsonObject;
    readonly declare?: readonly Json[];
    readonly fields: readonly string[];
    readonly dates: readonly string[];
}

function snapshotShape(): Shape {
    // Each column: its name, its kind, the type of the field over it, and its values.
    const columns: [string, string, string, () => Json][] = [
        ['Region', 'text', 'text', text], ['Product', 'text', 'text', () => (chance(0.1) ? null : pick(['Apples', 'Pears', 'plums', 'Plums']))],
        ['Amount', 'decimal', 'number', decimal], ['Risk', 'double', 'number', double], ['Units', 'integer', 'number', integer],
        ['When', 'date', 'date', date], ['Live', 'boolean', 'boolean', boolean],
    ];
    const rows = Array.from({ length: int(60) }, () => columns.map(([, , , make]) => make()));
    const data = { snapshot: { columns: columns.map(([name, kind]) => ({ name, kind, ...(chance(0.2) ? { caption: `${name} caption` } : {}) })) }, rows };
    const names = columns.map(([name]) => name);
    if (!chance(0.4)) return { data, fields: names, dates: ['When'] };
    const declare: Json[] = columns.map(([name, kind, type]) => ({ name, type, ...(kind === 'decimal' && chance(0.5) ? { format: '#,##0.00' } : {}) }));
    const parts = some(['year', 'quarter', 'month'], 3);
    declare.push(...parts.map(part => ({ name: `When ${part}`, type: 'date', column: 'When', datePart: part })));
    return { data, declare, fields: [...names, ...parts.map(part => `When ${part}`)], dates: ['When'] };
}

function recordShape(): Shape {
    const fields = [
        { name: 'Desk', type: 'text' }, { name: 'Book', type: 'text', caption: 'Trading book' }, { name: 'Amount', type: 'number', format: '#,##0.00' },
        { name: 'Risk', type: 'number' }, { name: 'Trade', type: 'date', format: 'yyyy-MM-dd' }, { name: 'Tag', type: 'text' },
    ];
    const records = Array.from({ length: int(50) }, () => [
        text(), chance(0.1) ? null : pick(['LDN-1', 'NY-2', 'ny-2', 'TKY-3']), chance(0.85) ? pick([decimal, double, integer])() : anything(),
        chance(0.85) ? double() : anything(), chance(0.9) ? date() : anything(), anything(),
    ]);
    return { data: { columns: fields.map(f => f.name), fields, records }, fields: fields.map(f => f.name), dates: ['Trade'] };
}

// ---- The layout ---------------------------------------------------------------------------------

function sort(values: number): Json {
    const direction = pick(['ascending', 'descending']);
    return values > 0 && chance(0.4) ? { direction, byValue: int(values) } : { direction };
}

// A placement, with Hidden Items and toggled Items from the Items the field has.
function placement(field: string, keys: readonly Json[], values: number): JsonObject {
    const placed: Record<string, Json> = { field };
    if (keys.length > 0 && chance(0.3)) placed['hiddenItems'] = some(keys, Math.max(1, keys.length - 1));
    if (chance(0.3)) placed['sort'] = sort(values);
    if (chance(0.2)) placed['subtotals'] = false;
    if (chance(0.15)) placed['collapsed'] = true;
    if (keys.length > 0 && chance(0.15)) placed['toggledItems'] = some(keys, 2);
    return placed;
}

const showAs = ['noCalculation', 'percentOfGrandTotal', 'percentOfColumnTotal', 'percentOfRowTotal'];

function layoutOver(shape: Shape, itemsOf: (field: string) => readonly Json[]): JsonObject {
    const free = [...shape.fields];
    const take = (): string => {
        const field = pick(free);
        free.splice(free.indexOf(field), 1);
        return field;
    };
    const values = Array.from({ length: int(4) }, () => ({
        field: pick(shape.fields),
        aggregation: pick<PivotAggregation>(pivotAggregations),
        ...(chance(0.15) ? { showValuesAs: pick(showAs) } : {}),
        ...(chance(0.15) ? { numberFormat: pick(['#,##0.00', '0%', '0.0', 'N2', '0.00E+00']) } : {}),
    }));
    const area = (most: number): JsonObject[] => Array.from({ length: int(most + 1) }, () => take()).map(f => placement(f, itemsOf(f), values.length));
    const layout: Record<string, Json> = { rows: area(3), columns: area(2), filters: area(1), values };
    if (chance(0.5)) layout['form'] = pick(['compact', 'outline', 'tabular']);
    if (values.length > 1 && chance(0.3)) layout['valuesAxis'] = 'rows';
    if (chance(0.2)) layout['subtotalsAtTop'] = false;
    if (chance(0.15)) layout['grandTotalRow'] = false;
    if (chance(0.15)) layout['grandTotalColumn'] = false;
    if (chance(0.2)) layout['repeatItemLabels'] = true;
    return layout;
}

// The Items a field has, as the Script Core's source lists them: a layout's Hidden Items are drawn from them.
async function items(file: JsonObject, shape: Shape, field: string): Promise<readonly Json[]> {
    const answered = await sourceAnswer(file, { name: 'items', data: shape.data, ...(shape.declare === undefined ? {} : { declare: shape.declare }), asks: [{ aggregate: {} }, { items: field }] });
    const [, page] = array(answered);
    if (page === undefined) throw new Error(`The source answered ${JSON.stringify(answered)}, not a page of Items.`);
    return array(must(object(page), 'items'));
}

test(`Whole reports, seed ${seed}`, async () => {
    expect.hasAssertions();
    const requests: Record<string, unknown>[] = [];
    const mine: Json[] = [];
    for (let i = 0; i < 600; i++) {
        const file: JsonObject = { locale: pick(['en-US', 'ja-JP', 'de-DE', 'fr-FR', 'en-GB']), words: pick(['english', 'japanese', 'unitedKingdom']) };
        const shape = chance(0.5) ? snapshotShape() : recordShape();
        const known = new Map<string, readonly Json[]>();
        for (const field of shape.fields) known.set(field, await items(file, shape, field));
        const layout = layoutOver(shape, field => {
            const listed = known.get(field);
            if (listed === undefined) throw new Error(`Internal: the Items of '${field}' were not listed.`);
            return listed;
        });
        const entry: JsonObject = {
            name: `random ${i}`,
            data: shape.data,
            ...(shape.declare === undefined ? {} : { declare: shape.declare }),
            asks: [
                { aggregate: { rows: array(must(layout, 'rows')).map(p => must(object(p), 'field')) } },
                { report: layout, also: ['names', 'keys', 'numbers', 'captions', 'empty', 'toggles', 'columns', 'tiers'] },
                { items: pick(shape.fields), search: pick(['', 'e', 'A', '2025']) },
            ],
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

// Where two answers first part, and what each holds there.
function firstDifference(theirs: unknown, ours: unknown, at: string): string {
    if (JSON.stringify(theirs) === JSON.stringify(ours)) return '';
    if (typeof theirs === 'object' && theirs !== null && typeof ours === 'object' && ours !== null && Array.isArray(theirs) === Array.isArray(ours)) {
        const keys = new Set([...Object.keys(theirs), ...Object.keys(ours)]);
        for (const key of keys) {
            const found = firstDifference(Reflect.get(theirs, key), Reflect.get(ours, key), `${at}/${key}`);
            if (found !== '') return found;
        }
    }
    return `${at}: C# ${JSON.stringify(theirs)}, ours ${JSON.stringify(ours)}`;
}

// A JSON value with its objects' keys in order, so two answers compare by what they hold.
function normal(value: unknown): unknown {
    if (Array.isArray(value)) return value.map(normal);
    if (typeof value !== 'object' || value === null) return value;
    return Object.fromEntries(Object.entries(value).toSorted(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)).map(([k, v]) => [k, normal(v)]));
}
