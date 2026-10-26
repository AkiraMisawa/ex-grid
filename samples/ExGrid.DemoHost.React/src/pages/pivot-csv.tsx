import { useEffect, useRef, useState, type ChangeEvent, type ReactElement } from 'react';
import {
    SnapshotError, csvExcelFalse, csvExcelTrue, csvIsoDateFormats, readCsvSnapshot, suggestCsvSchema,
    type CsvColumn, type CsvSchema, type CsvSeparator, type CsvSuggestion, type SnapshotKind,
} from '@ex-grid/data';
import { SnapshotPivotSource, createPivotLayout, type PivotField, type PivotLayout } from '@ex-grid/pivot-engine';
import { ExPivot } from '@ex-grid/pivot-react';
import { DemoNav } from '../demo-nav';
import { malformedRow, spreadsheetFile, tradeExport, tradeExportFile, tradeFields } from '../demo-csv';
import { demoPivotFirstLayout, demoPivotTradeCount } from '../pivot-data';

const count = (value: number): string => value.toLocaleString('en-US');
const largeSample = 100_000;
const kinds: readonly SnapshotKind[] = ['text', 'decimal', 'double', 'integer', 'date', 'boolean'];

/** A kind as ExGrid.Data names it, which the Blazor page's list shows: Text, Decimal, … */
function kindName(kind: SnapshotKind): string {
    return kind.charAt(0).toUpperCase() + kind.slice(1);
}

function kindNamed(name: string): SnapshotKind {
    const kind = kinds.find(each => kindName(each) === name);
    if (kind === undefined) throw new RangeError(`'${name}' is not a kind.`);
    return kind;
}

function separatorName(separator: CsvSeparator | undefined): string {
    return separator === 'tab' ? 'a tab' : separator === 'semicolon' ? 'a semicolon' : 'a comma';
}

// How the column's text is read, in words: what the table shows beside its kind.
function readAs(column: CsvColumn): string {
    let how: string;
    switch (column.kind) {
        case 'decimal':
        case 'double':
            how = `decimal point '${column.decimalPoint ?? '.'}'`
                + (column.thousandsSeparator === undefined || column.thousandsSeparator === '' ? '' : `, thousands '${column.thousandsSeparator}'`);
            break;
        case 'integer': how = 'whole numbers'; break;
        case 'date': how = `dates: ${(column.dateFormats ?? csvIsoDateFormats).join(', ')}`; break;
        case 'boolean': how = `${(column.trueText ?? csvExcelTrue).join('/')} or ${(column.falseText ?? csvExcelFalse).join('/')}`; break;
        case 'text': how = 'as written'; break;
        default: return column.kind satisfies never;
    }
    return column.blankText !== undefined && column.blankText.length > 0 ? `${how}; blank: ${column.blankText.join(', ')}` : how;
}

const megabytes = (bytes: number): string => (bytes / 1_048_576).toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 });

/** A file waiting for the user to confirm the Schema suggested for it, with the kind chosen for each column. */
interface Pending {
    readonly name: string;
    readonly file: Blob | Uint8Array<ArrayBuffer>;
    readonly suggestion: CsvSuggestion;
    readonly kinds: readonly SnapshotKind[];
}

function kindAt(pending: Pending, index: number): SnapshotKind {
    const kind = pending.kinds[index];
    if (kind === undefined) throw new RangeError(`The suggestion has no column ${index}.`);
    return kind;
}

interface Shown {
    readonly source: SnapshotPivotSource;
    readonly text: string;
}

/** The page /pivot-csv of the Blazor hosts under the Script Core: a CSV the user brings, read into a Snapshot under a
 * Schema and pivoted (ADR-0064, ADR-0069). A file this application knows is read under the Schema it declares; one
 * nobody has described gets a Schema suggested from its first rows, read under only once the user confirms it. */
export function PivotCsv(): ReactElement {
    const [layout, setLayout] = useState<PivotLayout>(demoPivotFirstLayout);
    const [shown, setShown] = useState<Shown | undefined>(undefined);
    const [reading, setReading] = useState<AbortController | undefined>(undefined);
    const [writing, setWriting] = useState(false);
    const [size, setSize] = useState(0);
    const [rows, setRows] = useState(0);
    const [bytes, setBytes] = useState(0);
    const [status, setStatus] = useState('Choose a file, or a sample.');
    const [refusal, setRefusal] = useState<string | undefined>(undefined);
    const [pending, setPending] = useState<Pending | undefined>(undefined);
    const [suggesting, setSuggesting] = useState(false);
    // The rows read so far, for the sentence a cancel ends with: the state is a render behind.
    const rowsRead = useRef(0);
    const current = useRef<AbortController | undefined>(undefined);
    useEffect(() => () => { current.current?.abort(); }, []);
    const busy = reading !== undefined || writing || suggesting;

    // #region The code: read
    // Read in slices that yield between them: the page paints the progress, and Cancel stops the read at the next
    // slice. A value a column cannot read refuses the whole file, by name. While a file is read, nothing else can be
    // chosen.
    async function read(name: string, file: Blob | Uint8Array<ArrayBuffer>, schema: CsvSchema, fields: readonly PivotField[] | undefined,
        next: PivotLayout, how: string): Promise<void> {
        const controller = new AbortController();
        current.current = controller;
        setReading(controller);
        setShown(undefined);
        setRefusal(undefined);
        setSize(file instanceof Blob ? file.size : file.length);
        setRows(0);
        setBytes(0);
        rowsRead.current = 0;
        setStatus(`Reading ${name} ${how}…`);
        const begun = performance.now();
        try {
            const snapshot = await readCsvSnapshot(schema, file, {
                signal: controller.signal,
                onProgress: progress => {
                    rowsRead.current = progress.rows;
                    setRows(progress.rows);
                    if (progress.bytes !== undefined) setBytes(progress.bytes);
                },
            });
            // The fields over its columns; with none declared, one field per column.
            const source = new SnapshotPivotSource(snapshot, fields);
            setLayout(next);
            setShown({ source, text: `${name}: ${count(snapshot.rowCount)} rows, read ${how}.` });
            const seconds = ((performance.now() - begun) / 1000).toLocaleString('en-US', { minimumFractionDigits: 1, maximumFractionDigits: 1 });
            setStatus(`${name}: ${count(snapshot.rowCount)} rows read in ${seconds} s.`);
        } catch (error) {
            if (error instanceof SnapshotError) {
                // The library's sentence, naming the row and the column:
                // "Row 1,234, column 'Notional': '40,00O.00' is not a number (line 1,235)."
                setRefusal(error.message);
                setStatus(`${name} was refused: nothing was read.`);
            } else if (controller.signal.aborted && error === controller.signal.reason) {
                setStatus(`Cancelled after ${count(rowsRead.current)} rows: nothing was read.`);
            } else if (error instanceof DOMException && error.name === 'NotReadableError') {
                // The browser could not hand the file over.
                setRefusal(error.message);
                setStatus(`${name} could not be read: nothing was read.`);
            } else {
                throw error;
            }
        } finally {
            if (current.current === controller) current.current = undefined;
            setReading(undefined);
        }
    }
    // #endregion

    // #region The code: suggest
    // A file nobody has described: a Schema is suggested from its first rows, with every column whose reading is not
    // clear marked. It is a proposal: the file is read under it, or under the kinds the user changed, only once they
    // confirm.
    // While its first rows are read, nothing else can be chosen.
    async function suggest(name: string, file: Blob | Uint8Array<ArrayBuffer>): Promise<void> {
        setPending(undefined);
        setRefusal(undefined);
        setSuggesting(true);
        setStatus(`Reading the first rows of ${name}…`);
        try {
            const suggestion = await suggestCsvSchema(file, { culture: 'en-US' });
            setPending({ name, file, suggestion, kinds: suggestion.schema.columns.map(column => column.kind) });
            setStatus(`A Schema is suggested for ${name}: confirm it to read the file.`);
        } catch (error) {
            if (error instanceof SnapshotError) {
                // Its first rows cannot be read at all: an empty file, text not valid UTF-8, a quote left open.
                setRefusal(error.message);
                setStatus(`No Schema can be suggested for ${name}.`);
            } else if (error instanceof DOMException && error.name === 'NotReadableError') {
                // The browser could not hand the file over.
                setRefusal(error.message);
                setStatus(`${name} could not be read.`);
            } else {
                throw error;
            }
        } finally {
            setSuggesting(false);
        }
    }

    function confirm(): void {
        if (pending === undefined) return;
        setPending(undefined);
        const suggested = pending.suggestion.schema;
        const confirmed: CsvSchema = { ...suggested, columns: suggested.columns.map((column, i) => ({ ...column, kind: kindAt(pending, i) })) };
        void read(pending.name, pending.file, confirmed, undefined, createPivotLayout(), 'under the confirmed Schema');
    }
    // #endregion

    // #region The code: choose
    const chooseExport = (event: ChangeEvent<HTMLInputElement>): void => {
        const file = event.target.files?.[0];
        event.target.value = '';
        if (file !== undefined) void read(file.name, file, tradeExport, tradeFields, demoPivotFirstLayout(), 'under the declared Schema');
    };

    const chooseUnknown = (event: ChangeEvent<HTMLInputElement>): void => {
        const file = event.target.files?.[0];
        event.target.value = '';
        if (file !== undefined) void suggest(file.name, file);
    };
    // #endregion

    async function readSample(trades: number, malformed: boolean): Promise<void> {
        const name = malformed ? 'trades-malformed.csv' : `trades-${trades}.csv`;
        setStatus(`Writing ${name}…`);
        setWriting(true);
        let file: Uint8Array<ArrayBuffer>;
        try {
            // Written in slices, the page painting how far it has come between them.
            file = await tradeExportFile(trades, malformed, async written => {
                setStatus(`Writing ${name}: ${count(written)} of ${count(trades)} trades…`);
                await new Promise(resolve => { setTimeout(resolve, 1); });
            });
        } finally {
            setWriting(false);
        }
        await read(name, file, tradeExport, tradeFields, demoPivotFirstLayout(), 'under the declared Schema');
    }

    const progressMax = Math.max(1, size);
    const suggestion = pending?.suggestion;
    return (
        <>
            <DemoNav route="pivot-csv" />
            <h1>Pivot over a CSV — a file read under a Schema</h1>
            <p>
                <strong>The use case:</strong> a file the user brings, pivoted in the page. A CSV is read straight into a
                Snapshot&apos;s columns under a Schema, which says what each column is and how to read it; nothing is
                guessed, because a guess reads the account number <code>004417</code> as the number 4,417 (ADR-0064). A
                file the application knows is read under the Schema it declares. A file nobody has described gets a
                Schema suggested from its first rows, with every column whose reading is not clear marked, and is read
                under it only once the user confirms it.
            </p>
            <p>
                The file is read in slices that yield between them, so the page paints its progress and Cancel stops it.
                A value its column cannot read refuses the whole file, naming the row and the column: a report over the
                rows that could be read would be quietly short. Only a file read whole is pivoted.
            </p>

            <h2>A file this application knows: the trade export, under its declared Schema</h2>
            <div className="demo-toolbar">
                <label>Choose a trade export <input id="csv-file" type="file" accept=".csv,.txt" disabled={busy} onChange={chooseExport} /></label>
            </div>
            <div className="demo-toolbar">
                <span>Or a sample, written in memory from /pivot&apos;s trades:</span>
                <button id="csv-sample" type="button" disabled={busy} onClick={() => { void readSample(demoPivotTradeCount, false); }}>2,000 trades</button>
                <button id="csv-sample-large" type="button" disabled={busy} onClick={() => { void readSample(largeSample, false); }}>100,000 trades</button>
                <button id="csv-sample-malformed" type="button" disabled={busy} onClick={() => { void readSample(demoPivotTradeCount, true); }}>
                    2,000 trades, row {count(malformedRow)} malformed
                </button>
            </div>

            <h2>A file nobody has described: a suggested Schema, read under once you confirm it</h2>
            <div className="demo-toolbar">
                <label>Choose any CSV <input id="csv-unknown-file" type="file" accept=".csv,.txt,.tsv" disabled={busy} onChange={chooseUnknown} /></label>
            </div>
            <div className="demo-toolbar">
                <span>Or a sample:</span>
                <button id="csv-unknown-sample" type="button" disabled={busy}
                        onClick={() => { void suggest('trades-de.csv', spreadsheetFile(demoPivotTradeCount)); }}>
                    the trades as a spreadsheet set to German saves them
                </button>
            </div>

            {pending === undefined || suggestion === undefined ? null : (
                <section id="csv-suggestion" aria-labelledby="csv-suggestion-title">
                    <h3 id="csv-suggestion-title">The Schema suggested for {pending.name}</h3>
                    <p id="csv-suggestion-file">
                        From its first {count(suggestion.sampledRows)} rows: {separatorName(suggestion.schema.separator)} between
                        fields, {suggestion.schema.hasHeader === false ? 'no header row' : 'a header row first'},{' '}
                        {suggestion.schema.encoding === 'shift-jis' ? 'Shift-JIS' : 'UTF-8'}.
                        {suggestion.marks.map((mark, i) => <span key={i} className="demo-mark"> {mark.note}</span>)}
                    </p>
                    <table className="demo-table">
                        <thead>
                            <tr><th>Column</th><th>Kind</th><th>Read as</th><th>Examples</th><th>Not clear</th></tr>
                        </thead>
                        <tbody>
                            {suggestion.columns.map((column, index) => (
                                <tr key={column.column.name} data-column={column.column.name}>
                                    <td>{column.column.name}</td>
                                    <td>
                                        <select aria-label={`Kind of ${column.column.name}`} value={kindName(kindAt(pending, index))}
                                                disabled={busy}
                                                onChange={event => {
                                                    const kind = kindNamed(event.target.value);
                                                    setPending(now => (now === undefined ? now : { ...now, kinds: now.kinds.map((each, i) => (i === index ? kind : each)) }));
                                                }}>
                                            {kinds.map(kind => <option key={kind} value={kindName(kind)}>{kindName(kind)}</option>)}
                                        </select>
                                    </td>
                                    <td>{readAs(column.column)}</td>
                                    <td>{column.examples.join('  ')}</td>
                                    <td>{column.marks.map(mark => mark.note).join(' ')}</td>
                                </tr>
                            ))}
                        </tbody>
                    </table>
                    <div className="demo-toolbar">
                        <button id="csv-confirm" type="button" disabled={busy} onClick={confirm}>Read the file under this Schema</button>
                        <button id="csv-discard" type="button" disabled={busy} onClick={() => { setPending(undefined); setRefusal(undefined); }}>Discard</button>
                    </div>
                </section>
            )}

            <h2>Reading</h2>
            <div className="demo-toolbar">
                <progress id="csv-progress" max={progressMax} value={Math.min(progressMax, bytes)} />
                <span id="csv-read" className="demo-status">{size === 0 ? '' : `${count(rows)} rows, ${megabytes(bytes)} of ${megabytes(size)} MB`}</span>
                <button id="csv-cancel" type="button" disabled={reading === undefined} onClick={() => { reading?.abort(new DOMException('The read was cancelled.', 'AbortError')); }}>
                    Cancel
                </button>
            </div>
            <p className="demo-status"><span id="csv-status" role="status">{status}</span></p>
            <p id="csv-refusal" className="demo-refusal" role="alert">{refusal}</p>

            {shown === undefined ? null : (
                <>
                    <p id="csv-shown">The report shows {shown.text}</p>
                    <ExPivot source={shown.source} layout={layout} onLayoutChanged={({ layout: changed }) => { setLayout(changed); }}
                             locale="en-US" viewportHeight={460} />
                </>
            )}
        </>
    );
}
