import { elementAt } from '../must';
import { bottomRow, rightColumn, type SelectionRange } from '../selection/range';
import type { CopyPlan } from './clipboard-rules';

// Assembles an approved copy into the two formats that go on the clipboard in one operation (ADR-0005):
// the displayed format as TSV for `text/plain`, and the raw, locale-free value as an HTML table for
// `text/html`. Excel prefers the HTML flavour, so it receives precision and type intact while a text
// editor receives what was on screen. Pure: the values are read through the providers, so the assembly
// works the same whether the rows came from the Window or were gathered from the Consumer (ADR-0005's two
// routes).

/**
 * The attribute on the table of ExGrid's HTML flavour saying that every field in it is raw and
 * locale-free, so a paste reads each one as invariant (ADR-0050, item 10). Another program ignores an
 * attribute it does not know.
 */
export const invariantMarker = 'data-ex-grid="invariant"';

/**
 * The attribute a cell of a table marked {@link invariantMarker} may carry to tell its own Consumer more
 * than the field's text says: `<td data-ex-mark="…">`. A paste hands its value over beside the field and
 * gives it no meaning of its own; another program ignores it and reads the field. ExSheet marks a cell
 * whose Formula it refused, so that a paste into a Sheet carries the refusal where another program reads
 * `#VALUE!`.
 */
export const markAttribute = 'data-ex-mark';

/** Where an assembly reads its values: each a Consumer's or an adapter's function, so each answer is
 * checked to be text. */
export interface CopyValues {
    /** The displayed text of (row, column): what the cell paints before the overflow decision, never
     * `####` (ADR-0016). */
    readonly displayAt: (row: number, column: number) => string;
    /** The raw value of (row, column), unformatted and locale-free. */
    readonly rawAt: (row: number, column: number) => string;
    /**
     * The declared header of a column; without it there is no header row (ADR-0005). It is the full
     * declared text, never the truncated paint, by the rule that keeps `####` off the clipboard, and it
     * goes into both flavours, because Excel reads the HTML one and that is the case headers exist for.
     */
    readonly headerAt?: (column: number) => string;
    /**
     * A header's raw form for the HTML flavour, as `rawAt` is a cell's: `undefined` for a column whose
     * header has none, which writes `headerAt`'s text into both flavours. A header that shows a number
     * rounded (a pivot's number column Item, `1.23457E+11`) carries the number in full here, so Excel
     * reads it whole (ADR-0005's note of 2026-10-13). Read only when `headerAt` is given.
     */
    readonly headerRawAt?: (column: number) => string | undefined;
}

/** What a copy asks a Consumer that answers copies itself (ADR-0050, item 9): the copy the grid's own rules approved,
 * whether the declared headers were asked for as a row above the block, and the order its positions are written in,
 * the source's (ADR-0011). */
export interface GridCopyRequest {
    /** The ranges being copied, as the grid's own rules approved them: empty, misaligned and past-the-cap
     * Selections are refused before a Consumer is asked (ADR-0005). */
    readonly plan: CopyPlan;
    /** Whether the copy was asked for with the declared headers as one row above the block (ADR-0036). */
    readonly withHeaders: boolean;
    /** The source's Row Sequence Version the positions are written in. */
    readonly rowSequenceVersion: number;
}

/** A Consumer's answer to one copy (ADR-0050, item 9): the two flavours to write, exactly as given, or a refusal in
 * the Consumer's own sentence, which the grid says and writes nothing. */
export type GridCopyAnswer =
    | { readonly kind: 'write'; readonly text: string; readonly html: string }
    | { readonly kind: 'refuse'; readonly sentence: string };

/** `answer` checked as an untyped Consumer may hand it: a write of two texts, or a refusal with something to say. */
export function checkedCopyAnswer(answer: unknown): GridCopyAnswer {
    if (typeof answer === 'object' && answer !== null) {
        const { kind, text, html, sentence }: { readonly kind?: unknown; readonly text?: unknown; readonly html?: unknown; readonly sentence?: unknown } = answer;
        if (kind === 'write' && typeof text === 'string' && typeof html === 'string') return Object.freeze({ kind, text, html });
        if (kind === 'refuse' && typeof sentence === 'string' && sentence.trim() !== '') return Object.freeze({ kind, sentence });
    }
    throw new TypeError("copyAnswer answers { kind: 'write', text, html } or { kind: 'refuse', sentence } with a sentence to say (ADR-0050, item 9).");
}

/** Both flavours of one approved copy (ADR-0005). */
export interface CopyFlavours {
    /** The TSV of the displayed values, for `text/plain`. */
    readonly text: string;
    /** A table of the raw values, for `text/html`. */
    readonly html: string;
}

/** Both flavours of one approved copy: `text` the TSV of the displayed values, `html` a table of the raw
 * ones (ADR-0005). */
export function assembleCopy(plan: CopyPlan, values: CopyValues): CopyFlavours {
    const { displayAt, rawAt, headerAt, headerRawAt } = values;
    const text: string[] = [];
    const html: string[] = [`<table ${invariantMarker}>`];
    const cell = (row: number, column: number, index: number) => {
        if (index > 0) text.push('\t');
        text.push(quoteForTsv(textOf(displayAt(row, column), 'displayAt', row, column)));
        html.push('<td>', escapeHtml(textOf(rawAt(row, column), 'rawAt', row, column)), '</td>');
    };

    if (headerAt !== undefined) {
        html.push('<tr>');
        headerColumns(plan).forEach((column, index) => {
            const header = textOf(headerAt(column), 'headerAt', undefined, column);
            const raw = headerRawAt?.(column);
            if (raw !== undefined) textOf(raw, 'headerRawAt', undefined, column);
            if (index > 0) text.push('\t');
            // The TSV takes the header as shown; the HTML its raw form where it has one, else the same text.
            text.push(quoteForTsv(header));
            html.push('<th>', escapeHtml(raw ?? header), '</th>');
        });
        text.push('\r\n');
        html.push('</tr>');
    }

    if (plan.orientation === 'vertical') {
        // Segments share a column span and stack in position order (ADR-0011).
        for (const segment of plan.segments) {
            for (let row = segment.topRow; row <= bottomRow(segment); row++) {
                html.push('<tr>');
                for (let column = segment.leftColumn; column <= rightColumn(segment); column++) {
                    cell(row, column, column - segment.leftColumn);
                }
                text.push('\r\n');
                html.push('</tr>');
            }
        }
    } else {
        // Segments share a row span and concatenate left to right; every segment has the same rows, so the
        // first names them for all.
        const first = elementAt(plan.segments, 0);
        for (let row = first.topRow; row <= bottomRow(first); row++) {
            html.push('<tr>');
            let index = 0;
            for (const segment of plan.segments) {
                for (let column = segment.leftColumn; column <= rightColumn(segment); column++) {
                    cell(row, column, index++);
                }
            }
            text.push('\r\n');
            html.push('</tr>');
        }
    }

    html.push('</table>');
    return Object.freeze({ text: text.join(''), html: html.join('') });
}

/** The columns of the header row, whichever orientation the plan has. Vertical segments share a column
 * span, so the first names them for all; horizontal ones concatenate, so the names follow the order the
 * cells are emitted in (ADR-0005). A disjoint selection has a well-defined header row either way. */
function headerColumns(plan: CopyPlan): number[] {
    const spans: readonly SelectionRange[] = plan.orientation === 'vertical' ? [elementAt(plan.segments, 0)] : plan.segments;
    return spans.flatMap(span => Array.from({ length: span.columnCount }, (_, i) => span.leftColumn + i));
}

/** A provider's answer, which must be text: a number or `undefined` from an untyped caller would copy as
 * whatever `String` made of it, a value nobody chose. */
function textOf(value: unknown, provider: string, row: number | undefined, column: number): string {
    if (typeof value !== 'string') {
        const at = row === undefined ? `column ${column}` : `(${row}, ${column})`;
        throw new TypeError(`${provider} answers text, and answered ${typeof value} for ${at}.`);
    }
    return value;
}

/** Excel's own TSV convention: a cell holding a tab, a line break or a quote is wrapped in quotes with
 * inner quotes doubled, and the receiving Excel reads it back as one cell. Copy never truncates, and it
 * does not mangle either. */
function quoteForTsv(value: string): string {
    return /[\t\n\r"]/.test(value) ? `"${value.replaceAll('"', '""')}"` : value;
}

/** The flavours of a text field's own copy, taken at its turn after a Pending Commit (ADR-0380): its selected text,
 * plain and as HTML text, as a browser's copy from a field carries it. */
export function fieldCopyFlavours(text: string): CopyFlavours {
    return { text, html: escapeHtml(text) };
}

function escapeHtml(value: string): string {
    return value.replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;');
}
