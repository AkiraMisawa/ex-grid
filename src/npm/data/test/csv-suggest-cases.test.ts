// Runs the shared CSV suggestion cases of tests/cases/data/ (ADR-0064 Q33, ADR-0370 section 3), which xUnit runs
// against ExGrid.Data as well: a Schema is suggested for each case's file, and the suggestion, with every mark's
// words, or how it was refused, is held to the answer ExGrid.Data wrote. tests/cases/README.md says what a file holds.
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import { SnapshotError, suggestCsvSchema, type CsvSuggestion, type CsvSuggestionOptions } from '../src/index';
import { fileOf, isObject, key, list, text, type Json, type JsonObject } from './case-json';

const root = path.resolve(path.dirname(fileURLToPath(import.meta.url)), '../../../../tests/cases/data');

const files = fs.readdirSync(root)
    .filter(name => name.endsWith('.json'))
    .toSorted()
    .map(name => ({ name, json: JSON.parse(fs.readFileSync(path.join(root, name), 'utf8')) as Json }))
    .filter(({ json }) => key(json, 'kind') === 'csvSuggest');

/** The suggestion, as ExGrid.Data's runner writes it: each column's Schema entry is the Schema's own. */
function answer(suggestion: CsvSuggestion): Json {
    return JSON.parse(JSON.stringify({
        schema: suggestion.schema,
        columns: suggestion.columns.map(column => ({ marks: column.marks, isUnclear: column.isUnclear, examples: column.examples })),
        marks: suggestion.marks,
        sampledRows: suggestion.sampledRows,
        isUnclear: suggestion.isUnclear,
    })) as Json;
}

function refusal(error: unknown): Json {
    if (error instanceof SnapshotError) return { refused: { message: error.message } };
    // Options refused say so in the suggestion's words; another TypeError or RangeError is a defect of the
    // suggester, which must fail the case rather than pass one that expects a refusal.
    if ((error instanceof TypeError || error instanceof RangeError) && /^(The |A |Whether )/.test(error.message)) return { refused: 'argument' };
    throw error;
}

async function attempt(entry: JsonObject): Promise<Json> {
    const file = fileOf(key(entry, 'file'));
    // The options are the TypeScript CsvSuggestionOptions as they stand, a value no option takes included.
    const options = (entry['options'] === undefined ? undefined : entry['options']) as CsvSuggestionOptions | undefined;
    try {
        return answer(await suggestCsvSchema(file, options));
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the answer the case holds; refusal() rethrows anything else
        return refusal(error);
    }
}

describe('csvSuggest', () => {
    for (const { name: file, json } of files) {
        describe(file, () => {
            for (const entry of list(key(json, 'cases')).filter(isObject)) {
                it(text(key(entry, 'name')), async () => {
                    expect.hasAssertions();
                    expect(await attempt(entry)).toEqual(key(entry, 'expect'));
                });
            }
        });
    }
});
