// Layer 1: the layout cases (tests/cases/pivot; ADR-0370 sections 2 and 5), run against the Script Core's
// engine. Kind "pivotLayoutEdits": a layout and the Field List gestures and Layout menu choices made on it
// (ADR-0061), each answered by the layout it makes, `same` where it hands back the layout it was given, the
// refusal of an edit that may be refused, or what it throws; a step may also read the Layout menu and the
// report. Kind "pivotLayoutJson": a saved layout read and written again (ADR-0059), or what refuses it.
// dotnet/tests/ExPivot.Engine.Tests/Cases/PivotLayoutCaseTests.cs is the C# runner, and the one that writes the
// answers.
import { describe, expect, it } from 'vitest';
import {
    chooseLayout, computePivot, isLayoutChosen, layoutChoiceChanges, moveEntry, pivotLayoutChoices, placeField, readPivotLayoutJson,
    refusalWord, removeEntry, setCollapsed, setFieldCollapsed, setHiddenItems, setSort, setSubtotals, setValueField, tickField,
    untickField, valuesPseudoField, writePivotLayoutJson, type PivotArea, type PivotEditResult, type PivotEntry, type PivotFieldInfo,
    type PivotLayout, type PivotLayoutChoice, type PivotOptions,
} from '../src/index';
import { defined } from '../src/must';
import { englishWordFor } from '../src/words/pivot-words';
import { array, caseFiles, dataSet, itemKeyOf, layoutOf, must, object, optionsOf, orNull, parseJson, refusalOf, text, type DataSet, type Json, type JsonObject } from './cases';
import { read } from './readings';

const areas: readonly PivotArea[] = ['filters', 'rows', 'columns', 'values'];

function areaOf(name: Json): PivotArea {
    const found = areas.find(a => a === name);
    if (found === undefined) throw new Error(`${JSON.stringify(name)} is not an Area.`);
    return found;
}

function choiceOf(name: Json): PivotLayoutChoice {
    const found = pivotLayoutChoices.find(c => c === name);
    if (found === undefined) throw new Error(`${JSON.stringify(name)} is not a Layout menu choice.`);
    return found;
}

function numberOf(written: Json): number {
    if (typeof written !== 'number') throw new TypeError(`${JSON.stringify(written)} is not a number.`);
    return written;
}

function booleanOf(written: Json): boolean {
    if (typeof written !== 'boolean') throw new TypeError(`${JSON.stringify(written)} is not true or false.`);
    return written;
}

// The layout as a case writes it: the layout's JSON without its version.
function layoutJson(layout: PivotLayout): Json {
    const { version: _, ...rest } = object(parseJson(writePivotLayoutJson(layout)));
    return rest;
}

// An edit as a step writes it: its gesture's name, the field or entry it is made on, and its arguments.
function apply(layout: PivotLayout, step: JsonObject, infos: ReadonlyMap<string, PivotFieldInfo>, data: DataSet, options: PivotOptions):
    { readonly made: PivotLayout; readonly result?: PivotEditResult } {
    const info = (field: Json): PivotFieldInfo => {
        const found = infos.get(text(field));
        if (found === undefined) throw new Error(`The data set declares no field ${JSON.stringify(field)}.`);
        return found;
    };
    const entry = (written: Json): PivotEntry => (typeof written === 'string'
        ? valuesPseudoField(layout.valuesAxis)
        : { area: areaOf(must(object(written), 'area')), index: numberOf(must(object(written), 'index')) });
    const tick = step['tick'];
    if (tick !== undefined) return { made: tickField(layout, info(tick)) };
    const untick = step['untick'];
    if (untick !== undefined) return { made: untickField(layout, text(untick)) };
    const place = step['place'];
    if (place !== undefined) return { made: placeField(layout, info(place), areaOf(must(step, 'area')), numberOf(must(step, 'index'))) };
    const move = step['move'];
    if (move !== undefined) {
        const field = step['field'];
        return { made: moveEntry(layout, entry(move), areaOf(must(step, 'to')), numberOf(must(step, 'index')), field === undefined ? undefined : info(field)) };
    }
    const remove = step['remove'];
    if (remove !== undefined) return { made: removeEntry(layout, entry(remove)) };
    const hide = step['setHiddenItems'];
    if (hide !== undefined) {
        const result = setHiddenItems(layout, text(hide), array(must(step, 'hidden')).map(itemKeyOf), array(must(step, 'items')).map(itemKeyOf));
        return { made: result.layout, result };
    }
    const sort = step['setSort'];
    if (sort !== undefined) {
        const [placement] = layoutOf({ rows: [{ field: 'f', sort: must(step, 'sort') }] }).rows;
        if (placement === undefined) throw new Error('The sort reads as no placement.');
        return { made: setSort(layout, text(sort), placement.sort) };
    }
    const subtotals = step['setSubtotals'];
    if (subtotals !== undefined) return { made: setSubtotals(layout, text(subtotals), booleanOf(must(step, 'subtotals'))) };
    const collapse = step['setCollapsed'];
    if (collapse !== undefined) return { made: setCollapsed(layout, text(collapse), itemKeyOf(must(step, 'item')), booleanOf(must(step, 'collapsed'))) };
    const field = step['setFieldCollapsed'];
    if (field !== undefined) return { made: setFieldCollapsed(layout, text(field), booleanOf(must(step, 'collapsed'))) };
    const index = step['setValueField'];
    if (index !== undefined) {
        const [value] = layoutOf({ values: [must(step, 'value')] }).values;
        if (value === undefined) throw new Error('The Value Field reads as none.');
        const result = setValueField(layout, numberOf(index), value, data.fields, options);
        return { made: result.layout, result };
    }
    const choice = step['choose'];
    if (choice !== undefined) return { made: chooseLayout(layout, choiceOf(choice)) };
    throw new Error(`${JSON.stringify(step)} names no edit the cases make.`);
}

function edits(file: JsonObject, entry: JsonObject): Json {
    const data = dataSet(file, entry);
    const infos = new Map(data.fields.map(f => [f.name, { name: f.name, caption: f.caption, type: f.type }]));
    const options = optionsOf(file, entry);
    const start = layoutOf(must(entry, 'layout'));
    let previous = start;
    return array(must(entry, 'edits')).map(written => {
        const step = object(written);
        const on = step['on'] === 'previous' ? previous : start;
        try {
            const { made, result } = apply(on, step, infos, data, options);
            const answer: Record<string, Json> = {};
            const refusal = result?.refusal;
            if (refusal !== undefined) {
                answer['refused'] = refusal;
                answer['word'] = orNull(englishWordFor(refusalWord(refusal)));
            } else if (made === on) {
                answer['same'] = true;
            } else {
                answer['layout'] = layoutJson(made);
            }
            if (step['menu'] !== undefined) {
                answer['chosen'] = pivotLayoutChoices.filter(c => isLayoutChosen(made, c));
                answer['enabled'] = pivotLayoutChoices.filter(c => layoutChoiceChanges(made, c));
            }
            if (step['report'] !== undefined) answer['report'] = read(computePivot(data.records, data.fields, made, options), ['captions'], data);
            previous = made;
            return answer;
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the step's answer, compared below
            return refusalOf(error);
        }
    });
}

function layoutJsonAnswer(entry: JsonObject): Json {
    try {
        return writePivotLayoutJson(readPivotLayoutJson(text(must(entry, 'read'))));
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the document's answer, compared below
        return refusalOf(error);
    }
}

// What a case expects of the Script Core: a step's refusal that names the platform's own API, or that .NET
// splits from the value it refuses, says the Script Core's message in the step's `scriptSaying`; a whole
// answer that differs is the case's `scriptExpect`.
function expected(entry: JsonObject): Json {
    const whole = entry['scriptExpect'];
    if (whole !== undefined) return whole;
    const answer = must(entry, 'expect');
    const steps = entry['edits'];
    if (steps === undefined) return answer;
    return array(answer).map((step, i) => {
        const saying = object(defined(array(steps)[i], 'a step for each answer'))['scriptSaying'];
        return saying === undefined ? step : { ...object(step), saying };
    });
}

const kinds: readonly [string, (file: JsonObject, entry: JsonObject) => Json][] = [
    ['pivotLayoutEdits', edits],
    ['pivotLayoutJson', (_, entry) => layoutJsonAnswer(entry)],
];

for (const [name, answer] of kinds) {
    for (const file of caseFiles(name)) {
        describe(`pivot/${file.file}`, () => {
            for (const entry of file.cases) {
                it(text(must(entry, 'name')), () => {
                    expect(answer(file.document, entry)).toEqual(expected(entry));
                });
            }
        });
    }
}
