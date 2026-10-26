// What the Script Core's engine answers to a report source case (kind "pivotReportSource"; tests/cases/README.md):
// a local report source over a case's Pivot Source, asked for Windows, Change Batches applied to its source,
// its clock moved, and Copy, the Selection Summary, a field's Items and the records behind a cell asked under a
// Report Version. The C# side is tests/ExPivot.Engine.Tests/Cases/PivotReportSourceCases.cs, which writes the
// answers.
import { createChangeBatch, decimalText, SnapshotError } from '@ex-grid/data';
import {
    LocalPivotReportSource, pivotReportDetailsQuery, pivotWordIdList, SnapshotPivotSource, type PivotDisplayRow, type PivotFieldItem,
    type PivotLayout, type PivotNumber, type PivotOrderKey, type PivotReportRange, type PivotReportRefusal, type PivotReportSettings,
    type PivotReportState,
} from '../src/index';
import { invariantNumbers } from '../src/text/invariant';
import { formatDouble } from '../src/text/number-text';
import { array, inherited, layoutOf, must, object, refusalOf, rowsSnapshot, text, wordsOf, type Json, type JsonObject } from './cases';
import { itemKey } from './readings';
import { detailValue, open, type Bench } from './source-cases';

// A report source over a case's Pivot Source; every Window it answered, by its Report Version; the one adopted
// last; what the next Window is asked under; the clock; and the versions it named, as the case writes them.
interface Run {
    readonly bench: Bench;
    readonly reports: LocalPivotReportSource;
    readonly clock: { now: number };
    readonly states: Map<string, PivotReportState>;
    adopted: PivotReportState | undefined;
    layout: PivotLayout | undefined;
    settings: JsonObject | undefined;
    locale: string;
    words: Json;
    readonly tokens: Map<string, string>;
    readonly counts: Map<string, number>;
}

// A version as the case writes it: v1, v2… for Report Versions, s1… for Source Versions and r1… for row
// sequences, in the order the source first named them.
function token(run: Run, kind: 'v' | 's' | 'r', version: string): string {
    const known = run.tokens.get(version);
    if (known !== undefined) return known;
    const count = (run.counts.get(kind) ?? 0) + 1; // oxlint-disable-line ex-grid/no-stand-in-default -- a kind not named yet has named none
    run.counts.set(kind, count);
    const written = `${kind}${count}`;
    run.tokens.set(version, written);
    return written;
}

// The version a case's token names, or the token itself where no version was named so ("stale").
function raw(run: Run, written: string): string {
    for (const [version, named] of run.tokens) if (named === written) return version;
    return written;
}

// A message with every version it names written as its token.
function saying(run: Run, message: string): string {
    let said = message;
    for (const [version, named] of [...run.tokens].toSorted((a, b) => b[0].length - a[0].length)) said = said.replaceAll(version, named);
    return said;
}

// The Order Keys a case registers by a policy's name, each the Consumer's function the C# tests declared.
function orderKeyNamed(name: string): PivotOrderKey {
    switch (name) {
        case 'longFirst': return value => -String(value).length;
        case 'throwsOnBB': return value => {
            if (value === 'BB') throw new Error('no BB');
            return String(value);
        };
        default: throw new Error(`'${name}' is not an Order Key the report source cases register.`);
    }
}

/** The engine's answer to a report source case: each step's answer, or what it threw. */
export async function reportSourceAnswer(file: JsonObject, entry: JsonObject): Promise<Json> {
    const bench = open(file, entry);
    const clock = { now: Date.UTC(2026, 9, 6, 12) };
    const keys = entry['orderKeys'];
    const orderKeys = keys === undefined ? {} : Object.fromEntries(Object.entries(object(keys)).map(([policy, name]) => [policy, orderKeyNamed(text(name))]));
    const kept = entry['versionsKept'];
    const reports = new LocalPivotReportSource(bench.source, {
        orderKeys, ...(typeof kept === 'number' ? { versionsKept: kept } : {}), now: () => clock.now,
    });
    const settings = entry['settings'];
    const run: Run = {
        bench, reports, clock, states: new Map(), adopted: undefined, layout: undefined,
        settings: settings === undefined ? undefined : object(settings),
        locale: text(inherited(file, entry, 'locale')), words: inherited(file, entry, 'words'),
        tokens: new Map(), counts: new Map(),
    };
    const steps: Json[] = [];
    try {
        for (const step of array(must(entry, 'asks'))) {
            try {
                steps.push(await ask(run, object(step)));
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refusal is the step's answer, compared below
                const refusal = refusalOf(error);
                steps.push({ ...refusal, saying: saying(run, text(must(refusal, 'saying'))) });
            }
        }
    } finally {
        reports.dispose();
    }
    return steps;
}

async function ask(run: Run, step: JsonObject): Promise<Json> {
    const window = step['window'];
    if (window !== undefined) return windowOf(run, step, array(window));
    const batch = step['apply'];
    if (batch !== undefined) return apply(run, object(batch));
    const advance = step['advance'];
    if (typeof advance === 'number') {
        run.clock.now += advance;
        return { advanced: advance };
    }
    const copy = step['copy'];
    if (copy !== undefined) return copyOf(run, object(copy));
    const summary = step['summary'];
    if (summary !== undefined) return summaryOf(run, object(summary));
    const items = step['reportItems'];
    if (items !== undefined) return itemsOf(run, object(items));
    const details = step['details'];
    if (details !== undefined) return detailsOf(run, object(details));
    const each = step['detailsEach'];
    if (each !== undefined) return detailsEach(run, text(each));
    throw new Error(`${JSON.stringify(step)} asks nothing the report source cases ask.`);
}

// ---- Windows ------------------------------------------------------------------------------------

function whole(written: Json): number {
    if (typeof written !== 'number' || !Number.isInteger(written)) throw new TypeError(`${JSON.stringify(written)} is not a whole number.`);
    return written;
}

// A Window of the report the layout makes: the layout, the settings, the locale and the words carry over from
// the step before where a step does not name them; the baseline is the Window adopted last unless the step names
// one (null for none); a Window answered with "discard" is not adopted.
async function windowOf(run: Run, step: JsonObject, asked: readonly Json[]): Promise<Json> {
    const written = step['layout'];
    if (written !== undefined) run.layout = layoutOf(written);
    const settings = step['settings'];
    if (settings !== undefined) run.settings = object(settings);
    const locale = step['locale'];
    if (locale !== undefined) run.locale = text(locale);
    const words = step['words'];
    if (words !== undefined) run.words = words;
    const layout = run.layout;
    if (layout === undefined) throw new Error('The first Window of a case names its layout.');
    const named = step['baseline'];
    const baseline = named === undefined ? run.adopted?.metadata.version : named === null ? undefined : raw(run, text(named));
    const max = step['maxLeaves'];
    const update = await run.reports.window({
        requestId: crypto.randomUUID(), layout, settings: settingsOf(run),
        window: { start: whole(asked[0] ?? null), count: whole(asked[1] ?? null) }, // oxlint-disable-line ex-grid/no-stand-in-default -- a Window not written whole is refused by whole()
        ...(baseline === undefined ? {} : { baseline }),
        ...(max === undefined ? {} : { maxLeaves: whole(max) }),
        ...(step['markChanges'] === false ? { markChanges: false } : {}),
        ...(step['refreshData'] === true ? { refreshData: true } : {}),
    });
    if (update.refusal !== undefined) return refused(run, update.refusal);
    const state: PivotReportState = { metadata: update.metadata, window: update.window, rows: update.rows };
    run.states.set(state.metadata.version, state);
    if (step['discard'] !== true) run.adopted = state;
    const also = step['also'] === undefined ? [] : array(step['also']).map(text);
    return windowJson(run, state, also);
}

// The settings a Window is asked under: the case's locale and named words, or those the settings give, with the
// settings' highlight, label geometry and Order Key policies.
function settingsOf(run: Run): PivotReportSettings {
    const written = run.settings ?? {}; // oxlint-disable-line ex-grid/no-stand-in-default -- a case that names no settings asks under none
    const locale = written['locale'];
    const own = written['words'];
    let words: Record<string, string>;
    if (own === undefined) {
        const label = wordsOf(run.words);
        words = {};
        if (label !== undefined) {
            for (const id of pivotWordIdList()) {
                const word = label(id);
                if (word !== undefined) words[id] = word;
            }
        }
    } else {
        words = Object.fromEntries(Object.entries(object(own)).map(([id, word]) => [id, text(word)]));
    }
    const highlight = written['changeHighlightMs'];
    const metrics = written['labelMetrics'];
    const policies = written['orderKeyPolicies'];
    return {
        locale: locale === undefined ? run.locale : text(locale),
        words,
        ...(typeof highlight === 'number' ? { changeHighlightMs: highlight } : {}),
        ...(metrics === undefined ? {} : { labelMetrics: metricsOf(object(metrics)) }),
        ...(policies === undefined ? {} : { orderKeyPolicies: Object.fromEntries(Object.entries(object(policies)).map(([field, policy]) => [field, text(policy)])) }),
    };
}

function number(written: Json): number {
    if (typeof written !== 'number') throw new TypeError(`${JSON.stringify(written)} is not a number.`);
    return written;
}

function metricsOf(written: JsonObject): NonNullable<PivotReportSettings['labelMetrics']> {
    const glyphs = written['glyphWidths'];
    return {
        wideWidthPx: number(must(written, 'wideWidthPx')),
        digitWidthPx: number(must(written, 'digitWidthPx')),
        narrowWidthPx: number(must(written, 'narrowWidthPx')),
        fullWidthPx: number(must(written, 'fullWidthPx')),
        otherWidthPx: number(must(written, 'otherWidthPx')),
        cellHorizontalPaddingPx: number(must(written, 'cellHorizontalPaddingPx')),
        ...(glyphs === undefined ? {} : { glyphWidths: Object.fromEntries(Object.entries(object(glyphs)).map(([point, width]) => [Number(point), number(width)])) }),
    };
}

const roleLetters = { item: 'i', group: 'g', subtotal: 's', grandTotal: 't', pending: 'p' } as const;

// One line a row, as a report's line is read (readings.ts).
function line(row: PivotDisplayRow): string {
    const labels = row.labels.map(label => {
        const button = label.toggle === undefined ? '' : label.toggle.isCollapsed ? '[+]' : '[-]';
        return ' '.repeat(label.indent * 2) + button + (label.text ?? ''); // oxlint-disable-line ex-grid/no-stand-in-default -- a label with no text reads as nothing in a line, as the C# runner writes it
    });
    const values = row.values.map(value => value?.text ?? ''); // oxlint-disable-line ex-grid/no-stand-in-default -- an empty cell reads as nothing in a line, as the C# runner writes it
    return `${roleLetters[row.role]} ${labels.join(' | ')} || ${values.join(' | ')}`.trimEnd();
}

function double(value: number): string {
    return `d${formatDouble(value, 'R', invariantNumbers)}`;
}

// A row's numbers, as a report's are read (readings.ts).
function numbers(row: PivotDisplayRow): Json {
    return {
        labels: row.labels.map(label => (label.number === undefined ? null : double(label.number))),
        values: row.values.map((value): Json => {
            if (value === undefined) return null;
            if (value.error !== undefined) return value.error;
            if (value.exact !== undefined) return `m${decimalText(value.exact)}`;
            return double(value.number);
        }),
    };
}

// An Item with its field, as a path writes it.
function item(given: PivotFieldItem): string {
    return `${given.field}=${JSON.stringify(itemKey(given.item))}`;
}

// A Window as a case reads it: its versions as tokens, the whole report's row count, its leaf headers, a line a
// row, the changes still marked, each row's marks where any cell has one, the label widths where the settings
// give a geometry, and the readings `also` asks for.
function windowJson(run: Run, state: PivotReportState, also: readonly string[]): Json {
    const metadata = state.metadata;
    const read: Record<string, Json> = {
        version: token(run, 'v', metadata.version),
        sourceVersion: token(run, 's', metadata.sourceVersion),
        rowSequence: token(run, 'r', metadata.rowSequenceVersion),
        rowCount: metadata.rowCount,
        headers: [...metadata.labelColumns.map(c => c.header), ...metadata.valueColumns.map(c => c.header)],
        lines: state.rows.map(line),
        changeMarks: metadata.changeMarks.map(mark => token(run, 'v', mark)),
    };
    if (state.rows.some(row => row.changedIn.some(mark => mark !== undefined))) {
        read['changed'] = state.rows.map(row => row.changedIn.map(mark => (mark === undefined ? null : token(run, 'v', mark))));
    }
    if (metadata.labelWidths.length > 0) read['labelWidths'] = [...metadata.labelWidths];
    for (const more of also) {
        switch (more) {
            case 'keys':
                read[more] = state.rows.map(row => ({ role: row.role, valueField: row.valueField, items: row.items.map(itemKey) }));
                break;
            case 'paths': read[more] = state.rows.map(row => row.rowPath.map(item)); break;
            case 'numbers': read[more] = state.rows.map(numbers); break;
            case 'columns':
                read[more] = metadata.valueColumns.map(c => ({ name: c.name, role: c.role, valueField: c.valueField, path: c.columnPath.map(item) }));
                break;
            case 'spans':
                read[more] = metadata.headerSpans
                    .toSorted((a, b) => (b.tier === a.tier ? a.firstColumn - b.firstColumn : b.tier - a.tier))
                    .map(s => `${s.label}@${s.firstColumn}+${s.columnCount} t${s.tier}x${s.tierSpan}`);
                break;
            case 'captions': read[more] = [...metadata.valueCaptions]; break;
            case 'tiers': read[more] = metadata.headerTierCount; break;
            default: throw new Error(`'${more}' is not a reading of a Window the cases name.`);
        }
    }
    return read;
}

// ---- Change Batches -----------------------------------------------------------------------------

// A Change Batch applied to the Pivot Source over a Snapshot: the Source Version it made, or the Snapshot's
// refusal.
function apply(run: Run, written: JsonObject): Json {
    const source = run.bench.source;
    const shape = run.bench.shape;
    if (!(source instanceof SnapshotPivotSource) || shape === undefined) throw new Error('A batch is applied to a source over a Snapshot.');
    const records = (role: string): { readonly [role]?: ReturnType<typeof rowsSnapshot> } => {
        const rows = written[role];
        return rows === undefined ? {} : { [role]: rowsSnapshot(shape, array(rows), undefined) };
    };
    const removed = written['removedKeys'];
    try {
        source.apply(createChangeBatch({
            ...records('added'),
            ...records('changed'),
            ...(removed === undefined ? {} : { removedKeys: array(removed).map(key => (typeof key === 'number' ? key : text(key))) }),
        }));
        return { applied: token(run, 's', source.sourceVersion) };
    } catch (error) { // oxlint-disable-line ex-grid/no-catch -- a refused batch is the step's answer; anything else is rethrown
        if (!(error instanceof SnapshotError)) throw error;
        return refusalOf(error);
    }
}

// ---- Versioned operations -----------------------------------------------------------------------

// A selection's columns: each by its index among the label columns and then the value columns of the Window the
// version names (or of the one adopted last, for a version none was answered under), or by a name of its own.
function columnsOf(run: Run, version: string, written: Json): string[] {
    const metadata = (run.states.get(version) ?? run.adopted)?.metadata;
    if (metadata === undefined) throw new Error('A selection is made on a Window answered before it.');
    const names = [...metadata.labelColumns.map(c => c.name), ...metadata.valueColumns.map(c => c.name)];
    return array(written).map(c => {
        if (typeof c !== 'number') return text(c);
        const name = names[c];
        if (name === undefined) throw new RangeError(`The Window has no column ${c}.`);
        return name;
    });
}

function rangesOf(written: Json): PivotReportRange[] {
    return array(written).map(r => {
        const [top, left, bottom, right] = array(r).map(whole);
        if (top === undefined || left === undefined || bottom === undefined || right === undefined) throw new TypeError('A range is four numbers.');
        return { top, left, bottom, right };
    });
}

// A Copy: each block's headers, their raw forms where any has one, and each cell as its text, its raw form and
// whether that is a number.
async function copyOf(run: Run, written: JsonObject): Promise<Json> {
    const version = raw(run, text(must(written, 'version')));
    const max = written['maxCells'];
    const result = await run.reports.copy({
        version, columns: columnsOf(run, version, must(written, 'columns')), ranges: rangesOf(must(written, 'ranges')),
        ...(max === undefined ? {} : { maxCells: whole(max) }),
    });
    if (result.refusal !== undefined) return refused(run, result.refusal);
    return {
        version: token(run, 'v', result.version),
        blocks: result.blocks.map(block => ({
            headers: [...block.headers],
            ...(block.headerRaws === undefined ? {} : { headerRaws: block.headerRaws.map(r => (r === undefined ? null : r)) }),
            rows: block.rows.map(row => row.map(cell => [cell.text, cell.raw === undefined ? null : cell.raw, cell.isNumber])),
        })),
    };
}

function numberText(given: PivotNumber): string {
    return given.kind === 'exact' ? `m${decimalText(given.value)}` : double(given.value);
}

// A Selection Summary: Count and Numerical Count; the sum and extremes where a number was selected, each an exact
// decimal ("m…") or a double ("d…"); whether an error was; the Focus value's format; the locale.
async function summaryOf(run: Run, written: JsonObject): Promise<Json> {
    const version = raw(run, text(must(written, 'version')));
    const focus = written['focus'];
    const [focusRow, focusColumn] = focus === undefined ? [] : array(focus).map(whole);
    const result = await run.reports.summary({
        version, columns: columnsOf(run, version, must(written, 'columns')), ranges: rangesOf(must(written, 'ranges')),
        ...(focusRow === undefined ? {} : { focusRow }), ...(focusColumn === undefined ? {} : { focusColumn }),
    });
    if (result.refusal !== undefined) return refused(run, result.refusal);
    const json: Record<string, Json> = { version: token(run, 'v', result.version), values: result.values, numbers: result.numbers };
    if (result.sum !== undefined) json['sum'] = numberText(result.sum);
    if (result.min !== undefined) json['min'] = numberText(result.min);
    if (result.max !== undefined) json['max'] = numberText(result.max);
    json['hasError'] = result.hasError;
    if (result.numberFormat !== undefined) json['numberFormat'] = result.numberFormat;
    json['locale'] = result.locale;
    return json;
}

// A field's Items as the report labels and orders them.
async function itemsOf(run: Run, written: JsonObject): Promise<Json> {
    const search = written['search'];
    const max = written['max'];
    const result = await run.reports.items({
        version: raw(run, text(must(written, 'version'))), field: text(must(written, 'field')),
        ...(search === undefined ? {} : { search: text(search) }), ...(max === undefined ? {} : { max: whole(max) }),
    });
    if (result.refusal !== undefined) return refused(run, result.refusal);
    return {
        version: token(run, 'v', result.version),
        sourceVersion: token(run, 's', result.sourceVersion),
        total: result.total,
        items: result.items.map(i => ({ key: itemKey(i.key), label: i.label, isHidden: i.isHidden })),
    };
}

function held(run: Run, version: string): PivotReportState {
    const state = run.states.get(raw(run, version));
    if (state === undefined) throw new Error(`No Window was answered under ${version}.`);
    return state;
}

// The records behind a cell of a Window the case was answered: its row by its place in the report, and its value
// column, −1 for its label.
async function detailsOf(run: Run, written: JsonObject): Promise<Json> {
    const state = held(run, text(must(written, 'version')));
    const row = state.rows[whole(must(written, 'row')) - state.window.start];
    if (row === undefined) throw new RangeError('The row is outside the Window.');
    const start = written['start'];
    const count = written['count'];
    const query = pivotReportDetailsQuery(state.metadata, row, whole(must(written, 'column')),
        start === undefined ? 0 : whole(start), count === undefined ? Number.MAX_SAFE_INTEGER : whole(count));
    const page = await run.reports.details(query);
    if (page.refusal !== undefined) return { refused: page.refusal.kind, saying: saying(run, page.refusal.message) };
    return {
        sourceVersion: token(run, 's', page.sourceVersion),
        start: page.start,
        total: page.total,
        fields: page.fields.map(f => f.name),
        records: page.records.map(r => r.values.map(detailValue)),
    };
}

// The question for the records behind every cell of a Window, resolved from its rows, and its records: a line a
// cell, "column: the row's Items / the column's / the Hidden Items -> the total: each record".
async function detailsEach(run: Run, version: string): Promise<Json> {
    const state = held(run, version);
    const lines: Json[] = [];
    for (const row of state.rows) {
        const cells: string[] = [];
        for (let column = -1; column < state.metadata.valueColumns.length; column++) {
            const query = pivotReportDetailsQuery(state.metadata, row, column);
            const page = await run.reports.details(query);
            const hidden = query.hiddenItems.flatMap(f => f.hiddenItems.map(k => `${f.field}!=${JSON.stringify(itemKey(k))}`));
            const records = page.records.map(r => JSON.stringify(r.values.map(detailValue)));
            cells.push(`${column}: ${query.rowItems.map(item).join(', ')} / ${query.columnItems.map(item).join(', ')}`
                + ` / ${hidden.join(', ')} -> ${page.total}: ${records.join('; ')}`);
        }
        lines.push(cells);
    }
    return lines;
}

// ---- What a report source says ------------------------------------------------------------------

// A refusal by its kind, the field it names, and its message with every version written as its token.
function refused(run: Run, refusal: PivotReportRefusal): Json {
    return { refused: refusal.kind, ...(refusal.field === undefined ? {} : { field: refusal.field }), saying: saying(run, refusal.message) };
}
