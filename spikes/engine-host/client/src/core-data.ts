// render-bench's data, as its Blazor pages make it, for the pages and for the TypeScript engine's probe:
// CoreSheet's Sheet as a Sheet Document's JSON, and CorePivot's trades from the same seed.

import { DotNetRandom } from '../../../../samples/ExGrid.DemoHost.React/src/dotnet-random';

/** The rows that read A1, as CoreSheet. */
export const sheetRows = 2_000;

/** CoreSheet's document in the Sheet Document's JSON form (version 9): A1 a number, A2:A2001 read it, B to
 * J each add one to the cell on their left, L1 read by nothing. */
export const documentJson = (): string => {
    const cells: object[] = [{ at: 'A1', number: 1 }, { at: 'L1', number: 0 }];
    for (let row = 2; row <= sheetRows + 1; row++) {
        cells.push({ at: `A${row}`, formula: '=$A$1+ROW()' });
        for (let c = 1; c < 10; c++) cells.push({ at: `${String.fromCharCode(65 + c)}${row}`, formula: `=${String.fromCharCode(64 + c)}${row}+1` });
    }
    return JSON.stringify({ version: 9, culture: 'en-US', name: 'Sheet1', cells });
};

export const tradeCount = 20_000;
export const books = ['BK-A', 'BK-B', 'BK-C', 'BK-D', 'BK-E', 'BK-F', 'BK-G', 'BK-H', 'BK-I', 'BK-J'];
const currencies = ['USD', 'EUR', 'JPY', 'GBP', 'CHF'];
const products = ['Swap', 'Bond', 'Option', 'Future', 'Spot'];

/** The trades, as columns: the form they cross to the Worker in. */
export interface Trades {
    readonly id: string[];
    readonly book: string[];
    readonly product: string[];
    readonly currency: string[];
    readonly tradeDate: string[];
    readonly pnl: number[];
}

const pick = (random: DotNetRandom, from: readonly string[]): string => {
    const item = from[random.next(from.length)];
    if (item === undefined) throw new Error('Internal: a pick outside its list.');
    return item;
};

/** CorePivot's trades, in its order and from its seed. */
export const makeTrades = (): Trades => {
    const random = new DotNetRandom(20261025);
    const trades: Trades = { id: [], book: [], product: [], currency: [], tradeDate: [], pnl: [] };
    const start = Date.UTC(2026, 0, 2);
    for (let i = 0; i < tradeCount; i++) {
        trades.id.push(`T${i}`);
        trades.book.push(pick(random, books));
        trades.product.push(pick(random, products));
        trades.currency.push(pick(random, currencies));
        trades.tradeDate.push(new Date(start + random.next(0, 270) * 86_400_000).toISOString().slice(0, 10));
        trades.pnl.push(random.next(-50_000, 60_000));
    }
    return trades;
};
