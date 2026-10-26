// Writes src/showcases/data.ts from ExChart.Showcases' fixed values (ADR-0280), so the React host's
// Showcases show the figures the Blazor hosts show, from one record. The C# files are the record; this reads
// their records and their array initialisers, which hold only literals, and writes them out as TypeScript.
// Run `node tools/showcase-data.mjs` after a change to samples/ExChart.Showcases/Data, and commit what it writes.
import { readdirSync, readFileSync, writeFileSync } from 'node:fs';
import { fileURLToPath } from 'node:url';

const at = (path) => fileURLToPath(new URL(path, import.meta.url));
const dataDir = at('../../ExChart.Showcases/Data/');
const out = at('../src/showcases/data.ts');

const camel = (name) => name[0].toLowerCase() + name.slice(1);

// The records: each one's fields in constructor order, with their C# types.
const records = new Map();
const recordsText = readFileSync(`${dataDir}Records.cs`, 'utf8');
for (const [, name, params] of recordsText.matchAll(/public sealed record (\w+)\(([^)]*)\)/g)) {
    const fields = params.split(',').map(p => p.trim()).filter(p => p !== '').map(p => {
        const m = /^(.+?)\s+(\w+)$/.exec(p);
        if (m === null) throw new Error(`Record ${name}: cannot read the parameter '${p}'.`);
        return { type: m[1], name: m[2] };
    });
    records.set(name, fields);
}

function tsType(type) {
    if (type.endsWith('?')) return `${tsType(type.slice(0, -1))} | null`;
    if (type.endsWith('[]')) return `readonly (${tsType(type.slice(0, -2))})[]`;
    const list = /^IReadOnlyList<(.+)>$/.exec(type);
    if (list !== null) return `readonly (${tsType(list[1])})[]`;
    switch (type) {
        case 'string': return 'string';
        case 'double': case 'decimal': case 'int': return 'number';
        case 'DateOnly': return 'Temporal.PlainDate';
        default:
            if (records.has(type)) return type;
            throw new Error(`No TypeScript type is known for ${type}.`);
    }
}

// A tokenizer and parser for the initialisers' literals.
function tokens(text) {
    const list = [];
    const re = /\s+|\/\/[^\n]*|"((?:[^"\\]|\\.)*)"|(-?\d+(?:\.\d+)?(?:[eE][-+]?\d+)?)([mMdD]?)|(\w+)|([[\](),:])/gy;
    let at = 0;
    while (at < text.length) {
        re.lastIndex = at;
        const m = re.exec(text);
        if (m === null) throw new Error(`Cannot read the initialiser at '${text.slice(at, at + 40)}'.`);
        at = re.lastIndex;
        if (m[1] !== undefined) list.push({ kind: 'string', value: JSON.parse(`"${m[1]}"`) });
        else if (m[2] !== undefined) list.push({ kind: 'number', value: m[2] });
        else if (m[4] !== undefined) list.push({ kind: 'word', value: m[4] });
        else if (m[5] !== undefined) list.push({ kind: 'punct', value: m[5] });
    }
    return list;
}

function parse(list, type) {
    let i = 0;
    const take = (value) => {
        const t = list[i++];
        if (t === undefined || t.value !== value) throw new Error(`Expected '${value}', found '${t === undefined ? 'the end' : t.value}'.`);
    };
    const value = (of) => {
        const t = list[i];
        if (t === undefined) throw new Error('The initialiser ends early.');
        if (of.endsWith('?')) {
            if (t.kind === 'word' && t.value === 'null') { i++; return 'null'; }
            return value(of.slice(0, -1));
        }
        const element = of.endsWith('[]') ? of.slice(0, -2) : /^IReadOnlyList<(.+)>$/.exec(of)?.[1];
        if (element !== undefined) {
            take('[');
            const items = [];
            while (list[i]?.value !== ']') {
                items.push(value(element));
                if (list[i]?.value === ',') i++;
            }
            take(']');
            return `[${items.join(', ')}]`;
        }
        if (of === 'string') {
            if (t.kind !== 'string') throw new Error(`Expected a string, found '${t.value}'.`);
            i++;
            return JSON.stringify(t.value);
        }
        if (of === 'double' || of === 'decimal' || of === 'int') {
            if (t.kind !== 'number') throw new Error(`Expected a number, found '${t.value}'.`);
            i++;
            return t.value;
        }
        take('new');
        take('(');
        const params = of === 'DateOnly' ? ['Year', 'Month', 'Day'].map(n => ({ name: n, type: 'int' })) : records.get(of);
        if (params === undefined) throw new Error(`No constructor is known for ${of}.`);
        // Arguments by position, or by name (`ZeroContinuous: [...]`) as C# allows, in any order.
        const values = new Array(params.length);
        for (let k = 0; list[i]?.value !== ')'; k++) {
            if (k > 0) take(',');
            let at = k;
            if (list[i]?.kind === 'word' && list[i + 1]?.value === ':') {
                const name = list[i].value;
                at = params.findIndex(p => p.name === name);
                if (at < 0) throw new Error(`${of} has no parameter named ${name}.`);
                i += 2;
            }
            if (at >= params.length || values[at] !== undefined) throw new Error(`${of}: argument ${k} has no parameter left to fill.`);
            values[at] = value(params[at].type);
        }
        take(')');
        if (values.some(v => v === undefined) || values.length !== params.length) throw new Error(`${of}: not every parameter is given.`);
        if (of === 'DateOnly') {
            const [y, mo, d] = values.map(Number);
            return `d('${String(y).padStart(4, '0')}-${String(mo).padStart(2, '0')}-${String(d).padStart(2, '0')}')`;
        }
        const fields = records.get(of);
        return `{ ${fields.map((f, k) => `${camel(f.name)}: ${values[k]}`).join(', ')} }`;
    };
    const result = value(type);
    if (i !== list.length) throw new Error(`The initialiser goes on after its value, at '${list[i].value}'.`);
    return result;
}

const lines = [
    '// Generated by tools/showcase-data.mjs from samples/ExChart.Showcases/Data, the fixed values of ExChart\'s',
    '// Showcases (ADR-0280): edit those, not this, and run the tool again. Every figure is invented.',
    '/* eslint-disable */',
    "import { Temporal } from 'temporal-polyfill';",
    '',
    'const d = (iso: string): Temporal.PlainDate => Temporal.PlainDate.from(iso);',
    '',
];
for (const [name, fields] of records) {
    lines.push(`export interface ${name} {`);
    for (const f of fields) lines.push(`    readonly ${camel(f.name)}: ${tsType(f.type)};`);
    lines.push('}', '');
}

const files = readdirSync(dataDir).filter(f => f.endsWith('.Values.cs')).sort();
for (const file of [...files, 'Records.cs']) {
    const text = readFileSync(dataDir + file, 'utf8');
    const owner = /public static (?:partial )?class (\w+)/.exec(text)?.[1];
    for (const m of text.matchAll(/public static readonly ([\w<>?]+(?:\[\])+) (\w+) =\s*\n?\s*(\[[\s\S]*?\n?\s*\]);/g)) {
        const [, type, name, init] = m;
        // Records.cs holds one class of arrays beside the records' own static members.
        const holder = file === 'Records.cs' ? [...text.slice(0, m.index).matchAll(/public static (?:partial )?class (\w+)/g)].at(-1)?.[1] : owner;
        if (holder === undefined) throw new Error(`${file}: ${name} stands in no class.`);
        const element = type.slice(0, -2);
        const elementType = element.endsWith('[]') ? `readonly (${tsType(element.slice(0, -2))})[]` : tsType(element);
        lines.push(`export const ${camel(holder)}${name}: readonly (${elementType})[] = ${parse(tokens(init), type)};`, '');
    }
}

writeFileSync(out, `${lines.join('\n').trimEnd()}\n`);
console.log(`Wrote ${out}.`);
