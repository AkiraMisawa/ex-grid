// The keys the Keyboard page is held against (ADR-0110), as tests/cases/keys/keyboard-reference.json lists them for
// the site's KeyboardCompletenessTests, which run in .NET and cannot read this table themselves.
import { readFileSync } from 'node:fs';
import { dirname, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { declareKeys, keysAnsweredOutsideTable, keysTakenFor } from '../src/keys/grid-keys';

interface Listed {
    readonly taken: readonly string[];
    readonly answeredOutsideTable: readonly string[];
    readonly typing: { readonly answered: readonly string[]; readonly free: readonly string[] };
}

const listed = JSON.parse(readFileSync(
    resolve(dirname(fileURLToPath(import.meta.url)), '../../../tests/cases/keys/keyboard-reference.json'), 'utf8')) as Listed;

const sorted = (keys: readonly string[]): readonly string[] => keys.toSorted();

describe('the keys the Keyboard page is held against', () => {
    it('ADR-0110, ADR-0012: the list names every key the table takes on a grid that edits, undoes, redoes and finds, and no other', () => {
        expect(sorted(listed.taken)).toEqual(sorted(keysTakenFor({ canEdit: true, canUndo: true, canRedo: true, canFind: true })));
    });

    it('ADR-0110, ADR-0050 item 14: the list names every key the core answers outside its table, and no other', () => {
        expect(sorted(listed.answeredOutsideTable)).toEqual(sorted(keysAnsweredOutsideTable));
    });

    it('ADR-0010, ADR-0050 item 14: the typing samples are answered, and the free ones left free, by the core', () => {
        for (const key of listed.typing.answered) expect(() => declareKeys([key]), key).toThrow(/typing/);
        for (const key of listed.typing.free) expect(() => declareKeys([key]), key).not.toThrow();
    });
});
