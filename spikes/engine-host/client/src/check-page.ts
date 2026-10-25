// /check: the TypeScript engine's answers against the C# engine's (ADR-0370 section 6, the third arm).
// Both Workers run on the page, the C# one over serve.mjs's published _framework; the same calls go to
// each, and every Window answered is compared cell by cell: the whole Sheet in use after each edit, and
// every row of the report in both layouts after each Change Batch. The answer is window.checkResult;
// tools/cdp-check.mjs reads it.

import { EngineClient } from './engine-client';
import { documentJson, makeTrades } from './core-data';

interface Mismatch {
    readonly where: string;
    readonly csharp: unknown;
    readonly typescript: unknown;
}

const columns = 26;
/** Every row in use, and some beyond: the document's rows end at 2,001. */
const sheetRows = 2_010;

interface SheetAnswer {
    readonly version: number;
    readonly changedRows?: readonly (readonly [number, number])[];
    readonly start: number;
    readonly rows: readonly (readonly (string | null)[])[];
}

interface ReportAnswer {
    readonly update: {
        readonly metadata: { readonly rowCount: number; readonly valueColumns: readonly { readonly name: string; readonly header: string }[] };
        readonly rows: readonly {
            readonly key: unknown;
            readonly labels: readonly { readonly text: string; readonly indent: number }[];
            readonly values: readonly ({ readonly number: number; readonly text: string } | null)[];
        }[];
    };
}

const main = async (): Promise<void> => {
    const csharp = new EngineClient('/dist/engine-worker.js');
    const typescript = new EngineClient('/dist/ts-engine-worker.js');
    await Promise.all([csharp.ready, typescript.ready]);
    const mismatches: Mismatch[] = [];
    const counts = { cells: 0, changedRowLists: 0, reportRows: 0, reportValues: 0 };
    const sizes: Record<string, { csharp: number; typescript: number }> = {};
    const differ = (where: string, a: unknown, b: unknown): void => {
        if (JSON.stringify(a) !== JSON.stringify(b)) mismatches.push({ where, csharp: a, typescript: b });
    };
    const both = async <T>(method: Parameters<EngineClient['call']>[0], ...args: readonly unknown[]): Promise<[T, T, number, number]> => {
        const call = (client: EngineClient): Promise<{ value: T }> =>
            (client.call as (m: typeof method, ...a: readonly unknown[]) => Promise<{ value: T }>)(method, ...args);
        const [a, b] = await Promise.all([call(csharp), call(typescript)]);
        return [a.value, b.value, JSON.stringify(a.value).length, JSON.stringify(b.value).length];
    };

    // --- The Sheet -----------------------------------------------------------------------------------
    const compareSheet = (where: string, a: SheetAnswer, b: SheetAnswer): void => {
        differ(`${where}: version`, a.version, b.version);
        a.rows.forEach((row, r) => {
            row.forEach((text, c) => {
                counts.cells++;
                differ(`${where}: ${String.fromCharCode(65 + c)}${a.start + r + 1}`, text, b.rows[r]?.[c]);
            });
        });
    };
    await both('SheetOpen', documentJson());
    const [opened, openedTs] = await both<SheetAnswer>('SheetWindow', 0, sheetRows, columns);
    compareSheet('open', opened, openedTs);
    // A1 and L1 as the driver types them, then what the scenario does not type: decimals, a negative,
    // text where a number is read, a number General writes in scientific form, an error that spreads.
    const edits: [string, string][] = [
        ['A1', '1234'], ['L1', '77'], ['A1', '0.1'], ['A1', '-5.5'], ['A1', '1E+20'], ['A1', '123456789012'],
        ['A1', '0.000001'], ['A1', 'abc'], ['A1', '7'], ['C5', '=B5*2-1/4'], ['B7', '=1/0'], ['K3', '=A3^0.5'],
        ['D9', '=-C9^2'], ['A1', '1'],
    ];
    for (const [address, typed] of edits) {
        const [a, b, aSize, bSize] = await both<SheetAnswer>('SheetEnter', address, typed, 0, 140, columns);
        if (sizes['SheetEnter, a Window of 140 rows'] === undefined) sizes['SheetEnter, a Window of 140 rows'] = { csharp: aSize, typescript: bSize };
        counts.changedRowLists++;
        differ(`${address} := ${typed}: changed rows`, a.changedRows, b.changedRows);
        compareSheet(`${address} := ${typed}: its Window`, a, b);
        const [all, allTs] = await both<SheetAnswer>('SheetWindow', 0, sheetRows, columns);
        compareSheet(`${address} := ${typed}`, all, allTs);
    }

    // --- The pivot -----------------------------------------------------------------------------------
    const trades = makeTrades();
    await both('PivotOpen', JSON.stringify(trades));
    const compareReport = async (where: string): Promise<void> => {
        for (const across of ['Currency', 'Product']) {
            const [a, b, aSize, bSize] = await both<ReportAnswer>('PivotWindow', across, 0, 3_000, true);
            if (sizes['PivotWindow, the whole report'] === undefined) sizes['PivotWindow, the whole report'] = { csharp: aSize, typescript: bSize };
            const at = `${where}, across ${across}`;
            differ(`${at}: rows`, a.update.metadata.rowCount, b.update.metadata.rowCount);
            differ(`${at}: columns`, a.update.metadata.valueColumns.map((c) => [c.name, c.header]), b.update.metadata.valueColumns.map((c) => [c.name, c.header]));
            a.update.rows.forEach((row, r) => {
                const other = b.update.rows[r];
                counts.reportRows++;
                differ(`${at}: row ${r} key`, row.key, other?.key);
                differ(`${at}: row ${r} labels`, row.labels.map((l) => [l.text, l.indent]), other?.labels.map((l) => [l.text, l.indent]));
                row.values.forEach((value, c) => {
                    counts.reportValues++;
                    const theirs = other?.values[c];
                    differ(`${at}: row ${r} column ${c}`, value === null ? null : [value.number, value.text], theirs === undefined ? 'missing' : theirs === null ? null : [theirs.number, theirs.text]);
                });
            });
        }
    };
    await compareReport('opened');
    const [small, smallTs, smallSize, smallTsSize] = await both<ReportAnswer>('PivotWindow', 'Currency', 0, 140, true);
    sizes['PivotWindow, a Window of 140 rows'] = { csharp: smallSize, typescript: smallTsSize };
    differ('a Window of 140 rows: rows', small.update.rows.length, smallTs.update.rows.length);
    // CorePivot's amendment, then amounts with cents, a trade of each layout's last column, the last trade.
    const amendments: [number, number][] = [[trades.book.indexOf('BK-A'), 1_000_003], [7, 12.5], [19_999, -7.25], [123, -1_000_000]];
    for (const [index, delta] of amendments) {
        await both('PivotAmend', index, delta, 'Currency', 0, 140);
        await compareReport(`trade ${index} += ${delta}`);
    }

    (window as unknown as { checkResult: object }).checkResult = {
        compared: counts,
        mismatchCount: mismatches.length,
        mismatches: mismatches.slice(0, 40),
        sizes,
    };
};

main().catch((error: unknown) => {
    console.error(error);
    (window as unknown as { checkResult: object }).checkResult = { failed: String(error instanceof Error ? error.stack ?? error.message : error) };
});
