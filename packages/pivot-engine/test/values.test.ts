// Layer 1: the cases of what a report is made of (tests/cases/pivot; ADR-0370 sections 2 and 5), run against
// the Script Core's engine: the words (kind "pivotWords"), General (kind "pivotGeneral") and an Item's key
// (kind "pivotItemKey"). dotnet/tests/ExPivot.Engine.Tests/Cases/PivotValueCaseTests.cs is the C# runner, and the
// one that writes the answers.
import { describe, expect, it } from 'vitest';
import { itemKeysEqual, japanesePivotWords, pivotItemKey, pivotItemKinds, pivotWordIdList, unitedKingdomPivotWords, type PivotItemKey, type PivotItemKind } from '../src/index';
import { cultureOf } from '../src/culture/culture-data';
import { isDecimal, itemKeyOfValue, readRecordValue } from '../src/items/record-value';
import { toPublicKey } from '../src/items/item-key';
import { generalOfDecimal, generalOfDouble } from '../src/text/general';
import { englishWordFor } from '../src/words/pivot-words';
import { array, caseFiles, inherited, must, object, orNull, refusalOf, text, caseValue, type Json, type JsonObject } from './cases';

function keyJson(key: PivotItemKey): JsonObject {
    return key.value === undefined ? { kind: key.kind } : { kind: key.kind, value: key.value };
}

function kind(name: string): PivotItemKind {
    const found = pivotItemKinds.find(k => k === name);
    if (found === undefined) throw new Error(`'${name}' is not a kind of Item.`);
    return found;
}

function keyOf(written: Json): PivotItemKey {
    const key = object(written);
    const given = key['value'];
    return pivotItemKey(kind(text(must(key, 'kind'))), given === undefined || given === null ? undefined : text(given));
}

// Every id, a date part's after the rest, with its word in each edition the engine bundles.
function wordTables(): JsonObject {
    const words: Record<string, Json> = {};
    for (const id of pivotWordIdList()) {
        words[id] = { english: orNull(englishWordFor(id)), japanese: orNull(japanesePivotWords(id)), unitedKingdom: orNull(unitedKingdomPivotWords(id)) };
    }
    return { words };
}

function general(file: JsonObject, entry: JsonObject): Json {
    const culture = cultureOf(text(inherited(file, entry, 'locale'))).data.number;
    return array(must(entry, 'numbers')).map(written => {
        const number = caseValue(written);
        if (isDecimal(number)) return generalOfDecimal(number, culture);
        if (typeof number === 'number') return generalOfDouble(number, culture);
        throw new TypeError(`${JSON.stringify(written)} is not a number a General case writes.`);
    });
}

function itemKeys(entry: JsonObject): Json {
    const of = entry['of'];
    if (of !== undefined) return array(of).map(written => keyJson(toPublicKey(itemKeyOfValue(readRecordValue(caseValue(written))))));
    const named = entry['named'];
    if (named !== undefined) {
        return array(named).map(written => {
            try {
                return keyJson(keyOf(written));
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the key's answer, compared below
                return refusalOf(error);
            }
        });
    }
    return array(must(entry, 'pairs')).map(pair => {
        const [a, b] = array(pair);
        if (a === undefined || b === undefined) throw new TypeError(`${JSON.stringify(pair)} is not a pair of keys.`);
        return itemKeysEqual(keyOf(a), keyOf(b));
    });
}

const kinds: readonly [string, (file: JsonObject, entry: JsonObject) => Json][] = [
    ['pivotWords', () => wordTables()],
    ['pivotGeneral', general],
    ['pivotItemKey', (_, entry) => itemKeys(entry)],
];

for (const [name, answer] of kinds) {
    for (const file of caseFiles(name)) {
        describe(`pivot/${file.file}`, () => {
            for (const entry of file.cases) {
                it(text(must(entry, 'name')), () => {
                    // An answer that names the platform's own API or types says the Script Core's in `scriptExpect`.
                    expect(answer(file.document, entry)).toEqual(entry['scriptExpect'] ?? must(entry, 'expect'));
                });
            }
        });
    }
}
