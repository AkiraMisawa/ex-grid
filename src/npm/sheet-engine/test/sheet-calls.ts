// Runs a shared case of kind `sheetCalls` (tests/cases/README.md) against the ported engine, as
// tests/ExSheet.Engine.Tests/Cases/SheetCallCases.cs runs it against ExSheet.Engine: one call of the engine's
// surface, its arguments, and what it answers or refuses. C# finds the member by reflection; this runner names
// each one here, as the Script Core spells it.
import { CellAddress } from '../src/cell-address';
import { CellRange } from '../src/cell-range';
import { serialFromDate } from '../src/date-serial';
import { DeclaredFunction } from '../src/declared-function';
import { Entry } from '../src/entry';
import {
    completeFormula, constantText, cycleReference, formulaHintAt, formulaPointAt, formulaReferences, lookupFinds, lookupText,
    pointedReferenceAt, referenceText, structuredReferenceText,
} from '../src/formula-entry';
import { Value } from '../src/value';
import { CaseError, compare, cultureOf, decode, encode, isObject, list, object, text, thrown, type Json, type JsonObject } from './sheet-case';

type Callable = (...args: unknown[]) => unknown;

/** The subjects a case may name: a class, whose statics are its members and which `new` makes, or a module's functions under the C# class's name. */
const subjects: Readonly<Record<string, object>> = {
    CellAddress,
    CellRange,
    // C#'s DateSerial is a static class: its member is the module's function here, under the C# name.
    DateSerial: { fromDate: serialFromDate },
    DeclaredFunction,
    Entry,
    // C#'s FormulaEntry is a static class: its members are the module's functions here, under the C# names.
    FormulaEntry: {
        complete: completeFormula,
        constantText,
        cycleReference,
        hintAt: formulaHintAt,
        lookupFinds,
        lookupText,
        pointAt: formulaPointAt,
        pointedReferenceAt,
        references: formulaReferences,
        referenceText,
        structuredReferenceText,
    },
};

/** An argument as a case writes it: JSON as it stands, or one of the engine's own values, tagged. */
function argument(node: Json): unknown {
    if (Array.isArray(node)) return (node as readonly Json[]).map(argument);
    // null is the engine's absence, which the Script Core spells undefined.
    if (node === null) return undefined;
    if (!isObject(node)) return node;
    const keys = Object.keys(node);
    const tag = keys[0];
    if (keys.length !== 1 || tag === undefined) throw new CaseError(`${JSON.stringify(node)} is no argument: an object is one tag.`);
    const given = node[tag];
    if (given === undefined) throw new CaseError(`${JSON.stringify(node)} holds nothing under '${tag}'.`);
    switch (tag) {
        case 'value':
            return decode(given);
        case 'address':
            return CellAddress.parse(text(given, 'an address'));
        case 'range':
            return CellRange.parse(text(given, 'a range'));
        case 'culture':
            return cultureOf(text(given, 'a culture'));
        case 'lookup': {
            // A function from a name to what the map holds for it, found without regard to case, or nothing.
            const map = object(given, 'a lookup');
            return (name: string) => {
                const key = Object.keys(map).find(k => k.toUpperCase() === name.toUpperCase());
                const held = key === undefined ? undefined : map[key];
                return held === undefined ? undefined : argument(held);
            };
        }
        default:
            throw new CaseError(`'${tag}' is no tag an argument takes.`);
    }
}

/**
 * The answer, as JSON in the shape the case expects it: a Value as a sheet case writes one, an address or a range
 * as its text unless the case reads its members, a list element by element, and an object by the keys the case writes, each read as a member.
 */
function project(answer: unknown, expected: Json | undefined): Json {
    if (answer === undefined || answer === null) return null;
    if (answer instanceof Value) return encode(answer);
    const shaped = expected !== undefined && isObject(expected);
    if ((answer instanceof CellAddress || answer instanceof CellRange) && !shaped) return answer.toString();
    if (typeof answer === 'string' || typeof answer === 'number' || typeof answer === 'boolean') return answer;
    if (typeof answer === 'bigint') return answer.toString();
    if (Array.isArray(answer) || (typeof answer === 'object' && Symbol.iterator in answer)) {
        const items = [...(answer as Iterable<unknown>)];
        const shapes = Array.isArray(expected) ? (expected as readonly Json[]) : [];
        return items.map((item, i) => project(item, shapes[i]));
    }
    if (typeof answer === 'object' && shaped) {
        const shape = expected;
        const read: Record<string, Json> = {};
        for (const key of Object.keys(shape)) read[key] = project((answer as Record<string, unknown>)[key], shape[key]);
        return read;
    }
    const named = typeof answer === 'object' ? answer.constructor.name : typeof answer;
    throw new CaseError(`An answer of ${named} is read as an object, and the case names none of its members.`);
}

/** Reads a member of the subject: a property, or a method called with the arguments. */
function invoke(target: object, member: string, args: readonly unknown[] | undefined): unknown {
    const found = (target as Record<string, unknown>)[member];
    if (args === undefined) {
        if (found === undefined && !(member in target)) throw new CaseError(`${member} is no member here.`);
        return found;
    }
    if (typeof found !== 'function') throw new CaseError(`${member} is no method here.`);
    return (found as Callable).apply(target, [...args]);
}

/** Holds the case's expectation of one reading: its answer (`is`), or its refusal (`throws`), then the checks on the answer. */
function hold(where: string, read: () => unknown, check: JsonObject, differences: string[], outermost = false): void {
    // The culture a case names is the thread's, which C# has and JavaScript does not.
    const known = new Set(['call', 'get', 'args', 'is', 'throws', 'then', 'name', 'note', ...(outermost ? ['culture'] : [])]);
    const stray = Object.keys(check).find(k => !known.has(k));
    if (stray !== undefined) throw new CaseError(`${where}: '${stray}' is no part of a call.`);
    let answer: unknown;
    try {
        answer = read();
    } catch (e) { // oxlint-disable-line ex-grid/no-catch -- a refusal a case expects is read, not rethrown
        if (check['throws'] === undefined) throw e;
        compare(`${where}.throws`, check['throws'], thrown(e), differences);
        return;
    }
    if (check['throws'] !== undefined) {
        differences.push(`${where}: expected a refusal, got ${JSON.stringify(project(answer, check['is']))}`);
        return;
    }
    if (check['is'] === undefined && check['then'] === undefined) throw new CaseError(`${where} checks nothing.`);
    if (check['is'] !== undefined) compare(`${where}.is`, check['is'], project(answer, check['is']), differences);
    if (check['then'] !== undefined) {
        list(check['then'], 'then').forEach((node, i) => {
            const next = object(node, 'a check');
            if (typeof answer !== 'object' || answer === null) throw new CaseError(`${where}: an answer of ${String(answer)} has no members.`);
            const subject = answer;
            const member = text(next['call'] ?? next['get'], 'the member a check reads');
            const args = next['call'] === undefined ? undefined : list(next['args'], 'the args of a call').map(argument);
            hold(`${where}.then[${String(i)}]`, () => invoke(subject, member, args), next, differences);
        });
    }
}

/** Runs the case, and answers every way the engine differs from what it expects; empty when it agrees. */
export function runSheetCall(entry: JsonObject): string[] {
    const differences: string[] = [];
    const call = text(entry['call'], 'call');
    // No args is a property read; a method, or `new`, is called with the args written, `[]` for none.
    const args = entry['args'] === undefined ? undefined : list(entry['args'], 'args').map(argument);
    const made = /^new (\w+)$/u.exec(call);
    const named = /^(\w+)\.(\w+)$/u.exec(call);
    const read = (): unknown => {
        if (made?.[1] !== undefined) {
            const type = subjects[made[1]];
            if (typeof type !== 'function') throw new CaseError(`'${made[1]}' is no class this runner makes.`);
            if (args === undefined) throw new CaseError(`${call} is called with args, [] for none.`);
            return Reflect.construct(type, args);
        }
        if (named?.[1] === undefined || named[2] === undefined) throw new CaseError(`'${call}' is no call: Subject.member, or new Subject.`);
        const subject = subjects[named[1]];
        if (subject === undefined) throw new CaseError(`'${named[1]}' is no subject this runner knows.`);
        return invoke(subject, named[2], args);
    };
    hold('call', read, entry, differences, true);
    return differences;
}
