// The Pivot Layout's serialised form (ADR-0059): versioned JSON a Consumer stores as View State and reads
// back as the same layout, the C# engine's PivotLayoutJson member for member. A document of a version this
// reader does not know is refused rather than half-read, and so is a member of the wrong type or an
// unknown name; a member left out takes its default.

import { PivotFormatError, PivotVersionError } from '../errors';
import { pivotItemKey, type PivotItemKey } from '../items/item-key';
import { pivotAggregations, pivotItemKinds, type PivotAggregation, type PivotItemKind, type PivotShowValuesAs } from './pivot-enums';
import {
    createPivotFieldPlacement, createPivotLayout, createPivotValueField, pivotSort,
    type PivotFieldPlacement, type PivotLayout, type PivotValueField,
} from './pivot-layout';

/** The version this writer writes and the only one this reader reads. */
export const pivotLayoutJsonVersion = 1;

/** The layout as JSON, its members in the C# writer's order. */
export function writePivotLayoutJson(layout: PivotLayout): string {
    return JSON.stringify({
        version: pivotLayoutJsonVersion,
        form: layout.form,
        valuesAxis: layout.valuesAxis,
        subtotalsAtTop: layout.subtotalsAtTop,
        grandTotalRow: layout.grandTotalRow,
        grandTotalColumn: layout.grandTotalColumn,
        repeatItemLabels: layout.repeatItemLabels,
        filters: layout.filters.map(placementJson),
        rows: layout.rows.map(placementJson),
        columns: layout.columns.map(placementJson),
        values: layout.values.map(valueJson),
    });
}

function placementJson(placement: PivotFieldPlacement): Record<string, unknown> {
    const json: Record<string, unknown> = { field: placement.field };
    if (placement.sort.direction !== 'ascending' || placement.sort.byValue !== undefined) {
        const sort: Record<string, unknown> = { direction: placement.sort.direction };
        if (placement.sort.byValue !== undefined) sort['byValue'] = placement.sort.byValue;
        json['sort'] = sort;
    }
    if (!placement.subtotals) json['subtotals'] = false;
    if (placement.collapsed) json['collapsed'] = true;
    if (placement.hiddenItems.length > 0) json['hiddenItems'] = placement.hiddenItems.map(keyJson);
    if (placement.toggledItems.length > 0) json['toggledItems'] = placement.toggledItems.map(keyJson);
    return json;
}

function keyJson(key: PivotItemKey): Record<string, unknown> {
    return key.value === undefined ? { kind: key.kind } : { kind: key.kind, value: key.value };
}

function valueJson(value: PivotValueField): Record<string, unknown> {
    const json: Record<string, unknown> = { field: value.field, aggregation: value.aggregation };
    if (value.caption !== undefined) json['caption'] = value.caption;
    if (value.showValuesAs !== 'noCalculation') json['showValuesAs'] = value.showValuesAs;
    if (value.numberFormat !== undefined) json['numberFormat'] = value.numberFormat;
    return json;
}

/** A number as the document spells it: C# reads an integer from its spelling, so 1.0 is not one. */
class JsonNumber {
    readonly raw: string;

    constructor(raw: string) {
        this.raw = raw;
    }

    /** The spelling's value as a 32-bit integer, or `undefined` where it has a fraction, an exponent or
     * a value outside 32 bits, as `JsonElement.TryGetInt32` refuses it. */
    get int32(): number | undefined {
        if (!/^-?(?:0|[1-9]\d*)$/u.test(this.raw)) return undefined;
        const value = Number(this.raw);
        return value >= -(2 ** 31) && value < 2 ** 31 ? value + 0 : undefined;
    }
}

type Json = null | boolean | JsonNumber | string | readonly Json[] | { readonly [key: string]: Json };
type JsonObject = { readonly [key: string]: Json };

// The values of a text JSON.parse has read without complaint, each number kept as it is spelled; a name
// written twice is the last one written, as JSON.parse and JsonElement both take it.
function readJson(text: string): Json {
    let at = 0;
    const skip = (): void => {
        while (at < text.length && ' \t\n\r'.includes(text.charAt(at))) at++;
    };
    const stringAt = (): string => {
        const start = at;
        at++;
        while (text.charAt(at) !== '"') at += text.charAt(at) === '\\' ? 2 : 1;
        at++;
        const decoded: unknown = JSON.parse(text.slice(start, at));
        if (typeof decoded !== 'string') throw new Error('Internal: a JSON string read as another value.');
        return decoded;
    };
    const valueAt = (): Json => {
        skip();
        const ch = text.charAt(at);
        if (ch === '{') {
            at++;
            const object: { [key: string]: Json } = {};
            skip();
            if (text.charAt(at) === '}') {
                at++;
                return object;
            }
            for (;;) {
                skip();
                const name = stringAt();
                skip();
                at++; // the colon
                Object.defineProperty(object, name, { value: valueAt(), enumerable: true, writable: true, configurable: true });
                skip();
                if (text.charAt(at++) === '}') return object;
            }
        }
        if (ch === '[') {
            at++;
            const entries: Json[] = [];
            skip();
            if (text.charAt(at) === ']') {
                at++;
                return entries;
            }
            for (;;) {
                entries.push(valueAt());
                skip();
                if (text.charAt(at++) === ']') return entries;
            }
        }
        if (ch === '"') return stringAt();
        for (const [word, value] of [['true', true], ['false', false], ['null', null]] as const) {
            if (text.startsWith(word, at)) {
                at += word.length;
                return value;
            }
        }
        const number = /-?(?:0|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?/uy;
        number.lastIndex = at;
        const spelled = number.exec(text);
        if (spelled === null) throw new Error('Internal: JSON.parse read a value this reader does not.');
        at += spelled[0].length;
        return new JsonNumber(spelled[0]);
    };
    return valueAt();
}

function isObject(value: Json | undefined): value is JsonObject {
    return typeof value === 'object' && value !== null && !Array.isArray(value) && !(value instanceof JsonNumber);
}

function member(element: JsonObject, name: string): Json | undefined {
    return Object.hasOwn(element, name) ? element[name] : undefined;
}

/**
 * A layout read back from {@link writePivotLayoutJson}'s JSON. Refuses by name, with a PivotFormatError, a
 * document that is not an object, an unknown name for a form, an axis, an Aggregation, a Show Values As, a
 * sort direction or an Item kind, and a member of the wrong type; and with a PivotVersionError a document of
 * another version.
 */
export function readPivotLayoutJson(json: string): PivotLayout {
    try {
        JSON.parse(json);
    } catch (error) {
        throw new PivotFormatError('A Pivot Layout is JSON.', { cause: error });
    }
    const parsed = readJson(json);
    if (!isObject(parsed)) throw new PivotFormatError('A Pivot Layout is a JSON object.');
    const version = member(parsed, 'version');
    if (!(version instanceof JsonNumber)) throw new PivotFormatError('A Pivot Layout names its version.');
    if (version.int32 !== pivotLayoutJsonVersion) {
        throw new PivotVersionError(`A Pivot Layout of version ${version.raw} cannot be read; this reader reads version ${pivotLayoutJsonVersion}.`);
    }
    return createPivotLayout({
        form: parse(parsed, 'form', 'form', ['compact', 'outline', 'tabular'], 'compact'),
        valuesAxis: parse(parsed, 'valuesAxis', 'valuesAxis', ['columns', 'rows'], 'columns'),
        subtotalsAtTop: boolean(parsed, 'subtotalsAtTop', true),
        grandTotalRow: boolean(parsed, 'grandTotalRow', true),
        grandTotalColumn: boolean(parsed, 'grandTotalColumn', true),
        repeatItemLabels: boolean(parsed, 'repeatItemLabels', false),
        filters: readPlacements(parsed, 'filters'),
        rows: readPlacements(parsed, 'rows'),
        columns: readPlacements(parsed, 'columns'),
        values: array(parsed, 'values').map(readValue),
    });
}

function readPlacements(root: JsonObject, name: string): PivotFieldPlacement[] {
    return array(root, name).map(element => {
        const sortJson = member(element, 'sort');
        let sort = pivotSort('ascending');
        if (sortJson !== undefined) {
            if (!isObject(sortJson)) throw new PivotFormatError("'sort' is an object.");
            const direction = parse(sortJson, 'direction', 'direction', ['ascending', 'descending'], 'ascending');
            const byValue = member(sortJson, 'byValue');
            const index = byValue instanceof JsonNumber ? byValue.int32 : undefined;
            if (byValue !== undefined && index === undefined) throw new PivotFormatError("'byValue' is an integer.");
            sort = pivotSort(direction, index);
        }
        return createPivotFieldPlacement({
            field: string(element, 'field'),
            sort,
            subtotals: boolean(element, 'subtotals', true),
            collapsed: boolean(element, 'collapsed', false),
            hiddenItems: readKeys(element, 'hiddenItems'),
            toggledItems: readKeys(element, 'toggledItems'),
        });
    });
}

function readKeys(element: JsonObject, name: string): PivotItemKey[] {
    return array(element, name).map(key => {
        const kind: PivotItemKind = parse(key, 'kind', 'kind', pivotItemKinds, 'blank');
        const value = member(key, 'value');
        if (value !== undefined && typeof value !== 'string') throw new PivotFormatError("An Item's 'value' is a string.");
        return pivotItemKey(kind, value);
    });
}

function readValue(element: JsonObject): PivotValueField {
    const aggregation: PivotAggregation = parse(element, 'aggregation', 'aggregation', pivotAggregations, 'sum');
    const showValuesAs: PivotShowValuesAs = parse(element, 'showValuesAs', 'showValuesAs',
        ['noCalculation', 'percentOfGrandTotal', 'percentOfColumnTotal', 'percentOfRowTotal'], 'noCalculation');
    const caption = optionalString(element, 'caption');
    const numberFormat = optionalString(element, 'numberFormat');
    return createPivotValueField({
        field: string(element, 'field'),
        aggregation,
        showValuesAs,
        ...(caption === undefined ? {} : { caption }),
        ...(numberFormat === undefined ? {} : { numberFormat }),
    });
}

function array(element: JsonObject, name: string): JsonObject[] {
    const value = member(element, name);
    if (value === undefined) return [];
    if (!Array.isArray(value)) throw new PivotFormatError(`'${name}' is an array.`);
    return (value as readonly Json[]).map(entry => {
        // An entry that is not an object has no member to read: C#'s JsonElement throws an
        // InvalidOperationException, which is a plain Error here.
        if (!isObject(entry)) throw new Error(`An entry of '${name}' is not an object.`);
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

function boolean(element: JsonObject, name: string, fallback: boolean): boolean {
    const value = member(element, name);
    if (value === undefined) return fallback;
    if (typeof value !== 'boolean') throw new PivotFormatError(`'${name}' is true or false.`);
    return value;
}

function parse<T extends string>(element: JsonObject, name: string, what: string, names: readonly T[], fallback: T): T {
    const value = member(element, name);
    if (value === undefined) return fallback;
    if (typeof value !== 'string') throw new PivotFormatError(`'${name}' is a string.`);
    const known = names.find(candidate => candidate === value);
    if (known === undefined) throw new PivotFormatError(`'${value}' is not a ${what} this reader knows.`);
    return known;
}
