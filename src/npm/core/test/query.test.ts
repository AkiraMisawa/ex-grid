// Layer 1: the reference implementation's Query semantics (tests/cases/query, kind "query"), run against the
// Script Core. A case's rows are filtered and sorted, and it says which rows stay, in which order, or how
// the Query is refused.
import { describe, expect, it } from 'vitest';
import type { QueryColumn } from '../src/query/columns';
import { filterOperatorsFor, type GridFilter } from '../src/query/filter';
import { queryPositions } from '../src/query/query-engine';
import type { SortSpec } from '../src/query/sort';
import { defined, elementAt } from '../src/must';
import { caseFiles, expectRefusal, type Refusal } from './cases';
import { filterOf, value } from './query-values';

type Row = readonly unknown[];

interface QueryCase {
    readonly rows: readonly Row[];
    readonly filter?: GridFilter;
    readonly sorts?: readonly SortSpec[];
    readonly keeps?: readonly number[];
    readonly throws?: Refusal;
    readonly saying?: string;
}

interface QueryFile {
    readonly columns: readonly Pick<QueryColumn<Row>, 'name' | 'type' | 'dateType'>[];
}

for (const file of caseFiles<QueryCase>('query', 'query')) {
    const { columns: declared } = file as unknown as QueryFile;
    const columns: QueryColumn<Row>[] = declared.map((column, index) => ({ ...column, value: row => value(row[index]) }));
    describe(`query/${file.file}`, () => {
        for (const c of file.cases) {
            it(c.name, () => {
                expect.hasAssertions();
                const run = () => queryPositions(c.rows, columns, c.filter === undefined ? undefined : filterOf(c.filter), c.sorts);
                if (c.throws !== undefined) {
                    expectRefusal(run, c.throws, c.saying);
                    return;
                }
                if (c.keeps === undefined) throw new Error(`The case "${c.name}" says neither "keeps" nor "throws".`);
                expect(run()).toEqual(c.keeps);
            });
        }
    });
}

describe('the operator matrix (FL-6)', () => {
    it('FL-6, ADR-0023/0009: every operator on every column type is either offered, with its cases, or refused naming the column', () => {
        const matrix = defined(caseFiles<QueryCase>('query', 'query').find(file => file.file === 'operators.json'), 'operators.json');
        const { columns: declared } = matrix as unknown as QueryFile;
        const cases = matrix.cases.map(c => {
            const [entry] = Object.entries(defined(c.filter, `the filter of "${c.name}"`).columns);
            const [name, spec] = defined(entry, `the filtered column of "${c.name}"`);
            const index = declared.findIndex(column => column.name === name);
            return {
                type: elementAt(declared, index).type,
                operator: elementAt(spec.clauses, 0).operator,
                column: name,
                blank: c.rows.some(row => row[index] === null),
                kept: c.keeps === undefined ? undefined : c.keeps.length > 0,
                refusal: c.throws,
                saying: c.saying,
            };
        });
        const types = ['text', 'number', 'date', 'boolean'] as const;
        const operators = new Set(types.flatMap(type => filterOperatorsFor(type)));
        for (const type of types) {
            expect(declared.some(column => column.type === type), `a ${type} column`).toBe(true);
            for (const operator of operators) {
                const pair = cases.filter(c => c.type === type && c.operator === operator);
                if (filterOperatorsFor(type).includes(operator)) {
                    expect(pair.some(c => c.kept === true) && pair.some(c => c.kept === false), `${operator} on ${type} keeps and drops`).toBe(true);
                    expect(pair.some(c => c.blank), `${operator} on ${type} meets a Blank`).toBe(true);
                } else {
                    expect(pair.length > 0 && pair.every(c => c.refusal === 'invalidOperation' && c.saying === c.column),
                        `${operator} on ${type} is refused naming the column`).toBe(true);
                }
            }
        }
    });
});
