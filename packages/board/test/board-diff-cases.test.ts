// Runs the shared Board Diff cases of tests/cases/board/ (kind boardDiff; ADR-0340 Q7, ADR-0370 section 3), which xUnit
// runs against ExBoard's BoardDiff as well: a Diff read and written again, byte for byte, or its refusal, path and words,
// and a Diff laid against a Board, with what it dropped. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { BoardRefusedError } from '@ex-grid/data';
import { cases, isObject, key, list, startingWith, text, type Json } from '../../data/test/case-json';
import { BoardDiff, readBoard } from '../src/index';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../tests/cases/board');

const files = fs.readdirSync(root)
    .filter(name => name.endsWith('.json'))
    .toSorted()
    .map(name => ({ name, json: JSON.parse(fs.readFileSync(path.join(root, name), 'utf8')) as Json }))
    .filter(({ json }) => key(json, 'kind') === 'boardDiff');

for (const { name: file, json } of files) {
    describe(`board/${file}`, () => {
        for (const entry of cases(json)) {
            it(text(key(entry, 'name')), () => {
                const read = (): BoardDiff => BoardDiff.read(text(key(entry, 'read')));
                const refused = entry['refused'];
                if (refused !== undefined) {
                    expect(read).toThrow(BoardRefusedError);
                    expect(read).toThrow(expect.objectContaining({ path: text(key(refused, 'path')) }));
                    const saying = isObject(refused) ? refused['saying'] : undefined;
                    if (saying === undefined) expect(read).toThrow(expect.objectContaining({ message: text(key(refused, 'message')) }));
                    else expect(read).toThrow(startingWith(text(saying)));
                    return;
                }
                let diff = read();
                const board = entry['fitTo'];
                if (board !== undefined) {
                    const fitted = diff.fitTo(readBoard(text(key(key(json, 'boards'), text(board)))));
                    expect(fitted.dropped).toEqual(list(key(entry, 'dropped')));
                    diff = fitted.diff;
                }
                expect(diff.toJson()).toBe(text(key(entry, 'writes')));
            });
        }
    });
}
