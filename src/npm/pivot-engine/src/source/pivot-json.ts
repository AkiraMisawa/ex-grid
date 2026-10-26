// Every Pivot Source question and answer as JSON (ADR-0066, ADR-0370 section 3), so it crosses to a server
// unchanged: the C# engine's PivotJson, document for document, its refusals worded as it words them. Each
// document names its format's version and its type, and a reader refuses a version it does not know rather
// than half-read it. Both engines write one document as one text, to the byte: a string spelt as
// System.Text.Json's default encoder spells it, a double in its shortest round-trip form.

import { decimalText, type SnapshotDecimal } from '@ex-grid/data';
import { noExtremes, noSum, PartColumns } from '../aggregates/parts';
import {
    PivotAnswer, PivotAnswerAxis, answerValues, madeAnswer, noWindowing, type PivotAnswerValues, type PivotAnswerWindow, type PivotAnswerWindowing, type PivotNumber,
    type PivotSourceRefusal, type PivotSourceRefusalKind,
} from '../cube/answer';
import {
    partBits, pivotDetailsQuery, pivotItemsQuery, pivotParts, pivotQuery,
    type PivotDetailsQuery, type PivotFieldItem, type PivotItemsQuery, type PivotLevelOrder, type PivotLevelRule, type PivotLevelRuleInput, type PivotLevels,
    type PivotLevelWindowInput, type PivotPart, type PivotQuery, type PivotQueryField, type PivotQueryInput,
} from '../cube/query';
import { PivotFormatError, PivotVersionError } from '../errors';
import { pivotField, type PivotField } from '../fields/pivot-field';
import { dateItemKey, itemKeyText, numberOfText, pivotItemKey, publicKeyId, ticksOfKeyText, type PivotItemKey } from '../items/item-key';
import { isDecimal } from '../items/record-value';
import { pivotAggregations, pivotItemKinds, type PivotDatePart, type PivotFieldType } from '../layout/pivot-enums';
import { isArray, isObject, JsonNumber, jsonArray, jsonDouble, jsonObject, jsonString, member, memberNames, namesWrittenTwice, readJson, type Json, type JsonObject } from '../json/json-text';
import { defined, elementAt } from '../must';
import {
    PivotDetailPage, PivotItemCounts, PivotItemPage, type PivotDetailRecord, type PivotDetailValue, type PivotServerChangeBatch, type PivotServerNotice,
    type PivotSourceChanged,
} from './pivot-source';

const version = 1;
const dateFormat = "yyyy-MM-dd'T'HH:mm:ss.FFFFFFF";
const fieldTypes: readonly PivotFieldType[] = ['text', 'number', 'date', 'boolean'];
const dateParts: readonly PivotDatePart[] = ['year', 'quarter', 'month'];
const refusalKinds: readonly PivotSourceRefusalKind[] = ['tooManyLeaves', 'unknownField', 'aggregationNotOffered', 'sourceVersionNotHeld'];

type Members = [string, string][];

// ---- Writing ----------------------------------------------------------------------------------------

function document(type: string, members: Members): string {
    return jsonObject([['version', String(version)], ['type', jsonString(type)], ...members]);
}

function refusalMembers(refusal: PivotSourceRefusal): Members {
    const members: Members = [['kind', jsonString(refusal.kind)], ['message', jsonString(refusal.message)]];
    if (refusal.field !== undefined) members.push(['field', jsonString(refusal.field)]);
    if (refusal.limit !== undefined) members.push(['limit', String(refusal.limit)]);
    return [['refusal', jsonObject(members)]];
}

function keyJson(key: PivotItemKey): string {
    return jsonObject(key.value === undefined ? [['kind', jsonString(key.kind)]] : [['kind', jsonString(key.kind)], ['value', jsonString(key.value)]]);
}

function keysJson(keys: readonly PivotItemKey[]): string {
    return jsonArray(keys.map(keyJson));
}

function fieldsJson(fields: readonly PivotQueryField[]): string {
    return jsonArray(fields.map(field => jsonObject(field.hiddenItems.length > 0
        ? [['field', jsonString(field.field)], ['hiddenItems', keysJson(field.hiddenItems)]]
        : [['field', jsonString(field.field)]])));
}

function pathJson(path: readonly PivotFieldItem[]): string {
    return jsonArray(path.map(step => jsonObject([['field', jsonString(step.field)], ['item', keyJson(step.item)]])));
}

function partsJson(parts: readonly PivotPart[]): string {
    return jsonArray(parts.map(jsonString));
}

// The shortest text that reads back as the same double, as C#'s "R" writes it; NaN, Infinity and -Infinity by
// name, as JavaScript and .NET both spell them.
function doubleText(value: number): string {
    return Number.isFinite(value) ? jsonDouble(value) : String(value);
}

// A double that is always one: a JSON number, or its name when it is not finite.
function doubleJson(value: number): string {
    return Number.isFinite(value) ? jsonDouble(value) : jsonString(doubleText(value));
}

// A part that is exact or not: an exact decimal is a JSON number, a double a string.
function exactOrDoubleJson(number: PivotNumber): string {
    return number.kind === 'exact' ? decimalText(number.value) : jsonString(doubleText(number.value));
}

// A whole number a document holds in 32 bits, as the C# engine's int, refused here rather than by the reader.
function int32Json(value: number, name: string): string {
    if (!Number.isInteger(value) || value < -(2 ** 31) || value >= 2 ** 31) {
        throw new RangeError(`'${name}' is ${value}, and a Pivot Source document holds it in 32 bits, as the C# engine does.`);
    }
    return String(value);
}

function leavesOf(leafCount: number, write: (leaf: number) => string): string {
    return jsonArray(Array.from({ length: leafCount }, (_, leaf) => write(leaf)));
}

// A field's rule of a question's levels: written only where it is not every Item expanded and ordered by label.
function rulesJson(rules: readonly PivotLevelRule[]): string {
    return jsonArray(rules.map(rule => {
        const members: Members = [];
        if (rule.collapsed) members.push(['collapsed', 'true']);
        if (rule.toggledItems.length > 0) members.push(['toggledItems', keysJson(rule.toggledItems)]);
        if (rule.order !== undefined) {
            members.push(['order', jsonObject([
                ['value', String(rule.order.value)], ['aggregation', jsonString(rule.order.aggregation)], ['descending', String(rule.order.descending)],
            ])]);
        }
        return jsonObject(members);
    }));
}

// A question's levels: their rules, and the window when it carries one.
function levelsJson(levels: PivotLevels): string {
    const members: Members = [['rows', rulesJson(levels.rows)], ['columns', rulesJson(levels.columns)]];
    const window = levels.window;
    if (window !== undefined) {
        members.push(['window', jsonObject([
            ['limit', int32Json(window.limit, 'limit')],
            ['starts', jsonArray(window.starts.map(start => jsonObject([['rows', keysJson(start.rows)], ['start', int32Json(start.start, 'start')]])))],
        ])]);
    }
    return jsonObject(members);
}

// An answer's windows and each cell's rest code, written only where it has a window.
function windowingMembers(answer: PivotAnswer, leafCount: number): Members {
    const windows = answer.windows;
    if (windows.length === 0) return [];
    return [
        ['windows', jsonArray(windows.map(window => jsonObject([
            ['parent', jsonArray(window.parent.map(String))], ['before', int32Json(window.before, 'before')], ['after', int32Json(window.after, 'after')],
        ])))],
        ['rests', leavesOf(leafCount, cell => {
            const rest = answer.restAt(cell);
            return rest === undefined ? '-1' : String(2 * rest.window + (rest.side === 'after' ? 1 : 0));
        })],
    ];
}

function valuesJson(values: PivotAnswerValues, leafCount: number, levels: boolean): string {
    const parts = values.parts;
    const members: Members = [
        ['field', jsonString(values.field)],
        ['parts', partsJson(parts)],
        ['values', leavesOf(leafCount, leaf => String(values.valuesAt(leaf)))],
        ['numbers', leavesOf(leafCount, leaf => String(values.numbersAt(leaf)))],
        // Rare, so written as the leaves that have one.
        ['nonFinite', jsonArray(Array.from({ length: leafCount }, (_, leaf) => leaf).filter(leaf => values.nonFiniteAt(leaf)).map(String))],
    ];
    if (parts.includes('sum')) {
        members.push(['sum', leavesOf(leafCount, leaf => exactOrDoubleJson(values.sumAt(leaf)))]);
        // A level answer's double sum is carried with its magnitudes (ADR-0370 section 3); an exact one, and a
        // cell of #NUM!, whose parts are not read, with none.
        if (levels) {
            members.push(['absoluteSum', leavesOf(leafCount, leaf => {
                const absolute = values.absoluteSumAt(leaf);
                return absolute === undefined || Number.isNaN(absolute) ? 'null' : doubleJson(absolute);
            })]);
        }
    }
    if (parts.includes('extremes')) {
        members.push(['min', leavesOf(leafCount, leaf => exactOrDoubleJson(values.minAt(leaf)))]);
        members.push(['max', leavesOf(leafCount, leaf => exactOrDoubleJson(values.maxAt(leaf)))]);
    }
    if (parts.includes('product')) members.push(['product', leavesOf(leafCount, leaf => doubleJson(values.productAt(leaf)))]);
    if (parts.includes('variance')) {
        members.push(['mean', leavesOf(leafCount, leaf => doubleJson(values.meanAt(leaf)))]);
        members.push(['m2', leavesOf(leafCount, leaf => doubleJson(values.m2At(leaf)))]);
    }
    return jsonObject(members);
}

function axesJson(axes: readonly PivotAnswerAxis[], leafCount: number): string {
    return jsonArray(axes.map(axis => jsonObject([
        ['field', jsonString(axis.field)],
        ['items', keysJson(axis.items)],
        ['leaves', leavesOf(leafCount, leaf => String(axis.itemAt(leaf)))],
    ])));
}

// A detail value by its field's declared type; a value of another type is tagged with its own.
function detailValueJson(type: PivotFieldType, value: PivotDetailValue): string {
    if (value === null) return 'null';
    if (type === 'text' && typeof value === 'string') return jsonString(value);
    if (type === 'number' && isDecimal(value)) return decimalText(value);
    if (type === 'number' && typeof value === 'number') return jsonString(doubleText(value));
    if (type === 'date' && typeof value === 'bigint') return jsonString(dateText(value));
    if (type === 'boolean' && typeof value === 'boolean') return String(value);
    if (typeof value === 'string') return jsonObject([['text', jsonString(value)]]);
    if (isDecimal(value)) return jsonObject([['number', decimalText(value)]]);
    if (typeof value === 'number') return jsonObject([['double', jsonString(doubleText(value))]]);
    if (typeof value === 'bigint') return jsonObject([['date', jsonString(dateText(value))]]);
    if (typeof value === 'boolean') return jsonObject([['boolean', String(value)]]);
    throw new TypeError(`A value of type ${typeof value} is no detail value: text, a number, a SnapshotDecimal, a date by its ticks, a Boolean, or null.`);
}

// A batch's records, each value by its field's declared type.
function batchRecordsJson(records: readonly (readonly PivotDetailValue[])[], fields: readonly PivotField[], side: string): string {
    return jsonArray(records.map((record, r) => {
        // Typed as an array, and checked as one: a batch may come from a script that never met the type.
        const isRecord: boolean = Array.isArray(record);
        if (!isRecord || record.length !== fields.length) {
            throw new TypeError(`A Change Batch's ${side} record ${r + 1} carries ${isRecord ? String(record.length) : 'no array of'} values, and there are ${fields.length} fields.`);
        }
        return jsonArray(record.map((value, f) => {
            const field = elementAt(fields, f);
            // A date part crosses as its whole number, as the C# engine's decimal writes it.
            if (field.datePart !== undefined && typeof value === 'number' && Number.isSafeInteger(value)) return jsonObject([['number', String(value)]]);
            return detailValueJson(field.type, value);
        }));
    }));
}

const batchVersions = 'A Change Batch goes from one Source Version to another, each named with text.';

function dateText(ticks: bigint): string {
    return defined(dateItemKey(ticks).value, 'a date key has its text');
}

// ---- Reading ----------------------------------------------------------------------------------------

// Opens a document: a JSON object naming its version — refused with a PivotVersionError when it is not one
// this reader reads — and its type, refused with a PivotFormatError when it is not one expected. A value the
// model refuses is a document this reader cannot take.
function read<T>(json: string, type: string | readonly [string, string], take: (root: JsonObject, type: string) => T): T {
    if (typeof json !== 'string') throw new TypeError('A Pivot Source document is text.');
    try {
        JSON.parse(json);
    } catch (error) {
        throw new PivotFormatError('A Pivot Source document is JSON.', { cause: error });
    }
    const root = readJson(json);
    if (!isObject(root)) throw new PivotFormatError('A Pivot Source document is a JSON object.');
    const written = member(root, 'version');
    if (!(written instanceof JsonNumber)) throw new PivotFormatError('A Pivot Source document names its version.');
    if (written.int32 !== version) {
        throw new PivotVersionError(`A Pivot Source document of version ${written.raw} cannot be read; this reader reads version ${version}.`);
    }
    const actual = optionalString(root, 'type');
    const types = typeof type === 'string' ? [type] : type;
    if (actual === undefined || !types.includes(actual)) {
        throw new PivotFormatError(`The document is a Pivot Source '${actual ?? '(untyped)'}' document, not a '${types.join("' or a '")}'.`); // oxlint-disable-line ex-grid/no-stand-in-default -- the refusal names a document that names no type
    }
    try {
        return take(root, actual);
    } catch (error) {
        if (error instanceof TypeError || error instanceof RangeError) {
            throw new PivotFormatError(`The document holds a value that cannot be: ${error.message}`, { cause: error });
        }
        throw error;
    }
}

function fieldList(fields: readonly { readonly name: string; readonly type: PivotFieldType; readonly datePart?: PivotDatePart | undefined }[]): string {
    return fields.map(field => `${field.name} (${field.datePart === undefined ? field.type : `${field.type} ${field.datePart}`})`).join(', ');
}

// A batch's records, required: a batch that leaves out what its data lost or gained is not the batch.
function batchRecordsOf(root: JsonObject, side: string, fields: readonly PivotField[]): (readonly PivotDetailValue[])[] {
    if (member(root, side) === undefined) throw new PivotFormatError(`'${side}' is an array, and is required.`);
    return entries(root, side).map(record => {
        if (!isArray(record)) throw new PivotFormatError('A batch record is an array of values.');
        if (record.length !== fields.length) throw new PivotFormatError(`A batch record carries ${record.length} values, and the batch has ${fields.length} fields.`);
        return Object.freeze(record.map((value, f) => {
            const field = elementAt(fields, f);
            const taken = detailValueOf(value, field);
            // A date part crosses as its whole number, which a decimal holds: read as the number it is.
            return field.datePart !== undefined && isDecimal(taken) && taken.scale === 0 ? Number(taken.mantissa) : taken;
        }));
    });
}

function refusalOf(root: JsonObject): PivotSourceRefusal | undefined {
    const refusal = member(root, 'refusal');
    if (refusal === undefined) return undefined;
    if (!isObject(refusal)) throw new PivotFormatError("'refusal' is an object.");
    const field = optionalString(refusal, 'field');
    const limit = member(refusal, 'limit');
    return Object.freeze({
        kind: named(refusal, 'kind', 'refusal kind', refusalKinds),
        message: string(refusal, 'message'),
        ...(field === undefined ? {} : { field }),
        ...(limit === undefined ? {} : { limit: integerValue(limit, 'limit') }),
    });
}

function fieldsOf(element: JsonObject, name: string): PivotQueryField[] {
    return objects(element, name).map(field => ({ field: string(field, 'field'), hiddenItems: keysOf(field, 'hiddenItems') }));
}

function pathOf(element: JsonObject, name: string): PivotFieldItem[] {
    return objects(element, name).map(step => {
        const item = member(step, 'item');
        if (item === undefined) throw new PivotFormatError("A step of a cell's path names its 'item'.");
        return { field: string(step, 'field'), item: keyOf(item) };
    });
}

function keysOf(element: JsonObject, name: string): PivotItemKey[] {
    return entries(element, name).map(keyOf);
}

function keyOf(key: Json): PivotItemKey {
    if (!isObject(key)) throw new PivotFormatError("An Item is an object with its 'kind' and its 'value'.");
    return pivotItemKey(named(key, 'kind', 'kind', pivotItemKinds), optionalString(key, 'value'));
}

function partsOf(element: JsonObject): PivotPart[] {
    return entries(element, 'parts').map(part => {
        if (typeof part !== 'string') throw new PivotFormatError('A part is named by a string.');
        const known = pivotParts.find(p => p === part);
        if (known === undefined) throw new PivotFormatError(`'${part}' is not a part this reader knows.`);
        return known;
    });
}

function axesOf(root: JsonObject, name: string, leafCount: number, levels: boolean): PivotAnswerAxis[] {
    return objects(root, name).map(axis => {
        const field = string(axis, 'field');
        const items = keysOf(axis, 'items');
        const distinct = new Set<string>();
        for (const item of items) {
            const id = publicKeyId(item);
            if (distinct.has(id)) throw new PivotFormatError(`'${field}' lists the Item ${itemKeyText(item)} twice; text Items are told apart ignoring case (ADR-0060).`);
            distinct.add(id);
        }
        const itemOfLeaf = column(axis, 'leaves', leafCount, entry => int32Value(entry, 'leaves'));
        itemOfLeaf.forEach((item, leaf) => {
            if ((item < 0 || item >= items.length) && !(levels && item === -1)) {
                throw new PivotFormatError(`${levels ? 'Cell' : 'Leaf'} ${leaf} names Item ${item} of '${field}', which has ${items.length}.`);
            }
        });
        return new PivotAnswerAxis(field, items, Object.freeze(itemOfLeaf));
    });
}

function valuesOf(element: JsonObject, records: readonly number[], levels: boolean): PivotAnswerValues {
    const field = string(element, 'field');
    const leafCount = records.length;
    const columns = new PartColumns(partBits(partsOf(element)));
    const values = column(element, 'values', leafCount, entry => integerValue(entry, 'values'));
    const numbers = column(element, 'numbers', leafCount, entry => integerValue(entry, 'numbers'));
    for (let leaf = 0; leaf < leafCount; leaf++) {
        const valueCount = elementAt(values, leaf);
        const numberCount = elementAt(numbers, leaf);
        const recordCount = elementAt(records, leaf);
        if (numberCount < 0 || numberCount > valueCount || valueCount > recordCount) {
            throw new PivotFormatError(`Leaf ${leaf} of '${field}' holds ${recordCount} records, ${valueCount} values and ${numberCount} numbers.`);
        }
        const cell = columns.addCell();
        const counts = elementAt(columns.counts, cell);
        counts.values = valueCount;
        counts.numbers = numberCount;
    }
    for (const entry of entries(element, 'nonFinite')) {
        const leaf = int32Value(entry, 'nonFinite');
        if (leaf < 0 || leaf >= leafCount) throw new PivotFormatError(`'nonFinite' names leaf ${leaf}, and the answer has ${leafCount}.`);
        elementAt(columns.counts, leaf).nonFinite = true;
    }
    if (columns.sums !== undefined) {
        const sums = columns.sums;
        const written = column(element, 'sum', leafCount, exactOrDoubleOf);
        written.forEach((sum, leaf) => {
            sums[leaf] = sum.kind === 'exact' ? { ...noSum(), exact: sum.value } : { ...noSum(), double: sum.value, inexact: true };
        });
        if (levels) {
            columns.trackAbsolutes();
            const absolutes = defined(columns.absolutes, 'the magnitudes tracked');
            const magnitudes = (entry: Json): number | undefined => {
                if (entry === null) return undefined;
                const absolute = doubleOf(entry);
                if (Number.isNaN(absolute) || absolute < 0) {
                    throw new PivotFormatError(`A sum of magnitudes of '${field}' is ${rawOf(entry)}; it is a number not below 0, or null for none (ADR-0370 section 3).`);
                }
                return absolute;
            };
            column(element, 'absoluteSum', leafCount, magnitudes).forEach((absolute, leaf) => {
                if (elementAt(written, leaf).kind === 'exact' && absolute !== undefined) {
                    throw new PivotFormatError(`Cell ${leaf}'s sum of '${field}' is exact, and carries a sum of magnitudes; only a double's does (ADR-0370 section 3).`);
                }
                absolutes[leaf] = absolute === undefined ? Number.NaN : absolute;
            });
        }
    }
    if (columns.extremes !== undefined) {
        const extremes = columns.extremes;
        const min = column(element, 'min', leafCount, exactOrDoubleOf);
        const max = column(element, 'max', leafCount, exactOrDoubleOf);
        for (let leaf = 0; leaf < leafCount; leaf++) {
            const low = elementAt(min, leaf);
            const high = elementAt(max, leaf);
            if (low.kind === 'exact' && high.kind === 'exact') extremes[leaf] = { ...noExtremes(), exactMin: low.value, exactMax: high.value };
            else if (low.kind === 'double' && high.kind === 'double') extremes[leaf] = { ...noExtremes(), min: low.value, max: high.value, inexact: true };
            else throw new PivotFormatError(`Leaf ${leaf} of '${field}' has one extreme exact and the other a double.`);
        }
    }
    if (columns.products !== undefined) {
        const products = columns.products;
        column(element, 'product', leafCount, doubleOf).forEach((product, leaf) => {
            products[leaf] = product;
        });
    }
    if (columns.variances !== undefined) {
        const variances = columns.variances;
        const mean = column(element, 'mean', leafCount, doubleOf);
        const m2 = column(element, 'm2', leafCount, doubleOf);
        for (let leaf = 0; leaf < leafCount; leaf++) variances[leaf] = { mean: elementAt(mean, leaf), m2: elementAt(m2, leaf) };
    }
    return answerValues(field, columns, leafCount);
}

// One array of an answer's columns: exactly one entry per leaf.
function column<T>(element: JsonObject, name: string, leafCount: number, take: (entry: Json) => T): T[] {
    const written = member(element, name);
    if (!isArray(written)) throw new PivotFormatError(`'${name}' is an array, and is required.`);
    if (written.length !== leafCount) throw new PivotFormatError(`'${name}' has ${written.length} entries, and the answer has ${leafCount} leaves.`);
    return written.map(take);
}

// A part that is exact or not: a JSON number is an exact decimal, a JSON string a double.
function exactOrDoubleOf(entry: Json): PivotNumber {
    if (entry instanceof JsonNumber) return Object.freeze({ kind: 'exact', value: exactOf(entry) });
    if (typeof entry === 'string') return Object.freeze({ kind: 'double', value: parseDouble(entry) });
    throw new PivotFormatError('An exact part is a number, and a double one a string.');
}

function exactOf(entry: JsonNumber): SnapshotDecimal {
    const exact = entry.decimal;
    if (exact === undefined) throw new PivotFormatError(`${entry.raw} is not an exact decimal; a double is written as a string.`);
    return exact;
}

// A double that is not finite, by its name, or `undefined` for any other text.
function namedDouble(text: string): number | undefined {
    switch (text) {
        case 'NaN': return Number.NaN;
        case 'Infinity': return Number.POSITIVE_INFINITY;
        case '-Infinity': return Number.NEGATIVE_INFINITY;
        default: return undefined;
    }
}

// A flag, written only when it is true; false when it is left out.
function optionalBool(element: JsonObject, name: string): boolean {
    const value = member(element, name);
    if (value === undefined) return false;
    if (typeof value !== 'boolean') throw new PivotFormatError(`'${name}' is true or false.`);
    return value;
}

// A question's levels, as the members of its settings: none where it carries none.
function levelsOf(root: JsonObject): Pick<PivotQueryInput, 'levels'> {
    const levels = member(root, 'levels');
    if (levels === undefined) return {};
    if (!isObject(levels)) throw new PivotFormatError("'levels' is an object.");
    const rules = (name: string): PivotLevelRuleInput[] => objects(levels, name).map(rule => {
        const order = member(rule, 'order');
        const input = { collapsed: optionalBool(rule, 'collapsed'), toggledItems: keysOf(rule, 'toggledItems') };
        return order === undefined ? input : { ...input, order: orderOf(order) };
    });
    const window = member(levels, 'window');
    const rows = rules('rows');
    const columns = rules('columns');
    return { levels: window === undefined ? { rows, columns } : { rows, columns, window: windowOf(window) } };
}

function orderOf(order: Json): PivotLevelOrder {
    if (!isObject(order)) throw new PivotFormatError("'order' is an object.");
    const aggregation = string(order, 'aggregation');
    const known = pivotAggregations.find(a => a === aggregation);
    if (known === undefined) throw new PivotFormatError(`'${aggregation}' is not an Aggregation this reader knows.`);
    const descending = member(order, 'descending');
    if (typeof descending !== 'boolean') throw new PivotFormatError("'descending' is true or false, and is required.");
    return { value: int32(order, 'value'), aggregation: known, descending };
}

function windowOf(window: Json): PivotLevelWindowInput {
    if (!isObject(window)) throw new PivotFormatError("'window' is an object.");
    return {
        limit: int32(window, 'limit'),
        starts: objects(window, 'starts').map(start => ({ rows: keysOf(start, 'rows'), start: int32(start, 'start') })),
    };
}

// An answer's windows and rest codes: none where it has no window.
function windowingOf(root: JsonObject, leafCount: number, levels: boolean): PivotAnswerWindowing {
    if (member(root, 'windows') === undefined) {
        if (member(root, 'rests') !== undefined) throw new PivotFormatError("The answer has 'rests' and no 'windows' (ADR-0370 section 3).");
        return noWindowing;
    }
    if (!levels) throw new PivotFormatError('An answer of leaves has no window (ADR-0370 section 3).');
    const windows: PivotAnswerWindow[] = objects(root, 'windows').map(window => Object.freeze({
        parent: Object.freeze(entries(window, 'parent').map(entry => int32Value(entry, 'parent'))),
        before: int32(window, 'before'),
        after: int32(window, 'after'),
    }));
    if (windows.length === 0) throw new PivotFormatError("'windows' is left out where the answer has none.");
    const rests = column(root, 'rests', leafCount, entry => int32Value(entry, 'rests'));
    return Object.freeze({ windows: Object.freeze(windows), rests: Object.freeze(rests) });
}

// A double: a JSON number, or a named literal.
function doubleOf(entry: Json): number {
    if (entry instanceof JsonNumber) return entry.double;
    if (typeof entry !== 'string') throw new PivotFormatError('A double is a number, or NaN, Infinity or -Infinity.');
    const value = namedDouble(entry);
    if (value === undefined) throw new PivotFormatError(`'${entry}' is not NaN, Infinity or -Infinity; a finite double is a number.`);
    return value;
}

// A double's invariant text, NaN, Infinity and -Infinity by name.
function parseDouble(text: string): number {
    const value = namedDouble(text) ?? numberOfText(text);
    if (value === undefined) throw new PivotFormatError(`'${text}' is not the invariant text of a double.`);
    return value;
}

function detailValueOf(entry: Json, field: PivotField): PivotDetailValue {
    if (entry === null) return null;
    if (isObject(entry)) return taggedValueOf(entry);
    if (typeof entry === 'string' && field.type === 'text') return entry;
    if (typeof entry === 'string' && field.type === 'number') return parseDouble(entry);
    if (typeof entry === 'string' && field.type === 'date') return ticksOf(entry);
    if (entry instanceof JsonNumber && field.type === 'number') return exactOf(entry);
    if (typeof entry === 'boolean' && field.type === 'boolean') return entry;
    throw new PivotFormatError(`A ${kindOf(entry)} is not a value of '${field.name}', declared ${typeTitle(field.type)}; a value of another type is tagged with its own.`);
}

function taggedValueOf(entry: JsonObject): PivotDetailValue {
    const names = memberNames(entry);
    const [tag] = names;
    if (names.length !== 1 || tag === undefined || namesWrittenTwice(entry).length > 0) {
        throw new PivotFormatError('A tagged detail value has one member: text, number, double, date or boolean.');
    }
    const value = defined(member(entry, tag), 'the tag\'s value');
    if (tag === 'text' && typeof value === 'string') return value;
    if (tag === 'number' && value instanceof JsonNumber) return exactOf(value);
    if (tag === 'double' && typeof value === 'string') return parseDouble(value);
    if (tag === 'date' && typeof value === 'string') return ticksOf(value);
    if (tag === 'boolean' && typeof value === 'boolean') return value;
    throw new PivotFormatError(`'${tag}' with a ${kindOf(value)} is not a tagged detail value this reader knows.`);
}

function ticksOf(text: string): bigint {
    const ticks = ticksOfKeyText(text);
    if (ticks === undefined) throw new PivotFormatError(`'${text}' is not an ISO date (${dateFormat}).`);
    return ticks;
}

// A JSON value's kind as System.Text.Json's JsonValueKind names it.
function kindOf(value: Json): string {
    if (value === null) return 'Null';
    if (value === true) return 'True';
    if (value === false) return 'False';
    if (value instanceof JsonNumber) return 'Number';
    if (typeof value === 'string') return 'String';
    return isArray(value) ? 'Array' : 'Object';
}

// A field type as C# names the enum member.
function typeTitle(type: PivotFieldType): string {
    return type.charAt(0).toUpperCase() + type.slice(1);
}

function entries(element: JsonObject, name: string): readonly Json[] {
    const value = member(element, name);
    if (value === undefined) return [];
    if (!isArray(value)) throw new PivotFormatError(`'${name}' is an array.`);
    return value;
}

function objects(element: JsonObject, name: string): JsonObject[] {
    return entries(element, name).map(entry => {
        if (!isObject(entry)) throw new PivotFormatError(`An entry of '${name}' is not an object.`);
        return entry;
    });
}

function string(element: JsonObject, name: string): string {
    const value = member(element, name);
    if (typeof value !== 'string') throw new PivotFormatError(`'${name}' is a string, and is required.`);
    return value;
}

function optionalString(element: JsonObject, name: string): string | undefined {
    const value = member(element, name);
    if (value === undefined) return undefined;
    if (typeof value !== 'string') throw new PivotFormatError(`'${name}' is a string.`);
    return value;
}

// An integer member of an object, of 32 bits.
function int32(element: JsonObject, name: string): number {
    const value = member(element, name);
    if (value === undefined) throw new PivotFormatError(`'${name}' is an integer, and is required.`);
    return int32Value(value, name);
}

// An integer value of 32 bits: a member's, or an entry of an array named by name.
function int32Value(value: Json, name: string): number {
    const number = value instanceof JsonNumber ? value.int32 : undefined;
    if (number === undefined) throw new PivotFormatError(`'${name}' holds integers; ${rawOf(value)} is not one.`);
    return number;
}

function integer(element: JsonObject, name: string): number {
    const value = member(element, name);
    if (value === undefined) throw new PivotFormatError(`'${name}' is an integer, and is required.`);
    return integerValue(value, name);
}

// A whole number a double holds exactly, where C# reads a long.
function integerValue(value: Json, name: string): number {
    const number = value instanceof JsonNumber ? value.integer : undefined;
    if (number === undefined) throw new PivotFormatError(`'${name}' holds integers; ${rawOf(value)} is not one.`);
    return number;
}

// A value as the document wrote it, as JsonElement.GetRawText gives it back, without its spaces.
function rawOf(value: Json): string {
    if (value instanceof JsonNumber) return value.raw;
    if (isArray(value)) return `[${value.map(rawOf).join(',')}]`;
    if (isObject(value)) return `{${memberNames(value).map(name => `${JSON.stringify(name)}:${rawOf(defined(member(value, name), 'a member'))}`).join(',')}}`;
    return JSON.stringify(value);
}

function named<T extends string>(element: JsonObject, name: string, what: string, names: readonly T[]): T {
    const value = member(element, name);
    if (typeof value !== 'string') throw new PivotFormatError(`'${name}' is a string, and is required.`);
    const known = names.find(candidate => candidate === value);
    if (known === undefined) throw new PivotFormatError(`'${value}' is not a ${what} this reader knows.`);
    return known;
}

// ---- The documents ----------------------------------------------------------------------------------

/**
 * Every Pivot Source question and answer as JSON (ADR-0066), so it crosses to a server unchanged — the C#
 * engine's `PivotJson`, which writes each document as the same text. Each document names its format's
 * `version` and its `type`; a reader refuses, with a {@link PivotVersionError}, a version it does not know
 * rather than half-read it, and, with a {@link PivotFormatError}, a document of another type or one that
 * cannot be, naming what is wrong. An exact decimal crosses as its digits and a double as its shortest
 * round-trip text, a non-finite one by name (`"NaN"`, `"Infinity"`, `"-Infinity"`); a part that is exact or
 * not, a sum or an extreme, is a JSON number when it is exact and a JSON string when it is a double. An
 * answer's leaves are written column by column: one array per level and per part, never an object per leaf.
 */
export const PivotJson = Object.freeze({
    /** The format version this writer writes and the only one this reader reads. */
    version,

    /** A question for the Leaf Aggregates, as JSON. */
    writeQuery(query: PivotQuery): string {
        return document('query', [
            ['rows', fieldsJson(query.rows)],
            ['columns', fieldsJson(query.columns)],
            ['filters', fieldsJson(query.filters)],
            ['values', jsonArray(query.values.map(value => jsonObject([['field', jsonString(value.field)], ['parts', partsJson(value.parts)]])))],
            ['maxLeaves', int32Json(query.maxLeaves, 'maxLeaves')],
            ...(query.levels === undefined ? [] : [['levels', levelsJson(query.levels)] as [string, string]]),
        ]);
    },

    /** The Leaf Aggregates, or a refusal, as JSON: the leaves column by column. */
    writeAnswer(answer: PivotAnswer): string {
        if (answer.refusal !== undefined) return document('answer', refusalMembers(answer.refusal));
        const leafCount = answer.leafCount;
        const levels = answer.isLevels;
        return document('answer', [
            ['sourceVersion', jsonString(answer.sourceVersion)],
            ...(levels ? [['levels', 'true'] as [string, string]] : []),
            ['rows', axesJson(answer.rows, leafCount)],
            ['columns', axesJson(answer.columns, leafCount)],
            ['records', leavesOf(leafCount, leaf => String(answer.recordsAt(leaf)))],
            ['values', jsonArray(answer.values.map(values => valuesJson(values, leafCount, levels)))],
            ...windowingMembers(answer, leafCount),
        ]);
    },

    /** A question for a field's Items, as JSON. */
    writeItemsQuery(query: PivotItemsQuery): string {
        const members: Members = [['field', jsonString(query.field)], ['sourceVersion', jsonString(query.sourceVersion)]];
        if (query.search !== undefined) members.push(['search', jsonString(query.search)]);
        members.push(['max', int32Json(query.max, 'max')]);
        return document('itemsQuery', members);
    },

    /** A page of Items, or a refusal, as JSON. */
    writeItemPage(page: PivotItemPage): string {
        if (page.refusal !== undefined) return document('itemPage', refusalMembers(page.refusal));
        return document('itemPage', [['sourceVersion', jsonString(page.sourceVersion)], ['items', keysJson(page.items)], ['total', int32Json(page.total, 'total')]]);
    },

    /** A question for the records behind a cell, as JSON. */
    writeDetailsQuery(query: PivotDetailsQuery): string {
        return document('detailsQuery', [
            ['sourceVersion', jsonString(query.sourceVersion)],
            ['rowItems', pathJson(query.rowItems)],
            ['columnItems', pathJson(query.columnItems)],
            ['hiddenItems', fieldsJson(query.hiddenItems)],
            ['start', int32Json(query.start, 'start')],
            ['count', int32Json(query.count, 'count')],
        ]);
    },

    /** A page of the records behind a cell, or a refusal, as JSON. Each value is written by its field's
     * declared type, and one of another type tagged with its own; the Consumer's own record objects are not
     * written. */
    writeDetailPage(page: PivotDetailPage): string {
        if (page.refusal !== undefined) return document('detailPage', refusalMembers(page.refusal));
        const fields = page.fields;
        return document('detailPage', [
            ['sourceVersion', jsonString(page.sourceVersion)],
            ['fields', jsonArray(fields.map(field => {
                const members: Members = [['name', jsonString(field.name)], ['type', jsonString(field.type)]];
                if (field.caption !== field.name) members.push(['caption', jsonString(field.caption)]);
                if (field.format !== undefined) members.push(['format', jsonString(field.format)]);
                return jsonObject(members);
            }))],
            ['start', int32Json(page.start, 'start')],
            ['total', String(page.total)],
            ['records', jsonArray(page.records.map(record => jsonArray(record.values.map((value, f) => detailValueJson(elementAt(fields, f).type, value)))))],
        ]);
    },

    /** A source's notice that its data moved on, as JSON. */
    writeSourceChanged(change: PivotSourceChanged): string {
        return document('sourceChanged', change.sourceVersion === undefined ? [] : [['sourceVersion', jsonString(change.sourceVersion)]]);
    },

    /**
     * A server's Change Batch (ADR-0370 section 3), as JSON: the records its data lost and gained between two
     * Source Versions, each value by its field's declared type, under the names and types of the fields they
     * are in, so a reader whose fields differ refuses the batch rather than place its values in the wrong ones.
     */
    writeChangeBatch(batch: PivotServerChangeBatch, fields: readonly PivotField[]): string {
        return document('changeBatch', [
            ['from', jsonString(batch.from)],
            ['to', jsonString(batch.to)],
            ['fields', jsonArray(fields.map(field => {
                const members: Members = [['name', jsonString(field.name)], ['type', jsonString(field.type)]];
                if (field.datePart !== undefined) members.push(['datePart', jsonString(field.datePart)]);
                return jsonObject(members);
            }))],
            ['removed', batchRecordsJson(batch.removed, fields, 'removed')],
            ['added', batchRecordsJson(batch.added, fields, 'added')],
        ]);
    },

    /** Each field's count of Items (ADR-0370 section 3), as JSON: `counts` holds a member per counted field. */
    writeItemCounts(counts: PivotItemCounts): string {
        return document('itemCounts', [
            ['sourceVersion', jsonString(counts.sourceVersion)],
            ['counts', jsonObject(Array.from(counts.counts, ([field, count]) => [field, String(count)] as const))],
        ]);
    },

    /** A question read back from {@link PivotJson.writeQuery}'s JSON. */
    readQuery(json: string): PivotQuery {
        return read(json, 'query', root => pivotQuery({
            rows: fieldsOf(root, 'rows'),
            columns: fieldsOf(root, 'columns'),
            filters: fieldsOf(root, 'filters'),
            values: objects(root, 'values').map(value => ({ field: string(value, 'field'), parts: partsOf(value) })),
            maxLeaves: int32(root, 'maxLeaves'),
            ...levelsOf(root),
        }));
    },

    /**
     * An answer read back from {@link PivotJson.writeAnswer}'s JSON. Refuses by name a leaf array of the wrong
     * length, an Item index out of range, two Items of one field that are one Item, counts that cannot be —
     * more numbers than values, more values than records — and a part in a form it cannot be: an exact
     * number no decimal holds exactly among them, which is refused rather than rounded.
     */
    readAnswer(json: string): PivotAnswer {
        return read(json, 'answer', root => {
            const refusal = refusalOf(root);
            if (refusal !== undefined) return PivotAnswer.refused(refusal);
            const sourceVersion = string(root, 'sourceVersion');
            const levels = optionalBool(root, 'levels');
            const records = entries(root, 'records').map(entry => integerValue(entry, 'records'));
            records.forEach((count, leaf) => {
                if (count < 1) throw new PivotFormatError(levels ? `Cell ${leaf} holds ${count} records; a cell has records.` : `Leaf ${leaf} holds ${count} records; a leaf has records.`);
            });
            const rows = axesOf(root, 'rows', records.length, levels);
            const columns = axesOf(root, 'columns', records.length, levels);
            const values = objects(root, 'values').map(element => valuesOf(element, records, levels));
            return madeAnswer(sourceVersion, rows, columns, records.length, Object.freeze(records), values, levels, windowingOf(root, records.length, levels));
        });
    },

    /** A question for a field's Items read back from {@link PivotJson.writeItemsQuery}'s JSON. */
    readItemsQuery(json: string): PivotItemsQuery {
        return read(json, 'itemsQuery', root => {
            const search = optionalString(root, 'search');
            return pivotItemsQuery({ field: string(root, 'field'), sourceVersion: string(root, 'sourceVersion'), ...(search === undefined ? {} : { search }), max: int32(root, 'max') });
        });
    },

    /** A page of Items read back from {@link PivotJson.writeItemPage}'s JSON. */
    readItemPage(json: string): PivotItemPage {
        return read(json, 'itemPage', root => {
            const refusal = refusalOf(root);
            return refusal === undefined
                ? PivotItemPage.of(string(root, 'sourceVersion'), keysOf(root, 'items'), int32(root, 'total'))
                : PivotItemPage.refused(refusal);
        });
    },

    /** A question for the records behind a cell read back from {@link PivotJson.writeDetailsQuery}'s JSON. */
    readDetailsQuery(json: string): PivotDetailsQuery {
        return read(json, 'detailsQuery', root => pivotDetailsQuery({
            sourceVersion: string(root, 'sourceVersion'),
            rowItems: pathOf(root, 'rowItems'),
            columnItems: pathOf(root, 'columnItems'),
            hiddenItems: fieldsOf(root, 'hiddenItems'),
            start: int32(root, 'start'),
            count: int32(root, 'count'),
        }));
    },

    /** A page of the records behind a cell read back from {@link PivotJson.writeDetailPage}'s JSON, each value
     * read by its field's declared type. The records carry no object of the Consumer's: those never left the
     * process. */
    readDetailPage(json: string): PivotDetailPage {
        return read(json, 'detailPage', root => {
            const refusal = refusalOf(root);
            if (refusal !== undefined) return PivotDetailPage.refused(refusal);
            const fields = objects(root, 'fields').map(field => {
                const caption = optionalString(field, 'caption');
                const format = optionalString(field, 'format');
                return pivotField({
                    name: string(field, 'name'),
                    type: named(field, 'type', 'type', fieldTypes),
                    ...(caption === undefined ? {} : { caption }),
                    ...(format === undefined ? {} : { format }),
                });
            });
            const records = entries(root, 'records').map((record): PivotDetailRecord => {
                if (!isArray(record)) throw new PivotFormatError('A detail record is an array of values.');
                const values = record;
                if (values.length !== fields.length) throw new PivotFormatError(`A detail record carries ${values.length} values, and the page has ${fields.length} fields.`);
                return { values: values.map((value, f) => detailValueOf(value, elementAt(fields, f))) };
            });
            return PivotDetailPage.of(string(root, 'sourceVersion'), fields, int32(root, 'start'), integer(root, 'total'), records);
        });
    },

    /** A source's notice read back from {@link PivotJson.writeSourceChanged}'s JSON. */
    readSourceChanged(json: string): PivotSourceChanged {
        return read(json, 'sourceChanged', root => {
            const sourceVersion = optionalString(root, 'sourceVersion');
            return Object.freeze(sourceVersion === undefined ? {} : { sourceVersion });
        });
    },

    /**
     * A notice a server sent a fetching source (ADR-0370 section 3), read back from
     * {@link PivotJson.writeSourceChanged}'s or {@link PivotJson.writeChangeBatch}'s JSON, to hand to the fetchers'
     * `notify`. A batch is read against the source's `fields`: one written under other names or types, or in
     * another order, is refused by name.
     */
    readServerNotice(json: string, fields: readonly PivotField[]): PivotServerNotice {
        return read(json, ['sourceChanged', 'changeBatch'], (root, type): PivotServerNotice => {
            if (type === 'sourceChanged') {
                const sourceVersion = optionalString(root, 'sourceVersion');
                return Object.freeze(sourceVersion === undefined ? { kind: 'changed' } : { kind: 'changed', sourceVersion });
            }
            const from = string(root, 'from');
            const to = string(root, 'to');
            if (from.length === 0 || to.length === 0 || from === to) throw new TypeError(batchVersions);
            const written = objects(root, 'fields').map(field => {
                const part = member(field, 'datePart');
                return { name: string(field, 'name'), type: named(field, 'type', 'type', fieldTypes), datePart: part === undefined ? undefined : named(field, 'datePart', 'date part', dateParts) };
            });
            const differs = written.length !== fields.length || written.some((field, f) => {
                const own = elementAt(fields, f);
                return field.name !== own.name || field.type !== own.type || field.datePart !== own.datePart;
            });
            if (differs) throw new PivotFormatError(`The batch's fields are ${fieldList(written)}, and the source's are ${fieldList(fields)}.`);
            const removed = Object.freeze(batchRecordsOf(root, 'removed', fields));
            const added = Object.freeze(batchRecordsOf(root, 'added', fields));
            return Object.freeze({ kind: 'changeBatch', from, to, removed, added });
        });
    },

    /** Each field's count of Items read back from {@link PivotJson.writeItemCounts}'s JSON; a field named twice
     * is refused. */
    readItemCounts(json: string): PivotItemCounts {
        return read(json, 'itemCounts', root => {
            const counts = member(root, 'counts');
            if (!isObject(counts)) throw new PivotFormatError("'counts' is an object, and is required.");
            const [twice] = namesWrittenTwice(counts);
            if (twice !== undefined) throw new PivotFormatError(`'counts' names '${twice}' twice.`);
            return new PivotItemCounts(string(root, 'sourceVersion'), memberNames(counts).map(field => [field, int32Value(defined(member(counts, field), 'a counted field'), 'counts')] as const));
        });
    },
});
