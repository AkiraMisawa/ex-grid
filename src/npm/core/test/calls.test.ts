// Runs the shared cases of kind `calls` (tests/cases/README.md): a subject made from named arguments, and
// what its members answer. The C# runner (tests/ExGrid.Tests/Cases/CallCaseTests.cs) finds the same
// members by name.
import { describe, expect, it } from 'vitest';
import { AutoWidth } from '../src/columns/auto-width';
import { CellTextMetrics } from '../src/columns/cell-text-metrics';
import { acceptCandidate, EditorCompletion, EditorHint, replaceSpan } from '../src/cells/editor-text';
import { colourForAppearance, editorReference, referenceColour, ReferenceColouring } from '../src/cells/reference-colouring';
import { parseClipboard, parseClipboardBlock } from '../src/clipboard/clipboard-parse';
import { ColumnWidth, ColumnWidthSpec } from '../src/columns/column-width';
import { ColumnGeometry } from '../src/columns/column-geometry';
import { GlyphWidthTable } from '../src/columns/glyph-width-table';
import { HeaderGroup, HeaderGroupLayout } from '../src/columns/header-groups';
import { allowedBoundaries, reorderColumns } from '../src/columns/header-reorder';
import { firstEnabled, lastEnabled, letterOf, menuLetter, resolveColumnMenuKey, resolveMenuKey, splitAtLetter } from '../src/chrome/menu-keys';
import { editingKeysByMode } from '../src/keys/editing-keys';
import { canonicalKey, clipboardKeys, declareKeys, keysTakenFor, resolveKey, takenOnEveryGrid } from '../src/keys/grid-keys';
import { decideOverflow, hashesWhenOverflowing } from '../src/columns/overflow-rules';
import { GridMetrics } from '../src/grid-metrics';
import { findMatches } from '../src/finding/find';
import { headerAfterAnswer, headerBeforeAsking } from '../src/finding/header-find-walk';
import { refusedCells, replacedEntry } from '../src/finding/replace';
import { filterOperatorsFor } from '../src/query/filter';
import { nextSort } from '../src/query/sort';
import { cellClasses } from '../src/paint/cell-classes';
import { rowClasses } from '../src/paint/row-classes';
import { box, cellRange, copied, focusStyle, pinned, reaches, scrollable, tint, tinted, whole } from '../src/paint/selection-styles';
import { lineUpMarks, rowMarkHeaderPress, rowMarkHeaderState, RowMarkCounts } from '../src/rows/row-marks';
import { GridPresentationDefaults } from '../src/grid-presentation-defaults';
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
    // C#'s CellClasses and RowClasses are static classes: each is one function of its module here.
    CellClasses: {
        create: (_, via) => noFactory('CellClasses', via),
        statics: { for: cellClasses },
    },
    CellTextMetrics: {
        create: (args, via) => via === undefined ? new CellTextMetrics(args as never) : noFactory('CellTextMetrics', via),
        statics: CellTextMetrics,
    },
    // C#'s ClipboardParse is a static class: its members are the module's functions here.
    ClipboardParse: {
        create: (_, via) => noFactory('ClipboardParse', via),
        statics: { parse: parseClipboard, parseBlock: parseClipboardBlock },
    },
    // A candidate is a Consumer's plain object in the Script Core.
    CompletionCandidate: {
        create: (args, via) => via === undefined ? Object.freeze({ ...args }) : noFactory('CompletionCandidate', via),
        statics: {},
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
    EditorCompletion: {
        create: (args, via) => via === undefined ? new EditorCompletion(args as never) : noFactory('EditorCompletion', via),
        statics: EditorCompletion,
    },
    EditorHint: {
        create: (args, via) => via === undefined ? new EditorHint(args as never) : noFactory('EditorHint', via),
        statics: {},
    },
    EditorReference: {
        create: (args, via) => via === undefined ? editorReference(args as never) : noFactory('EditorReference', via),
        statics: {},
    },
    // C#'s EditorTextRules is a static class: its members are the module's functions here.
    EditorTextRules: {
        create: (_, via) => noFactory('EditorTextRules', via),
        statics: { replace: replaceSpan, accept: acceptCandidate },
    },
    GlyphWidthTable: {
        create: (args, via) => via === undefined
            ? new GlyphWidthTable(args['measuredAtPx'] as number, args['glyphs'] as never)
            : noFactory('GlyphWidthTable', via),
        statics: {},
    },
    // C#'s GridFind, GridReplace and HeaderFindWalk are static classes, and GridReplaceResult's factories
    // are functions: each member is the module's function here, under the C# name.
    GridFind: {
        create: (_, via) => noFactory('GridFind', via),
        statics: { matches: findMatches },
    },
    GridReplace: {
        create: (_, via) => noFactory('GridReplace', via),
        statics: { replaced: replacedEntry },
    },
    GridReplaceResult: {
        create: (_, via) => noFactory('GridReplaceResult', via),
        statics: { refused: refusedCells },
    },
    HeaderFindWalk: {
        create: (_, via) => noFactory('HeaderFindWalk', via),
        statics: { beforeAsking: headerBeforeAsking, afterAnswer: headerAfterAnswer },
    },
    GridMetrics: {
        create: (_, via) => noFactory('GridMetrics', via),
        statics: GridMetrics,
    },
    GridPresentationDefaults: {
        create: (args, via) => via === undefined ? new GridPresentationDefaults(args as never) : noFactory('GridPresentationDefaults', via),
        statics: {},
    },
    // C#'s OverflowRules is a static class: its members are functions of the module here.
    // C#'s GridKeys and GridEditingKeys are static classes: their members are functions and constants of
    // the modules here, under the C# names.
    // C#'s FilterOperators and SortCycle are static classes: each is one function of its module here.
    FilterOperators: {
        create: (_, via) => noFactory('FilterOperators', via),
        statics: { allowedFor: filterOperatorsFor },
    },
    GridEditingKeys: {
        create: (_, via) => noFactory('GridEditingKeys', via),
        statics: { byMode: editingKeysByMode },
    },
    GridKeys: {
        create: (_, via) => noFactory('GridKeys', via),
        statics: {
            resolve: resolveKey, canonical: canonicalKey, taken: takenOnEveryGrid, takenFor: keysTakenFor,
            declare: declareKeys, clipboardKeys,
        },
    },
    HeaderGroup: {
        create: (args, via) => via === undefined ? new HeaderGroup(args as never) : noFactory('HeaderGroup', via),
        statics: {},
    },
    HeaderGroupLayout: {
        create: (args, via) => {
            switch (via) {
                case 'empty': return HeaderGroupLayout.empty;
                case 'resolve': return HeaderGroupLayout.resolve(args as never);
                case undefined:
                default: return noFactory('HeaderGroupLayout', via);
            }
        },
        statics: HeaderGroupLayout,
    },
    // C#'s HeaderReorder and MenuKeys are static classes: their members are the modules' functions here, under
    // the C# names.
    HeaderReorder: {
        create: (_, via) => noFactory('HeaderReorder', via),
        statics: { allowedBoundaries, reorder: reorderColumns },
    },
    MenuKeys: {
        create: (_, via) => noFactory('MenuKeys', via),
        statics: {
            resolve: resolveMenuKey, resolveInColumnMenu: resolveColumnMenuKey, letter: menuLetter, letterOf, splitAtLetter,
            first: firstEnabled, last: lastEnabled,
        },
    },
    OverflowRules: {
        create: (_, via) => noFactory('OverflowRules', via),
        statics: { decide: decideOverflow, hashesWhenOverflowing },
    },
    // A Reference colour is its place in the palette, a number, in the Script Core.
    ReferenceColour: {
        create: (args, via) => via === undefined ? { place: referenceColour(args['place'] as number) } : noFactory('ReferenceColour', via),
        statics: { forAppearance: colourForAppearance },
    },
    ReferenceColouring: {
        create: (_, via) => noFactory('ReferenceColouring', via),
        statics: ReferenceColouring,
    },
    RowClasses: {
        create: (_, via) => noFactory('RowClasses', via),
        statics: { for: rowClasses },
    },
    // C#'s SelectionStyles is a static class: its members are the module's functions here, Focus's as
    // focusStyle.
    SelectionStyles: {
        create: (_, via) => noFactory('SelectionStyles', via),
        statics: { box, tint, tinted, reaches, whole, copied, focus: focusStyle, scrollable, pinned, cellRange },
    },
    RowMarkCounts: {
        create: (args, via) => via === undefined ? new RowMarkCounts(args as never) : noFactory('RowMarkCounts', via),
        statics: {},
    },
    // C#'s RowMarkRules is a static class: its members are the module's functions here, under the C# names.
    RowMarkRules: {
        create: (_, via) => noFactory('RowMarkRules', via),
        statics: { lineUp: lineUpMarks, headerState: rowMarkHeaderState, headerPress: rowMarkHeaderPress },
    },
    RowRange: {
        create: (args, via) => via === undefined ? rowRange(args['start'] as number, args['count'] as number) : noFactory('RowRange', via),
        statics: {},
    },
    SortCycle: {
        create: (_, via) => noFactory('SortCycle', via),
        statics: { next: nextSort },
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
            const made = value as { readonly new: string; readonly with?: Arguments; readonly via?: string; readonly after?: readonly Check[] };
            const subject = subjects[made.new];
            if (subject === undefined) throw new Error(`"${made.new}" is not a subject this runner knows.`);
            // oxlint-disable-next-line ex-grid/no-stand-in-default -- tests/cases/README.md: a subject made with no "with" takes no arguments
            let result: unknown = subject.create(argument(made.with ?? {}) as Arguments, made.via);
            // oxlint-disable-next-line ex-grid/no-stand-in-default -- tests/cases/README.md: "after" is optional, and no calls leave the subject as made
            for (const step of made.after ?? []) {
                if (step.call === undefined || !Array.isArray(step.args)) throw new Error(`An "after" step ${JSON.stringify(step)} is a call with positional args.`);
                if (typeof result !== 'object' || result === null) throw new Error(`An "after" step ${step.call} has no object to call.`);
                result = read(result, step.call, argument(step.args) as unknown[]);
            }
            return result;
        }
        return Object.fromEntries(Object.entries(value).map(([key, item]) => [key, argument(item)]));
    }
    return revive(value);
}

/** Whether the answer is the value the case expects: numbers by value, `undefined` as `null`, objects
 * by every key, arrays in order. */
function same(wanted: unknown, actual: unknown): boolean {
    // A key the case leaves out of an object is null, as one the answer leaves out is.
    const expected = wanted === undefined ? null : wanted;
    // A set is compared as the list of its members in order, as C# serialises one.
    const answer = actual === undefined ? null : actual instanceof Set ? [...(actual as ReadonlySet<unknown>)] : actual;
    if (typeof expected === 'number' && typeof answer === 'number') {
        return expected === answer || (Number.isNaN(expected) && Number.isNaN(answer));
    }
    if (Array.isArray(expected)) {
        return Array.isArray(answer) && answer.length === expected.length && expected.every((item, i) => same(item, answer[i]));
    }
    if (typeof expected === 'object' && expected !== null) {
        if (typeof answer !== 'object' || answer === null || Array.isArray(answer)) return false;
        // A key one side lacks is null there: "none" is null in a case, and a language may leave it out.
        const keys = new Set([...Object.keys(expected), ...Object.keys(answer)]);
        return [...keys].every(key => same(Reflect.get(expected, key), Reflect.get(answer, key)));
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
    readonly atLeast?: number | string;
    readonly atMost?: number | string;
    readonly throws?: Refusal;
    readonly saying?: string;
    readonly then?: readonly Check[];
    readonly has?: readonly unknown[];
    readonly lacks?: readonly unknown[];
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
    if (check.has !== undefined || check.lacks !== undefined) {
        if (typeof result !== 'object' || result === null || !(Symbol.iterator in result)) throw new Error(`${label} answered no list for "has" or "lacks" to search.`);
        const members = [...(result as Iterable<unknown>)];
        const isMember = (member: unknown) => members.some(each => same(revive(member), each));
        if (check.has !== undefined) {
            for (const wanted of check.has) expect(isMember(wanted), `${label}: expected it to hold ${JSON.stringify(wanted)}`).toBe(true);
        }
        if (check.lacks !== undefined) {
            for (const unwanted of check.lacks) expect(isMember(unwanted), `${label}: expected it not to hold ${JSON.stringify(unwanted)}`).toBe(false);
        }
        return;
    }
    if (check.atLeast !== undefined || check.atMost !== undefined) {
        if (typeof result !== 'number') throw new Error(`${label} answered no number to bound.`);
        if (check.atLeast !== undefined) expect(result, `${label}: expected at least ${String(check.atLeast)}`).toBeGreaterThanOrEqual(revive(check.atLeast) as number);
        if (check.atMost !== undefined) expect(result, `${label}: expected at most ${String(check.atMost)}`).toBeLessThanOrEqual(revive(check.atMost) as number);
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
