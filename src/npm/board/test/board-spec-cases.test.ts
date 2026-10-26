// Runs the shared Board cases of tests/cases/board/ (kind boardSpec; ADR-0340, ADR-0370 section 3), which xUnit runs
// against ExBoard's reader as well: a Board read, held to what the case says it holds, or its refusal, path and
// words. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { BoardRefusedError } from '@ex-grid/data';
import { cases, isObject, key, startingWith, text, type Json, type JsonObject } from '../../data/test/case-json';
import { readBoard, type BoardReading, type BoardView } from '../src/index';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases/board');

function view(shown: BoardView): Json {
    const base = { id: shown.id, kind: shown.kind, title: shown.title, data: [...shown.data] };
    switch (shown.kind) {
        case 'grid':
            return { ...base, columns: shown.grid.columns.map(c => ({ column: c.column, kind: c.kind, header: c.header, format: c.format === undefined ? null : c.format })) };
        case 'pivot':
            return {
                ...base, rows: shown.layout.rows.map(f => f.field), columns: shown.layout.columns.map(f => f.field), values: shown.layout.values.map(f => f.field),
            };
        case 'chart': return base;
        default: throw new Error(`'${String(shown satisfies never)}' is not a view.`);
    }
}

/** What a Board holds, as tests/cases/README.md writes it under boardSpec. */
function summary(board: BoardReading): JsonObject {
    return {
        title: board.title,
        inputs: board.inputs.map(input => ({ name: input.name, columns: input.columns.map(c => ({ name: c.name, type: c.kind })) })),
        views: board.views.map(view),
        layout: board.layout.map(row => ({ cells: row.cells.map(c => [c.view, c.width]), height: row.height })),
        links: board.links.map(l => ({ from: l.from, to: [...l.to], on: [...l.on] })),
        outputs: board.outputs.map(o => ({ name: o.name, from: o.from, on: [...o.on], confirm: o.confirm })),
    };
}

const files = fs.readdirSync(root)
    .filter(name => name.endsWith('.json'))
    .toSorted()
    .map(name => ({ name, json: JSON.parse(fs.readFileSync(path.join(root, name), 'utf8')) as Json }))
    .filter(({ json }) => key(json, 'kind') === 'boardSpec');

for (const { name: file, json } of files) {
    describe(`board/${file}`, () => {
        for (const entry of cases(json)) {
            it(text(key(entry, 'name')), () => {
                expect.hasAssertions();
                const read = (): BoardReading => readBoard(text(key(entry, 'read')));
                const refused = entry['refused'];
                if (refused !== undefined) {
                    expect(read).toThrow(BoardRefusedError);
                    expect(read).toThrow(expect.objectContaining({ path: text(key(refused, 'path')) }));
                    const saying = isObject(refused) ? refused['saying'] : undefined;
                    // A message quoting a platform's own words is held to the words before them.
                    if (saying === undefined) expect(read).toThrow(expect.objectContaining({ message: text(key(refused, 'message')) }));
                    else expect(read).toThrow(startingWith(text(saying)));
                    return;
                }
                const held = summary(read());
                const expected = entry['expect'];
                if (expected !== undefined) {
                    expect(held).toEqual(expected);
                    return;
                }
                const keys = ['links', 'outputs'].filter(k => entry[k] !== undefined);
                expect(keys.length).toBeGreaterThan(0);
                for (const k of keys) expect(held[k]).toEqual(entry[k]);
            });
        }
    });
}
