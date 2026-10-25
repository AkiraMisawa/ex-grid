// What the Script Core's engine answers to a Pivot Source case (kind "pivotSource"; tests/cases/README.md): a
// source over a data set's records or a Snapshot its columns make, optionally behind a fetching source, asked
// for its fields, Leaf Aggregates, Items, the records behind a cell, or a report. The C# side is
// tests/ExPivot.Engine.Tests/Cases/PivotSourceCases.cs, which writes the answers and which tools/pivot-oracle
// runs for a differential run's random cases.
import { decimalText, ticksText } from '@ex-grid/data';
import {
    datePartField, FetchingPivotSource, pivotAggregations, pivotCube, pivotDetailsQuery, pivotField, pivotItemsQuery, pivotParts, pivotQuery,
    pivotReport, PivotSourceFeatures, queryForLayout, RecordPivotSource, SnapshotPivotSource, type PivotAggregation, type PivotAnswer,
    type PivotAnswerAxis, type PivotDatePart, type PivotDetailValue, type PivotField, type PivotFieldItem, type PivotFieldType,
    type PivotNumber, type PivotOptions, type PivotPart, type PivotQuery, type PivotQueryField, type PivotSource, type PivotSourceRefusal,
} from '../src/index';
import { isDecimal } from '../src/items/record-value';
import { array, dataSet, itemKeyOf, layoutOf, must, object, optionsOf, refusalOf, snapshotOf, text, type DataSet, type Json, type JsonObject } from './cases';
import { itemKey, read } from './readings';

// A source and how often its server was asked for Leaf Aggregates.
interface Bench {
    source: PivotSource;
    readonly again: () => PivotSource;
    readonly data: DataSet;
    asked: number;
    version: string | undefined;
}

function member<T extends string>(members: readonly T[], name: Json, what: string): T {
    const found = members.find(m => m === name);
    if (found === undefined) throw new Error(`${JSON.stringify(name)} is not ${what}.`);
    return found;
}

const fieldTypes: readonly PivotFieldType[] = ['text', 'number', 'date', 'boolean'];
const dateParts: readonly PivotDatePart[] = ['year', 'quarter', 'month'];

// A field declared over a Snapshot's columns: its name, type, caption, format, and the column it reads, or the
// part of a Date column it is.
function declared(written: JsonObject): PivotField {
    const name = text(must(written, 'name'));
    const caption = written['caption'];
    const part = written['datePart'];
    if (part !== undefined) {
        return datePartField(name, text(must(written, 'column')), member(dateParts, part, 'a date part'), caption === undefined ? undefined : text(caption));
    }
    const format = written['format'];
    const column = written['column'];
    return pivotField({
        name,
        type: member(fieldTypes, must(written, 'type'), 'a field type'),
        ...(caption === undefined ? {} : { caption: text(caption) }),
        ...(format === undefined ? {} : { format: text(format) }),
        ...(column === undefined ? {} : { column: text(column) }),
    });
}

// The source a case asks: over the data set's Snapshot or its records, and behind a fetching source when the
// case names one.
function open(file: JsonObject, entry: JsonObject): Bench {
    const snapshot = snapshotOf(file, entry);
    const data: DataSet = snapshot === undefined ? dataSet(file, entry) : { fields: [], records: [], keyCalls: [] };
    const fields = entry['declare'] === undefined ? undefined : array(entry['declare']).map(f => declared(object(f)));
    const bundled = (): PivotSource => (snapshot === undefined ? new RecordPivotSource(data.records, data.fields) : new SnapshotPivotSource(snapshot, fields));
    const fetch = entry['fetch'] === undefined ? undefined : object(entry['fetch']);
    const make = (): PivotSource => {
        const inner = bundled();
        if (fetch === undefined) return inner;
        const offers = fetch['offers'];
        const features = offers === undefined ? inner.features : new PivotSourceFeatures(array(offers).map(a => member<PivotAggregation>(pivotAggregations, a, 'an Aggregation')));
        const answers = fetch['answers'];
        const instead = answers === undefined ? undefined : queryOf(object(answers));
        return new FetchingPivotSource(inner.fields, features, {
            aggregate: (query, signal) => {
                bench.asked++;
                return inner.aggregate(instead ?? query, signal);
            },
            items: (query, signal) => inner.items(query, signal),
            details: (query, signal) => inner.details(query, signal),
        });
    };
    const bench: Bench = { source: make(), again: make, data, asked: 0, version: undefined };
    return bench;
}

/** The engine's answer to a source case: each step's answer, or the refusal that made the source. */
export async function sourceAnswer(file: JsonObject, entry: JsonObject): Promise<Json> {
    let bench: Bench;
    try {
        bench = open(file, entry);
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a source refused at its making is the case's answer, compared below
        return refusalOf(error);
    }
    const options = optionsOf(file, entry);
    const steps: Json[] = [];
    for (const step of array(must(entry, 'asks'))) {
        try {
            steps.push(await ask(bench, object(step), options));
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the step's answer, compared below
            steps.push(refusalOf(error));
        }
    }
    return steps;
}

async function ask(bench: Bench, step: JsonObject, options: PivotOptions): Promise<Json> {
    const source = bench.source;
    if (step['fields'] !== undefined) return source.fields.map(fieldJson);
    const asked = step['aggregate'];
    if (asked !== undefined) {
        const answered = await source.aggregate(queryOf(object(asked)));
        if (answered.refusal !== undefined) return refused(answered.refusal, undefined);
        bench.version = answered.sourceVersion;
        return answerJson(answered);
    }
    const field = step['items'];
    if (field !== undefined) {
        const under = underOf(bench, step);
        const search = step['search'];
        const max = step['max'];
        const page = await source.items(pivotItemsQuery({
            field: text(field), sourceVersion: under, ...(search === undefined ? {} : { search: text(search) }), ...(max === undefined ? {} : { max: whole(max) }),
        }));
        if (page.refusal !== undefined) return refused(page.refusal, under);
        return { version: page.sourceVersion === under ? 'asked' : page.sourceVersion, total: page.total, items: page.items.map(itemKey) };
    }
    const cell = step['details'];
    if (cell !== undefined) {
        const under = underOf(bench, step);
        const page = await source.details(detailsOf(under, object(cell)));
        if (page.refusal !== undefined) return refused(page.refusal, under);
        return {
            version: page.sourceVersion === under ? 'asked' : page.sourceVersion,
            start: page.start,
            total: page.total,
            fields: page.fields.map(f => f.name),
            records: page.records.map(r => r.values.map(detailValue)),
        };
    }
    const written = step['report'];
    if (written !== undefined) {
        const layout = layoutOf(written);
        const query = queryForLayout(layout);
        const answered = await source.aggregate(query);
        if (answered.refusal !== undefined) return refused(answered.refusal, undefined);
        const report = pivotReport(pivotCube(query, answered, source.fields), layout, options);
        const also = step['also'] === undefined ? [] : array(step['also']).map(text);
        return read(report, also, bench.data);
    }
    const cubed = step['cube'];
    if (cubed !== undefined) {
        const answered = await source.aggregate(queryOf(object(must(step, 'from'))));
        pivotCube(queryOf(object(cubed)), answered, source.fields);
        return { built: true };
    }
    if (step['asked'] !== undefined) return bench.asked;
    if (step['refresh'] !== undefined) {
        bench.source = bench.again();
        return { refreshed: true };
    }
    throw new Error(`${JSON.stringify(step)} asks nothing the cases ask.`);
}

// The version a page is asked under: the last answer's, or "stale", one no source held.
function underOf(bench: Bench, step: JsonObject): string {
    if (step['under'] === 'stale') return 'stale';
    if (bench.version === undefined) throw new Error('A page is asked for under an answer\'s version, and no question was answered yet.');
    return bench.version;
}

function whole(written: Json): number {
    if (typeof written !== 'number' || !Number.isSafeInteger(written)) throw new TypeError(`${JSON.stringify(written)} is not a whole number.`);
    return written;
}

// ---- What a case writes -------------------------------------------------------------------------

function placedOf(written: Json): PivotQueryField {
    if (typeof written === 'string') return { field: written, hiddenItems: [] };
    const placed = object(written);
    const hidden = placed['hiddenItems'];
    return { field: text(must(placed, 'field')), hiddenItems: hidden === undefined ? [] : array(hidden).map(itemKeyOf) };
}

// A question as a case writes it: each placed field by name, or with its Hidden Items; each value by name
// (its counts), or with the parts it asks for; and the most leaves.
function queryOf(written: JsonObject): PivotQuery {
    const placed = (area: string): PivotQueryField[] => {
        const fields = written[area];
        return fields === undefined ? [] : array(fields).map(placedOf);
    };
    const values = written['values'] === undefined ? [] : array(written['values']).map(v => {
        if (typeof v === 'string') return { field: v, parts: [] };
        const value = object(v);
        const parts = value['parts'];
        return { field: text(must(value, 'field')), parts: parts === undefined ? [] : array(parts).map(p => member<PivotPart>(pivotParts, p, 'a part')) };
    });
    const maxLeaves = written['maxLeaves'];
    return pivotQuery({
        rows: placed('rows'), columns: placed('columns'), filters: placed('filters'), values,
        ...(maxLeaves === undefined ? {} : { maxLeaves: whole(maxLeaves) }),
    });
}

// The records behind a cell as a case writes them: the row's and the column's Items, each a field and its
// Item's key, the Hidden Items of placed fields, and the page.
function detailsOf(sourceVersion: string, written: JsonObject): ReturnType<typeof pivotDetailsQuery> {
    const path = (key: string): PivotFieldItem[] => {
        const steps = written[key];
        return steps === undefined ? [] : array(steps).map(s => ({ field: text(must(object(s), 'field')), item: itemKeyOf(must(object(s), 'item')) }));
    };
    const hidden = written['hiddenItems'];
    const start = written['start'];
    const count = written['count'];
    return pivotDetailsQuery({
        sourceVersion,
        rowItems: path('rows'),
        columnItems: path('columns'),
        hiddenItems: hidden === undefined ? [] : array(hidden).map(placedOf),
        ...(start === undefined ? {} : { start: whole(start) }),
        ...(count === undefined ? {} : { count: whole(count) }),
    });
}

// ---- What a source says -------------------------------------------------------------------------

function fieldJson(field: PivotField): Json {
    return {
        name: field.name, caption: field.caption, type: field.type,
        ...(field.format === undefined ? {} : { format: field.format }),
        ...(field.column === undefined ? {} : { column: field.column }),
        ...(field.datePart === undefined ? {} : { datePart: field.datePart }),
    };
}

// A refusal by its kind, the field and the cap it names, and its message, the version asked under written
// {version} where it names it.
function refused(refusal: PivotSourceRefusal, under: string | undefined): Json {
    return {
        refused: refusal.kind,
        ...(refusal.field === undefined ? {} : { field: refusal.field }),
        ...(refusal.limit === undefined ? {} : { limit: refusal.limit }),
        saying: under === undefined ? refusal.message : refusal.message.replaceAll(under, '{version}'),
    };
}

function axes(list: readonly PivotAnswerAxis[]): Json {
    return list.map(a => ({ field: a.field, items: a.items.map(itemKey) }));
}

// The Leaf Aggregates as a case writes them: the leaves, each axis's field and Items, each leaf's Item at
// every level (rows, then columns) and its records, and each value's parts and what it carries at every
// leaf, its numbers to the last bit.
function answerJson(answered: PivotAnswer): Json {
    const levels = [...answered.rows, ...answered.columns];
    const leaves: number[] = Array.from({ length: answered.leafCount }, (_, leaf) => leaf);
    return {
        leaves: answered.leafCount,
        rows: axes(answered.rows),
        columns: axes(answered.columns),
        leaf: leaves.map(leaf => ({ at: levels.map(a => a.itemAt(leaf)), records: answered.recordsAt(leaf) })),
        values: answered.values.map(value => ({
            field: value.field,
            parts: value.parts,
            leaves: leaves.map(leaf => {
                const carried: Record<string, Json> = { values: value.valuesAt(leaf), numbers: value.numbersAt(leaf) };
                if (value.nonFiniteAt(leaf)) carried['nonFinite'] = true;
                if (value.parts.includes('sum')) carried['sum'] = numberJson(value.sumAt(leaf));
                if (value.parts.includes('extremes')) {
                    carried['min'] = numberJson(value.minAt(leaf));
                    carried['max'] = numberJson(value.maxAt(leaf));
                }
                if (value.parts.includes('product')) carried['product'] = doubleJson(value.productAt(leaf));
                if (value.parts.includes('variance')) {
                    carried['mean'] = doubleJson(value.meanAt(leaf));
                    carried['m2'] = doubleJson(value.m2At(leaf));
                }
                return carried;
            }),
        })),
    };
}

// An exact number as {"decimal": its value, trailing zeros dropped}, a double as a number, or as {"double": …}
// where JSON cannot hold it or would drop its sign (-0).
function numberJson(number: PivotNumber): Json {
    return number.kind === 'exact' ? exactJson(decimalText(number.value)) : doubleJson(number.value);
}

function exactJson(written: string): Json {
    return { decimal: written.includes('.') ? written.replace(/0+$/u, '').replace(/\.$/u, '') : written };
}

function doubleJson(number: number): Json {
    if (Object.is(number, -0)) return { double: '-0' };
    return Number.isFinite(number) ? number : { double: String(number) };
}

// A value a record behind a cell carries, as a case writes a value: a date, carried by its ticks (a whole
// number is carried as a decimal), as its text.
function detailValue(value: PivotDetailValue): Json {
    if (value === null || typeof value === 'string' || typeof value === 'boolean') return value;
    if (typeof value === 'number') return doubleJson(value);
    if (isDecimal(value)) return exactJson(decimalText(value));
    const written = ticksText(value);
    return { date: written.endsWith('T00:00:00') ? written.slice(0, -'T00:00:00'.length) : written };
}
