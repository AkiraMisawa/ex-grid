import { useCallback, useMemo, useRef, useState, useSyncExternalStore, type ReactElement } from 'react';
import {
    actionColumn, declinedSummary, type ActionRefusalReason, GridRuleset, GridSource, summaryOf, type CommitRefusalReason, type EditDiscardReason, type GridClearIntent, type GridColumn, type GridColumnWidthChange,
    type GridActionEvent, type GridCommand, type GridContextMenuContext, type GridDeclaredKeyPress, type GridEditIntent, type GridHeaderGroup, type GridOverwriteNotice, type GridPasteIntent,
    type EditVerdict, type GridRefusal, type GridReplaceRequest, type GridReplaceResult, type GridSummaryRequest, type GridSummaryResult,
} from '@ex-grid/core';
import { ExGrid } from '@ex-grid/react';
import { Temporal } from 'temporal-polyfill';
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

// The Blazor page's rule on Notional, held by the bundled ruleset (ADR-0034): what does not parse is rejected and the
// editor holds, and a negative value is applied and flagged. Which is which is this side's convention.
function notionalRule(_row: DemoTrade, text: string): EditVerdict {
    const value = parsedNumber(text);
    if (value === undefined) return { kind: 'reject', message: `'${text}' is not a number` };
    return value < 0 ? { kind: 'flag', message: 'a negative notional needs approval' } : { kind: 'accept' };
}

// The page's ruleset: the Notional column's editor asks its rule, and the rows are judged by the same one after
// every change. One page shows it at a time, and each judgement replaces the last whole.
const rules = new GridRuleset<DemoTrade>().column('Notional', notionalRule);
const notionalValidate = rules.validatorFor('Notional');
if (notionalValidate === undefined) throw new Error('The ruleset holds the rule on Notional.');

const columns: readonly GridColumn<DemoTrade>[] = Object.freeze([
    // Book and Trader offer their values and a condition, as on the Blazor page (ADR-0009).
    { name: 'Book', type: 'text', value: (t: DemoTrade) => t.book, width: 110, filterUi: 'both' },
    { name: 'Trader', type: 'text', value: (t: DemoTrade) => t.trader, width: 120, editable: true, filterUi: 'both' },
    // The verdict seam, wired to the ruleset's rule (ADR-0034).
    { name: 'Notional', type: 'number', value: (t: DemoTrade) => t.notional, width: 140, format: twoPlaces, editable: true, validate: notionalValidate },
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

// The Blazor page's sentences for an edit the grid could not land (ADR-0011, ADR-0142), the reason first by
// the name C# gives its enum member.
function commitRefused(reason: CommitRefusalReason, column: string): string {
    switch (reason) {
        case 'orderMoved':
            return `OrderMoved — ${column} was not written: the rows moved while you were typing; press Escape to leave it`;
        case 'rowLeftTheWindow':
            return `RowLeftTheWindow — ${column} was not written: the row is not in the rows the grid holds now; bring it back into view and commit again, or press Escape`;
    }
}

// The Blazor page's sentences for an action the grid did not do (ADR-0142).
function actionRefused(reason: ActionRefusalReason, action: string): string {
    switch (reason) {
        case 'rowLeftTheWindow':
            return `RowLeftTheWindow — ${action} was not done: the row is not in the rows the grid holds now; bring it back into view and press it again`;
        case 'orderMoved':
            return `OrderMoved — ${action} was not done: the rows moved before the press landed; press it on the row again`;
        case 'sourceChanged':
            return `SourceChanged — ${action} was not done: the data the grid shows was replaced before the press landed`;
    }
}

function editDiscarded(reason: EditDiscardReason): string {
    switch (reason) {
        case 'rowLeftTheWindow':
            return 'RowLeftTheWindow — that row is no longer loaded, so the entry could not be placed';
        case 'sourceChanged':
            return 'SourceChanged — the data the grid shows was replaced while you were typing; the entry was not written';
        case 'orderMoved':
            return 'OrderMoved — the rows moved before your typing reached the grid; nothing was written';
        case 'columnsChanged':
        case 'columnNoLongerEditable':
            return pascal(reason);
    }
}

/** The row at a position of the source's current order. This page's source holds every row in memory, so a
 * position it does not hold is a defect, said rather than skipped. */
function heldRows(source: GridSource<DemoTrade>): (row: number) => DemoTrade {
    const { window, windowStart } = source.snapshot();
    return row => {
        const held = window[row - windowStart];
        if (held === undefined) throw new RangeError(`Row ${row} is not held, though this page's source holds every row.`);
        return held;
    };
}

/** The columns in the order the grid reported, each the one held by that name: the grid names only columns it
 * was handed, so a name not held is a defect, said rather than skipped. */
function inOrder(held: readonly GridColumn<DemoTrade>[], order: readonly string[]): readonly GridColumn<DemoTrade>[] {
    return order.map(name => {
        const column = held.find(c => c.name === name);
        if (column === undefined) throw new RangeError(`The grid reported a column this page does not hold: ${name}.`);
        return column;
    });
}

/** One history step: the replacements one intent made, each as the row before and the row after. */
type Step = [DemoTrade, DemoTrade][];

const groups: readonly GridHeaderGroup[] = Object.freeze([
    { label: 'Who', columns: ['Book', 'Trader'] },
    { label: 'What', columns: ['Notional', 'Narrow', 'TradeDate', 'Confirmed'] },
]);

// ?upstream=1 lets the browser layer change values under a gesture, as a live feed would (ADR-0142, LV-11 to
// LV-14): F9, declared to the grid, moves the Notional of the first five rows up by one, with an edit open too,
// where the grid raises a declared key and changes nothing in the editor (ADR-0050, item 14). An Action Column
// joins, so a press can be raced against it too. ?rowkey=1 beside it binds the source by the trade's number
// (ADR-0140) and declares F8: the top row's Notional jumps past every other, as a live amendment can.
// An action that would write, approving a trade at the notional shown (ADR-0142, LV-12).
const approveColumn = actionColumn<DemoTrade>('Act', [{ name: 'approve', label: 'Approve' }], { width: 90 });
const upstreamColumns: readonly GridColumn<DemoTrade>[] = Object.freeze([...columns.slice(0, 3), approveColumn, ...columns.slice(3)]);
// The Act column stands between Notional and Narrow, so it joins the group: a Header Group's members are
// adjacent, or the grid refuses it (ADR-0032).
const upstreamGroups: readonly GridHeaderGroup[] = Object.freeze([
    { label: 'Who', columns: ['Book', 'Trader'] },
    { label: 'What', columns: ['Notional', 'Act', 'Narrow', 'TradeDate', 'Confirmed'] },
]);
const upstreamKeys: readonly string[] = Object.freeze(['F9']);
const upstreamAndResortKeys: readonly string[] = Object.freeze(['F9', 'F8']);
const tradeNumber = (t: DemoTrade): number => t.id;

const secondColumns: readonly GridColumn<DemoTrade>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (t: DemoTrade) => t.book },
    { name: 'Trader', type: 'text', value: (t: DemoTrade) => t.trader },
]);

/** The most cells the held summary sums: more are declined at once, with the reason shown in place of the figures. */
const heldMostCells = 4;
const summaryRows: readonly DemoTrade[] = Object.freeze(rows.slice(0, 20));
// Notional with no format: the figures are the numbers' own text, as the Blazor page's decimal column shows them.
const heldColumns: readonly GridColumn<DemoTrade>[] = Object.freeze([
    { name: 'Book', type: 'text', value: (t: DemoTrade) => t.book },
    { name: 'Notional', type: 'number', value: (t: DemoTrade) => t.notional },
]);
const heldValueOf = (name: string): ((row: DemoTrade) => unknown) | undefined => heldColumns.find(c => c.name === name)?.value;

/** How many distinct cells a request names, each counted once, as C#'s GridSummary.Cells counts them. */
function cellsNamed(request: GridSummaryRequest): number {
    const cells = new Set<number>();
    for (const range of request.ranges) {
        for (let row = range.topRow; row < Math.min(range.topRow + range.rowCount, summaryRows.length); row++) {
            for (let column = range.leftColumn; column < Math.min(range.leftColumn + range.columnCount, request.columns.length); column++) {
                cells.add((row * request.columns.length) + column);
            }
        }
    }
    return cells.size;
}

/** A question the page holds until a button answers it. */
interface HeldQuestion {
    readonly request: GridSummaryRequest;
    readonly answer: (result: GridSummaryResult) => void;
    done: boolean;
}

/**
 * The Blazor page's `?summary=held` grid: its Selection Summary is answered by this page through `onSummarize`
 * (ADR-0130), each question held until the Answer button is pressed, so the pending words stand meanwhile, and a
 * selection of more than four cells declined at once with a reason. With `?late=1` the page ignores the signal, as
 * a careless Consumer may, and can answer the question a new selection replaced after the grid let it go.
 */
function HeldSummary({ late }: { readonly late: boolean }): ReactElement {
    const [counts, setCounts] = useState({ asked: 0, cancelled: 0, answered: 0, late: 0 });
    const waiting = useRef<HeldQuestion | undefined>(undefined);
    const replaced = useRef<HeldQuestion | undefined>(undefined);
    const onSummarize = useCallback(async (request: GridSummaryRequest, signal: AbortSignal): Promise<GridSummaryResult> => {
        setCounts(was => ({ ...was, asked: was.asked + 1 }));
        if (cellsNamed(request) > heldMostCells) return declinedSummary(`Not summed: this page sums ${heldMostCells} cells at most.`);
        return new Promise((resolve, reject) => {
            const question: HeldQuestion = { request, answer: resolve, done: false };
            signal.addEventListener('abort', () => {
                if (question.done) return;
                setCounts(was => ({ ...was, cancelled: was.cancelled + 1 }));
                if (late) return;
                question.done = true;
                reject(signal.reason);
            }, { once: true });
            if (late && waiting.current !== undefined && !waiting.current.done) replaced.current = waiting.current;
            waiting.current = question;
        });
    }, [late]);
    // Answers a held question with its own figures; one already answered or cancelled can no longer be.
    const answer = useCallback((held: { current: HeldQuestion | undefined }, counted: 'answered' | 'late') => {
        const question = held.current;
        held.current = undefined;
        if (question === undefined || question.done) return;
        question.done = true;
        question.answer(summaryOf(summaryRows, question.request, heldValueOf));
        setCounts(was => ({ ...was, [counted]: was[counted] + 1 }));
    }, []);
    return (
        <>
            <h2>A Selection Summary the page answers when asked to</h2>
            <p>
                A grid the page hands its rows answers its Selection Summary through <code>onSummarize</code> (ADR-0130). This
                one holds each answer until the button is pressed, so the pending mark stands meanwhile, and declines a selection
                of more than {heldMostCells} cells with a reason, which the line shows in place of the figures.
            </p>
            <p className="demo-status">
                <button type="button" id="summary-answer" onClick={() => { answer(waiting, 'answered'); }}>Answer the summary</button>
                <span id="summary-asked">Asked: {counts.asked}; cancelled: {counts.cancelled}; answered: {counts.answered}</span>
                {late
                    ? (
                        <>
                            <button type="button" id="summary-answer-late" onClick={() => { answer(replaced, 'late'); }}>Answer the replaced question</button>
                            <span id="summary-late">Answered late: {counts.late}</span>
                        </>
                    )
                    : null}
            </p>
            <div id="held-summary">
                <ExGrid columns={heldColumns} rows={summaryRows} rowHeight={20} viewportHeight={140} viewportWidth={480} onSummarize={onSummarize} />
            </div>
        </>
    );
}

/**
 * The page /features of the Blazor hosts: an interaction surface on one page. The Blazor page writes every
 * notification to status lines below the heading; a line comes here with the notification it shows, so a
 * test that reads one the Script Core does not raise yet (an edit, a paste, a filter, a reorder, a menu)
 * fails by name rather than reading a value nothing could change.
 */
export function Features(): ReactElement {
    const showGroups = useQuery('groups') === '1';
    const heldSummary = useQuery('summary') === 'held';
    const late = useQuery('late') === '1';
    const upstream = useQuery('upstream') === '1';
    const rowKeyAsked = useQuery('rowkey') === '1';
    // ?paged=1 gives the main grid a pager of 50 rows a page (ADR-0015), as Features.razor's does.
    const paged = useQuery('paged') === '1';
    const rowKeyOn = upstream && rowKeyAsked;
    const source = useMemo(() => GridSource.from(rows, rowKeyOn ? { rowKey: tradeNumber } : {}), [rowKeyOn]);
    const second = useMemo(() => GridSource.from(rows.slice(0, 20)), []);
    // The rows judged again whenever they change, so a paste, a fill or an undo wears the marks a typed edit does;
    // the ruleset answers the grid's two channels, the Cell State and the message (ADR-0034).
    const subscribe = useMemo(() => source.subscribe.bind(source), [source]);
    const held = useSyncExternalStore(subscribe, () => source.snapshot().window);
    const judged = useMemo(() => {
        rules.evaluate(held, columns);
        return { cellState: rules.cellState, cellMessage: rules.cellMessage };
    }, [held]);
    const [editStatus, setEditStatus] = useState('—');
    const [clearStatus, setClearStatus] = useState('—');
    const [historyStatus, setHistoryStatus] = useState('—');
    // The undo stack ADR-0007 says the Consumer owns: one step per intent, a typed edit, a paste, a fill or a
    // clear, each a list of the replacements it made. The grid only forwards the keys.
    const history = useRef<{ undo: Step[]; redo: Step[] }>({ undo: [], redo: [] });
    // Replaces a row through the source and notes it in the step being made.
    const replace = useCallback((step: Step, before: DemoTrade, after: DemoTrade) => {
        source.replaceRow(before, after);
        step.push([before, after]);
    }, [source]);
    // A step made is one Ctrl+Z, and what could be redone is gone.
    const record = useCallback((step: Step) => {
        if (step.length === 0) return;
        history.current.undo.push(step);
        history.current.redo = [];
    }, []);
    // Undoes or redoes one step: its replacements reversed, last first, and the reverse step pushed onto the
    // other stack, so replaying that one last first puts them back in their original order.
    const replay = useCallback((from: 'undo' | 'redo', verb: string) => {
        const stacks = history.current;
        const step = stacks[from].pop();
        if (step === undefined) {
            setHistoryStatus(`nothing to be ${verb}`);
            return;
        }
        const reversed: Step = [];
        for (const [before, after] of step.toReversed()) {
            source.replaceRow(after, before);
            reversed.push([after, before]);
        }
        stacks[from === 'undo' ? 'redo' : 'undo'].push(reversed);
        setHistoryStatus(`${step.length} changes ${verb}`);
    }, [source]);
    const onUndo = useCallback(() => { replay('undo', 'undone'); }, [replay]);
    const onRedo = useCallback(() => { replay('redo', 'redone'); }, [replay]);
    const [editDiscardedStatus, setEditDiscardedStatus] = useState('—');
    const [commitRefusedStatus, setCommitRefusedStatus] = useState('—');
    const [overwriteStatus, setOverwriteStatus] = useState('—');
    // The columns as the user last left them: a resize or a reorder reaches the screen when they are handed
    // back (ADR-0011, ADR-0016).
    const [shownColumns, setShownColumns] = useState(upstream ? upstreamColumns : columns);
    const [widthStatus, setWidthStatus] = useState('—');
    // As it came: a MaxWidth bounds only what the grid computes, so a drag past it is recorded as it is.
    const onColumnWidthChanged = useCallback((change: GridColumnWidthChange) => {
        setWidthStatus(`${change.column}=${Math.round(change.widthPx)}`);
        setShownColumns(held => held.map(c => (c.name === change.column ? { ...c, width: change.widthPx } : c)));
    }, []);
    const onColumnOrderChanged = useCallback((order: readonly string[]) => {
        setShownColumns(held => inOrder(held, order));
    }, []);
    // The column menu's Hide and Pin are the page's to apply (ADR-0010): the grid only says what was asked.
    const onColumnHidden = useCallback((column: string) => {
        setShownColumns(held => held.filter(c => c.name !== column));
    }, []);
    const [pinned, setPinned] = useState(0);
    // What the Consumer adds to the Context Menu (ADR-0036): handed the row clicked and the Selection as rectangles
    // in their version. Resolving the rest of the Selection into rows is this side's question, since this side
    // owns the data; the demo does the one-row case, opening what the row is about.
    const [contextStatus, setContextStatus] = useState('—');
    const contextCommands = useCallback((context: GridContextMenuContext<DemoTrade>): readonly GridCommand[] => {
        const { row } = context;
        const cells = context.selection.reduce((sum, range) => sum + (range.rowCount * range.columnCount), 0);
        return [{
            id: 'open-trade', enabled: true,
            invoke: () => { setContextStatus(`open ${row.book}/${row.trader} (${cells} cells selected, v${context.rowSequenceVersion})`); },
        }];
    }, []);
    const [counts, setCounts] = useState({ edits: 0, pastes: 0 });
    const [copyStatus, setCopyStatus] = useState('—');
    const [pasteStatus, setPasteStatus] = useState('—');
    const [pasteFields, setPasteFields] = useState('—');
    const [pasteRefusedStatus, setPasteRefusedStatus] = useState('—');
    const [findStatus, setFindStatus] = useState('—');
    const [replaceStatus, setReplaceStatus] = useState('—');
    // How often each reason has been heard, so a refusal repeated shows that it was, as the Blazor page's do.
    const repeats = useRef(new Map<string, number>());
    const status = useCallback((key: string, text: string, set: (text: string) => void) => {
        const before = repeats.current.get(key);
        const seen = before === undefined ? 1 : before + 1;
        repeats.current.set(key, seen);
        set(seen > 1 ? `${text} (×${seen})` : text);
    }, []);
    const onRefused = useCallback((refusal: GridRefusal) => {
        const key = `${refusal.kind}:${refusal.reason}`;
        switch (refusal.kind) {
            case 'commit':
                status(key, commitRefused(refusal.reason, refusal.column), setCommitRefusedStatus);
                return;
            // The reason written as the Blazor page writes C#'s enum member, so one spec reads both.
            case 'copy':
                status(key, pascal(refusal.reason), setCopyStatus);
                return;
            case 'paste':
                status(key, pascal(refusal.reason), setPasteRefusedStatus);
                return;
            // Find's refusals (ADR-0055). This page's source is GridSource.from's, which finds, so Unavailable
            // never arrives here; the panel words NotFound itself too.
            case 'find':
                status(key, pascal(refusal.reason), setFindStatus);
                return;
            // An action whose row the grid could not find in the rows it holds when the press landed (ADR-0142,
            // LV-12, LV-20). Nothing was done; the row's values changing never refuses one.
            case 'action':
                status(key, actionRefused(refusal.reason, refusal.action), setActionRefusedStatus);
                return;
        }
    }, [status]);
    const [upstreamStatus, setUpstreamStatus] = useState('—');
    const [actionStatus, setActionStatus] = useState('—');
    const [actionRefusedStatus, setActionRefusedStatus] = useState('—');
    const upstreamTicks = useRef(0);
    // F9 and F8 under ?upstream=1: the data moves on under the user, as a live feed moves it, in new instances
    // (ADR-0003, ADR-0142). A live change is no step of the user's history.
    const onUpstreamKey = useCallback((press: GridDeclaredKeyPress) => {
        const window = source.snapshot().window;
        upstreamTicks.current += 1;
        if (press.key === 'F8') {
            const [top] = window;
            if (top === undefined) return;
            source.replaceRow(top, Object.freeze({ ...top, notional: Math.max(...window.map(t => t.notional)) + 1_000_000 }));
            setUpstreamStatus(`trade #${top.id} amended past every other (×${upstreamTicks.current})`);
            return;
        }
        const count = Math.min(5, window.length);
        for (const old of window.slice(0, count)) source.replaceRow(old, Object.freeze({ ...old, notional: old.notional + 1 }));
        setUpstreamStatus(`rows 1-${count} moved (×${upstreamTicks.current})`);
    }, [source]);
    const onAction = useCallback((press: GridActionEvent<DemoTrade>) => {
        setActionStatus(`${press.action} ${press.row.book}/${press.row.trader} at ${press.row.notional.toFixed(2)}`);
    }, []);
    const onEditDiscarded = useCallback((reason: EditDiscardReason) => {
        status(`discard:${reason}`, editDiscarded(reason), setEditDiscardedStatus);
    }, [status]);
    // A commit that landed over a cell whose value moved while the user typed (ADR-0142, LV-11, LV-21): the
    // editor covered the cell, so the sentence says what it showed and what was written over.
    const onOverwriteNotice = useCallback((notice: GridOverwriteNotice) => {
        status('overwrite',
            `${notice.column} written over ${notice.replacedText}; it showed ${notice.seenText} when you started typing`,
            setOverwriteStatus);
    }, [status]);
    // The Consumer's half of ADR-0007: a new instance carrying the change goes back through the source, and the
    // screen follows the identity change. An unparseable value applies nothing: validation is the Consumer's.
    const onEdit = useCallback((intent: GridEditIntent<DemoTrade>) => {
        setCounts(was => ({ ...was, edits: was.edits + 1 }));
        const said = `${intent.column}=${intent.value} on ${intent.row.book}/${intent.row.trader}${rowKeyOn ? ` #${intent.row.id}` : ''}`;
        const replacement = edited(intent.row, intent.column, intent.value);
        if (replacement === undefined) {
            setEditStatus(`${said} (not applied: unparseable)`);
            return;
        }
        const step: Step = [];
        replace(step, intent.row, replacement);
        record(step);
        setEditStatus(said);
    }, [replace, record, rowKeyOn]);
    // Replace All (ADR-0332 §3): GridSource.from plans every match through this page's own write of one cell, the
    // one its edits use, and applies them all, or none; the rows it rewrote are one history step, so one Ctrl+Z
    // undoes the whole Replace All.
    const onReplace = useCallback((request: GridReplaceRequest): GridReplaceResult => {
        const answer = source.replace(request, (row, column, text) => {
            const replacement = edited(row, column, text);
            return replacement === undefined ? { refusal: `'${text}' is not a number` } : { row: replacement };
        });
        record(answer.changes.map(({ before, after }): [DemoTrade, DemoTrade] => [before, after]));
        const { result } = answer;
        setReplaceStatus(result.kind === 'replaced' ? `${result.count} cells in ${answer.changes.length} rows` : `refused, ${result.cells.length} cells`);
        return result;
    }, [source, record]);
    // Delete's Clear Intent (ADR-0054), mapped onto this Consumer's own Blank: Trader has one, the empty name;
    // Notional does not, so a clear there applies nothing, and says so, rather than writing a zero.
    const onClear = useCallback((intent: GridClearIntent) => {
        const rowAt = heldRows(source);
        // Ranges may overlap; a cell covered twice is cleared once (ADR-0054).
        const resolved = new Set<DemoTrade>();
        const counted = new Set<string>();
        let skipped = 0;
        for (const target of intent.targets) {
            for (let row = target.topRow; row < target.topRow + target.rowCount; row++) {
                for (let column = target.leftColumn; column < target.leftColumn + target.columnCount; column++) {
                    if (counted.has(`${row} ${column}`)) continue;
                    counted.add(`${row} ${column}`);
                    if (pick(shownColumns, column).name === 'Trader') resolved.add(rowAt(row));
                    else skipped += 1;
                }
            }
        }
        const step: Step = [];
        for (const row of resolved) {
            const replacement = edited(row, 'Trader', '');
            if (replacement !== undefined) replace(step, row, replacement);
        }
        record(step);
        setClearStatus(`${intent.cellCount} cells, ${step.length} applied, ${skipped} have no blank`);
    }, [source, replace, record, shownColumns]);
    const onPaste = useCallback((intent: GridPasteIntent) => {
        setCounts(was => ({ ...was, pastes: was.pastes + 1 }));
        const status = `${intent.cellCount} cells from ${intent.plan.source.rows}x${intent.plan.source.columns}`;
        // Shown in part: the page is read by people and by tests, and a large paste written whole into one
        // text node costs the browser seconds of text shaping.
        const fields = intent.values.flatMap((row, r) => row.map((value, c) => `${shown(value)}:${pascal(pick(pick(intent.origins, r), c))}`));
        setPasteFields(fields.slice(0, shownFields).join(' | ') + (fields.length > shownFields ? ` | … ${fields.length - shownFields} more` : ''));
        // Positions are the current order's, so every cell is resolved against the Window first: applying as
        // it goes could reorder the rows under a Sort on the edited column (ADR-0011, ADR-0014).
        const rowAt = heldRows(source);
        const resolved: { readonly row: DemoTrade; readonly column: string; readonly value: string }[] = [];
        for (const target of intent.plan.targets) {
            for (let row = target.topRow; row < target.topRow + target.rowCount; row++) {
                const held = rowAt(row);
                for (let column = target.leftColumn; column < target.leftColumn + target.columnCount && column < shownColumns.length; column++) {
                    resolved.push({ row: held, column: pick(shownColumns, column).name, value: intent.valueFor({ row, column }) });
                }
            }
        }
        // Each change is built on the instance the one before it left, followed through the latest replacement.
        const latest = new Map<DemoTrade, DemoTrade>();
        const step: Step = [];
        for (const { row, column, value } of resolved) {
            // A row not yet replaced is its own latest version.
            const current = latest.get(row) ?? row;
            const replacement = edited(current, column, value);
            if (replacement === undefined) continue;
            replace(step, current, replacement);
            latest.set(row, replacement);
        }
        record(step);
        setPasteStatus(`${status}, ${step.length} applied`);
    }, [source, replace, record, shownColumns]);
    return (
        <>
            <DemoNav route="features" />
            <h1>Features — editing, clipboard, sorting, filtering, Header Groups</h1>
            <p>
                The interaction surface on one page, for the browser layer to drive: an editable source-bound grid with
                tiered headers. Sorting, filtering, the Selection, editing, copying and pasting, and Find and Replace
                (Ctrl+F, Ctrl+H) work here now; the rest of the Blazor page's status lines join as the Script Core comes
                to them.
            </p>
            <p className="demo-status">
                <span id="edit-status">Edited: {editStatus}</span>
                <span id="copy-status">Copy refused: {copyStatus}</span>
                <span id="paste-status">Pasted: {pasteStatus}</span>
                {/* What the last paste's block held, field by field, with where each field came from (ADR-0050, item 10). */}
                <span id="paste-fields">Fields: {pasteFields}</span>
                <span id="clear-status">Cleared: {clearStatus}</span>
                <span id="history-status">History: {historyStatus}</span>
                <span id="paste-refused-status">Paste refused: {pasteRefusedStatus}</span>
                <span id="edit-discarded-status" role="status">Edit discarded: {editDiscardedStatus}</span>
                {/* A write refused because its row is gone or its order moved (ADR-0142). */}
                <span id="commit-refused-status" role="status">Commit refused: {commitRefusedStatus}</span>
                {/* The Overwrite Notice (ADR-0142, LV-21), announced as a refusal is, with both texts, because the
                    user never saw the change. Nothing was refused. */}
                <span id="overwrite-notice-status" role="status">Overwritten: {overwriteStatus}</span>
                <span id="order-status">Order: {shownColumns.map(c => c.name).join(',')}</span>
                <span id="width-status">Width: {widthStatus}</span>
                <span id="context-status">Context: {contextStatus}</span>
                <span id="find-refused-status" role="status">Find: {findStatus}</span>
                <span id="replace-status">Replace All: {replaceStatus}</span>
                {upstream
                    ? (
                        <>
                            <span id="action-refused-status" role="status">Action refused: {actionRefusedStatus}</span>
                            <span id="upstream-status">Upstream: {upstreamStatus}</span>
                            <span id="action-status">Action: {actionStatus}</span>
                        </>
                    )
                    : null}
            </p>
            <ExGrid
                columns={shownColumns}
                source={source}
                rowHeight={24}
                headerHeight={28}
                viewportHeight={320}
                viewportWidth={720}
                headerGroups={upstream ? upstreamGroups : groups}
                pinnedColumnCount={pinned}
                // The polyfill this page's dates are, which a date typed into a filter is made with.
                temporal={Temporal}
                onRefused={onRefused}
                onEdit={onEdit}
                cellState={judged.cellState}
                cellMessage={judged.cellMessage}
                onEditDiscarded={onEditDiscarded}
                onOverwriteNotice={onOverwriteNotice}
                onClear={onClear}
                onUndo={onUndo}
                onRedo={onRedo}
                onPaste={onPaste}
                onColumnWidthChanged={onColumnWidthChanged}
                onColumnOrderChanged={onColumnOrderChanged}
                onColumnHidden={onColumnHidden}
                onPinnedCountChanged={setPinned}
                onReplace={onReplace}
                {...(paged ? { pageSize: 50 } : {})}
                {...(upstream ? { declaredKeys: rowKeyOn ? upstreamAndResortKeys : upstreamKeys, onDeclaredKey: onUpstreamKey, onAction } : {})}
                contextCommands={contextCommands} />
            <p className="demo-status"><span id="intent-count">Intents: {counts.edits} edits, {counts.pastes} pastes</span></p>
            {showGroups
                ? (
                    <p className="demo-status">
                        <span id="groups-status">
                            Groups: {(upstream ? upstreamGroups : groups).map(g => `${g.label} [${g.columns.join(',')}] tier ${g.tier ?? 1} span ${g.tierSpan ?? 1}`).join('; ')}
                        </span>
                    </p>
                )
                : null}
            <h2>A second, independent instance</h2>
            <p>Two grids on one page hold independent DOM, tokens and handles (ADR-0018).</p>
            <ExGrid columns={secondColumns} source={second} rowHeight={20} viewportHeight={140} viewportWidth={480} />
            {heldSummary ? <HeldSummary late={late} /> : null}
        </>
    );
}
