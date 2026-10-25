// Layer 1: the reference Find step and Replace All (tests/cases/finding, kinds "find" and "replace"), run
// against the Script Core. A row is the texts of its cells, in the file's columns: what a Find matches, the
// displayed text, or what a Replace matches, the entry. The C# runner is
// tests/ExGrid.Tests/Cases/FindCaseTests.cs.
import { describe, expect, it } from 'vitest';
import { findMatches, findStep } from '../src/finding/find';
import { planReplace, type GridReplaceWrite } from '../src/finding/replace';
import { elementAt } from '../src/must';
import { caseFiles, expectRefusal, type CellText, type Refusal } from './cases';
import { findRequestOf, numberText, replaceRequestOf, type WrittenFind, type WrittenReplace } from './find-requests';

type Row = readonly string[];

interface FindFile {
    readonly columns: readonly string[];
    readonly rows: readonly Row[];
}

interface FindCase {
    readonly steps: readonly { readonly find: WrittenFind; readonly is: readonly [number, string] | null }[];
}

interface ReplaceCase {
    readonly rows?: readonly Row[];
    readonly repeat?: number;
    readonly replaceAll: WrittenReplace;
    readonly readIn?: number;
    readonly writeInPlace?: true;
    readonly is?: {
        readonly count?: number;
        readonly version?: number;
        readonly changes?: readonly (readonly [number, Row])[];
        readonly changeCount?: number;
        readonly refused?: readonly (readonly [number, string, string | null])[];
    };
    readonly throws?: Refusal;
    readonly saying?: string;
}

function textsOf(columns: readonly string[]) {
    return (name: string): ((row: Row) => string) | undefined => {
        const at = columns.indexOf(name);
        return at < 0 ? undefined : (row: Row) => elementAt(row, at);
    };
}

for (const file of caseFiles<FindCase>('finding', 'find')) {
    const { columns, rows } = file as unknown as FindFile;
    describe(`finding/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                for (const step of c.steps) {
                    const found = findStep(rows, findRequestOf(step.find, columns), textsOf(columns));
                    expect(found.found ? [found.row, found.column] : null, JSON.stringify(step.find)).toEqual(step.is);
                }
            });
        }
    });
}

for (const file of caseFiles<ReplaceCase>('finding', 'replace')) {
    const { columns, rows: fileRows, numberColumns } = file as unknown as FindFile & { readonly numberColumns: readonly string[] };
    describe(`finding/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                const written = c.rows === undefined ? fileRows : c.rows;
                const rows = c.repeat === undefined ? written : Array.from({ length: c.repeat }, () => written).flat().map(row => [...row]);
                const readIn = c.readIn === undefined ? 0 : c.readIn;
                const request = replaceRequestOf(c.replaceAll, columns, 0);
                const write = (row: Row, column: string, text: string): GridReplaceWrite<Row> => {
                    if (c.writeInPlace === true) return { row };
                    if (numberColumns.includes(column) && !numberText.test(text)) return { refusal: 'not a number' };
                    const at = columns.indexOf(column);
                    if (at < 0) return { refusal: 'no such column' };
                    return { row: row.with(at, text) };
                };
                const plan = () => planReplace(rows, readIn, request, textsOf(columns), write);
                if (c.throws !== undefined) {
                    expectRefusal(plan, c.throws, c.saying);
                    return;
                }
                const expected = c.is;
                if (expected === undefined) throw new Error(`"${c.name}" says neither what it is nor what it throws.`);
                expect.hasAssertions();
                const { result, changes } = plan();
                if (expected.version !== undefined) expect(result.rowSequenceVersion, 'version').toBe(expected.version);
                if (expected.refused !== undefined) {
                    expect(result.kind === 'refused' ? result.cells.map(cell => [cell.row, cell.column, cell.message === undefined ? null : cell.message]) : result.kind, 'refused')
                        .toEqual(expected.refused);
                    expect(changes, 'nothing rewritten when refused').toEqual([]);
                }
                if (expected.count !== undefined) expect(result.kind === 'replaced' ? result.count : result.kind, 'count').toBe(expected.count);
                if (expected.changeCount !== undefined) expect(changes.length, 'changeCount').toBe(expected.changeCount);
                if (expected.changes !== undefined) {
                    expect(changes.map(change => [rows.indexOf(change.before), change.after]), 'changes').toEqual(expected.changes);
                }
            });
        }
    });
}

// ADR-0332 §4: the step and the mark judge by one rule, so the cells a step can land on are exactly the
// ones the rule marks. A property over the find file's rows, as C#'s GridFindTests holds it.
describe('finding: the step lands only on cells the rule marks', () => {
    const [file] = caseFiles<FindCase>('finding', 'find');
    if (file === undefined) throw new Error('No find file to read rows from.');
    const { columns, rows } = file as unknown as FindFile;
    it('ADR-0332 §4: for every text and option, the cells reached step by step are the cells the rule marks', () => {
        for (const text of ['alpha', 'ALPHA', 'nova', 'Novak', 'a', '1,000.00', 'et', 'beta', '0']) {
            for (const matchCase of [false, true]) {
                for (const wholeCell of [false, true]) {
                    const pattern = { text, matchCase, wholeCell };
                    const marked = new Set<string>();
                    rows.forEach((row, at) => columns.forEach((column, c) => {
                        if (findMatches(elementAt(row, c), pattern)) marked.add(`${at}:${column}`);
                    }));
                    const reached = new Set<string>();
                    let from: CellText | undefined;
                    for (let i = 0; i < rows.length * columns.length; i++) {
                        const found = findStep(rows, findRequestOf({ text, matchCase, wholeCell, ...(from === undefined ? {} : { from }) }, columns), textsOf(columns));
                        if (!found.found) break;
                        reached.add(`${found.row}:${found.column}`);
                        from = [found.row, columns.indexOf(found.column)];
                    }
                    expect([...reached].toSorted(), JSON.stringify(pattern)).toEqual([...marked].toSorted());
                }
            }
        }
    });
});
