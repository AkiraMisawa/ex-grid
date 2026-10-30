// Layer 1: a report laid out from Sibling Windows (ADR-0370 section 3). Every sibling of a windowed set has its
// rows, those outside the window Pending Rows, so the row count is the whole report's; each answered row is the
// whole report's row at its place, in every form; a report source over a server asks for windows only once
// the question is refused whole, moves them where a report Window reaches Pending Rows, and refuses a Copy that
// reaches one by name; and a window over a hundred thousand siblings is laid out in slices (PV-40).
import { decimalFromText } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import {
    createPivotLayout, FetchingPivotSource, levelQueryForLayout, LocalPivotReportSource, PivotAnswer, PivotAnswerBuilder, pivotCube, PivotJson,
    pivotRecordField, PivotRefusals, pivotReport, pivotReportAsync, PivotSourceFeatures, RecordPivotSource, textItemKey, type PivotLayout,
    type PivotLayoutInput, type PivotQuery, type PivotReport, type PivotReportAnswer, type PivotReportUpdate, type PivotSlicing,
} from '../src/index';
import { elementAt } from '../src/must';

interface Book {
    readonly region: string;
    readonly book: string;
    readonly amount: string | null;
}

// Three regions of 1,500 books each, two records a book. Sums repeat, so siblings tie at a window's edges, and
// now and then a book has no amount, whose sum is no number and follows every number.
const books: readonly Book[] = ['East', 'West', 'North'].flatMap((region, r) => Array.from({ length: 3000 }, (_, i): Book => {
    const n = Math.floor(i / 2);
    return {
        region,
        book: `B${String(n).padStart(4, '0')}`,
        amount: n % 97 === 13 ? null : String(((n * 7919 + r * 31 + (i % 2) * 17) % 1000) / 10),
    };
}));

const fields = [
    pivotRecordField<Book>({ name: 'Region', type: 'text', value: b => b.region }),
    pivotRecordField<Book>({ name: 'Book', type: 'text', value: b => b.book }),
    pivotRecordField<Book>({ name: 'Amount', type: 'number', value: b => (b.amount === null ? null : decimalFromText(b.amount)) }),
];

function layoutOf(changes: Partial<PivotLayoutInput> = {}, descending = true): PivotLayout {
    return createPivotLayout({
        rows: ['Region', { field: 'Book', sort: { direction: descending ? 'descending' : 'ascending', byValue: 0 } }],
        values: [{ field: 'Amount', aggregation: 'sum', numberFormat: '0.0' }, { field: 'Amount', aggregation: 'count', caption: 'Records' }],
        ...changes,
    });
}

// A row as the report shows it: its role, its labels and its values; a Pending Row has neither.
function lineOf(report: PivotReport, i: number): string {
    const row = elementAt(report.rows, i);
    if (row.role === 'pending') return 'pending';
    return `${row.role} ${row.labels.map(label => label.text).join('/')} ${report.valueColumns.map((_, c) => report.valueAt(row, c)?.text).join(' ')}`;
}

describe('ADR-0370: a report laid out from Sibling Windows', () => {
    const forms: readonly (readonly [string, Partial<PivotLayoutInput>])[] = [
        ['compact', {}],
        ['outline, the values on rows', { form: 'outline', valuesAxis: 'rows' }],
        ['tabular', { form: 'tabular' }],
        ['tabular, the values on rows, labels repeated', { form: 'tabular', valuesAxis: 'rows', repeatItemLabels: true }],
        ['compact, subtotals at the bottom', { subtotalsAtTop: false }],
    ];
    for (const [name, changes] of forms) {
        it(`ADR-0370: each answered row is the whole report's row at its place, ${name}`, async () => {
            const source = new RecordPivotSource(books, fields);
            const layout = layoutOf(changes);
            const whole = pivotReport(pivotCube(levelQueryForLayout(layout, 100_000), await source.aggregate(levelQueryForLayout(layout, 100_000)), fields), layout, { locale: 'invariant' });
            const query = levelQueryForLayout(layout, 4000, { limit: 300, starts: [{ rows: [textItemKey('East')], start: 600 }, { rows: [textItemKey('North')], start: 1400 }] });
            const answer = await source.aggregate(query);
            expect(answer.windows).toHaveLength(3);
            const report = pivotReport(pivotCube(query, answer, fields), layout, { locale: 'invariant' });
            expect(report.rows).toHaveLength(whole.rows.length);
            let read = 0;
            for (let i = 0; i < report.rows.length; i++) {
                const line = lineOf(report, i);
                if (line === 'pending') continue;
                read++;
                expect(line, `row ${i}`).toBe(lineOf(whole, i));
            }
            // Every sibling outside a window has a sibling's rows, pending: a group row and a row for each Value Field
            // with the values on rows, but for the Tabular form, which has no group row.
            const perSibling = changes.valuesAxis === 'rows' ? (changes.form === 'tabular' ? 2 : 3) : 1;
            expect(report.rows.length - read).toBe(answer.windows.reduce((n, window) => n + window.before + window.after, 0) * perSibling);
            expect(read).toBeGreaterThan(3 * 300 * perSibling);
        });
    }

    it('ADR-0370: a Pending Row is read by none of the report\'s readings, each refusal naming its parent', async () => {
        const source = new RecordPivotSource(books, fields);
        const layout = layoutOf();
        const query = levelQueryForLayout(layout, 4000, { limit: 300 });
        const report = pivotReport(pivotCube(query, await source.aggregate(query), fields), layout, { locale: 'invariant' });
        const pending = report.rows.find(row => row.role === 'pending');
        if (pending === undefined) throw new Error('A Pending Row.');
        const message = 'The row stands for a sibling the Pivot Source has not answered: the siblings under Text:East were answered as a window that does not hold it.';
        expect(() => report.valueAt(pending, 0)).toThrow(message);
        expect(() => report.rowPath(pending)).toThrow(message);
        expect(() => report.valueFieldAt(pending, 0)).toThrow(message);
        expect(() => report.detailsQuery(pending, 0)).toThrow(message);
    });

    it('ADR-0370: a cube of windows holds a layout that orders and collapses its sets alike, and no other', async () => {
        const source = new RecordPivotSource(books, fields);
        const layout = layoutOf();
        const query = levelQueryForLayout(layout, 4000, { limit: 300 });
        const cube = pivotCube(query, await source.aggregate(query), fields);
        expect(cube.holds(layoutOf({ form: 'tabular' }))).toBe(true);
        expect(cube.holds(layoutOf({}, false))).toBe(false);
        expect(() => pivotReport(cube, layoutOf({}, false))).toThrow('The cube holds Sibling Windows cut by the orders and collapse its question asked');
        // A sort by a Value Field the layout does not have is named, not read past the list.
        const unsorted = createPivotLayout({ rows: ['Region', { field: 'Book', sort: { direction: 'descending', byValue: 2 } }], values: [{ field: 'Amount' }] });
        expect(() => levelQueryForLayout(unsorted, 4000, { limit: 300 })).toThrow("'Book' is ordered by Value Field 2, and the layout has 1 (ADR-0060).");
    });
});

// A Window answered, or the refusal thrown.
function answered(update: PivotReportUpdate): PivotReportAnswer {
    if (update.refusal !== undefined) throw new Error(`Refused: ${update.refusal.message}`);
    return update;
}

// The texts of a Window's rows.
function texts(update: PivotReportUpdate): string[] {
    return answered(update).rows.map(row => `${row.role} ${row.labels.map(label => label.text).join('/')} ${row.values.map(value => value?.text).join(' ')}`);
}

// A server over the books: every question and answer crosses PivotJson, as a server's would, and each question is
// kept in `asked`.
function serverOver(asked: PivotQuery[]): FetchingPivotSource {
    const inner = new RecordPivotSource(books, fields);
    return new FetchingPivotSource(inner.fields, new PivotSourceFeatures(inner.features.aggregations, false, false, true), {
        aggregate: async (question, signal) => {
            const read = PivotJson.readQuery(PivotJson.writeQuery(question));
            asked.push(read);
            return PivotJson.readAnswer(PivotJson.writeAnswer(await inner.aggregate(read, signal)));
        },
        items: (question, signal) => inner.items(question, signal),
        details: (question, signal) => inner.details(question, signal),
    });
}

const at = (start: number): { start: number; count: number } => ({ start, count: 40 });

describe('ADR-0370: a report source over a server that answers Sibling Windows', () => {
    const settings = { locale: 'invariant', words: {} };
    // 4,504 groups shown; the windows of 1,000 and their margins, ties and rests fit.
    const maxLeaves = 4000;

    it('ADR-0370: asks for windows once refused whole, and moves them where a report Window reaches Pending Rows', async () => {
        const asked: PivotQuery[] = [];
        const reports = new LocalPivotReportSource(serverOver(asked));
        const everything = new LocalPivotReportSource(new RecordPivotSource(books, fields));
        const layout = layoutOf();

        const top = answered(await reports.window({ requestId: 'r1', layout, settings, window: at(0), maxLeaves }));
        expect(asked.map(question => question.levels?.window?.limit)).toEqual([undefined, 1000]);
        const all = answered(await everything.window({ requestId: 'e1', layout, settings, window: at(0) }));
        expect(texts(top)).toEqual(texts(all));
        expect(top.metadata.rowCount).toBe(all.metadata.rowCount);

        // Past East's window: asked again, the window started so it holds the rows asked for, the rows' sequence kept.
        // East's books are rows 1 to 1,500, North's 1,502 to 3,001 and West's 3,003 to 4,502.
        const deep = answered(await reports.window({ requestId: 'r2', layout, settings, window: at(1200), maxLeaves }));
        expect(asked).toHaveLength(3);
        // Centred on the rows asked for, and no further than the limit from the end.
        expect(asked.at(-1)?.levels?.window?.starts).toEqual([{ rows: [textItemKey('East')], start: 500 }]);
        expect(texts(deep)).toEqual(texts(await everything.window({ requestId: 'e2', layout, settings, window: at(1200) })));
        expect(deep.metadata.rowSequenceVersion).toBe(top.metadata.rowSequenceVersion);

        // Inside the window answered: nothing is asked.
        const near = await reports.window({ requestId: 'r3', layout, settings, window: at(1500), maxLeaves });
        expect(asked).toHaveLength(3);
        expect(texts(near)).toEqual(texts(await everything.window({ requestId: 'e3', layout, settings, window: at(1500) })));

        // A layout that asks the same question, another number format, keeps the windows: nothing is asked.
        const formatted = layoutOf({ values: [{ field: 'Amount', aggregation: 'sum', numberFormat: '0.00' }, { field: 'Amount', aggregation: 'count', caption: 'Records' }] });
        const reformatted = await reports.window({ requestId: 'r3b', layout: formatted, settings, window: at(1200), maxLeaves });
        expect(asked).toHaveLength(3);
        expect(texts(reformatted)).toEqual(texts(await everything.window({ requestId: 'e3b', layout: formatted, settings, window: at(1200) })));

        // A Window across two sets moves each that it reaches pending, and keeps the others' windows.
        await reports.window({ requestId: 'r4', layout, settings, window: at(4300), maxLeaves });
        const across = await reports.window({ requestId: 'r5', layout, settings, window: { start: 2980, count: 60 }, maxLeaves });
        expect(asked).toHaveLength(5);
        const starts = asked.at(-1)?.levels?.window?.starts.map(start => `${start.rows.map(item => item.value).join('/')} ${start.start}`).toSorted();
        expect(starts).toEqual(['East 500', 'North 500', 'West 0']);
        expect(texts(across)).toEqual(texts(await everything.window({ requestId: 'e5', layout, settings, window: { start: 2980, count: 60 } })));

        // Another order asks afresh, each window at its first sibling until a Window reaches past it.
        const ascending = await reports.window({ requestId: 'r6', layout: layoutOf({}, false), settings, window: at(0), maxLeaves });
        expect(asked.at(-1)?.levels?.window?.starts).toEqual([]);
        expect(texts(ascending)).toEqual(texts(await everything.window({ requestId: 'e6', layout: layoutOf({}, false), settings, window: at(0) })));

        // In the Tabular form a region's label stands on its block's first row, which a window started part way leaves pending.
        const tabular = layoutOf({ form: 'tabular' });
        const block = await reports.window({ requestId: 'r7', layout: tabular, settings, window: { start: 500, count: 1000 }, maxLeaves });
        expect(texts(block)).toEqual(texts(await everything.window({ requestId: 'e7', layout: tabular, settings, window: { start: 500, count: 1000 } })));
    });

    it('ADR-0370: a Copy that reaches Pending Rows is refused by name, and one within the rows answered is the whole report\'s', async () => {
        const reports = new LocalPivotReportSource(serverOver([]));
        const everything = new LocalPivotReportSource(new RecordPivotSource(books, fields));
        const layout = layoutOf();
        const shown = answered(await reports.window({ requestId: 'r1', layout, settings, window: at(0), maxLeaves }));
        const all = answered(await everything.window({ requestId: 'e1', layout, settings, window: at(0) }));
        const version = shown.metadata.version;
        const columns = ['row-labels', ...shown.metadata.valueColumns.map(column => column.name)];
        const through = await reports.copy({ version, columns, ranges: [{ top: 0, left: 0, bottom: 2000, right: 2 }] });
        expect(through.refusal?.kind).toBe('rowsNotAnswered');
        const summary = await reports.summary({ version, columns, ranges: [{ top: 900, left: 1, bottom: 1100, right: 1 }] });
        expect(summary.refusal?.kind).toBe('rowsNotAnswered');
        const focused = await reports.summary({ version, columns, ranges: [{ top: 0, left: 1, bottom: 3, right: 1 }], focusRow: 1200, focusColumn: 1 });
        expect(focused.refusal?.kind).toBe('rowsNotAnswered');
        const within = await reports.copy({ version, columns, ranges: [{ top: 0, left: 0, bottom: 39, right: 2 }] });
                expect(within.blocks).toEqual((await everything.copy({ version: all.metadata.version, columns, ranges: [{ top: 0, left: 0, bottom: 39, right: 2 }] })).blocks);
    });
});

// Slices that end after every step, counted.
function everyStep(): { readonly slicing: PivotSlicing; readonly yields: () => number } {
    let yields = 0;
    return {
        slicing: {
            sliceBudgetMs: 0,
            yield: () => {
                yields++;
                return Promise.resolve();
            },
        },
        yields: () => yields,
    };
}

// A piece of the work makes 256 rows at most (a piece's 1,024 units, four a row), so `rows` take at least half as
// many pieces as that. Made in one step, as a window's Pending Rows once were, a hundred thousand took 15 yields
// in all, the cube's and the answer's included.
const piecesAtLeast = (rows: number): number => rows / 512;

describe('ADR-0370, PV-40: a window over a hundred thousand siblings', { timeout: 20_000 }, () => {
    const siblings = 100_000;
    const shown = 1000;
    const customers = [
        pivotRecordField<unknown>({ name: 'Region', type: 'text', value: () => 'East' }),
        pivotRecordField<unknown>({ name: 'Customer', type: 'text', value: () => 'C' }),
        pivotRecordField<unknown>({ name: 'Orders', type: 'number', value: () => null }),
    ];
    const layout = createPivotLayout({
        rows: ['Region', { field: 'Customer', sort: { direction: 'descending', byValue: 0 } }],
        values: [{ field: 'Orders', aggregation: 'count' }],
    });
    const east = textItemKey('East');

    // East's customers by how many orders they hold, the window `before` siblings from the first: a margin and a
    // rest on each side of it that has siblings, the first sibling holding the most orders and each the next one fewer.
    function answerFor(query: PivotQuery, before: number): PivotAnswer {
        const builder = new PivotAnswerBuilder(query, 'v1');
        const after = siblings - before - shown;
        const orders = (sibling: number): number => siblings - sibling + 1;
        const count = (cell: number, records: number): void => {
            builder.setCounts(cell, 0, records, records);
        };
        const sum = (from: number, to: number): number => ((orders(from) + orders(to - 1)) * (to - from)) / 2;
        count(builder.addCell([], [], sum(0, siblings)), sum(0, siblings));
        count(builder.addCell([east], [], sum(0, siblings)), sum(0, siblings));
        const window = builder.addWindow([east], before, after);
        for (let sibling = Math.max(0, before - 1); sibling <= Math.min(siblings - 1, before + shown); sibling++) {
            count(builder.addCell([east, textItemKey(`C${sibling}`)], [], orders(sibling)), orders(sibling));
        }
        if (before > 1) count(builder.addRest(window, 'before', [], sum(0, before - 1)), sum(0, before - 1));
        if (after > 1) count(builder.addRest(window, 'after', [], sum(before + shown + 1, siblings)), sum(before + shown + 1, siblings));
        return builder.build();
    }

    it('ADR-0370, PV-40: its Pending Rows on both sides are made a piece at a time, as at once', async () => {
        const query = levelQueryForLayout(layout, 10_000, { limit: shown, starts: [{ rows: [east], start: 40_000 }] });
        const cube = pivotCube(query, answerFor(query, 40_000), customers);
        const pieces = everyStep();
        const sliced = await pivotReportAsync(cube, layout, { locale: 'invariant' }, pieces.slicing);
        const whole = pivotReport(cube, layout, { locale: 'invariant' });
        expect(sliced.rows).toHaveLength(siblings + 2);
        expect(sliced.rows.map(row => row.role)).toEqual(whole.rows.map(row => row.role));
        expect(sliced.rows.filter(row => row.role === 'pending')).toHaveLength(siblings - shown);
        expect(lineOf(sliced, 40_001)).toBe(lineOf(whole, 40_001));
        expect(pieces.yields()).toBeGreaterThan(piecesAtLeast(siblings));
    });

    it('ADR-0370, PV-40: a report source lays a window\'s Pending Rows out a piece at a time', async () => {
        const inner = new RecordPivotSource([], customers);
        const server = new FetchingPivotSource(inner.fields, new PivotSourceFeatures(inner.features.aggregations, false, false, true), {
            aggregate: question => Promise.resolve(question.levels?.window === undefined
                ? PivotAnswer.refused(PivotRefusals.tooManyLeaves(question.maxLeaves))
                : answerFor(question, 0)),
            items: (question, signal) => inner.items(question, signal),
            details: (question, signal) => inner.details(question, signal),
        });
        const pieces = everyStep();
        const reports = new LocalPivotReportSource(server, { slicing: pieces.slicing });
        const top = answered(await reports.window({ requestId: 'r1', layout, settings: { locale: 'invariant', words: {} }, window: at(0), maxLeaves: 10_000 }));
        expect(top.metadata.rowCount).toBe(siblings + 2);
        expect(top.rows.map(row => row.labels[0]?.text).slice(0, 3)).toEqual(['East', 'C0', 'C1']);
        expect(pieces.yields()).toBeGreaterThan(piecesAtLeast(siblings));
    });
});
