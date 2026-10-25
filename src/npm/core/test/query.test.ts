// Layer 1: the reference implementation's Query semantics (tests/cases/query, kind "query"), run against the
// Script Core. A case's rows are filtered and sorted, and it says which rows stay, in which order, or how
// the Query is refused.
import { describe, expect, it } from 'vitest';
import type { QueryColumn } from '../src/query/columns';
import type { GridFilter } from '../src/query/filter';
import { queryPositions } from '../src/query/query-engine';
import type { SortSpec } from '../src/query/sort';
import { caseFiles, expectRefusal, type Refusal } from './cases';

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
    readonly columns: readonly { readonly name: string; readonly type: QueryColumn<Row>['type'] }[];
}

/** A value as a case writes it: JSON's own, `{"bigint": text}` a bigint, `{"double": text}` a number JSON
 * cannot hold. Anything else is a defect in the case. */
function value(written: unknown): unknown {
    if (written === null || typeof written !== 'object') return written;
    if (Array.isArray(written)) return written.map(value);
    const entries: [string, unknown][] = Object.entries(written);
    const [only] = entries;
    if (entries.length === 1 && only !== undefined) {
        const [kind, text] = only;
        if (kind === 'bigint' && typeof text === 'string') return BigInt(text);
        if (kind === 'double' && typeof text === 'string') return Number(text);
    }
    throw new Error(`${JSON.stringify(written)} is not a value a query case writes.`);
}

/** The Filter as the Script Core takes it, its operands read as values. */
function filterOf(filter: GridFilter): GridFilter {
    const columns = Object.fromEntries(Object.entries(filter.columns).map(([name, spec]) => [name, {
        ...spec,
        clauses: spec.clauses.map(clause => ({
            ...clause,
            ...('value' in clause ? { value: value(clause.value) } : {}),
            ...(clause.values === undefined ? {} : { values: clause.values.map(value) }),
        })),
    }]));
    return { ...filter, columns };
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
