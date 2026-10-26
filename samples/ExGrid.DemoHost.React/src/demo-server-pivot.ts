// The demo API server's Pivot Source (ADR-0066, ADR-0370 section 3), as DemoServerPivot is on the Blazor hosts:
// its fields, and a FetchingPivotSource that asks it over HTTP.
import {
    FetchingPivotSource, PivotDetailPage, PivotJson, PivotRefusals, PivotSourceFeatures, pivotField,
    type PivotAggregation, type PivotDetailRecord, type PivotDetailsQuery, type PivotFetchers, type PivotField, type PivotFieldType,
} from '@ex-grid/pivot-engine';
import { apiUrl } from './demo-api';

/** The most records the server answers in one Details page. */
const maxDetailsPage = 10_000;

/** C#'s enum name, as the server writes it ("Text", "CountNumbers"), in the Script Core's spelling. */
function camel(name: unknown): string {
    if (typeof name !== 'string' || name === '') throw new TypeError(`The server named a field type or an Aggregation as ${JSON.stringify(name)}.`);
    return name.charAt(0).toLowerCase() + name.slice(1);
}

/** The server's fields and what it answers. */
export interface ServerPivotDeclared {
    readonly fields: readonly PivotField[];
    readonly features: PivotSourceFeatures;
}

/** GET /api/pivot/fields: the server's fields, with their captions and formats, the Aggregations SQLite answers
 * exactly (ADR-0066), and whether it counts each field's Items and answers a question past its cap with the groups
 * shown (ADR-0370 section 3). */
export async function serverPivotDeclared(signal: AbortSignal): Promise<ServerPivotDeclared> {
    const response = await fetch(apiUrl('api/pivot/fields'), { signal });
    if (!response.ok) throw new Error(`GET /api/pivot/fields answered ${response.status}.`);
    const declared = await response.json() as {
        readonly fields: readonly { readonly name: string; readonly type: string; readonly caption: string; readonly format: string | null }[];
        readonly features: {
            readonly aggregations: readonly string[]; readonly canRefresh: boolean; readonly countsItems: boolean; readonly answersLevels: boolean;
        };
    };
    const fields = declared.fields.map(field => pivotField({
        name: field.name, type: camel(field.type) as PivotFieldType, caption: field.caption, ...(field.format === null ? {} : { format: field.format }),
    }));
    const { aggregations, canRefresh, countsItems, answersLevels } = declared.features;
    return { fields, features: new PivotSourceFeatures(aggregations.map(each => camel(each) as PivotAggregation), canRefresh, countsItems, answersLevels) };
}

/** GETs a PivotJson document. */
async function get(path: string, signal: AbortSignal | undefined): Promise<string> {
    const response = await fetch(apiUrl(path), signal === undefined ? {} : { signal });
    if (!response.ok) throw new Error(`GET /${path} answered ${response.status}: ${await response.text()}`);
    return response.text();
}

/** POSTs a PivotJson document and reads the answer's. */
async function post(path: string, body: string, signal: AbortSignal | undefined): Promise<string> {
    const response = await fetch(apiUrl(path), { method: 'POST', headers: { 'Content-Type': 'application/json' }, body, ...(signal === undefined ? {} : { signal }) });
    if (!response.ok) throw new Error(`POST /${path} answered ${response.status}: ${await response.text()}`);
    return response.text();
}

/** What a page hears of the server source's questions, and how it hears the server's data move on. */
export interface ServerPivotOptions {
    /** Told of each question before it is sent: `aggregate`, `items` or `details`. What it throws
     * fails the question, as a server out of reach would. */
    readonly asked?: (kind: string) => void;
    /** The page's transport, telling the source that the server's data moved on (ADR-0370 section 3). */
    readonly subscribe?: PivotFetchers['subscribe'];
}

/** The server as a Pivot Source (ADR-0066, ADR-0370 section 3): it answers the Leaf Aggregates of each question
 * in SQL, and the pivot builds the report and its totals from them here. Details come a page at a time, each asked
 * under the question's Source Version. */
export function serverPivotSource(declared: ServerPivotDeclared, options: ServerPivotOptions = {}): FetchingPivotSource {
    const asked = (kind: string): void => { options.asked?.(kind); };
    return new FetchingPivotSource(declared.fields, declared.features, {
        aggregate: async (query, signal) => {
            asked('aggregate');
            return PivotJson.readAnswer(await post('api/pivot/aggregate', PivotJson.writeQuery(query), signal));
        },
        items: async (query, signal) => {
            asked('items');
            return PivotJson.readItemPage(await post('api/pivot/items', PivotJson.writeItemsQuery(query), signal));
        },
        details: async (query, signal) => {
            const records: PivotDetailRecord[] = [];
            for (;;) {
                const next: PivotDetailsQuery = Object.freeze({
                    ...query, start: query.start + records.length, count: Math.min(maxDetailsPage, query.count - records.length),
                });
                asked('details');
                const page = PivotJson.readDetailPage(await post('api/pivot/details', PivotJson.writeDetailsQuery(next), signal));
                if (page.isRefused) return page;
                // Pages of two versions are not one page: the records moved between them.
                if (page.sourceVersion !== query.sourceVersion) return PivotDetailPage.refused(PivotRefusals.sourceVersionNotHeld(query.sourceVersion));
                records.push(...page.records);
                if (page.records.length === 0 || records.length >= query.count || query.start + records.length >= page.total) {
                    return PivotDetailPage.of(page.sourceVersion, page.fields, query.start, page.total, records);
                }
            }
        },
        ...(declared.features.countsItems ? {
            itemCounts: async (signal?: AbortSignal) => PivotJson.readItemCounts(await get('api/pivot/item-counts', signal)),
        } : {}),
        ...(options.subscribe === undefined ? {} : { subscribe: options.subscribe }),
    });
}
