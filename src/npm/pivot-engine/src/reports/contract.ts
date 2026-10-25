// What a report source is asked and answers (ADR-0151, ADR-0152, ADR-0153), as ADR-0370 section 4 leaves
// them: a Window of a report's rows, detached from the report, with the whole report's extent and columns, under
// a Report Version that versioned operations — Copy, the Selection Summary, a field's Items — name back. Every
// value here is plain data, so a Window crosses to a Web Worker and back as it is. ADR-0151's Window Changes
// and their digest end with the server's report Windows (ADR-0370 section 3): a Window is answered whole.
// The C# engine's PivotReportContract and PivotReportOperations.

import type { PivotSourceRefusal } from '../cube/answer';
import { placedFields, pivotDetailsQuery, queryForLayout, type PivotDetailsQuery, type PivotFieldItem } from '../cube/query';
import type { PivotItemInfo } from '../engine';
import type { PivotItemKey } from '../items/item-key';
import type { PivotColumnRole, PivotRowRole } from '../layout/pivot-enums';
import type { PivotLayout } from '../layout/pivot-layout';
import type { PivotNumber } from '../cube/answer';
import type { PivotHeaderSpan, PivotLabelColumn, PivotRowLabel } from '../report/report';
import type { PivotValue } from '../report/value';
import type { PivotLabelMetrics } from './label-metrics';

/** The rows of a report asked for: from `start`, at most `count` of them. The whole report's row count is
 * the metadata's. */
export interface PivotReportWindow {
    /** The first row, from 0. */
    readonly start: number;
    /** The most rows. */
    readonly count: number;
}

/** A report's display settings, chosen explicitly: a locale never chooses the words (ADR-0151). */
export interface PivotReportSettings {
    /** The locale numbers and dates are written in and text is ordered by; the browser's language when left
     * out (ADR-0370 section 10). */
    readonly locale?: string;
    /** The report's words by id, for those the Consumer translates; English for the rest. */
    readonly words?: Readonly<Record<string, string>>;
    /** How long a change of the data stays marked for a cell first asked for after it, in milliseconds;
     * 1,000 when left out (ADR-0068). */
    readonly changeHighlightMs?: number;
    /** The label geometry the label columns' widths are estimated under, or none when no widths are wanted. */
    readonly labelMetrics?: PivotLabelMetrics;
    /** The Order Key a field's Items are ordered by, by the field's name: one of those the report source was
     * given, by its name (ADR-0152). */
    readonly orderKeyPolicies?: Readonly<Record<string, string>>;
}

/** What a report source is asked: a Window of the report a layout makes, under display settings. */
export interface PivotReportRequest {
    /** The request's identity, which its answer names back. */
    readonly requestId: string;
    /** The report's View State. */
    readonly layout: PivotLayout;
    /** Its locale, words, label geometry and Order Key policies. */
    readonly settings: PivotReportSettings;
    /** The rows wanted. */
    readonly window: PivotReportWindow;
    /** The Report Version of the Window the Consumer holds — the data it shows — or none. A request that
     * marks no changes (a layout gesture) lays out the data of that version, while the source holds it, and
     * not newer data the Consumer has not been shown; a row of the answer that the held Window shows alike is
     * the very row it holds (ADR-0152, ADR-0153). */
    readonly baseline?: string;
    /** The cap on the answer's leaves; 200,000 when left out (ADR-0066). */
    readonly maxLeaves?: number;
    /** Whether this gesture can mark changes of the data; a layout or display-setting gesture cannot. True
     * when left out. */
    readonly markChanges?: boolean;
    /** Asks a source that cannot tell its data changed again, though it said nothing: an explicit Refresh or
     * Retry (ADR-0153). */
    readonly refreshData?: boolean;
}

/** A row of a Window, detached from the report, the cube and the source. */
export interface PivotDisplayRow {
    /** What the row stands for. */
    readonly role: PivotRowRole;
    /** The Value Field the row shows, or −1 when its columns say. */
    readonly valueField: number;
    /** The row's Items, outermost first: with its role and Value Field, what it stands for across reports. */
    readonly items: readonly PivotItemKey[];
    /** Whether the row carries values: a group row whose subtotal stands at the bottom carries none. */
    readonly carriesValues: boolean;
    /** Its label in each label column. */
    readonly labels: readonly PivotRowLabel[];
    /** Its value in each value column, `undefined` for an empty cell. */
    readonly values: readonly (PivotValue | undefined)[];
    /** The row's Items, each with its field. */
    readonly rowPath: readonly PivotFieldItem[];
    /**
     * Per value cell, the change that last moved its shown text: the Report Version whose data changed it,
     * or `undefined` for a cell with no recent change and for every cell a layout gesture laid out. A cell keeps
     * its mark while its text does not change, after the source stops listing the change, so that its row
     * stays the same row. It says which change, never when: when a Change Highlight starts is the Consumer's,
     * on its own clock ({@link PivotChangeTimes}, ADR-0068).
     */
    readonly changedIn: readonly (string | undefined)[];
}

/** A value column of a report, with its Items. */
export interface PivotDisplayColumn {
    /** Its stable name. */
    readonly name: string;
    /** Its leaf header. */
    readonly header: string;
    /** An Item's column, a subtotal's, or the grand total's. */
    readonly role: PivotColumnRole;
    /** The Value Field it shows, or −1 when its rows say. */
    readonly valueField: number;
    /** The column's Items, each with its field. */
    readonly columnPath: readonly PivotFieldItem[];
}

/** The whole report's extent and columns, without its rows or its data. */
export interface PivotReportMetadata {
    /** The Report Version: one report, immutable, under its layout and settings. */
    readonly version: string;
    /** The Source Version the report was computed from. */
    readonly sourceVersion: string;
    /** Changes when the ordered sequence of the rows' identities changes. */
    readonly rowSequenceVersion: string;
    /** The layout it was laid out under. */
    readonly layout: PivotLayout;
    /** The settings it was laid out under. */
    readonly settings: PivotReportSettings;
    /** The whole report's row count, rows outside the Window included. */
    readonly rowCount: number;
    /** The label columns. */
    readonly labelColumns: readonly PivotLabelColumn[];
    /** The value columns. */
    readonly valueColumns: readonly PivotDisplayColumn[];
    /** The Header Group spans. */
    readonly headerSpans: readonly PivotHeaderSpan[];
    /** How many tiers the column header has above the leaf headers. */
    readonly headerTierCount: number;
    /** Each Value Field's caption. */
    readonly valueCaptions: readonly string[];
    /** The widest label of each label column under the settings' label geometry, in pixels; none without it. */
    readonly labelWidths: readonly number[];
    /** The changes the source still marks, newest first: a cell marked with one changed recently, and a
     * Consumer stamps each the first time it shows a Window listing it ({@link PivotChangeTimes}). A change
     * leaves the list once it is older than the settings' highlight duration, and never comes back. */
    readonly changeMarks: readonly string[];
}

/** Why a report source did not answer. */
export type PivotReportRefusalKind = 'reportVersionNotHeld' | 'unknownOrderKeyPolicy' | 'sourceRefused' | 'invalidRequest' | 'orderKeyFailed';

/** A refusal by name; an empty answer is never given in its place. */
export interface PivotReportRefusal {
    /** Why. */
    readonly kind: PivotReportRefusalKind;
    /** A sentence a person can read. */
    readonly message: string;
    /** The field it concerns, where one does. */
    readonly field?: string;
    /** The Pivot Source's own refusal, when it refused the question. */
    readonly sourceRefusal?: PivotSourceRefusal;
}

/** A Window as the Consumer holds it: the report's metadata, the Window answered, and its rows. */
export interface PivotReportState {
    /** The whole report's metadata. */
    readonly metadata: PivotReportMetadata;
    /** The Window answered. */
    readonly window: PivotReportWindow;
    /** Its rows, in order. */
    readonly rows: readonly PivotDisplayRow[];
}

/** A Window answered. */
export interface PivotReportAnswer extends PivotReportState {
    /** The request answered. */
    readonly requestId: string;
    /** None: the Window was answered. */
    readonly refusal?: undefined;
}

/** A Window refused. */
export interface PivotReportRefused {
    /** The request refused. */
    readonly requestId: string;
    /** The Window asked for. */
    readonly window: PivotReportWindow;
    /** Why. */
    readonly refusal: PivotReportRefusal;
}

/** A Window answered, or refused by name. */
export type PivotReportUpdate = PivotReportAnswer | PivotReportRefused;

/** A field's Items as the report labels and orders them, under a Report Version. */
export interface PivotReportItemsQuery {
    /** The report the Items are listed for. */
    readonly version: string;
    /** The field. */
    readonly field: string;
    /** What the Items' text holds, where the source searches. */
    readonly search?: string;
    /** The most Items listed; 10,000 when left out. */
    readonly max?: number;
}

/** A field's Items in the report's order, or a refusal. */
export interface PivotReportItemsResult {
    /** The report answered. */
    readonly version: string;
    /** Its Source Version, or `''` where the version is not held. */
    readonly sourceVersion: string;
    /** The Items, labelled, in the field's order. */
    readonly items: readonly PivotItemInfo[];
    /** How many Items matched before the cap. */
    readonly total: number;
    /** Why they were not listed. */
    readonly refusal?: PivotReportRefusal;
}

/** A rectangle of a selection, its edges included: rows of the report, columns among the query's columns. */
export interface PivotReportRange {
    /** The first row. */
    readonly top: number;
    /** The first column. */
    readonly left: number;
    /** The last row. */
    readonly bottom: number;
    /** The last column. */
    readonly right: number;
}

/** A versioned Copy: each rectangle copied whole, in the order given. */
export interface PivotReportCopyQuery {
    /** The report the selection was made on. */
    readonly version: string;
    /** The columns' names, in the Consumer's order. */
    readonly columns: readonly string[];
    /** The selected rectangles. */
    readonly ranges: readonly PivotReportRange[];
    /** The most cells copied; 1,000,000 when left out. A copy past it is refused, never cut short (spine 1). */
    readonly maxCells?: number;
}

/** A copied cell: its text, and its locale-free form. */
export interface PivotReportCopyCell {
    /** The shown text. */
    readonly text: string;
    /** The raw, locale-free text — a number in full — or absent for a blank. */
    readonly raw?: string;
    /** Whether the raw text is a number. */
    readonly isNumber: boolean;
}

/** One copied rectangle, with its headers. */
export interface PivotReportCopyBlock {
    /** The headers, in the rectangle's column order. */
    readonly headers: readonly string[];
    /** Each header's raw form for a copy's HTML flavour: a number column Item's header carries its number in
     * full (ADR-0005's note of 2026-10-13), any other `undefined`. Absent when no header has one. */
    readonly headerRaws?: readonly (string | undefined)[];
    /** Every cell of its rows. */
    readonly rows: readonly (readonly PivotReportCopyCell[])[];
}

/** A Copy, or a refusal. */
export interface PivotReportCopyResult {
    /** The report answered. */
    readonly version: string;
    /** One block per rectangle asked for. */
    readonly blocks: readonly PivotReportCopyBlock[];
    /** Why it was not copied. */
    readonly refusal?: PivotReportRefusal;
}

/** A versioned Selection Summary; a cell under two rectangles is counted once. */
export interface PivotReportSummaryQuery {
    /** The report the selection was made on. */
    readonly version: string;
    /** The columns' names, in the Consumer's order. */
    readonly columns: readonly string[];
    /** The selected rectangles. */
    readonly ranges: readonly PivotReportRange[];
    /** The Focus row, whose number format the figures are shown in. */
    readonly focusRow?: number;
    /** The Focus column among the columns. */
    readonly focusColumn?: number;
}

/** The figures of a Selection Summary, or a refusal (ADR-0130). */
export interface PivotReportSummaryResult {
    /** The report answered. */
    readonly version: string;
    /** The cells that are not blank: Count. */
    readonly values: number;
    /** The cells that are numbers: Numerical Count. An error is not one. */
    readonly numbers: number;
    /** The sum, exact wherever every number was; absent with no number. */
    readonly sum?: PivotNumber;
    /** The smallest number; absent with no number. */
    readonly min?: PivotNumber;
    /** The largest number; absent with no number. */
    readonly max?: PivotNumber;
    /** An error was selected: Average, Min, Max and Sum are not shown, and Count and Numerical Count are. */
    readonly hasError: boolean;
    /** The Focus value's number format, where it has one. */
    readonly numberFormat?: string;
    /** The report's locale. */
    readonly locale: string;
    /** Why it was not summed. */
    readonly refusal?: PivotReportRefusal;
}

/**
 * The question for the records behind a cell of a report — Show Details (ADR-0063, ADR-0151) — from a row of
 * its Window and the report's own layout and columns: the row's Items, the value column's (none for a label
 * cell, column −1), the Hidden Items the report was computed under, the page, and its Source Version. Made of
 * values, it stays the same question after any later layout, and is answered while the Pivot Source holds that
 * Source Version.
 */
export function pivotReportDetailsQuery(metadata: PivotReportMetadata, row: PivotDisplayRow, valueColumn: number,
    start = 0, count: number = Number.MAX_SAFE_INTEGER): PivotDetailsQuery {
    if (!Number.isInteger(valueColumn) || valueColumn < -1 || valueColumn >= metadata.valueColumns.length) {
        throw new RangeError('Not a value column of the report, nor −1.');
    }
    if (row.values.length !== metadata.valueColumns.length) throw new TypeError("The row is not a row of this report's columns.");
    const column = valueColumn < 0 ? undefined : metadata.valueColumns[valueColumn];
    return pivotDetailsQuery({
        sourceVersion: metadata.sourceVersion,
        rowItems: row.rowPath,
        columnItems: column === undefined ? [] : column.columnPath,
        hiddenItems: placedFields(queryForLayout(metadata.layout)).filter(field => field.hiddenItems.length > 0),
        start,
        count,
    });
}
