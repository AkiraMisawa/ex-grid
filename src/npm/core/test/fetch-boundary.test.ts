// Layer 1: what GridSource.fetch does beyond the shared cases (tests/cases/source, kind `fetch`): what the
// Consumer's server answers is checked where it arrives, what the Consumer hands the source cannot move it,
// and the questions beside the rows (Find, a Summary) reach the server under the Query in force.
import { describe, expect, it } from 'vitest';
import { elementAt } from '../src/must';
import type { FilterSpec, GridFilter } from '../src/query/filter';
import { notFound, type GridFindRequest } from '../src/finding/find';
import { declinedSummary, type GridSummaryRequest } from '../src/summary/grid-summary';
import { GridSource } from '../src/source/grid-source';
import type { GridPage, GridQuery, QueryInForce } from '../src/source/fetching-source';

interface Call {
    readonly query: GridQuery;
    readonly resolve: (page: GridPage<string>) => void;
}

function server() {
    const calls: Call[] = [];
    const failures: unknown[] = [];
    const source = GridSource.fetch<string>(
        query => new Promise(resolve => { calls.push({ query, resolve }); }),
        { readAheadRows: 0, onFetchFailed: ({ error }) => { failures.push(error); } });
    source.setColumns([{ name: 'Book', type: 'text', value: row => row }]);
    return { source, calls, failures };
}

/** A server that never answers. */
function unanswered(): Promise<GridPage<string>> {
    return new Promise(() => { /* never answered */ });
}

async function settle(): Promise<void> {
    await new Promise(resolve => { setTimeout(resolve, 0); });
}

const findRequest: GridFindRequest = { text: 'Rates', matchCase: false, wholeCell: false, backward: false, columns: ['Book'], rowSequenceVersion: 0 };
const summaryRequest: GridSummaryRequest = { ranges: [], columns: ['Book'], rowSequenceVersion: 0, figures: ['sum'] };

describe('GridSource.fetch', () => {
    it('ADR-0001: a page whose rows run past its own total is refused as a failed fetch, and nothing of it is shown', async () => {
        const { source, calls, failures } = server();
        elementAt(calls, 0).resolve({ rows: ['Row 9', 'Row 10'], start: 9, totalCount: 10 });
        await settle();
        expect(failures).toHaveLength(1);
        const [failure] = failures;
        expect(failure).toBeInstanceOf(RangeError);
        expect(source.snapshot().window).toEqual([]);
        expect(source.snapshot().lastError).toBe(failure);
    });

    it('ADR-0001: a page that is not a page is refused by name', async () => {
        const { calls, failures } = server();
        // A server written in JavaScript can answer anything.
        elementAt(calls, 0).resolve(JSON.parse('{"rows": "Row 0", "start": 0, "totalCount": 1}') as GridPage<string>);
        await settle();
        expect(failures).toHaveLength(1);
        expect(failures[0]).toBeInstanceOf(TypeError);
    });

    it('ADR-0023: the Filter is kept as a copy, so the Consumer changing its own object moves nothing', () => {
        const { source, calls } = server();
        const clauses = [{ operator: 'isBlank' as const }];
        const spec: FilterSpec = { clauses };
        const filter: GridFilter = { columns: { Book: spec } };
        source.setFilter(filter);
        expect(calls).toHaveLength(2);
        clauses.push({ operator: 'isBlank' });
        expect(source.snapshot().filter?.columns['Book']?.clauses).toHaveLength(1);
        expect(elementAt(calls, 1).query.filter?.columns['Book']?.clauses).toHaveLength(1);
        // Now different in what it says, so it is a change.
        source.setFilter(filter);
        expect(calls).toHaveLength(3);
    });

    it('ADR-0025: a read-ahead that is not a whole number of rows is refused', () => {
        expect(() => GridSource.fetch(unanswered, { readAheadRows: -1 })).toThrow(RangeError);
        expect(() => GridSource.fetch(unanswered, { readAheadRows: 2.5 })).toThrow(RangeError);
    });

    it('ADR-0350: a superseded fetch is called off through its signal, and a disposed source asks nothing more', async () => {
        const signals: AbortSignal[] = [];
        const source = GridSource.fetch<string>((_query, signal) => {
            signals.push(signal);
            return new Promise(() => { /* never answered */ });
        });
        source.setColumns([]);
        source.dispose();
        expect(elementAt(signals, 0).aborted).toBe(true);
        await expect(source.rows({ start: 0, count: 1 }, new AbortController().signal)).rejects.toThrow('disposed');
        await expect(source.distinctValues('Book', new AbortController().signal)).rejects.toThrow('disposed');
        expect(signals).toHaveLength(1);
    });

    it('ADR-0055 / ADR-0130: a source finds and summarises only when the Consumer gave it a server that does', () => {
        const { source } = server();
        expect(source.find).toBeUndefined();
        expect(source.summarize).toBeUndefined();
    });

    it('ADR-0055 / ADR-0130: Find and a Summary are asked under the Sort and the Filter in force', async () => {
        const handed: QueryInForce[] = [];
        const source = GridSource.fetch<string>(() => new Promise(() => { /* never answered */ }), {
            find: async (_request, query) => {
                handed.push(query);
                return notFound;
            },
            summarize: async (_request, query) => {
                handed.push(query);
                return declinedSummary('a million rows');
            },
        });
        source.setSorts([{ column: 'Book', direction: 'descending' }]);
        source.setFilter({ columns: { Book: { clauses: [{ operator: 'isNotBlank' }] } } });
        const signal = new AbortController().signal;
        await expect(source.find?.(findRequest, signal)).resolves.toEqual(notFound);
        await expect(source.summarize?.(summaryRequest, signal)).resolves.toEqual({ kind: 'declined', reason: 'a million rows' });
        expect(handed).toHaveLength(2);
        for (const query of handed) {
            expect(query.sorts).toEqual([{ column: 'Book', direction: 'descending' }]);
            expect(query.filter).toEqual({ columns: { Book: { clauses: [{ operator: 'isNotBlank' }] } } });
        }
    });
});
