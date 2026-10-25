// /core/pivot over the Engine Host (ADR-0370 section 6): render-bench's pivot (Bench.Live/CorePivot.razor)
// computed by ExPivot.Engine in the Worker, its report Window drawn by ExGrid's Script Core on the page.
// ExPivot's own Script Core does not exist yet, so this page stands in for it as far as core-bench.js
// needs: the report's label column and value columns, and CorePivot's two controls.
//
//   #core-amend   one trade of the first book on its first date gains 1,000,003 in a Change Batch
//   #core-layout  swaps the columns between Currency and Product
//   #core-state's data-expected is the first book's new P&L, as the report paints it ("#,##0"), and
//   data-columns names the field across the columns.
//
// The trades are CorePivot's, drawn from the same seed by .NET's Random (DotNetRandom), so the figures
// are the ones the Blazor page paints. They cross to the Worker once, as columns of JSON.

import { createGrid, type GridColumn, type GridHandle } from '../../../../src/npm/core/src/index';
import { DotNetRandom } from '../../../../samples/ExGrid.DemoHost.React/src/dotnet-random';
import { EngineClient } from './engine-client';
import { EngineWindowSource, type WindowAnswer } from './engine-window-source';
import { recordBoot, waitForPaint } from './boot';

const tradeCount = 20_000;
const books = ['BK-A', 'BK-B', 'BK-C', 'BK-D', 'BK-E', 'BK-F', 'BK-G', 'BK-H', 'BK-I', 'BK-J'];
const currencies = ['USD', 'EUR', 'JPY', 'GBP', 'CHF'];
const products = ['Swap', 'Bond', 'Option', 'Future', 'Spot'];
const rowHeightPx = 22;

interface Trades {
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
const makeTrades = (): Trades => {
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

interface ReportRow {
    readonly key: string;
    readonly texts: readonly (string | null)[];
}

interface ReportJson {
    readonly engineMs: number;
    readonly jsonMs: number;
    readonly update: {
        readonly window: { readonly start: number };
        readonly refusal: { readonly message: string } | null;
        readonly metadata: {
            readonly rowCount: number;
            readonly labelColumns: readonly { readonly header: string }[];
            readonly valueColumns: readonly { readonly name: string; readonly header: string }[];
        };
        readonly rows: readonly {
            readonly key: unknown;
            readonly labels: readonly { readonly text: string }[];
            readonly values: readonly ({ readonly text: string } | null)[];
        }[];
    };
}

const sameRow = (painted: ReportRow, next: ReportRow): boolean =>
    painted.key === next.key && painted.texts.every((text, c) => text === next.texts[c]);

const main = async (): Promise<void> => {
    const trades = makeTrades();
    const first = trades.tradeDate.filter((_, i) => trades.book[i] === books[0]).sort()[0];
    const amendedAt = trades.id.findIndex((_, i) => trades.book[i] === books[0] && trades.tradeDate[i] === first);
    let firstBookPnl = trades.pnl.reduce((sum, pnl, i) => (trades.book[i] === books[0] ? sum + pnl : sum), 0);
    const format = new Intl.NumberFormat('en-US', { maximumFractionDigits: 0 });

    const engine = new EngineClient('/dist/engine-worker.js');
    const boot = await engine.ready;
    const opened = await engine.call<{ engineMs: number }>('PivotOpen', JSON.stringify(trades));
    const openedAt = performance.now();

    let across = 'Currency';
    let columnsFor = '';
    let columns: readonly GridColumn<ReportRow>[] = [];
    const answerOf = (json: ReportJson): WindowAnswer<ReportRow> => {
        const update = json.update;
        if (update.refusal !== null) throw new Error(`The report was refused: ${update.refusal.message}`);
        const names = update.metadata.valueColumns.map((c) => c.name).join('|');
        if (names !== columnsFor) {
            columnsFor = names;
            const headers = [...update.metadata.labelColumns.map((c) => c.header), ...update.metadata.valueColumns.map((c) => c.header)];
            columns = headers.map((header, c) => ({
                name: `c${c}`, header, type: 'text', width: c === 0 ? 160 : 110, align: c === 0 ? 'left' : 'right',
                value: (row: ReportRow) => row.texts[c] ?? null,
            }));
        }
        return {
            rows: update.rows.map((row) => ({
                key: JSON.stringify(row.key),
                texts: [...row.labels.map((l) => l.text), ...row.values.map((v) => (v === null ? null : v.text))],
            })),
            start: update.window.start,
            totalCount: update.metadata.rowCount,
        };
    };
    const firstWindow = answerOf((await engine.call<ReportJson>('PivotWindow', across, 0, 80, true)).value);
    const firstWindowAt = performance.now();

    const source = new EngineWindowSource<ReportRow>(
        async (range) => answerOf((await engine.call<ReportJson>('PivotWindow', across, range.start, range.count, true)).value),
        sameRow,
        firstWindow);

    const box = document.getElementById('core-bench');
    const element = box?.querySelector<HTMLElement>('#pivot');
    const state = document.getElementById('core-state');
    if (!box || !element || !state) throw new Error('The page has no #core-bench with a #pivot and a #core-state.');
    let shownColumns = columns;
    const grid: GridHandle<ReportRow> = createGrid(element, { columns, source, rowHeight: rowHeightPx, viewportWidth: 1200, viewportHeight: 600 });
    const redraw = (): void => {
        if (shownColumns === columns) return;
        shownColumns = columns;
        grid.update({ columns, source, rowHeight: rowHeightPx, viewportWidth: 1200, viewportHeight: 600 });
    };
    source.subscribe(redraw);

    const operations: object[] = [];
    (window as unknown as { engineOperations: () => object[] }).engineOperations = () => operations.splice(0);
    let amendments = 0;
    const show = (): void => {
        state.dataset['expected'] = format.format(firstBookPnl);
        state.dataset['columns'] = across;
        state.textContent = `${amendments} amendments`;
    };
    show();

    document.getElementById('core-amend')?.addEventListener('click', () => {
        firstBookPnl += 1_000_003;
        amendments += 1;
        show();
        void source.write(async (held) => {
            const answer = await engine.call<ReportJson>('PivotAmend', amendedAt, 1_000_003, across, held.start, held.count);
            operations.push({ op: 'amend', engineMs: answer.value.engineMs, jsonMs: answer.value.jsonMs, workerMs: answer.workerMs, roundTripMs: answer.roundTripMs });
            return answerOf(answer.value);
        });
    });
    document.getElementById('core-layout')?.addEventListener('click', () => {
        across = across === 'Currency' ? 'Product' : 'Currency';
        const asked = across;
        show();
        void source.write(async (held) => {
            const answer = await engine.call<ReportJson>('PivotWindow', asked, held.start, held.count, false);
            operations.push({ op: 'layout', engineMs: answer.value.engineMs, jsonMs: answer.value.jsonMs, workerMs: answer.workerMs, roundTripMs: answer.roundTripMs });
            return answerOf(answer.value);
        });
    });

    const firstPaintAt = await waitForPaint(element);
    recordBoot({ boot, openedAt, firstWindowAt, firstPaintAt, openEngineMs: opened.value.engineMs });
};

main().catch((error: unknown) => {
    console.error(error);
});
