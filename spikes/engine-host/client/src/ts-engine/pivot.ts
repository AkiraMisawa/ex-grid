// The TypeScript engine's pivot (ADR-0370 section 6, the third arm): only what /core/pivot exercises,
// written to answer as ExPivot.Engine answers there. Its layout is fixed to CorePivot's: the sum of P&L
// by Book and Trade date down, across one text field (Currency or Product), compact, subtotals at the
// top, a grand total row and column, "#,##0" in en-US. Not a port: any other layout is refused.
//
// A report Window is written in PivotReportJson's shape (ExPivot.Engine), the fields of each row
// included, so that what crosses to the page is the size the C# engine sends. A Change Batch of one
// trade is applied to the sums it reaches, without reading the other trades again.

import type { Trades } from '../core-data';
import { Refused } from './sheet';

type Across = 'Currency' | 'Product';

/** The sums of one report row: one per column item and the grand total last; NaN where no trade fell. */
interface Sums {
    readonly values: Float64Array;
}

interface Aggregate {
    readonly across: Across;
    readonly items: readonly string[];
    readonly itemIndex: ReadonlyMap<string, number>;
    readonly leaves: Map<string, Sums>;
    readonly books: Map<string, Sums>;
    readonly grand: Sums;
    /** The report's rows, in order: a book's subtotal, its dates, and the grand total last. */
    readonly rows: readonly { readonly book: string | null; readonly date: string | null; readonly sums: Sums }[];
}

const collator = new Intl.Collator('en-US');

const newSums = (columns: number): Sums => ({ values: new Float64Array(columns).fill(Number.NaN) });

const add = (sums: Sums, column: number, amount: number): void => {
    const now = sums.values[column] ?? Number.NaN;
    sums.values[column] = Number.isNaN(now) ? amount : now + amount;
};

/** "#,##0" under en-US: rounded half away from zero, grouped by threes. */
export const wholeGrouped = (number: number): string => {
    const rounded = Math.round(Math.abs(number));
    const digits = String(rounded);
    let text = '';
    for (let i = 0; i < digits.length; i++) {
        if (i > 0 && (digits.length - i) % 3 === 0) text += ',';
        text += digits[i];
    }
    // ExPivot.Engine's "-" for a negative that rounds to 0 is not reached by the scenario: unverified.
    return number < 0 && rounded !== 0 ? `-${text}` : text;
};

const dateKey = (date: string): string => `${date}T00:00:00`;

/** A trade's field: the columns were checked to be as long as the ids, so a missing one is a defect. */
const at = <T>(column: readonly T[], index: number): T => {
    const value = column[index];
    if (value === undefined) throw new Refused(`Internal: no trade ${index}.`);
    return value;
};

export class Pivot {
    readonly #trades: Trades;
    #aggregate: Aggregate | undefined;
    #version = 0;

    constructor(trades: Trades) {
        const count = trades.id.length;
        for (const column of [trades.book, trades.product, trades.currency, trades.tradeDate, trades.pnl]) {
            if (column.length !== count) throw new Refused('The trades\' columns are not all as long as their ids.');
        }
        this.#trades = { ...trades, pnl: [...trades.pnl] };
    }

    get tradeCount(): number {
        return this.#trades.id.length;
    }

    /** Adds <delta> to trade <index>'s P&L, as a Change Batch of that trade, and moves the report's version. */
    amend(index: number, delta: number): void {
        const trades = this.#trades;
        if (!Number.isSafeInteger(index) || index < 0 || index >= trades.id.length) throw new Refused(`There is no trade ${index}.`);
        trades.pnl[index] = at(trades.pnl, index) + delta;
        this.#version++;
        const aggregate = this.#aggregate;
        if (aggregate === undefined) return;
        const item = at(aggregate.across === 'Currency' ? trades.currency : trades.product, index);
        const column = aggregate.itemIndex.get(item);
        const book = at(trades.book, index);
        const leaf = aggregate.leaves.get(`${book}\u0000${at(trades.tradeDate, index)}`);
        const bookSums = aggregate.books.get(book);
        if (column === undefined || leaf === undefined || bookSums === undefined) throw new Refused('Internal: an amended trade outside the report.');
        const total = aggregate.items.length;
        for (const sums of [leaf, bookSums, aggregate.grand]) {
            add(sums, column, delta);
            add(sums, total, delta);
        }
    }

    #aggregateFor(across: string): Aggregate {
        if (across !== 'Currency' && across !== 'Product') throw new Refused(`The spike lays out across Currency or Product, not ${across}.`);
        if (this.#aggregate?.across === across) return this.#aggregate;
        const trades = this.#trades;
        const acrossColumn = across === 'Currency' ? trades.currency : trades.product;
        const items = [...new Set(acrossColumn)].sort(collator.compare);
        const itemIndex = new Map(items.map((item, i) => [item, i]));
        const columns = items.length + 1;
        const leaves = new Map<string, Sums>();
        const books = new Map<string, Sums>();
        const grand = newSums(columns);
        for (let i = 0; i < trades.id.length; i++) {
            const book = at(trades.book, i);
            const leafKey = `${book}\u0000${at(trades.tradeDate, i)}`;
            const column = itemIndex.get(at(acrossColumn, i));
            if (column === undefined) throw new Refused('Internal: an item outside the columns.');
            const pnl = at(trades.pnl, i);
            let leaf = leaves.get(leafKey);
            if (leaf === undefined) leaves.set(leafKey, leaf = newSums(columns));
            let bookSums = books.get(book);
            if (bookSums === undefined) books.set(book, bookSums = newSums(columns));
            for (const sums of [leaf, bookSums, grand]) {
                add(sums, column, pnl);
                add(sums, items.length, pnl);
            }
        }
        const rows: { book: string | null; date: string | null; sums: Sums }[] = [];
        const datesOf = new Map<string, string[]>();
        for (const key of leaves.keys()) {
            const [book, date] = key.split('\u0000');
            if (book === undefined || date === undefined) throw new Refused('Internal: a leaf key without its book and date.');
            const dates = datesOf.get(book);
            if (dates === undefined) datesOf.set(book, [date]);
            else dates.push(date);
        }
        for (const book of [...books.keys()].sort(collator.compare)) {
            const bookSums = books.get(book);
            if (bookSums === undefined) throw new Refused('Internal: a book without its sums.');
            rows.push({ book, date: null, sums: bookSums });
            for (const date of (datesOf.get(book) ?? []).sort()) {
                const leaf = leaves.get(`${book}\u0000${date}`);
                if (leaf !== undefined) rows.push({ book, date, sums: leaf });
            }
        }
        rows.push({ book: null, date: null, sums: grand });
        this.#aggregate = { across, items, itemIndex, leaves, books, grand, rows };
        return this.#aggregate;
    }

    /** The report Window, rows <start> on, as PivotReportJson writes it. */
    window(across: string, start: number, count: number): string {
        const aggregate = this.#aggregateFor(across);
        const { items, rows } = aggregate;
        const end = Math.min(rows.length, start + count);
        const text = (item: string): string => JSON.stringify({ kind: 2, value: item });
        const valueColumns = [
            ...items.map((item) => ({
                name: `v:i/2:${item.toUpperCase()}`, header: item, role: 0, valueField: -1,
                columnPath: [{ field: across, item: { kind: 2, value: item } }],
            })),
            { name: 'v:g', header: 'Grand Total', role: 2, valueField: -1, columnPath: [] },
        ];
        const changedIn = JSON.stringify(valueColumns.map(() => null));
        const parts: string[] = [];
        for (let r = start; r < end; r++) {
            const row = rows[r];
            if (row === undefined) throw new Refused('Internal: a row outside the report.');
            let key: string;
            let labels: string;
            let path: string;
            let role: number;
            if (row.book === null) {
                role = 3;
                key = '{"role":3,"valueField":-1,"items":[]}';
                labels = '[{"text":"Grand Total","indent":0,"toggle":null}]';
                path = '[]';
            } else if (row.date === null) {
                role = 1;
                const book = text(row.book);
                key = `{"role":1,"valueField":-1,"items":[${book}]}`;
                labels = `[{"text":${JSON.stringify(row.book)},"indent":0,"toggle":{"field":"Book","item":${book},"itemLabel":${JSON.stringify(row.book)},"isCollapsed":false}}]`;
                path = `[{"field":"Book","item":${book}}]`;
            } else {
                role = 0;
                const book = text(row.book);
                const date = JSON.stringify({ kind: 1, value: dateKey(row.date) });
                key = `{"role":0,"valueField":-1,"items":[${book},${date}]}`;
                labels = `[{"text":${JSON.stringify(row.date)},"indent":1,"toggle":null}]`;
                path = `[{"field":"Book","item":${book}},{"field":"TradeDate","item":${date}}]`;
            }
            const values = Array.from(row.sums.values, (sum) =>
                Number.isNaN(sum) ? 'null' : `{"number":${sum},"exact":${sum},"error":null,"text":"${wholeGrouped(sum)}"}`).join(',');
            parts.push(`{"key":${key},"role":${role},"valueField":-1,"carriesValues":true,"labels":${labels},"values":[${values}],"rowPath":${path},"changedIn":${changedIn}}`);
        }
        const metadata = {
            version: { value: String(this.#version) },
            rowCount: rows.length,
            labelColumns: [{ name: 'row-labels', header: 'Row Labels', field: null }],
            valueColumns,
            valueCaptions: ['Sum of P&L'],
        };
        return `{"window":{"start":${start},"count":${count}},"metadata":${JSON.stringify(metadata)},"baseline":null,"rows":[${parts.join(',')}],"changes":[],"refusal":null}`;
    }
}
