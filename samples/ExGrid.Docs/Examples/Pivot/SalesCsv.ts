import type { SnapshotDecimal } from '@ex-grid/data';
import { sampleSales } from './SalesData';

/** The data record a malformed file breaks, counted from one. */
export const malformedRow = 1_234;

// An amount of cents as the export writes it: a thousands separator and two places, 1,234.50.
function money(amount: SnapshotDecimal): string {
    const cents = amount.mantissa.toString().padStart(3, '0');
    const whole = cents.slice(0, -2).replace(/\B(?=(\d{3})+(?!\d))/g, ',');
    return `${whole}.${cents.slice(-2)}`;
}

const quoted = (text: string): string => `"${text.replaceAll('"', '""')}"`;

/**
 * `count` sales as an order system's export writes them, in UTF-8 with CR LF line ends: a comma between fields, ISO
 * dates, money with a thousands separator, quoted. With `malformed`, the revenue of record `malformedRow` has a
 * letter O for its first digit, as a hand-edited file might. A file made in memory, as a user would otherwise choose
 * one with an `<input type="file">`.
 */
export function writeSalesCsv(count: number, malformed = false): Uint8Array {
    const lines = ['Order,Region,Country,Category,Product,Channel,Order date,Units,Revenue,Cost'];
    sampleSales(count).forEach((s, i) => {
        let revenue = money(s.revenue);
        if (malformed && i + 1 === malformedRow) revenue = `O${revenue.slice(1)}`;
        lines.push([s.id, s.region, s.country, s.category, quoted(s.product), s.channel, s.date.toString(), s.units,
            quoted(revenue), quoted(money(s.cost))].join(','));
    });
    return new TextEncoder().encode(`${lines.join('\r\n')}\r\n`);
}
