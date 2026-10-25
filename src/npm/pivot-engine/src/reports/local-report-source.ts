// One report's calculation and display state over a Pivot Source it does not own (ADR-0151, ADR-0153): it
// lays the report out under a request's layout and settings, answers the Window asked for, and keeps a bounded
// number of Report Versions for the versioned operations — Copy, the Selection Summary, a field's Items —
// which never wait on a newer computation. It marks the cells a change of the data moved, for the Change
// Highlight (ADR-0068). It runs where the Script Core's engine runs: on the page's thread or in a Worker
// (ADR-0370 section 4). The C# engine's LocalPivotReportSource.

import { decimalText, type SnapshotDecimal } from '@ex-grid/data';
import {
    addDoubleToExtremes, addDoubleToSum, addExactToExtremes, addExactToSum, finishExtremes, finishSum, noExtremes, noSum,
} from '../aggregates/parts';
import { decimalEquals } from '../aggregates/exact-decimal';
import { PivotComputationSession, type PivotComputationResult } from '../computation/session';
import type { PivotCube } from '../cube/cube';
import { defaultMaxLeaves, pivotItemsQuery, type PivotDetailsQuery, type PivotItemsQuery } from '../cube/query';
import type { PivotNumber } from '../cube/answer';
import { checkCubeHolds, itemsOfPage } from '../engine';
import { PivotOrderKeyError } from '../errors';
import type { PivotField, PivotOrderKey } from '../fields/pivot-field';
import { readPivotLayoutJson, writePivotLayoutJson } from '../layout/layout-json';
import { defined, elementAt } from '../must';
import { resolveOptions, type PivotOptions } from '../report/labels';
import { buildReportSteps, rowIdOf, rowLabelsAlike, sameRowsSteps, type PivotReport, type PivotReportRow, type PivotRowKey } from '../report/report';
import type { PivotValue } from '../report/value';
import { invariantNumbers } from '../text/invariant';
import { formatDouble } from '../text/number-text';
import {
    SnapshotPivotSource, type PivotDetailPage, type PivotItemPage, type PivotSource, type PivotSourceChanged, type PivotSourceFeatures,
} from '../source/pivot-source';
import { checkedSlicing, pieceUnits, Slicer, type CheckedSlicing, type PivotSlicing } from '../source/slicing';
import type {
    PivotDisplayColumn, PivotDisplayRow, PivotReportCopyBlock, PivotReportCopyCell, PivotReportCopyQuery, PivotReportCopyResult,
    PivotReportItemsQuery, PivotReportItemsResult, PivotReportMetadata, PivotReportRange, PivotReportRefusal, PivotReportRequest,
    PivotReportSettings, PivotReportState, PivotReportSummaryQuery, PivotReportSummaryResult, PivotReportUpdate, PivotReportWindow,
} from './contract';
import { checkLabelMetrics, PivotLabelSizing, sameLabelMetrics, type LabelBatch } from './label-metrics';

/** How a report source learns that its data changed (ADR-0153), declared: `incremental` when the Pivot
 * Source names its changes — Change Batches — and `fullRefresh` when it cannot, and a notice of newer data
 * asks it again ({@link PivotReportRequest.refreshData}). */
export type PivotReportUpdateMode = 'incremental' | 'fullRefresh';

/** The report boundary a Script Core asks (ADR-0151, ADR-0370 section 4): a Window of the report, and the
 * operations a Report Version names. An Engine Host that runs the engine in a Worker answers it across. */
export interface PivotReportSource {
    /** The fields offered to the Field List. */
    readonly fields: readonly PivotField[];
    /** The Aggregations offered. */
    readonly features: PivotSourceFeatures;
    /** How the source learns its data changed. */
    readonly updateMode: PivotReportUpdateMode;
    /** A Window of the report a request's layout makes, or a refusal by name. */
    window(request: PivotReportRequest, signal?: AbortSignal): Promise<PivotReportUpdate>;
    /** A field's Items, labelled and ordered as a held Report Version shows them. */
    items(query: PivotReportItemsQuery, signal?: AbortSignal): Promise<PivotReportItemsResult>;
    /** A field's Items as the Pivot Source lists them, under a Source Version. */
    rawItems(query: PivotItemsQuery, signal?: AbortSignal): Promise<PivotItemPage>;
    /** Every selected cell of a held Report Version, off-screen cells included. */
    copy(query: PivotReportCopyQuery, signal?: AbortSignal): Promise<PivotReportCopyResult>;
    /** The Selection Summary of a held Report Version. */
    summary(query: PivotReportSummaryQuery, signal?: AbortSignal): Promise<PivotReportSummaryResult>;
    /** A page of the records behind a cell, under the Source Version its question names (ADR-0151). */
    details(query: PivotDetailsQuery, signal?: AbortSignal): Promise<PivotDetailPage>;
    /** Tells `listener` each time the Pivot Source's data moves on; the function returned stops it. */
    subscribe(listener: (changed: PivotSourceChanged) => void): () => void;
    /** Releases the report's state; the Pivot Source is not its to release. */
    dispose(): void;
}

/** What a local report source is made with. */
export interface LocalPivotReportSourceOptions {
    /** The Order Keys a request's settings may name for a field, by name (ADR-0152). */
    readonly orderKeys?: Readonly<Record<string, PivotOrderKey>>;
    /** How many Report Versions are held for the versioned operations; 2 when left out. The reports the
     * Change Highlight compares are held as well, for as long as they are marked (ADR-0153). */
    readonly versionsKept?: number;
    /** The clock that times how long a change stays marked, in milliseconds; `Date.now` when left out. It
     * only bounds what is kept: when a highlight starts is the Consumer's ({@link PivotChangeTimes}). */
    readonly now?: () => number;
    /** How the work shares the thread (PV-40). */
    readonly slicing?: PivotSlicing;
}

// A Report Version held: its metadata, the report, the source and fields it was computed with, and the
// options it was laid out under.
interface HeldReport {
    readonly metadata: PivotReportMetadata;
    readonly report: PivotReport;
    readonly source: PivotSource;
    readonly fields: readonly PivotField[];
    readonly options: PivotOptions;
}

function keyId(key: PivotRowKey): string {
    return rowIdOf(key.role, key.valueField, key.items);
}

function displayId(row: PivotDisplayRow): string {
    return rowIdOf(row.role, row.valueField, row.items);
}

// A Source Version whose changes are marked: the report that shows it, the change's mark — the Report Version
// that first published it, kept when the same data is laid out again — and when this source published it, on
// this source's clock, which only bounds how long it is kept.
class HighlightVersion {
    readonly report: PivotReport;
    readonly mark: string;
    readonly at: number;
    readonly columns: ReadonlyMap<string, number>;
    #byKey: Map<string, PivotReportRow> | undefined;

    constructor(report: PivotReport, mark: string, at: number) {
        this.report = report;
        this.mark = mark;
        this.at = at;
        this.columns = new Map(report.valueColumns.map((column, index) => [column.name, index]));
    }

    row(key: PivotRowKey, id: string, hint: number): PivotReportRow | undefined {
        const rows = this.report.rows;
        const hinted = rows[hint];
        if (hinted?.key.equals(key) === true) return hinted;
        this.#byKey ??= new Map(rows.map(row => [keyId(row.key), row]));
        return this.#byKey.get(id);
    }
}

// A record's entries in one order; no record is no entries.
function sortedEntries<T>(record: Readonly<Record<string, T>> | undefined): [string, T][] {
    return Object.entries(record ?? {}).toSorted(([a], [b]) => (a < b ? -1 : a > b ? 1 : 0)); // oxlint-disable-line ex-grid/no-stand-in-default -- no words or policies are none
}

// Settings written in one form, so that two that lay a report out alike compare equal.
function settingsKey(settings: PivotReportSettings): string {
    const metrics = settings.labelMetrics;
    return JSON.stringify([
        optionsKeyOf(settings), highlightMs(settings), sortedEntries(settings.orderKeyPolicies),
        metrics === undefined ? null : [metrics.wideWidthPx, metrics.digitWidthPx, metrics.narrowWidthPx, metrics.fullWidthPx,
            metrics.otherWidthPx, metrics.cellHorizontalPaddingPx, sortedEntries(metrics.glyphWidths)],
    ]);
}

function sameSettings(a: PivotReportSettings, b: PivotReportSettings): boolean {
    return settingsKey(a) === settingsKey(b) && sameLabelMetrics(a.labelMetrics, b.labelMetrics);
}

function policiesKey(settings: PivotReportSettings): string {
    return JSON.stringify(sortedEntries(settings.orderKeyPolicies));
}

// What lays a report out alike: the locale and the words.
function optionsKeyOf(settings: PivotReportSettings): string {
    return JSON.stringify([settings.locale ?? null, sortedEntries(settings.words)]); // oxlint-disable-line ex-grid/no-stand-in-default -- no locale is the browser's
}

function highlightMs(settings: PivotReportSettings): number {
    return settings.changeHighlightMs ?? 1000; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0068: a highlight lasts a second unless told
}

// The settings as engine options: the locale, and the words given by id.
function optionsOf(settings: PivotReportSettings): PivotOptions {
    const words = settings.words;
    const given = words === undefined ? undefined : new Map(Object.entries(words));
    const label = given === undefined ? undefined : (id: string): string | undefined => given.get(id);
    return Object.freeze({
        ...(settings.locale === undefined ? {} : { locale: settings.locale }),
        ...(label === undefined ? {} : { label }),
    });
}

// Refuses settings that are not data the source can lay a report out under.
function checkSettings(settings: PivotReportSettings): void {
    const duration = settings.changeHighlightMs;
    if (duration !== undefined && (typeof duration !== 'number' || Number.isNaN(duration))) {
        throw new RangeError('A highlight lasts a number of milliseconds.');
    }
    for (const record of [settings.words, settings.orderKeyPolicies]) {
        for (const value of Object.values(record ?? {})) { // oxlint-disable-line ex-grid/no-stand-in-default -- no words or policies are none
            if (typeof value !== 'string') throw new TypeError('A word and an Order Key policy are named by strings.');
        }
    }
    if (settings.labelMetrics !== undefined) checkLabelMetrics(settings.labelMetrics);
}

// The settings as the source keeps them: a frozen copy, which a Consumer changing its own object afterwards
// cannot reach.
function frozenSettings(settings: PivotReportSettings): PivotReportSettings {
    const metrics = settings.labelMetrics;
    const glyphs = metrics?.glyphWidths;
    return Object.freeze({
        ...(settings.locale === undefined ? {} : { locale: settings.locale }),
        ...(settings.words === undefined ? {} : { words: Object.freeze({ ...settings.words }) }),
        ...(settings.changeHighlightMs === undefined ? {} : { changeHighlightMs: settings.changeHighlightMs }),
        ...(metrics === undefined ? {} : { labelMetrics: Object.freeze({ ...metrics, ...(glyphs === undefined ? {} : { glyphWidths: Object.freeze({ ...glyphs }) }) }) }),
        ...(settings.orderKeyPolicies === undefined ? {} : { orderKeyPolicies: Object.freeze({ ...settings.orderKeyPolicies }) }),
    });
}

function windowsEqual(a: PivotReportWindow, b: PivotReportWindow): boolean {
    return a.start === b.start && a.count === b.count;
}

// Two value cells alike as C#'s record compares them: their text, number, exact value and error.
function valuesEqual(a: PivotValue | undefined, b: PivotValue | undefined): boolean {
    if (a === undefined || b === undefined) return a === b;
    if (a.text !== b.text || a.number !== b.number || a.error !== b.error) return false;
    if (a.exact === undefined || b.exact === undefined) return a.exact === b.exact;
    return decimalEquals(a.exact, b.exact);
}

// A row as one version kept shows it, and as the version before it does.
interface MarkedPair {
    readonly newer: HighlightVersion;
    readonly newRow: PivotReportRow | undefined;
    readonly older: HighlightVersion;
    readonly oldRow: PivotReportRow | undefined;
}

// The change that last moved a cell's shown text, among the versions kept: the newest whose text differs from
// the version before it.
function changedIn(pairs: readonly MarkedPair[], column: string): string | undefined {
    for (const { newer, newRow, older, oldRow } of pairs) {
        const nc = newer.columns.get(column);
        if (newRow === undefined || nc === undefined) continue;
        const oc = older.columns.get(column);
        if (oldRow === undefined || oc === undefined
            || (newer.report.valueAt(newRow, nc)?.text ?? '') !== (older.report.valueAt(oldRow, oc)?.text ?? '')) { // oxlint-disable-line ex-grid/no-stand-in-default -- an empty cell's text is nothing, as the C# engine compares it
            return newer.mark;
        }
    }
    return undefined;
}

function notHeld(version: string): PivotReportRefusal {
    return Object.freeze({ kind: 'reportVersionNotHeld', message: `Report Version '${version}' is no longer held.` });
}

function orderKeyFailed(failed: PivotOrderKeyError): PivotReportRefusal {
    return Object.freeze({ kind: 'orderKeyFailed', message: failed.message, field: failed.field });
}

function invalid(message: string): PivotReportRefusal {
    return Object.freeze({ kind: 'invalidRequest', message });
}

function newVersion(): string {
    return globalThis.crypto.randomUUID().replaceAll('-', '');
}

// A number as .NET's invariant ToString writes a double: its shortest round-trip text.
function invariantText(value: number): string {
    return formatDouble(value, 'R', invariantNumbers);
}

// The report's cell in a column of the selection: a label column's index, or a value column's.
interface SelectedColumn {
    readonly value: boolean;
    readonly index: number;
    readonly header: string;
    readonly headerNumber: number | undefined;
}

function selectedColumns(report: PivotReport, names: readonly string[]): readonly SelectedColumn[] {
    const all = new Map<string, SelectedColumn>();
    report.labelColumns.forEach((c, index) => all.set(c.name, { value: false, index, header: c.header, headerNumber: undefined }));
    report.valueColumns.forEach((c, index) => all.set(c.name, { value: true, index, header: c.header, headerNumber: c.headerNumber }));
    return names.map(name => defined(all.get(name), `the column '${name}'`));
}

function checkRanges(report: PivotReport, names: readonly string[], ranges: readonly PivotReportRange[], maxCells: number): PivotReportRefusal | undefined {
    const known = new Set([...report.labelColumns.map(c => c.name), ...report.valueColumns.map(c => c.name)]);
    if (names.some(name => !known.has(name)) || new Set(names).size !== names.length) {
        return invalid('The selection names an unknown or repeated column.');
    }
    let count = 0;
    for (const r of ranges) {
        if (r.top < 0 || r.left < 0 || r.bottom < r.top || r.right < r.left || r.bottom >= report.rows.length || r.right >= names.length) {
            return invalid('The selection is outside the requested report.');
        }
        const cells = (r.bottom - r.top + 1) * (r.right - r.left + 1);
        if (cells > maxCells - count) return invalid(`Copy exceeds the limit of ${maxCells} cells.`);
        count += cells;
    }
    return undefined;
}

function checkRangeShape(ranges: unknown): void {
    if (!Array.isArray(ranges)) throw new TypeError('A selection is a list of rectangles.');
    for (const range of ranges as readonly unknown[]) {
        const r = range as Partial<Record<keyof PivotReportRange, unknown>>; // oxlint-disable-line typescript/no-unsafe-type-assertion -- each edge is checked here
        if (typeof range !== 'object' || range === null || ![r.top, r.left, r.bottom, r.right].every(edge => Number.isSafeInteger(edge))) {
            throw new TypeError('A rectangle of a selection has whole-number edges.');
        }
    }
}

// The cells of the union of the rectangles, each once, row by row: the rows cut into bands where a rectangle
// starts or ends, each band's column intervals merged, so a large selection allocates no entry per cell.
function* selectedCells(ranges: readonly PivotReportRange[]): Generator<readonly [number, number]> {
    const boundaries = [...new Set(ranges.flatMap(r => [r.top, r.bottom + 1]))].toSorted((a, b) => a - b);
    for (let b = 0; b + 1 < boundaries.length; b++) {
        const top = elementAt(boundaries, b);
        const intervals = ranges.filter(r => r.top <= top && r.bottom >= top).toSorted((x, y) => x.left - y.left);
        const merged: [number, number][] = [];
        for (const interval of intervals) {
            const lastInterval = merged.at(-1);
            if (lastInterval !== undefined && interval.left <= lastInterval[1] + 1) lastInterval[1] = Math.max(lastInterval[1], interval.right);
            else merged.push([interval.left, interval.right]);
        }
        for (let r = top; r < elementAt(boundaries, b + 1); r++) {
            for (const [left, right] of merged) {
                for (let c = left; c <= right; c++) yield [r, c];
            }
        }
    }
}

// The cell a copy carries: a number Item's label carries its number in full, as a value cell does (PV-7,
// ADR-0005); any other label its text; a value cell its raw, locale-free number or its error.
function copiedCell(report: PivotReport, row: PivotReportRow, column: SelectedColumn): PivotReportCopyCell {
    if (!column.value) {
        const label = elementAt(row.labels, column.index);
        const text = label.text ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- an empty label copies as nothing, as the C# engine copies it
        if (label.number !== undefined) return Object.freeze({ text, raw: invariantText(label.number), isNumber: true });
        return Object.freeze(label.text === undefined ? { text, isNumber: false } : { text, raw: label.text, isNumber: false });
    }
    const cell = report.valueAt(row, column.index);
    if (cell === undefined) return Object.freeze({ text: '', isNumber: false });
    const raw = cell.error ?? (cell.exact === undefined ? invariantText(cell.number) : decimalText(cell.exact));
    return Object.freeze({ text: cell.text, raw, isNumber: cell.error === undefined });
}

function exactOrDouble(inexact: boolean, exact: SnapshotDecimal, value: number): PivotNumber {
    return Object.freeze(inexact ? { kind: 'double', value } : { kind: 'exact', value: exact });
}

// What the computation returned that no published version shows yet: a request cancelled after its computation
// returned and before its version was published, or a version laid out aside. The next computation's result is
// merged into it, and published with it.
class Unpublished {
    reset = false;
    rowSequenceChanged = false;
    readonly batches: LabelBatch[] = [];

    add(result: PivotComputationResult): void {
        this.rowSequenceChanged ||= result.rowSequenceChanged;
        if (result.isReset) {
            this.reset = true;
            this.batches.length = 0;
        } else if (!this.reset) {
            this.batches.push({ labels: result.labelChanges, removed: result.removedRows });
        }
    }
}

/**
 * A report source over a Pivot Source in memory — a Snapshot's, records', or a server's behind a fetching
 * source (ADR-0151). Each request lays the report out afresh where its question, layout or data changed,
 * answers the Window asked for with every row projected from the newest report, and holds the last
 * `versionsKept` Report Versions for the operations that name one. A row the held Window shows alike is the
 * very row it holds. A cell whose shown text a change of the data moved is marked with the version that
 * published the change, for as long as the settings' highlight lasts (ADR-0068, ADR-0153).
 *
 * Requests are answered one at a time, in the order made. Each runs its pass, cube, report and label widths
 * on one Slicer (PV-40), and a cancelled one rejects with its signal's reason, leaving the versions held as
 * they were. A versioned operation reads the version it names and never waits on a newer computation.
 */
export class LocalPivotReportSource implements PivotReportSource {
    readonly #source: PivotSource;
    readonly #orderKeys: ReadonlyMap<string, PivotOrderKey>;
    readonly #versionsKept: number;
    readonly #now: () => number;
    readonly #slicing: CheckedSlicing;
    readonly #listeners = new Set<(changed: PivotSourceChanged) => void>();
    readonly #unsubscribe: (() => void) | undefined;
    readonly #versions: HeldReport[] = [];
    readonly #windows = new Map<string, PivotReportState>();
    readonly #waiting: (() => void)[] = [];
    #highlight: HighlightVersion[] = [];
    #busy = false;
    #disposed = false;
    #dirty = true;
    #effectiveFields: readonly PivotField[];
    #settings: string | undefined;
    #layout: string | undefined;
    #maxLeaves = -1;
    // The computation, under the Order Key policies it orders by; what it returned that no version shows yet;
    // and the label widths kept across versions.
    #session: PivotComputationSession | undefined;
    #policies: string | undefined;
    #unpublished: Unpublished | undefined;
    readonly #labelSizing = new PivotLabelSizing();

    /** A report source over `source`, which it does not own. Refuses, with a RangeError, a `versionsKept` that
     * is not a whole number of at least 1. */
    constructor(source: PivotSource, options: LocalPivotReportSourceOptions = {}) {
        const kept = options.versionsKept ?? 2; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0153: two Report Versions unless told
        if (!Number.isSafeInteger(kept) || kept < 1) throw new RangeError(`A report source holds at least one Report Version, not ${String(kept)}.`);
        const keys = options.orderKeys ?? {}; // oxlint-disable-line ex-grid/no-stand-in-default -- no Order Keys registered are none
        for (const [name, key] of Object.entries(keys)) {
            if (typeof key !== 'function') throw new TypeError(`The Order Key '${name}' is a function.`);
        }
        const now = options.now ?? Date.now;
        if (typeof now !== 'function') throw new TypeError("A report source's clock is a function.");
        this.#source = source;
        this.#effectiveFields = source.fields;
        this.#orderKeys = new Map(Object.entries(keys));
        this.#versionsKept = kept;
        this.#now = now;
        this.#slicing = checkedSlicing(options.slicing);
        this.#unsubscribe = source instanceof SnapshotPivotSource ? source.subscribe(changed => { this.#sourceChanged(changed); }) : undefined;
    }

    /** The Pivot Source's fields. */
    get fields(): readonly PivotField[] {
        return this.#source.fields;
    }

    /** The Pivot Source's Aggregations. */
    get features(): PivotSourceFeatures {
        return this.#source.features;
    }

    /** `incremental` over a Snapshot, whose Change Batches name what changed; `fullRefresh` otherwise. */
    get updateMode(): PivotReportUpdateMode {
        return this.#source instanceof SnapshotPivotSource ? 'incremental' : 'fullRefresh';
    }

    #sourceChanged(changed: PivotSourceChanged): void {
        this.#dirty = true;
        for (const listener of Array.from(this.#listeners)) {
            try {
                listener(changed);
            } catch (error) { // oxlint-disable-line ex-grid/no-catch -- every listener is told whatever one does; its error is reported, as an event listener's is, not swallowed
                queueMicrotask(() => {
                    throw error;
                });
            }
        }
    }

    /** Tells `listener` each new Source Version of a Pivot Source over a Snapshot; the function returned stops
     * telling it. */
    subscribe(listener: (changed: PivotSourceChanged) => void): () => void {
        if (typeof listener !== 'function') throw new TypeError('A listener is a function.');
        const held = (changed: PivotSourceChanged): void => {
            listener(changed);
        };
        this.#listeners.add(held);
        return () => {
            this.#listeners.delete(held);
        };
    }

    #checkOpen(): void {
        if (this.#disposed) throw new Error('The report source is disposed; make another over the Pivot Source.');
    }

    async #enter(signal: AbortSignal | undefined): Promise<void> {
        if (this.#busy) {
            await new Promise<void>(resolve => {
                this.#waiting.push(resolve);
            });
        }
        this.#busy = true;
        if (signal?.aborted === true || this.#disposed) {
            this.#leave();
            signal?.throwIfAborted();
            this.#checkOpen();
        }
    }

    #leave(): void {
        const next = this.#waiting.shift();
        if (next === undefined) this.#busy = false;
        else next();
    }

    /**
     * A Window of the report the request's layout makes from the newest data — or, for a layout gesture that
     * names the Window it holds, from the data that Window shows — or a refusal by name: of the Pivot Source,
     * of an Order Key policy not registered, or of an Order Key that failed on an Item. Rejects with what a
     * layout the source cannot lay out throws, and with the signal's reason once it is aborted.
     */
    async window(request: PivotReportRequest, signal?: AbortSignal): Promise<PivotReportUpdate> {
        this.#checkOpen();
        const window = Object.freeze({ start: request.window.start, count: request.window.count });
        if (!Number.isSafeInteger(window.start) || !Number.isSafeInteger(window.count)) throw new TypeError('A report Window is whole numbers of rows.');
        if (window.start < 0 || window.count < 0) return Object.freeze({ requestId: request.requestId, window, refusal: invalid('The report Window is negative.') });
        checkSettings(request.settings);
        // The request as it was made: the version published keeps its layout and settings, which the Consumer's
        // own objects, changed afterwards, do not reach.
        const made: PivotReportRequest = Object.freeze({
            ...request, window, settings: frozenSettings(request.settings), layout: readPivotLayoutJson(writePivotLayoutJson(request.layout)),
        });
        await this.#enter(signal);
        try {
            return await this.#window(made, new Slicer(this.#slicing, signal));
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- an Order Key's failure is the request's refusal by name (ADR-0152); anything else is thrown on
            this.#dirty = true;
            if (error instanceof PivotOrderKeyError) return Object.freeze({ requestId: request.requestId, window, refusal: orderKeyFailed(error) });
            throw error;
        } finally {
            this.#leave();
        }
    }

    async #window(request: PivotReportRequest, slicer: Slicer): Promise<PivotReportUpdate> {
        const settings = settingsKey(request.settings);
        const layout = writePivotLayoutJson(request.layout);
        const maxLeaves = request.maxLeaves ?? defaultMaxLeaves;
        const markChanges = request.markChanges !== false;
        const refusedWith = (refusal: PivotReportRefusal): PivotReportUpdate => Object.freeze({ requestId: request.requestId, window: request.window, refusal });
        // The fields under the request's Order Key policies, adopted only with the version they publish: a request
        // refused or cancelled leaves the next one ordered by the policies its own settings name.
        let fields = this.#effectiveFields;
        if (this.#settings !== settings) {
            const resolvedFields = this.#resolveFields(request.settings);
            if ('kind' in resolvedFields) return refusedWith(resolvedFields);
            fields = resolvedFields;
        }
        let last = this.#versions[0];
        // The Window the Consumer holds, read before a publication below evicts its version.
        const baseline = request.baseline === undefined ? undefined : this.#windows.get(request.baseline);
        const previous = baseline !== undefined && windowsEqual(baseline.window, request.window) ? baseline : undefined;
        if (request.refreshData === true || this.#dirty || this.#unpublished !== undefined || this.#settings !== settings || this.#layout !== layout
            || this.#maxLeaves !== maxLeaves || last === undefined) {
            const wasDirty = this.#dirty;
            this.#dirty = false;
            const resolved = resolveOptions(optionsOf(request.settings));
            const policies = policiesKey(request.settings);
            if (this.#session === undefined || this.#policies !== policies) {
                this.#session?.dispose();
                this.#session = new PivotComputationSession(this.#source, fields);
                this.#policies = policies;
            }
            let report: PivotReport;
            let detached = false;
            const shown = this.#shown(request, policies, maxLeaves, markChanges);
            if (shown !== undefined) {
                // A layout gesture lays out the data the Consumer shows — the request's baseline — which is older
                // than the newest computation: a version published for a request the Consumer discarded. Laid out
                // aside, that version is what the gesture shows; the newer data comes with the next request that
                // marks changes.
                checkCubeHolds(shown, request.layout);
                report = await slicer.run(buildReportSteps(shown, request.layout, resolved));
                (this.#unpublished ??= new Unpublished()).reset = true;
                detached = true;
            } else {
                const result = await this.#session.compute({
                    layout: request.layout, options: resolved, optionsKey: optionsKeyOf(request.settings), maxLeaves, slicing: this.#slicing, slicer,
                    refreshData: request.refreshData === true, preferHeld: !markChanges,
                });
                if (result.report === undefined) {
                    this.#dirty = true;
                    const computed = defined(result.refusal, "the computation's refusal");
                    return refusedWith(Object.freeze({ kind: 'sourceRefused', message: computed.message, ...(computed.field === undefined ? {} : { field: computed.field }), sourceRefusal: computed }));
                }
                // Kept until a version publishes it: a cancellation below must not lose the computation's changes,
                // which it returns once.
                (this.#unpublished ??= new Unpublished()).add(result);
                report = result.report;
            }
            const unpublished = this.#unpublished;
            const cube = report.cube;
            if (!markChanges && wasDirty && last?.metadata.sourceVersion === cube.sourceVersion) this.#dirty = true;
            const sameRows = last !== undefined && (last.report === report
                || (!unpublished.reset ? !unpublished.rowSequenceChanged : await slicer.run(sameRowsSteps(report, last.report))));
            const metrics = request.settings.labelMetrics;
            let widths: readonly number[] = Object.freeze([]);
            if (metrics === undefined) this.#labelSizing.forget();
            else widths = await slicer.run(this.#labelSizing.widthSteps(report, metrics, unpublished.batches, unpublished.reset));
            // Nothing below awaits: the version is published whole, or not at all; and not by a source disposed
            // while it was computed.
            this.#checkOpen();
            const version = newVersion();
            const marks = this.#rememberHighlight(version, report, request, markChanges, last);
            const metadata: PivotReportMetadata = Object.freeze({
                version,
                sourceVersion: cube.sourceVersion,
                rowSequenceVersion: last !== undefined && sameRows ? last.metadata.rowSequenceVersion : newVersion(),
                layout: request.layout,
                settings: request.settings,
                rowCount: report.rows.length,
                labelColumns: report.labelColumns,
                valueColumns: Object.freeze(report.valueColumns.map((column, index): PivotDisplayColumn => Object.freeze({
                    name: column.name, header: column.header, role: column.role, valueField: column.valueField, columnPath: report.columnPath(index),
                }))),
                headerSpans: report.headerSpans,
                headerTierCount: report.headerTierCount,
                valueCaptions: report.valueCaptions,
                labelWidths: widths,
                changeMarks: marks,
            });
            last = Object.freeze({ metadata, report, source: this.#source, fields, options: optionsOf(request.settings) });
            this.#versions.unshift(last);
            while (this.#versions.length > this.#versionsKept) this.#windows.delete(defined(this.#versions.pop(), 'the oldest version').metadata.version);
            this.#settings = settings;
            this.#effectiveFields = fields;
            this.#layout = layout;
            this.#maxLeaves = maxLeaves;
            this.#unpublished = undefined;
            if (detached) {
                // The computation holds newer data than this version, under the layout it last laid out: its next
                // result is compared with this version afresh, and the next request computes.
                this.#unpublished = new Unpublished();
                this.#unpublished.reset = true;
                this.#dirty = true;
            }
        }
        const comparable = previous !== undefined && sameSettings(previous.metadata.settings, request.settings)
            && writePivotLayoutJson(previous.metadata.layout) === layout;
        const priorById = comparable ? new Map(previous.rows.map(row => [displayId(row), row])) : undefined;
        const priorColumns = previous === undefined ? undefined : new Map(previous.metadata.valueColumns.map((column, index) => [column.name, index]));
        const start = request.window.start;
        const count = Math.min(request.window.count, Math.max(0, last.metadata.rowCount - start));
        this.#pruneHighlight(this.#now(), highlightMs(request.settings));
        const rows: PivotDisplayRow[] = [];
        for (let i = 0; i < count; i++) {
            const row = elementAt(last.report.rows, start + i);
            const prior = priorById?.get(keyId(row.key));
            rows.push(this.#project(last.report, row, start + i, prior, comparable && markChanges, priorColumns));
        }
        const state: PivotReportState = Object.freeze({ metadata: last.metadata, window: request.window, rows: Object.freeze(rows) });
        this.#windows.set(last.metadata.version, state);
        return Object.freeze({ requestId: request.requestId, ...state });
    }

    // The cube of the version the request names as its baseline — the data the Consumer shows — when a layout
    // gesture should lay it out rather than the newest computation's: held, from this Pivot Source under the
    // same Order Key policies, older than the newest cube, and holding the layout.
    #shown(request: PivotReportRequest, policies: string, maxLeaves: number, markChanges: boolean): PivotCube | undefined {
        const newest = this.#session?.heldCube;
        if (markChanges || request.baseline === undefined || newest === undefined) return undefined;
        const shown = this.#held(request.baseline);
        if (shown === undefined || shown.report.cube === newest || shown.source !== this.#source || policiesKey(shown.metadata.settings) !== policies) return undefined;
        const cube = shown.report.cube;
        return cube.holds(request.layout) && cube.query.maxLeaves === maxLeaves ? cube : undefined;
    }

    // The fields under the settings' Order Key policies, or the refusal of a policy that names an unknown field
    // or an Order Key not registered.
    #resolveFields(settings: PivotReportSettings): readonly PivotField[] | PivotReportRefusal {
        const policies = new Map(Object.entries(settings.orderKeyPolicies ?? {})); // oxlint-disable-line ex-grid/no-stand-in-default -- no policies are none
        const fields = this.fields;
        for (const name of policies.keys()) {
            if (!fields.some(field => field.name === name)) {
                return Object.freeze({ kind: 'unknownOrderKeyPolicy', message: `The Order Key policy names unknown field '${name}'.`, field: name });
            }
        }
        const effective: PivotField[] = [];
        for (const field of fields) {
            const id = policies.get(field.name);
            if (id === undefined) {
                effective.push(field);
                continue;
            }
            const key = this.#orderKeys.get(id);
            if (key === undefined) {
                return Object.freeze({ kind: 'unknownOrderKeyPolicy', message: `The Order Key policy '${id}' for '${field.name}' is not registered.`, field: field.name });
            }
            effective.push(Object.freeze({ ...field, orderKey: key }));
        }
        return Object.freeze(effective);
    }

    // A row of the Window, made from the report: the prior row itself when nothing it paints changed. A cell
    // keeps the mark it had while its text does not change, though the source no longer lists that change, so
    // that the row keeps its instance rather than paint again for a highlight that has ended.
    #project(report: PivotReport, row: PivotReportRow, rowIndex: number, prior: PivotDisplayRow | undefined, keepMarks: boolean,
        priorColumns: ReadonlyMap<string, number> | undefined): PivotDisplayRow {
        const path = report.rowPath(row);
        const columns = report.valueColumns;
        const values: (PivotValue | undefined)[] = [];
        const changed: (string | undefined)[] = [];
        const key = row.key;
        const marked = this.#highlightRows(key, keyId(key), rowIndex);
        let same = prior !== undefined && prior.role === row.role && prior.valueField === row.valueField
            && prior.carriesValues === row.carriesValues && prior.labels.length === row.labels.length
            && prior.labels.every((label, i) => rowLabelsAlike(label, elementAt(row.labels, i))) && prior.values.length === columns.length
            && prior.rowPath.length === path.length
            // By spelling, not as Items match: a parent's new spelling reaches its child's path (ADR-0153).
            && prior.rowPath.every((step, i) => step.field === elementAt(path, i).field && step.item.kind === elementAt(path, i).item.kind
                && step.item.value === elementAt(path, i).item.value);
        for (let c = 0; c < columns.length; c++) {
            const name = elementAt(columns, c).name;
            const value = report.valueAt(row, c);
            values.push(value);
            let mark = changedIn(marked, name);
            const oldColumn = priorColumns?.get(name);
            if (prior !== undefined && oldColumn !== undefined && oldColumn < prior.values.length) {
                if (mark === undefined && keepMarks) mark = prior.changedIn[oldColumn];
                same &&= oldColumn === c && valuesEqual(prior.values[oldColumn], value) && prior.changedIn[oldColumn] === mark;
            }
            changed.push(mark);
        }
        if (same && prior !== undefined) return prior;
        return Object.freeze({
            role: row.role, valueField: row.valueField, items: key.items, carriesValues: row.carriesValues, labels: row.labels,
            values: Object.freeze(values), rowPath: path, changedIn: Object.freeze(changed),
        });
    }

    // Keeps the reports of the Source Versions whose marks can still be shown, and their baseline; answers the
    // marks the source keeps now, newest first.
    #rememberHighlight(version: string, report: PivotReport, request: PivotReportRequest, markChanges: boolean, previous: HeldReport | undefined): readonly string[] {
        const at = this.#now();
        const first = this.#highlight[0];
        if (!markChanges || previous === undefined || writePivotLayoutJson(previous.metadata.layout) !== writePivotLayoutJson(request.layout)
            || !sameSettings(previous.metadata.settings, request.settings)) {
            this.#highlight = [];
        } else if (previous.metadata.sourceVersion === report.cube.sourceVersion && first !== undefined) {
            // The same data laid out again: its changes keep the mark and the time they were first published with.
            this.#highlight[0] = new HighlightVersion(report, first.mark, first.at);
            return this.#marks();
        }
        this.#highlight.unshift(new HighlightVersion(report, version, at));
        this.#pruneHighlight(at, highlightMs(request.settings));
        return this.#marks();
    }

    // The versions a cell can be marked with: each but the oldest, which is only the baseline the next newer
    // one is compared with.
    #marks(): readonly string[] {
        return Object.freeze(this.#highlight.slice(0, Math.max(0, this.#highlight.length - 1)).map(h => h.mark));
    }

    #pruneHighlight(now: number, duration: number): void {
        if (duration <= 0) {
            this.#highlight.splice(1);
            return;
        }
        for (let i = 0; i < this.#highlight.length - 1; i++) {
            if (now - elementAt(this.#highlight, i).at >= duration) {
                this.#highlight.splice(i + 1);
                break;
            }
        }
    }

    // The row in each version kept, paired with the row in the version before it, for the marks of its cells.
    #highlightRows(key: PivotRowKey, id: string, rowIndex: number): readonly MarkedPair[] {
        const pairs: MarkedPair[] = [];
        for (let i = 0; i + 1 < this.#highlight.length; i++) {
            const newer = elementAt(this.#highlight, i);
            const older = elementAt(this.#highlight, i + 1);
            pairs.push({ newer, newRow: newer.row(key, id, rowIndex), older, oldRow: older.row(key, id, rowIndex) });
        }
        return pairs;
    }

    #held(version: string): HeldReport | undefined {
        this.#checkOpen();
        return this.#versions.find(held => held.metadata.version === version);
    }

    /** A field's Items as the Pivot Source lists them, under a Source Version. */
    rawItems(query: PivotItemsQuery, signal?: AbortSignal): Promise<PivotItemPage> {
        this.#checkOpen();
        return this.#source.items(query, signal);
    }

    /** A field's Items labelled and ordered as a held Report Version shows them, under its Source Version; refused
     * by name for a version not held, a field not declared, the Pivot Source's refusal, and an Order Key that
     * fails on an Item. */
    async items(query: PivotReportItemsQuery, signal?: AbortSignal): Promise<PivotReportItemsResult> {
        signal?.throwIfAborted();
        const held = this.#held(query.version);
        const refused = (sourceVersion: string, refusal: PivotReportRefusal): PivotReportItemsResult =>
            Object.freeze({ version: query.version, sourceVersion, items: Object.freeze([]), total: 0, refusal });
        if (held === undefined) return refused('', notHeld(query.version));
        const field = held.fields.find(f => f.name === query.field);
        const max = query.max ?? 10_000; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0061: Filter… lists 10,000 Items unless told
        if (field === undefined || !Number.isSafeInteger(max) || max < 0) {
            return refused(held.metadata.sourceVersion, invalid('Items needs a declared field and nonnegative cap.'));
        }
        const page = await held.source.items(pivotItemsQuery({
            field: query.field, sourceVersion: held.metadata.sourceVersion, max, ...(query.search === undefined ? {} : { search: query.search }),
        }), signal);
        if (page.refusal !== undefined) {
            return refused(held.metadata.sourceVersion, Object.freeze({ kind: 'sourceRefused', message: page.refusal.message, sourceRefusal: page.refusal }));
        }
        let items;
        try {
            items = itemsOfPage(page, held.report.layout, field, held.options);
        } catch (error) { // oxlint-disable-line ex-grid/no-catch -- an Order Key's failure is the listing's refusal by name (ADR-0152); anything else is thrown on
            if (error instanceof PivotOrderKeyError) return refused(held.metadata.sourceVersion, orderKeyFailed(error));
            throw error;
        }
        return Object.freeze({ version: query.version, sourceVersion: page.sourceVersion, items, total: page.total });
    }

    /** A page of the records behind a cell under the Source Version its question names (ADR-0151): answered while
     * the Pivot Source holds that version, however many layouts came since, and refused by it once it does not. */
    details(query: PivotDetailsQuery, signal?: AbortSignal): Promise<PivotDetailPage> {
        this.#checkOpen();
        return this.#source.details(query, signal);
    }

    /** Every selected cell of a held Report Version, each rectangle whole, in the order asked; refused by name for
     * a version not held, a column not the report's or named twice, a rectangle outside the report, and a copy
     * past `maxCells` — never cut short (spine 1). */
    async copy(query: PivotReportCopyQuery, signal?: AbortSignal): Promise<PivotReportCopyResult> {
        signal?.throwIfAborted();
        checkRangeShape(query.ranges);
        const held = this.#held(query.version);
        const refused = (refusal: PivotReportRefusal): PivotReportCopyResult => Object.freeze({ version: query.version, blocks: Object.freeze([]), refusal });
        if (held === undefined) return refused(notHeld(query.version));
        const maxCells = query.maxCells ?? 1_000_000; // oxlint-disable-line ex-grid/no-stand-in-default -- ADR-0152: a million cells unless told
        if (!Number.isSafeInteger(maxCells) || maxCells < 0) return refused(invalid('Copy needs a whole, nonnegative cap on its cells.'));
        const issue = checkRanges(held.report, query.columns, query.ranges, maxCells);
        if (issue !== undefined) return refused(issue);
        const blocks = await new Slicer(this.#slicing, signal).run(copySteps(held.report, selectedColumns(held.report, query.columns), query.ranges));
        return Object.freeze({ version: query.version, blocks });
    }

    /** The Selection Summary of a held Report Version: Count, Numerical Count, Sum, Min and Max, a cell under two
     * rectangles counted once, shown in the Focus value's format (ADR-0130). */
    async summary(query: PivotReportSummaryQuery, signal?: AbortSignal): Promise<PivotReportSummaryResult> {
        signal?.throwIfAborted();
        checkRangeShape(query.ranges);
        const held = this.#held(query.version);
        const refused = (refusal: PivotReportRefusal): PivotReportSummaryResult =>
            Object.freeze({ version: query.version, values: 0, numbers: 0, hasError: false, locale: '', refusal });
        if (held === undefined) return refused(notHeld(query.version));
        const issue = checkRanges(held.report, query.columns, query.ranges, Number.MAX_SAFE_INTEGER);
        if (issue !== undefined) return refused(issue);
        const report = held.report;
        const columns = selectedColumns(report, query.columns);
        const figures = await new Slicer(this.#slicing, signal).run(summarySteps(report, columns, query.ranges));
        // Shown in the Focus cell's Value Field format — Excel's status bar shows its figures in the active cell's
        // format — and G15 under General, as ExSheet's status bar writes a General cell's (ADR-0130).
        let numberFormat: string | undefined;
        const focusRow = query.focusRow;
        const focusColumn = query.focusColumn;
        const focus = focusColumn === undefined ? undefined : columns[focusColumn];
        if (focusRow !== undefined && focusRow >= 0 && focusRow < report.rows.length && focus?.value === true) {
            const vf = report.valueFieldAt(elementAt(report.rows, focusRow), focus.index);
            if (vf >= 0) {
                const field = elementAt(report.layout.values, vf);
                numberFormat = field.numberFormat ?? (field.showValuesAs === 'noCalculation' ? 'G15' : '0.00%');
            }
        }
        return Object.freeze({ version: query.version, ...figures, ...(numberFormat === undefined ? {} : { numberFormat }), locale: report.locale });
    }

    /** Releases the report's state and stops hearing the Pivot Source, which it does not own: every later call
     * throws. */
    dispose(): void {
        if (this.#disposed) return;
        this.#disposed = true;
        this.#unsubscribe?.();
        this.#listeners.clear();
        this.#versions.length = 0;
        this.#windows.clear();
        this.#highlight = [];
        this.#session?.dispose();
        this.#session = undefined;
        this.#unpublished = undefined;
    }
}

function* copySteps(report: PivotReport, columns: readonly SelectedColumn[], ranges: readonly PivotReportRange[]): Generator<void, readonly PivotReportCopyBlock[]> {
    const blocks: PivotReportCopyBlock[] = [];
    let cells = 0;
    for (const range of ranges) {
        const rows: (readonly PivotReportCopyCell[])[] = [];
        for (let r = range.top; r <= range.bottom; r++) {
            const row = elementAt(report.rows, r);
            const copied: PivotReportCopyCell[] = [];
            for (let c = range.left; c <= range.right; c++) copied.push(copiedCell(report, row, elementAt(columns, c)));
            rows.push(Object.freeze(copied));
            cells += copied.length;
            if (cells >= pieceUnits) {
                cells = 0;
                yield;
            }
        }
        const spanned = columns.slice(range.left, range.right + 1);
        // A number column Item's header carries its number in full for the HTML flavour (ADR-0005's note of
        // 2026-10-13).
        const raws = spanned.map(c => (c.headerNumber === undefined ? undefined : invariantText(c.headerNumber)));
        blocks.push(Object.freeze({
            headers: Object.freeze(spanned.map(c => c.header)), rows: Object.freeze(rows),
            ...(raws.some(raw => raw !== undefined) ? { headerRaws: Object.freeze(raws) } : {}),
        }));
    }
    return Object.freeze(blocks);
}

function* summarySteps(report: PivotReport, columns: readonly SelectedColumn[], ranges: readonly PivotReportRange[]): Generator<void, {
    readonly values: number; readonly numbers: number; readonly hasError: boolean; readonly sum?: PivotNumber; readonly min?: PivotNumber; readonly max?: PivotNumber;
}> {
    let values = 0;
    let numbers = 0;
    let hasError = false;
    const sum = noSum();
    const extremes = noExtremes();
    let cells = 0;
    for (const [r, c] of selectedCells(ranges)) {
        if (++cells >= pieceUnits) {
            cells = 0;
            yield;
        }
        const row = elementAt(report.rows, r);
        const column = elementAt(columns, c);
        if (!column.value) {
            const text = elementAt(row.labels, column.index).text;
            if (text !== undefined && text !== '') values++;
            continue;
        }
        const value = report.valueAt(row, column.index);
        if (value === undefined) continue;
        values++;
        if (value.error !== undefined) {
            hasError = true;
            continue;
        }
        const first = numbers === 0;
        if (value.exact === undefined) {
            addDoubleToSum(sum, value.number, first);
            addDoubleToExtremes(extremes, value.number, first);
        } else {
            addExactToSum(sum, value.exact, first);
            addExactToExtremes(extremes, value.exact, first);
        }
        numbers++;
    }
    finishSum(sum);
    finishExtremes(extremes);
    if (numbers === 0) return Object.freeze({ values, numbers, hasError });
    return Object.freeze({
        values, numbers, hasError,
        sum: exactOrDouble(sum.inexact, sum.exact, sum.double),
        min: exactOrDouble(extremes.inexact, extremes.exactMin, extremes.min),
        max: exactOrDouble(extremes.inexact, extremes.exactMax, extremes.max),
    });
}
