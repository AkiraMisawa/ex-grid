// DemoCsv.cs under the Script Core: the /pivot-csv page's files — /pivot's trades, from the same seed, written as two
// applications would export them — the Schema one of them is declared with, and the Pivot Fields over the Snapshot
// it is read into. Everything is invented; the account numbers are derived from the book names.
import type { CsvSchema, SnapshotDecimal } from '@ex-grid/data';
import { datePartField, pivotField, type PivotField } from '@ex-grid/pivot-engine';
import type { Temporal } from 'temporal-polyfill';
import { demoPivotTrades } from './pivot-data';

// #region The code: schema
// The trade export this application knows, declared: nothing is guessed. Its header names the columns; one the
// Schema does not declare is skipped, and a declared one missing is refused.
export const tradeExport: CsvSchema = {
    columns: [
        { name: 'Id', kind: 'text' },
        { name: 'Account', kind: 'text' },                      // "004417" stays "004417"
        { name: 'Region', kind: 'text' },
        { name: 'Desk', kind: 'text' },
        { name: 'Book', kind: 'text' },
        { name: 'Product', kind: 'text' },
        { name: 'Currency', kind: 'text' },
        { name: 'TradeDate', kind: 'date', header: 'Trade date', caption: 'Trade date', dateFormats: ['yyyy-MM-dd'] },
        { name: 'Notional', kind: 'decimal', thousandsSeparator: ',' },   // "1,250,000.00"
        { name: 'Pnl', kind: 'decimal', header: 'P&L', caption: 'P&L', thousandsSeparator: ',' },
        { name: 'Quantity', kind: 'integer' },
        { name: 'Confirmed', kind: 'boolean' },                 // TRUE and FALSE, as Excel writes them
    ],
    blankText: ['NULL'],
    recordKey: 'Id',
};

// The Pivot Fields over the Snapshot's columns, by name. Month is a part of the trade date.
export const tradeFields: readonly PivotField[] = [
    pivotField({ name: 'Region', type: 'text' }),
    pivotField({ name: 'Desk', type: 'text' }),
    pivotField({ name: 'Book', type: 'text' }),
    pivotField({ name: 'Account', type: 'text' }),
    pivotField({ name: 'Product', type: 'text' }),
    pivotField({ name: 'Currency', type: 'text' }),
    pivotField({ name: 'TradeDate', type: 'date', caption: 'Trade date', format: 'yyyy-MM-dd' }),
    datePartField('Month', 'TradeDate', 'month'),
    pivotField({ name: 'Notional', type: 'number', format: '#,##0.00' }),
    pivotField({ name: 'Pnl', type: 'number', caption: 'P&L', format: '#,##0.00' }),
    pivotField({ name: 'Quantity', type: 'number' }),
    pivotField({ name: 'Confirmed', type: 'boolean' }),
];
// #endregion

/** The trade export's header row. */
const tradeExportHeader = 'Id,Account,Region,Desk,Book,Product,Currency,Trade date,Notional,P&L,Quantity,Confirmed';

/** The data record the malformed sample breaks, counted from one. */
export const malformedRow = 1234;

/** The trades written between two calls of the progress callback. */
const writeSlice = 5000;

/** A decimal as C#'s `#,##0.00` writes it: thousands between groups of three, `group`, and two places after `point`. */
function money(value: SnapshotDecimal, group: string, point: string): string {
    const negative = value.mantissa < 0n;
    const magnitude = negative ? -value.mantissa : value.mantissa;
    if (value.scale > 2) throw new RangeError('The demo trades hold money at two places at most.');
    const cents = (magnitude * 10n ** BigInt(2 - value.scale)).toString().padStart(3, '0');
    const whole = cents.slice(0, -2).replace(/\B(?=(\d{3})+$)/g, group);
    return `${negative ? '-' : ''}${whole}${point}${cents.slice(-2)}`;
}

function isoDate(date: Temporal.PlainDate): string {
    return date.toString();
}

function dayFirstDate(date: Temporal.PlainDate): string {
    const two = (n: number): string => String(n).padStart(2, '0');
    return `${two(date.day)}.${two(date.month)}.${String(date.year).padStart(4, '0')}`;
}

// An invented account number with leading zeros, the same for every trade of a book.
function accountOf(book: string): string {
    let sum = 0;
    for (let i = 0; i < book.length; i++) sum = ((sum * 31) + book.charCodeAt(i)) % 9000;
    return String(1000 + sum).padStart(6, '0');
}

// RFC 4180: a field holding the separator, a quote or a line break is quoted, its quotes doubled.
function quoted(field: string): string {
    return /[",\r\n]/.test(field) ? `"${field.replaceAll('"', '""')}"` : field;
}

/**
 * `trades` demo trades as the trade export writes them, in UTF-8 with CR LF line ends: a comma between fields, ISO
 * dates, money with a thousands separator and in quotes where it has one. With `malformed`, the notional of data
 * record {@link malformedRow} has a letter O for its last zero, as a hand-edited file might. After every five thousand
 * trades it awaits `written` with the count so far, so the page paints while a large sample is written.
 */
export async function tradeExportFile(trades: number, malformed: boolean, written: (count: number) => Promise<void>): Promise<Uint8Array<ArrayBuffer>> {
    const lines: string[] = [`${tradeExportHeader}\r\n`];
    let row = 0;
    for (const t of demoPivotTrades(trades)) {
        row++;
        let notional = money(t.notional, ',', '.');
        if (malformed && row === malformedRow) notional = `${notional.slice(0, -4)}O${notional.slice(-3)}`;
        lines.push(`${[
            t.id, accountOf(t.book), t.region, t.desk, t.book, t.product, t.currency, isoDate(t.tradeDate), notional,
            money(t.pnl, ',', '.'), String(t.quantity), t.confirmed ? 'TRUE' : 'FALSE',
        ].map(field => quoted(field)).join(',')}\r\n`);
        if (row % writeSlice === 0 && row < trades) await written(row);
    }
    return new TextEncoder().encode(lines.join(''));
}

/**
 * The demo trades as a spreadsheet set to German saves them, a file this application has no Schema for: a semicolon
 * between fields, dates day first, money with a decimal comma and a full stop between thousands.
 */
export function spreadsheetFile(trades: number): Uint8Array<ArrayBuffer> {
    const lines = ['Account;Desk;Book;Trade date;Notional;P&L\r\n'];
    for (const t of demoPivotTrades(trades)) {
        lines.push(`${[accountOf(t.book), t.desk, t.book, dayFirstDate(t.tradeDate), money(t.notional, '.', ','), money(t.pnl, '.', ',')].join(';')}\r\n`);
    }
    return new TextEncoder().encode(lines.join(''));
}
