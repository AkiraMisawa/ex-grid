// Layer 1: live server data (ADR-0370 section 3). The Consumer's transport, started by the fetchers'
// `subscribe`, tells a fetching source that the server's data moved on, or hands it a Change Batch of the records
// the data lost and gained. A batch is folded into the Leaf Aggregates the source holds — after any sequence of
// batches, what the server answers over the data they made — and anything the fold cannot keep exactly is asked
// of the server again: never a figure the server would not answer.
import { decimalFromText, type SnapshotDecimal } from '@ex-grid/data';
import { describe, expect, it } from 'vitest';
import {
    createPivotLayout, FetchingPivotSource, LocalPivotReportSource, PivotAnswer, PivotAnswerBuilder, PivotJson, pivotQuery, pivotRecordField,
    PivotSourceFeatures, RecordPivotSource, type PivotDetailPage, type PivotDetailValue, type PivotItemPage, type PivotPart, type PivotQuery, type PivotReportUpdate,
    type PivotServerNotice, type PivotSourceChanged,
} from '../src/index';
import { pivotItemKey } from '../src/items/item-key';
import { datePartField, pivotField } from '../src/fields/pivot-field';
import { checkBatchRecords, foldServerBatches } from '../src/source/server-batches';

interface Trade {
    readonly id: number;
    readonly region: string;
    readonly desk: string | null;
    readonly amount: string | null;
    readonly price: number | null;
}

const fields = [
    pivotRecordField<Trade>({ name: 'Region', type: 'text', value: t => t.region }),
    pivotRecordField<Trade>({ name: 'Desk', type: 'text', value: t => t.desk }),
    pivotRecordField<Trade>({ name: 'Amount', type: 'number', value: t => (t.amount === null ? null : decimalFromText(t.amount)) }),
    pivotRecordField<Trade>({ name: 'Price', type: 'number', value: t => t.price }),
];

// A record as a Change Batch carries it: each field's value in the fields' order.
function valuesOf(trade: Trade): readonly PivotDetailValue[] {
    return [trade.region, trade.desk, trade.amount === null ? null : decimalFromText(trade.amount), trade.price];
}

// Seeded, so a failure is the same failure on every run.
function random(seed: number): () => number {
    let state = seed >>> 0;
    return () => {
        state = (state + 0x6d2b79f5) >>> 0;
        let t = state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
    };
}

const regions = ['East', 'West', 'North', 'south'];
const desks = ['Rates', 'FX', null];

function tradeOf(id: number, next: () => number): Trade {
    const pick = <T>(list: readonly T[]): T => list[Math.floor(next() * list.length)] as T;
    const cents = Math.floor(next() * 200_000) - 50_000;
    return {
        id,
        region: pick(regions),
        desk: pick(desks),
        amount: next() < 0.1 ? null : (cents / 100).toFixed(2),
        price: next() < 0.1 ? null : Math.round(next() * 1000) / 8,
    };
}

/** A server over trades held in memory: it answers a question as the bundled source answers the trades it
 * holds now, under the version it is at, and hands its transport's notify the batches its changes make. */
class Server {
    trades: Trade[];
    version = 1;
    asked = 0;
    started = 0;
    stopped = 0;
    notify: ((notice: PivotServerNotice) => void) | undefined;
    // Set to hold the next answer until it is let go.
    hold: Promise<void> | undefined;

    constructor(initial: readonly Trade[]) {
        this.trades = [...initial];
    }

    get sourceVersion(): string {
        return `v${this.version}`;
    }

    /** The answer the server gives now: the bundled source's over the trades, under the server's version. */
    answer(query: PivotQuery): PivotAnswer {
        const json = PivotJson.writeAnswer(new RecordPivotSource(this.trades, fields).aggregateNow(query));
        return PivotJson.readAnswer(json.replace(/"sourceVersion":"[^"]*"/u, `"sourceVersion":"${this.sourceVersion}"`));
    }

    source(): FetchingPivotSource {
        const inner = new RecordPivotSource([], fields);
        return new FetchingPivotSource(fields, new PivotSourceFeatures(inner.features.aggregations, false, false, true), {
            aggregate: async query => {
                this.asked++;
                const answer = this.answer(query);
                if (this.hold !== undefined) await this.hold;
                return answer;
            },
            items: (query, signal) => inner.items(query, signal),
            details: (query, signal) => inner.details(query, signal),
            subscribe: notify => {
                this.started++;
                this.notify = notify;
                return () => {
                    this.stopped++;
                    this.notify = undefined;
                };
            },
        });
    }

    /** Changes the trades and tells the transport: `removed` by id, `added` and `changed` as they are now. */
    change(removedIds: readonly number[], added: readonly Trade[], changed: readonly Trade[] = []): void {
        const lost = this.trades.filter(t => removedIds.includes(t.id) || changed.some(c => c.id === t.id));
        this.trades = [...this.trades.filter(t => !lost.includes(t)), ...changed, ...added];
        const from = this.sourceVersion;
        this.version++;
        this.notify?.({ kind: 'changeBatch', from, to: this.sourceVersion, removed: lost.map(valuesOf), added: [...changed, ...added].map(valuesOf) });
    }
}

// A trade the server holds that carries an amount, so taking it away takes a number away.
function withAmount(server: Server): Trade {
    const trade = server.trades.find(t => t.amount !== null);
    if (trade === undefined) throw new Error('the server holds no trade with an amount');
    return trade;
}

function number(n: { readonly kind: string; readonly value: SnapshotDecimal | number }): string {
    return typeof n.value === 'number' ? `d${String(n.value)}` : `x${String(n.value.mantissa)}e-${String(n.value.scale)}`;
}

function nothing(): void {
    // A gate not yet given its opener.
}

// The grand total's text in a laid-out report.
function total(update: PivotReportUpdate): string | undefined {
    if (update.refusal !== undefined) throw new Error(update.refusal.message);
    return update.rows.at(-1)?.values[0]?.text;
}

// An answer read whole, leaf by leaf, whatever order its leaves and Items come in.
function read(answer: PivotAnswer, parts: readonly (readonly PivotPart[])[]): string[] {
    if (answer.isRefused) return [`refused ${answer.refusal?.kind ?? ''}`]; // oxlint-disable-line ex-grid/no-stand-in-default -- a refused answer carries its refusal
    const axes = [...answer.rows, ...answer.columns];
    const lines: string[] = [];
    for (let leaf = 0; leaf < answer.leafCount; leaf++) {
        const items = axes.map(axis => {
            const key = axis.items[axis.itemAt(leaf)];
            return `${key?.kind ?? ''}:${(key?.value ?? '').toLowerCase()}`; // oxlint-disable-line ex-grid/no-stand-in-default -- a Blank Item has no text
        }).join('/');
        const values = answer.values.map((v, i) => {
            let line = `${String(v.valuesAt(leaf))},${String(v.numbersAt(leaf))},${String(v.nonFiniteAt(leaf))}`;
            if (parts[i]?.includes('sum') === true) line += ` sum ${number(v.sumAt(leaf))}`;
            if (parts[i]?.includes('extremes') === true && v.numbersAt(leaf) > 0) line += ` min ${number(v.minAt(leaf))} max ${number(v.maxAt(leaf))}`;
            return line;
        });
        lines.push(`${items} ${String(answer.recordsAt(leaf))} | ${values.join(' | ')}`);
    }
    return lines.toSorted();
}

const exactParts: readonly (readonly PivotPart[])[] = [['sum'], []];
const exactQuery = pivotQuery({
    rows: [{ field: 'Region' }],
    columns: [{ field: 'Desk' }],
    values: [{ field: 'Amount', parts: ['sum'] }, { field: 'Price', parts: [] }],
});

function trades(count: number, seed: number): Trade[] {
    const next = random(seed);
    return Array.from({ length: count }, (_, i) => tradeOf(i + 1, next));
}

describe('ADR-0370: live server data', () => {
    it('ADR-0370: a Change Batch is folded into the answer held, as the server answers the data it makes, without asking it', async () => {
        const server = new Server(trades(400, 7));
        const source = server.source();
        const stop = source.subscribe(() => undefined);
        expect(read(await source.aggregate(exactQuery), exactParts)).toEqual(read(server.answer(exactQuery), exactParts));
        const next = random(11);
        let id = 1000;
        for (let step = 0; step < 40; step++) {
            const removed = server.trades.filter(() => next() < 0.02).map(t => t.id);
            const changed = server.trades.filter(t => !removed.includes(t.id) && next() < 0.02).map(t => ({ ...tradeOf(t.id, next), id: t.id }));
            const added = Array.from({ length: Math.floor(next() * 5) }, () => tradeOf(id++, next));
            server.change(removed, added, changed);
            // Every few batches the question is asked: those heard since are folded at once.
            if (step % 3 !== 2) continue;
            const answer = await source.aggregate(exactQuery);
            expect(answer.sourceVersion).toBe(server.sourceVersion);
            expect(read(answer, exactParts), `after batch ${String(step)}`).toEqual(read(server.answer(exactQuery), exactParts));
        }
        expect(server.asked).toBe(1);
        stop();
    });

    it('ADR-0370: an Item that first appears brings its leaves, and one whose last record leaves takes them away', async () => {
        const server = new Server([
            { id: 1, region: 'East', desk: 'FX', amount: '10', price: 1 },
            { id: 2, region: 'West', desk: 'FX', amount: '20', price: 2 },
        ]);
        const source = server.source();
        source.subscribe(() => undefined);
        await source.aggregate(exactQuery);
        server.change([2], [{ id: 3, region: 'NORTH', desk: null, amount: '7.25', price: null }]);
        const answer = await source.aggregate(exactQuery);
        expect(read(answer, exactParts)).toEqual(read(server.answer(exactQuery), exactParts));
        expect(answer.rows[0]?.items.map(item => item.value)).toEqual(['East', 'NORTH']);
        expect(server.asked).toBe(1);
    });

    const asksAgain: readonly (readonly [string, PivotQuery, (server: Server) => void])[] = [
        ['a sum that is a double, touched', pivotQuery({ rows: [{ field: 'Region' }], values: [{ field: 'Price', parts: ['sum'] }] }),
            server => { server.change([], [{ id: 900, region: 'East', desk: 'FX', amount: '1', price: 0.1 }]); }],
        ['an extreme a number left', pivotQuery({ rows: [{ field: 'Region' }], values: [{ field: 'Amount', parts: ['extremes'] }] }),
            server => { server.change([withAmount(server).id], []); }],
        ['a product, touched', pivotQuery({ rows: [{ field: 'Region' }], values: [{ field: 'Amount', parts: ['product'] }] }),
            server => { server.change([], [{ id: 900, region: 'East', desk: 'FX', amount: '2', price: 1 }]); }],
        ['a variance, touched', pivotQuery({ rows: [{ field: 'Region' }], values: [{ field: 'Amount', parts: ['variance'] }] }),
            server => { server.change([], [{ id: 900, region: 'East', desk: 'FX', amount: '2', price: 1 }]); }],
        ['a batch that does not start from the answer held', exactQuery,
            server => {
                const from = server.sourceVersion;
                server.version += 2;
                server.notify?.({ kind: 'changeBatch', from: `${from}x`, to: server.sourceVersion, removed: [], added: [] });
            }],
        ['a batch that takes a record from a leaf the answer does not hold', exactQuery,
            server => {
                const from = server.sourceVersion;
                server.version++;
                server.notify?.({ kind: 'changeBatch', from, to: server.sourceVersion, removed: [['Nowhere', 'FX', decimalFromText('1'), 1]], added: [] });
            }],
        ['a notice that the data moved on', exactQuery,
            server => {
                server.trades = server.trades.slice(1);
                server.version++;
                server.notify?.({ kind: 'changed', sourceVersion: server.sourceVersion });
            }],
    ];
    for (const [what, query, change] of asksAgain) {
        it(`ADR-0370: ${what} is asked of the server again, never folded`, async () => {
            const server = new Server(trades(60, 3));
            const source = server.source();
            source.subscribe(() => undefined);
            await source.aggregate(query);
            change(server);
            const parts = query.values.map(value => value.parts);
            expect(read(await source.aggregate(query), parts)).toEqual(read(server.answer(query), parts));
            expect(server.asked).toBe(2);
        });
    }

    it('ADR-0370: the extremes are folded where numbers come and none leave', async () => {
        const query = pivotQuery({ rows: [{ field: 'Region' }], values: [{ field: 'Amount', parts: ['extremes'] }, { field: 'Price', parts: ['extremes'] }] });
        const parts = query.values.map(value => value.parts);
        const server = new Server(trades(60, 5));
        const source = server.source();
        source.subscribe(() => undefined);
        await source.aggregate(query);
        server.change([], [
            { id: 901, region: 'East', desk: 'FX', amount: '-9999', price: 9999 },
            { id: 902, region: 'Moon', desk: null, amount: '3', price: 0.5 },
            { id: 903, region: 'West', desk: 'FX', amount: null, price: null },
        ]);
        expect(read(await source.aggregate(query), parts)).toEqual(read(server.answer(query), parts));
        expect(server.asked).toBe(1);
    });

    it('ADR-0370: an answer fetched while a batch arrived is shown and not held; another question lets the held one go', async () => {
        const server = new Server(trades(40, 9));
        const source = server.source();
        source.subscribe(() => undefined);
        let release: () => void = nothing;
        server.hold = new Promise(resolve => {
            release = resolve;
        });
        const first = source.aggregate(exactQuery);
        server.change([], [{ id: 900, region: 'East', desk: 'FX', amount: '5', price: 1 }]);
        release();
        server.hold = undefined;
        await first;
        await source.aggregate(exactQuery);
        expect(server.asked).toBe(2);
        const other = pivotQuery({ rows: [{ field: 'Desk' }], values: [{ field: 'Amount', parts: ['sum'] }] });
        await source.aggregate(other);
        server.change([], [{ id: 901, region: 'East', desk: 'FX', amount: '5', price: 1 }]);
        expect(read(await source.aggregate(exactQuery), exactParts)).toEqual(read(server.answer(exactQuery), exactParts));
        expect(server.asked).toBe(4);
    });

    it('ADR-0370: the transport runs while a listener is subscribed; each listener is told the new Source Version', () => {
        const server = new Server(trades(5, 1));
        const source = server.source();
        const told: (PivotSourceChanged | string)[] = [];
        const stopA = source.subscribe(changed => told.push(changed));
        const stopB = source.subscribe(() => told.push('b'));
        expect(server.started).toBe(1);
        server.change([], [{ id: 900, region: 'East', desk: 'FX', amount: '5', price: 1 }]);
        server.notify?.({ kind: 'changed' });
        expect(told).toEqual([{ sourceVersion: 'v2' }, 'b', {}, 'b']);
        stopA();
        stopA();
        expect(server.stopped).toBe(0);
        stopB();
        expect(server.stopped).toBe(1);
        expect(server.notify).toBeUndefined();
        source.subscribe(() => undefined);
        expect(server.started).toBe(2);
    });

    it('ADR-0370: with no transport running the source holds nothing, so each question goes to the server', async () => {
        const server = new Server(trades(10, 3));
        const source = server.source();
        await source.aggregate(exactQuery);
        await source.aggregate(exactQuery);
        expect(server.asked).toBe(2);
        const stop = source.subscribe(() => undefined);
        await source.aggregate(exactQuery);
        await source.aggregate(exactQuery);
        expect(server.asked).toBe(3);
        stop();
        await source.aggregate(exactQuery);
        expect(server.asked).toBe(4);
    });

    it('ADR-0370: a transport written as a method keeps its this, as the other fetchers do', () => {
        let stopped = 0;
        const fetchers = {
            aggregate: (): Promise<PivotAnswer> => Promise.reject(new Error('not asked')),
            items: (): Promise<PivotItemPage> => Promise.reject(new Error('not asked')),
            details: (): Promise<PivotDetailPage> => Promise.reject(new Error('not asked')),
            stopped(): void {
                stopped++;
            },
            subscribe(): () => void {
                return () => { this.stopped(); };
            },
        };
        const source = new FetchingPivotSource(fields, new PivotSourceFeatures(new RecordPivotSource([], fields).features.aggregations, false, false, true), fetchers);
        source.subscribe(() => undefined)();
        expect(stopped).toBe(1);
    });

    it('ADR-0370: a malformed notice throws to the transport and changes nothing', async () => {
        const server = new Server(trades(20, 2));
        const source = server.source();
        let told = 0;
        source.subscribe(() => told++);
        await source.aggregate(exactQuery);
        const notify = server.notify;
        if (notify === undefined) throw new Error('The transport runs.');
        const from = server.sourceVersion;
        const wrong: readonly (readonly [unknown, string])[] = [
            [{ kind: 'moved' }, "'moved' is no notice a fetching source takes"],
            [{ kind: 'changed', sourceVersion: '' }, 'A notice names its Source Version with text, or not at all.'],
            [{ kind: 'changeBatch', from, to: from, removed: [], added: [] }, 'A Change Batch goes from one Source Version to another'],
            [{ kind: 'changeBatch', from, to: 'v9', removed: [['East']], added: [] }, "A Change Batch's removed records: Row 1: the record carries 1 values, and there are 4 fields"],
            [{ kind: 'changeBatch', from, to: 'v9', removed: [], added: [['East', 'FX', new Date(0), 1]] }, "A Change Batch's added records:"],
        ];
        for (const [notice, message] of wrong) {
            expect(() => { notify(notice as PivotServerNotice); }).toThrow(message);
        }
        expect(told).toBe(0);
        // The answer held is still held: a batch from it folds.
        server.change([], [{ id: 900, region: 'East', desk: 'FX', amount: '5', price: 1 }]);
        expect(read(await source.aggregate(exactQuery), exactParts)).toEqual(read(server.answer(exactQuery), exactParts));
        expect(server.asked).toBe(1);
    });

    it('ADR-0370: a date part travels by its number', () => {
        const dated = [pivotField({ name: 'Date', type: 'date' }), datePartField('Year', 'Date', 'year'), pivotField({ name: 'Amount', type: 'number' })];
        const query = pivotQuery({ rows: [{ field: 'Year' }], values: [{ field: 'Amount', parts: ['sum'] }] });
        const builder = new PivotAnswerBuilder(query, 'v1');
        const leaf = builder.addLeaf([pivotItemKey('number', '2026')], 1);
        builder.setCounts(leaf, 0, 1, 1);
        builder.setSum(leaf, 0, { kind: 'exact', value: decimalFromText('4') });
        const folded = foldServerBatches(query, builder.build(), [{ from: 'v1', to: 'v2', removed: [], added: [[null, 2026, decimalFromText('6')], [null, 2025, decimalFromText('1')]] }], dated);
        expect(folded === undefined ? [] : read(folded, [['sum']])).toEqual(['number:2025 1 | 1,1,false sum x1e-0', 'number:2026 2 | 2,2,false sum x10e-0']);
        expect(() => { checkBatchRecords([[null, 638_000_000_000_000_000n, null]], dated); }).toThrow("Row 1, column 'Year': The Year of a date travels as its number");
    });

    it('ADR-0370: a report source over the server lays the batch out, tells its listeners, and Refresh asks the server', async () => {
        const server = new Server(trades(50, 4));
        const reports = new LocalPivotReportSource(server.source());
        const told: PivotSourceChanged[] = [];
        reports.subscribe(changed => told.push(changed));
        const layout = createPivotLayout({ rows: ['Region'], values: [{ field: 'Amount', aggregation: 'sum' }] });
        const settings = { locale: 'invariant', words: {} };
        const first = total(await reports.window({ requestId: 'r1', layout, settings, window: { start: 0, count: 10 } }));
        server.change([], [{ id: 900, region: 'East', desk: 'FX', amount: '1000', price: 1 }]);
        expect(told).toEqual([{ sourceVersion: 'v2' }]);
        const after = total(await reports.window({ requestId: 'r2', layout, settings, window: { start: 0, count: 10 } }));
        expect(Number(after) - Number(first)).toBeCloseTo(1000, 6);
        expect(server.asked).toBe(1);
        await reports.window({ requestId: 'r3', layout, settings, window: { start: 0, count: 10 }, refreshData: true });
        expect(server.asked).toBe(2);
        reports.dispose();
        expect(server.stopped).toBe(1);
    });
});
