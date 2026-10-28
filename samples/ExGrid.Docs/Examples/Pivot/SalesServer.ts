import { createChangeBatch } from '@ex-grid/data';
import {
    FetchingPivotSource, PivotJson, PivotSourceFeatures, SnapshotPivotSource, type PivotField, type PivotServerNotice,
} from '@ex-grid/pivot-engine';
import { mulberry32 } from './Seeded';
import { sampleSales } from './SalesData';
import { amendSales, salesSnapshot, salesSnapshotFields } from './SalesSnapshot';

const latencyMs = 500;

// A network's delay, ended early by a question the pivot no longer needs.
function delay(signal: AbortSignal | undefined): Promise<void> {
    return new Promise((resolve, reject) => {
        signal?.throwIfAborted();
        const abort = (): void => {
            clearTimeout(timer);
            reject(signal?.reason);
        };
        const timer = setTimeout(() => {
            signal?.removeEventListener('abort', abort);
            resolve();
        }, latencyMs);
        signal?.addEventListener('abort', abort, { once: true });
    });
}

/**
 * A server, simulated in the page: each endpoint takes a question written by PivotJson and answers, after half a
 * second, with the Leaf Aggregates, a page of Items or the records behind a cell. The browser builds the report from
 * them. In an application each would be an HTTP endpoint over your own data, and its notices a WebSocket, Server-Sent
 * Events, SignalR or a poll.
 */
export class SalesServer {
    readonly #sales = [...sampleSales()];
    readonly #data = new SnapshotPivotSource(salesSnapshot(this.#sales), salesSnapshotFields);
    readonly #random = mulberry32(7);
    #trading: ReturnType<typeof setInterval> | undefined;

    /** GET /api/pivot/fields: the fields the server offers. */
    get fields(): readonly PivotField[] {
        return this.#data.fields;
    }

    /** POST /api/pivot/aggregate. */
    async aggregate(question: string, signal?: AbortSignal): Promise<string> {
        await delay(signal);
        return PivotJson.writeAnswer(await this.#data.aggregate(PivotJson.readQuery(question), signal));
    }

    /** POST /api/pivot/items: a page of a field's Items, for Filter…. */
    async items(question: string, signal?: AbortSignal): Promise<string> {
        await delay(signal);
        return PivotJson.writeItemPage(await this.#data.items(PivotJson.readItemsQuery(question), signal));
    }

    /** POST /api/pivot/details: the records behind a cell, for Show Details. */
    async details(question: string, signal?: AbortSignal): Promise<string> {
        await delay(signal);
        return PivotJson.writeDetailPage(await this.#data.details(PivotJson.readDetailsQuery(question), signal));
    }

    /** What the server pushes when its data moves on: the Source Version it moved to, as PivotJson writes it. */
    listen(push: (notice: string) => void): () => void {
        return this.#data.subscribe(changed => { push(PivotJson.writeSourceChanged(changed)); });
    }

    /** Starts amending a few sales every `everyMs`, as orders are changed elsewhere; each amendment is one Change Batch. */
    startTrading(everyMs: number): void {
        this.#trading ??= setInterval(() => {
            this.#data.apply(createChangeBatch({ changed: salesSnapshot(amendSales(this.#sales, this.#random, 8)) }));
        }, everyMs);
    }

    dispose(): void {
        clearInterval(this.#trading);
        this.#trading = undefined;
    }
}

/**
 * The browser's source over the server: each fetcher writes the question as JSON, sends it, and reads the answer
 * back. The transport, its authentication and its retries are yours. `fields` are the server's, or yours over them.
 */
export function fetchFrom(server: SalesServer, fields: readonly PivotField[] = server.fields, reachable: () => boolean = () => true): FetchingPivotSource {
    // Cut off, a question fails as an unreachable server's does.
    const reach = (): void => {
        if (!reachable()) throw new Error('The server cannot be reached.');
    };
    return new FetchingPivotSource(fields, PivotSourceFeatures.all, {
        aggregate: async (query, signal) => {
            reach();
            return PivotJson.readAnswer(await server.aggregate(PivotJson.writeQuery(query), signal));
        },
        items: async (query, signal) => {
            reach();
            return PivotJson.readItemPage(await server.items(PivotJson.writeItemsQuery(query), signal));
        },
        details: async (query, signal) => {
            reach();
            return PivotJson.readDetailPage(await server.details(PivotJson.writeDetailsQuery(query), signal));
        },
        // However the news arrives, hand each message on: the pivot asks the report on screen again.
        subscribe: (notify: (notice: PivotServerNotice) => void) => server.listen(message => {
            notify(PivotJson.readServerNotice(message, fields));
        }),
    });
}
