// Runs the shared cases of kind `calls` (tests/cases/README.md): a subject made from named arguments, and
// what its members answer. The C# runner (tests/ExGrid.Tests/Cases/CallCaseTests.cs) finds the same
// members by name.
import { describe, expect, it } from 'vitest';
import { rowRange } from '../src/viewport/row-range';
import { ViewportBox } from '../src/viewport/viewport-box';
import { ViewportGeometry } from '../src/viewport/viewport-geometry';
import { areas, caseFiles, expectRefusal, type Refusal } from './cases';

type Arguments = Readonly<Record<string, unknown>>;

/** How a case makes a subject, by the name it uses: with named arguments, or through a named factory. */
interface Subject {
    readonly create: (args: Arguments, via: string | undefined) => object;
    readonly statics: object;
}

function noFactory(subject: string, via: string | undefined): never {
    throw new Error(`${subject} is made by no factory "${String(via)}" in this runner.`);
}

const subjects: Readonly<Record<string, Subject>> = {
    RowRange: {
        create: (args, via) => via === undefined ? rowRange(args['start'] as number, args['count'] as number) : noFactory('RowRange', via),
        statics: {},
    },
    ViewportBox: {
        create: (args, via) => {
            switch (via) {
                case undefined: return new ViewportBox(args as never);
                case 'declared': return ViewportBox.declared(args['outerWidthPx'] as number, args['outerHeightPx'] as number);
                default: return noFactory('ViewportBox', via);
            }
        },
        statics: ViewportBox,
    },
    ViewportGeometry: {
        create: (args, via) => via === undefined ? new ViewportGeometry(args as never) : noFactory('ViewportGeometry', via),
        statics: ViewportGeometry,
    },
};

/** A JSON value with its non-finite numbers, written "Infinity", "-Infinity" and "NaN", made numbers. */
function revive(value: unknown): unknown {
    if (value === 'Infinity') return Number.POSITIVE_INFINITY;
    if (value === '-Infinity') return Number.NEGATIVE_INFINITY;
    if (value === 'NaN') return Number.NaN;
    if (Array.isArray(value)) return value.map(revive);
    if (typeof value === 'object' && value !== null) {
        return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, revive(item)]));
    }
    return value;
}

/** Whether the answer is the value the case expects: numbers by value, `undefined` as `null`, objects
 * by every key, arrays in order. */
function same(expected: unknown, actual: unknown): boolean {
    const answer = actual === undefined ? null : actual;
    if (typeof expected === 'number' && typeof answer === 'number') {
        return expected === answer || (Number.isNaN(expected) && Number.isNaN(answer));
    }
    if (Array.isArray(expected)) {
        return Array.isArray(answer) && answer.length === expected.length && expected.every((item, i) => same(item, answer[i]));
    }
    if (typeof expected === 'object' && expected !== null) {
        if (typeof answer !== 'object' || answer === null || Array.isArray(answer)) return false;
        const keys = Object.keys(expected);
        return keys.length === Object.keys(answer).length
            && keys.every(key => key in answer && same(Reflect.get(expected, key), Reflect.get(answer, key)));
    }
    return expected === answer;
}

/** A member's answer: a property read, or a method called with the arguments. */
function read(target: object, name: string, args: readonly unknown[] | undefined): unknown {
    if (!(name in target)) {
        throw new Error(`${target.constructor.name} has no member ${name}.`);
    }
    const value: unknown = Reflect.get(target, name);
    if (args === undefined) {
        if (typeof value === 'function') throw new Error(`${name} is a method; the check names no args.`);
        return value;
    }
    if (typeof value !== 'function') throw new Error(`${name} is not a method; the check names args.`);
    return Reflect.apply(value, target, args) as unknown;
}

interface Check {
    readonly get?: string;
    readonly call?: string;
    readonly static?: string;
    readonly args?: readonly unknown[];
    readonly is?: unknown;
    readonly near?: number;
    readonly throws?: Refusal;
    readonly saying?: string;
    readonly then?: readonly Check[];
}

function holds(subject: Subject, instance: object | undefined, check: Check): void {
    const isStatic = check.static !== undefined;
    const name = check.static ?? check.get ?? check.call;
    if (name === undefined) throw new Error(`The check ${JSON.stringify(check)} names no member: get, call or static.`);
    const target = isStatic ? subject.statics : instance;
    if (target === undefined) throw new Error(`The check ${JSON.stringify(check)} reads an instance, and the case creates none.`);
    const args = check.args === undefined ? undefined : (revive(check.args) as unknown[]);
    const label = check.args === undefined ? name : `${name}(${check.args.map(arg => JSON.stringify(arg)).join(', ')})`;
    const answer = () => read(target, name, args);
    if (check.throws !== undefined) {
        expectRefusal(answer, check.throws, check.saying);
        return;
    }
    const result = answer();
    if (check.then !== undefined) {
        if (typeof result !== 'object' || result === null) throw new Error(`${label} answered no object for "then" to read.`);
        for (const next of check.then) holds(subject, result, next);
        return;
    }
    if (!('is' in check)) throw new Error(`The check ${JSON.stringify(check)} says neither what it is nor how it is refused.`);
    const expected = revive(check.is);
    if (check.near !== undefined) {
        expect(typeof result === 'number' && typeof expected === 'number' && Math.abs(result - expected) <= check.near,
            `${label}: expected ${String(expected)} within ${check.near}, got ${String(result)}`).toBe(true);
        return;
    }
    expect(same(expected, result), `${label}: expected ${JSON.stringify(check.is)}, got ${JSON.stringify(result)}`).toBe(true);
}

interface CallCase {
    readonly via?: string;
    readonly create?: Arguments;
    readonly throws?: Refusal;
    readonly saying?: string;
    readonly expect?: readonly Check[];
}

interface CallFile {
    readonly subject: string;
    readonly defaults?: Arguments;
}

for (const area of areas()) {
    for (const file of caseFiles<CallCase>(area, 'calls')) {
        const { subject: subjectName, defaults } = file as unknown as CallFile;
        const subject = subjects[subjectName];
        if (subject === undefined) throw new Error(`"${subjectName}" is not a subject this runner knows.`);
        describe(`${area}/${file.file}`, () => {
            for (const c of file.cases) {
                it(c.name, () => {
                    expect.hasAssertions();
                    let instance: object | undefined;
                    if (c.create !== undefined) {
                        const args = revive({ ...defaults, ...c.create }) as Arguments;
                        if (c.throws !== undefined) {
                            expectRefusal(() => subject.create(args, c.via), c.throws, c.saying);
                            expect(c.expect, 'a case refused at its creation expects nothing after it').toBeUndefined();
                            return;
                        }
                        instance = subject.create(args, c.via);
                    }
                    if (c.expect === undefined || c.expect.length === 0) throw new Error(`The case "${c.name}" checks nothing.`);
                    for (const check of c.expect) holds(subject, instance, check);
                });
            }
        });
    }
}
