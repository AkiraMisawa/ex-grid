import { useCallback, useMemo, useRef, useState, type ReactElement } from 'react';
import { GridSource, type GridColumn, type GridHeaderGroup, type GridPasteIntent, type GridRefusal } from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import type { Temporal } from 'temporal-polyfill';
import { DemoNav } from '../demo-nav';
import { firstDay, pick, type DemoTrade } from '../demo-data';
import { useQuery } from '../router';

const books = ['Alpha', 'Beta', 'Gamma', 'Delta'] as const;
// The last name is Arabic on purpose: RTL content renders correctly inside the LTR island (ADR-0031).
const traders = ['Ishikawa', 'Novak', 'Osei', 'عمر'] as const;

/** The Features page's 400 trades, as Features.razor makes them; the Notional is the decimal's nearest number. */
const rows: readonly DemoTrade[] = Object.freeze(Array.from({ length: 400 }, (_, i) => Object.freeze({
    id: i,
    book: pick(books, i % books.length),
    trader: pick(traders, Math.floor(i / 3) % traders.length),
    notional: (100_000_000 + (i * 1_234_567)) / 100,
    tradeDate: firstDay.add({ days: i % 200 }),
    confirmed: i % 3 !== 0,
})));

const isoDate = (value: unknown): string => (value as Temporal.PlainDateTime).toPlainDate().toString();
// The Blazor page's Notional is a decimal of two places, which C# writes with both; a number keeps no
// places, so they are written here.
const twoPlaces = (value: unknown): string => (value as number).toFixed(2);

const columns: readonly GridColumn<DemoTrade>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (t: DemoTrade) => t.book, width: 110 },
    { name: 'Trader', type: 'text', value: (t: DemoTrade) => t.trader, width: 120, editable: true },
    { name: 'Notional', type: 'number', value: (t: DemoTrade) => t.notional, width: 140, format: twoPlaces, editable: true },
    // Deliberately too narrow for its values: what #### looks like, and what the clipboard must never
    // receive (ADR-0016, CP-5). The label is the full word, so the header's own ellipsis is the real thing.
    { name: 'Narrow', type: 'number', value: (t: DemoTrade) => t.notional, width: 48, format: twoPlaces },
    // ISO, and the widths sum under the Viewport minus its scrollbar gutter: a horizontal bar here would sit
    // where the browser suite drags into the bottom edge band (SL-14/15).
    { name: 'TradeDate', type: 'date', value: (t: DemoTrade) => t.tradeDate, width: 120, format: isoDate },
    { name: 'Confirmed', type: 'boolean', value: (t: DemoTrade) => t.confirmed, width: 120 },
]);

// A number as C#'s decimal.TryParse reads one under the hosts' culture (NumberStyles.Number): white space
// around it, a sign before or after, group separators anywhere in the integer part, and a decimal point.
function parsedNumber(text: string): number | undefined {
    const match = /^\s*([+-]?)(\d[\d,]*)?(\.\d*)?([+-]?)\s*$/.exec(text);
    if (match === null || (match[1] !== '' && match[4] !== '') || !/\d/.test(text)) return undefined;
    const sign = match[1] === '-' || match[4] === '-' ? -1 : 1;
    return sign * Number(`${(match[2] ?? '0').replaceAll(',', '')}${match[3] ?? ''}`);
}

/** A new instance with one field changed, identity being the change signal (ADR-0003); none where the value
 * does not parse or the column is not one this Consumer writes. */
function edited(row: DemoTrade, column: string, value: string): DemoTrade | undefined {
    if (column === 'Trader') return Object.freeze({ ...row, trader: value });
    if (column !== 'Notional') return undefined;
    const notional = parsedNumber(value);
    return notional === undefined ? undefined : Object.freeze({ ...row, notional });
}

// The origin written as the Blazor page writes C#'s enum member, so one spec reads both.
const pascal = (name: string): string => name.charAt(0).toUpperCase() + name.slice(1);
const shownFields = 20;
const shownFieldLength = 40;
const shown = (value: string): string =>
    (value.length <= shownFieldLength ? value : `${value.slice(0, shownFieldLength)}… (${value.length} characters)`);

const groups: readonly GridHeaderGroup[] = Object.freeze([
    { label: 'Who', columns: ['Book', 'Trader'] },
    { label: 'What', columns: ['Notional', 'Narrow', 'TradeDate', 'Confirmed'] },
]);

const secondColumns: readonly GridColumn<DemoTrade>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (t: DemoTrade) => t.book },
    { name: 'Trader', type: 'text', value: (t: DemoTrade) => t.trader },
]);

/**
 * The page /features of the Blazor hosts: an interaction surface on one page. The Blazor page writes every
 * notification to status lines below the heading; a line comes here with the notification it shows, so a
 * test that reads one the Script Core does not raise yet (an edit, a paste, a filter, a reorder, a menu)
 * fails by name rather than reading a value nothing could change.
 */
export function Features(): ReactElement {
    const showGroups = useQuery('groups') === '1';
    const source = useMemo(() => GridSource.from(rows), []);
    const second = useMemo(() => GridSource.from(rows.slice(0, 20)), []);
    const [copyStatus, setCopyStatus] = useState('—');
    const [pasteStatus, setPasteStatus] = useState('—');
    const [pasteFields, setPasteFields] = useState('—');
    const [pasteRefusedStatus, setPasteRefusedStatus] = useState('—');
    // How often each reason has been heard, so a refusal repeated shows that it was, as the Blazor page's do.
    const repeats = useRef(new Map<string, number>());
    const onRefused = useCallback((refusal: GridRefusal) => {
        const key = `${refusal.kind}:${refusal.reason}`;
        const before = repeats.current.get(key);
        const seen = before === undefined ? 1 : before + 1;
        repeats.current.set(key, seen);
        // The reason written as the Blazor page writes C#'s enum member, so one spec reads both.
        const text = pascal(refusal.reason);
        (refusal.kind === 'copy' ? setCopyStatus : setPasteRefusedStatus)(seen > 1 ? `${text} (×${seen})` : text);
    }, []);
    const onPaste = useCallback((intent: GridPasteIntent) => {
        const status = `${intent.cellCount} cells from ${intent.plan.source.rows}x${intent.plan.source.columns}`;
        // Shown in part: the page is read by people and by tests, and a large paste written whole into one
        // text node costs the browser seconds of text shaping.
        const fields = intent.values.flatMap((row, r) => row.map((value, c) => `${shown(value)}:${pascal(pick(pick(intent.origins, r), c))}`));
        setPasteFields(fields.slice(0, shownFields).join(' | ') + (fields.length > shownFields ? ` | … ${fields.length - shownFields} more` : ''));
        // Positions are the current order's, so every cell is resolved against the Window first: applying as
        // it goes could reorder the rows under a Sort on the edited column (ADR-0011, ADR-0014).
        const { window, windowStart } = source.snapshot();
        const resolved: { readonly row: DemoTrade; readonly column: string; readonly value: string }[] = [];
        for (const target of intent.plan.targets) {
            for (let row = target.topRow; row < target.topRow + target.rowCount; row++) {
                const held = window[row - windowStart];
                if (held === undefined) continue;
                for (let column = target.leftColumn; column < target.leftColumn + target.columnCount && column < columns.length; column++) {
                    resolved.push({ row: held, column: pick(columns, column).name, value: intent.valueFor({ row, column }) });
                }
            }
        }
        // Each change is built on the instance the one before it left, followed through the latest replacement.
        const latest = new Map<DemoTrade, DemoTrade>();
        let applied = 0;
        for (const { row, column, value } of resolved) {
            // A row not yet replaced is its own latest version.
            const current = latest.get(row) ?? row;
            const replacement = edited(current, column, value);
            if (replacement === undefined) continue;
            source.replaceRow(current, replacement);
            latest.set(row, replacement);
            applied += 1;
        }
        setPasteStatus(`${status}, ${applied} applied`);
    }, [source]);
    return (
        <>
            <DemoNav route="features" />
            <h1>Features — editing, clipboard, sorting, filtering, Header Groups</h1>
            <p>
                The interaction surface on one page, for the browser layer to drive: a source-bound grid with tiered
                headers. Sorting, the Selection, copying and pasting work here now; editing and filtering, and the status
                lines that report them, join as the Script Core comes to them.
            </p>
            <p className="demo-status">
                <span id="copy-status">Copy refused: {copyStatus}</span>
                <span id="paste-status">Pasted: {pasteStatus}</span>
                {/* What the last paste's block held, field by field, with where each field came from (ADR-0050, item 10). */}
                <span id="paste-fields">Fields: {pasteFields}</span>
                <span id="paste-refused-status">Paste refused: {pasteRefusedStatus}</span>
            </p>
            <ExGrid
                columns={columns}
                source={source}
                rowHeight={24}
                headerHeight={28}
                viewportHeight={320}
                viewportWidth={720}
                headerGroups={groups}
                onRefused={onRefused}
                onPaste={onPaste} />
            {showGroups
                ? (
                    <p className="demo-status">
                        <span id="groups-status">
                            Groups: {groups.map(g => `${g.label} [${g.columns.join(',')}] tier ${g.tier ?? 1} span ${g.tierSpan ?? 1}`).join('; ')}
                        </span>
                    </p>
                )
                : null}
            <h2>A second, independent instance</h2>
            <p>Two grids on one page hold independent DOM, tokens and handles (ADR-0018).</p>
            <ExGrid columns={secondColumns} source={second} rowHeight={20} viewportHeight={140} viewportWidth={480} />
        </>
    );
}
