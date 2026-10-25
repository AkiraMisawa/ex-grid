// Layer 1: the clipboard rules' shared cases (tests/cases/clipboard, kind "clipboard"), run against the
// Script Core. A case's steps make a selection with the gestures of the selection cases, then plan a copy,
// a paste or a fill on it and say what the plan is or why it is refused.
import { describe, expect, it } from 'vitest';
import { assembleCopy } from '../src/clipboard/clipboard-data';
import {
    planCopy, planFill, planPaste, pasteShape, type CopyDecision, type CopyPlan, type FillDecision, type PasteDecision, type PastePlan,
} from '../src/clipboard/clipboard-rules';
import type { GridDirection, GridExtent } from '../src/selection/cell';
import { Selection } from '../src/selection/selection';
import { caseFiles, expectRefusal, readCell, readRange, readRanges, type CellText, type RangeText, type Refusal } from './cases';
import { apply, type Step } from './gestures';

interface ClipboardCase {
    readonly extent?: readonly [number, number];
    readonly steps: readonly Step[];
}

type Options = Readonly<Record<string, unknown>>;
type Pair = readonly [number, number];

const extentOf = (pair: Pair): GridExtent => ({ rowCount: pair[0], columnCount: pair[1] });

/** The Editable declaration a plan step names: every column, every column but those listed, or none. */
function editableFrom(options: Options): (column: number) => boolean {
    const locked = options['notEditable'];
    if (locked === undefined) return () => true;
    if (locked === 'every') return function noColumnIsEditable() { return false; };
    if (!Array.isArray(locked)) throw new Error(`"notEditable" is a list of columns or "every", not ${JSON.stringify(locked)}.`);
    return column => !locked.includes(column);
}

/** Reads every key of `expected` off the plan; a key this runner does not know is a defect in the case. */
function holdsPaste(plan: PastePlan, expected: Options, label: string): void {
    for (const [key, value] of Object.entries(expected)) {
        switch (key) {
            case 'targets': expect(plan.targets, `${label}: targets`).toEqual(readRanges(value as RangeText[] | string)); break;
            case 'source': expect([plan.source.rows, plan.source.columns], `${label}: source`).toEqual(value); break;
            case 'collapses': expect(plan.collapsesSelection, `${label}: collapses`).toBe(value); break;
            case 'sourceCells':
                for (const [at, block] of value as [CellText, Pair][]) {
                    const answered = plan.sourceCellFor(readCell(at));
                    expect([answered.row, answered.column], `${label}: the block cell on ${JSON.stringify(at)}`).toEqual(block);
                }
                break;
            case 'outside':
                for (const at of value as CellText[]) expectRefusal(() => plan.sourceCellFor(readCell(at)), 'outOfRange');
                break;
            default: throw new Error(`${label}: "${key}" is not a key of a paste plan this runner knows.`);
        }
    }
}

// What a cell shows and holds unless a step names other text (tests/cases/README.md, `clipboard`).
const shownAt = (row: number, column: number) => `d${row}.${column}`;
const rawFormAt = (row: number, column: number) => `r${row}.${column}`;

/** Both flavours of the copy (ADR-0005), from the providers the step names, held to its `text` and `html`. */
function assembles(plan: CopyPlan, options: Options, step: Step, label: string): void {
    let displayAt = shownAt;
    let rawAt = rawFormAt;
    let headerAt: ((column: number) => string) | undefined;
    let headerRaws: Readonly<Record<string, string>> | undefined;
    for (const [key, value] of Object.entries(options)) {
        switch (key) {
            case 'display': displayAt = () => value as string; break;
            case 'raw': rawAt = () => value as string; break;
            case 'headers':
                if (value === true) headerAt = column => `H${column}`;
                else if (typeof value === 'string') headerAt = () => value;
                else throw new Error(`${label}: "headers" is true or a text, not ${JSON.stringify(value)}.`);
                break;
            case 'headerRaws': headerRaws = value as Record<string, string>; break;
            default: throw new Error(`${label}: "${key}" is not an assembly option this runner knows.`);
        }
    }
    const raws = headerRaws;
    // Without a header row a raw form is never read, so one asked for then fails the case.
    const headerRawAt = raws === undefined ? undefined
        : headerAt === undefined ? (): never => { throw new Error(`${label}: a header's raw form was read without a header row.`); }
        : (column: number) => (Object.hasOwn(raws, String(column)) ? raws[String(column)] : undefined);
    const flavours = assembleCopy(plan, {
        displayAt, rawAt, ...(headerAt === undefined ? {} : { headerAt }), ...(headerRawAt === undefined ? {} : { headerRawAt }),
    });
    expect(flavours.text, `${label}: text`).toBe(step['text']);
    expect(flavours.html, `${label}: html`).toBe(step['html']);
}

function holdsDecision(decision: CopyDecision | PasteDecision | FillDecision, step: Step, label: string): void {
    if ('refused' in step) {
        expect(decision.refused ? decision.reason : 'approved', label).toBe(step['refused']);
        if ('column' in step) {
            expect(decision.refused && decision.reason === 'targetNotEditable' ? decision.column : undefined, `${label}: column`).toBe(step['column']);
        }
        return;
    }
    if (!('plan' in step || 'assemble' in step)) throw new Error(`${label} says neither "refused", "plan", "assemble" nor "throws".`);
    expect(decision.refused ? decision.reason : 'approved', `${label}: expected a plan`).toBe('approved');
    if (decision.refused) return;
    const { plan } = decision;
    if ('assemble' in step) {
        if (!('orientation' in plan)) throw new Error(`${label}: only a copy assembles.`);
        assembles(plan, step['assemble'] as Options, step, label);
    }
    if (!('plan' in step)) return;
    const expected = step['plan'] as Options;
    if ('orientation' in plan) {
        for (const [key, value] of Object.entries(expected)) {
            switch (key) {
                case 'segments': expect(plan.segments, `${label}: segments`).toEqual(readRanges(value as RangeText[] | string)); break;
                case 'orientation': expect(plan.orientation, `${label}: orientation`).toBe(value); break;
                case 'totalRows': expect(plan.totalRows, `${label}: totalRows`).toBe(value); break;
                case 'totalColumns': expect(plan.totalColumns, `${label}: totalColumns`).toBe(value); break;
                default: throw new Error(`${label}: "${key}" is not a key of a copy plan this runner knows.`);
            }
        }
    } else if ('paste' in plan) {
        const { source, ...paste } = expected;
        if (source !== undefined) expect(plan.source, `${label}: the fill's source`).toEqual(readRange(source as RangeText));
        const { shape, ...rest } = paste;
        if (shape !== undefined) expect([plan.paste.source.rows, plan.paste.source.columns], `${label}: shape`).toEqual(shape);
        holdsPaste(plan.paste, rest, label);
    } else {
        holdsPaste(plan, expected, label);
    }
}

function plans(selection: Selection, step: Step, extent: GridExtent, label: string): boolean {
    const plan = (): CopyDecision | PasteDecision | FillDecision => {
        if ('planCopy' in step) {
            return planCopy(selection, step['planCopy'] as never);
        }
        if ('planPaste' in step) {
            const options = step['planPaste'] as Options;
            const [rows, columns] = options['source'] as Pair;
            return planPaste(selection, pasteShape(rows, columns), editableFrom(options), {
                ...(options['spill'] === true ? { spillWithin: extent } : {}),
                ...(options['plainTextOnly'] === true ? { plainTextOnly: true } : {}),
            });
        }
        const options = step['planFill'] as Options;
        return planFill(selection, options['direction'] as GridDirection, editableFrom(options),
            options['cellCap'] === undefined ? {} : { cellCap: options['cellCap'] as number });
    };
    if (!('planCopy' in step || 'planPaste' in step || 'planFill' in step)) return false;
    if ('throws' in step) {
        expectRefusal(plan, step['throws'] as Refusal);
        return true;
    }
    holdsDecision(plan(), step, label);
    return true;
}

for (const file of caseFiles<ClipboardCase>('clipboard', 'clipboard')) {
    describe(`clipboard/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                const pair = c.extent ?? file.extent;
                if (pair === undefined) throw new Error(`The case "${c.name}" names no extent, and neither does its file.`);
                const extent = extentOf(pair);
                let selection: Selection = Selection.empty;
                c.steps.forEach((step, i) => {
                    if (!plans(selection, step, extent, `step ${i + 1}`)) selection = apply(selection, step, extent);
                });
            });
        }
    });
}

describe('a whole number where C# takes an int or a long', () => {
    const one = Selection.empty.click({ row: 0, column: 0 }, { rowCount: 10, columnCount: 10 });

    it('ADR-0014: a paste shape is whole numbers of rows and columns', () => {
        expect(() => pasteShape(1.5, 1)).toThrow(RangeError);
        expect(() => pasteShape(1, Number.NaN)).toThrow(RangeError);
        expect(() => planPaste(one, { rows: 0, columns: 1 }, () => true)).toThrow(RangeError);
    });

    it('ADR-0005: a cell cap is a whole number of cells', () => {
        expect(() => planCopy(one, { cellCap: 1.5 })).toThrow(RangeError);
        expect(() => planFill(one, 'down', () => true, { cellCap: Number.POSITIVE_INFINITY })).toThrow(RangeError);
    });
});
