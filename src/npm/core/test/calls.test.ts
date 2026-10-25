// Runs the shared cases of kind `calls` (tests/cases/README.md): a subject made from named arguments, and
// what its members answer. The C# runner (tests/ExGrid.Tests/Cases/CallCaseTests.cs) finds the same
// members by name.
import { describe, expect, it } from 'vitest';
import { AutoWidth } from '../src/columns/auto-width';
import { CellTextMetrics } from '../src/columns/cell-text-metrics';
import { ColumnWidth, ColumnWidthSpec } from '../src/columns/column-width';
import { ColumnGeometry } from '../src/columns/column-geometry';
import { GlyphWidthTable } from '../src/columns/glyph-width-table';
import { decideOverflow, hashesWhenOverflowing } from '../src/columns/overflow-rules';
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
    AutoWidth: {
        create: (args, via) => via === undefined ? AutoWidth.of(args['spec'] as ColumnWidthSpec) : noFactory('AutoWidth', via),
        statics: {},
    },
    CellTextMetrics: {
        create: (args, via) => via === undefined ? new CellTextMetrics(args as never) : noFactory('CellTextMetrics', via),
        statics: CellTextMetrics,
    },
    ColumnGeometry: {
        create: (args, via) => via === undefined ? new ColumnGeometry(args as never) : noFactory('ColumnGeometry', via),
        statics: ColumnGeometry,
    },
    ColumnWidth: {
        create: (args, via) => {
            switch (via) {
                case 'auto': return ColumnWidth.auto;
                case 'fixed': return ColumnWidth.fixed(args['px'] as number);
                case undefined:
                default: return noFactory('ColumnWidth', via);
            }
        },
        statics: ColumnWidth,
    },
    ColumnWidthSpec: {
        create: (args, via) => via === undefined ? new ColumnWidthSpec(args as never) : noFactory('ColumnWidthSpec', via),
        statics: ColumnWidthSpec,
    },
    GlyphWidthTable: {
        create: (args, via) => via === undefined
            ? new GlyphWidthTable(args['measuredAtPx'] as number, args['glyphs'] as never)
            : noFactory('GlyphWidthTable', via),
        statics: {},
    },
    // C#'s OverflowRules is a static class: its members are functions of the module here.
    OverflowRules: {
        create: (_, via) => noFactory('OverflowRules', via),
        statics: { decide: decideOverflow, hashesWhenOverflowing },
    },
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

/** An argument as the Script Core takes it: revived, "no value" (`null` in the cases) `undefined`, and
 * `{"new": subject, "with": {…}}` that subject, made as a case makes one. */
function argument(value: unknown): unknown {
    if (value === null) return undefined;
    if (Array.isArray(value)) return value.map(argument);
    if (typeof value === 'object') {
        if ('new' in value) {
            const made = value as { readonly new: string; readonly with?: Arguments; readonly via?: string };
            const subject = subjects[made.new];
            if (subject === undefined) throw new Error(`"${made.new}" is not a subject this runner knows.`);
            // oxlint-disable-next-line ex-grid/no-stand-in-default -- tests/cases/README.md: a subject made with no "with" takes no arguments
            return subject.create(argument(made.with ?? {}) as Arguments, made.via);
        }
        return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, argument(item)]));
    }
    return revive(value);
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
    readonly args?: readonly unknown[] | Arguments;
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
    // Named arguments are the Script Core's options object; positional ones are its parameters.
    const args = check.args === undefined ? undefined
        : Array.isArray(check.args) ? (argument(check.args) as unknown[]) : [argument(check.args)];
    const label = check.args === undefined ? name : `${name}(${JSON.stringify(check.args)})`;
    const answer = () => read(target, name, args);
    if (check.throws !== undefined) {
        expectRefusal(answer, check.throws, check.saying);
        return;
    }
    const result = answer();
    if (check.then !== undefined) {
        if (typeof result !== 'object' || result === null) throw new Error(`${label} answered no object for "then" to read.`);
        // The answer is the subject of each check, its own statics included, as C# reads them off its type.
        const answered: Subject = { create: (_, via) => noFactory(result.constructor.name, via), statics: result.constructor };
        for (const next of check.then) holds(answered, result, next);
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
                        const args = argument({ ...defaults, ...c.create }) as Arguments;
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
