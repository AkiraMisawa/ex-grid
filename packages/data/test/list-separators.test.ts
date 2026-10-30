// Holds the Script Core's copy of .NET's list separators to tests/cases/data/list-separators.json, which xUnit holds
// to .NET (ListSeparatorCaseTests): a CSV suggestion breaks a tie between two separators with it (ADR-0064 Q33).
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { listSeparatorOf } from '../src/csv/culture';
import { isObject, key, type Json } from './case-json';

const file = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../tests/cases/data/list-separators.json');
const cultures = key(JSON.parse(fs.readFileSync(file, 'utf8')) as Json, 'cultures');

describe('listSeparatorOf', () => {
    it('ADR-0064 (Q33): every culture .NET holds has the list separator .NET gives it', () => {
        if (!isObject(cultures)) throw new Error('The cultures are an object.');
        const differing = Object.entries(cultures).flatMap(([name, separator]) => {
            const answer = listSeparatorOf(name);
            return answer === (separator === null ? undefined : separator) ? [] : [`${name}: ${String(answer)} for ${JSON.stringify(separator)}`];
        });
        expect(differing).toEqual([]);
    });

    it('ADR-0064 (Q33): a culture is found by its language, script and region, whatever extensions it carries', () => {
        expect(listSeparatorOf('es-MX-u-nu-latn')).toBe(',');
        expect(listSeparatorOf('zh-HK')).toBe(',');
        expect(listSeparatorOf('zh-Hans-HK')).toBe(';');
        expect(listSeparatorOf(undefined)).toBe(',');
    });
});
