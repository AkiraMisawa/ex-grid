// The report laid out from a cube (ADR-0059/0060): its label columns, its value columns and the Header Group
// spans over them, and its rows — group rows, Item rows, subtotals and the grand total — in the Compact,
// Outline or Tabular form, each Item in its field's order; and the value at each row and column, as the
// Value Field shows it. The C# engine's ReportBuilder, CellReader, ValueCaptions, PivotReport and PivotRowKey.

import type { AggregateValue } from '../aggregates/parts';
import { divideByZeroValue, doubleValue, numberErrorValue } from '../aggregates/parts';
import type { PivotCube } from '../cube/cube';
import { cubeParts, readCell, type AxisNode, type CubeAxis, type ItemRef } from '../cube/cube';
import { everyRecord, pivotDetailsQuery, placedFields, type PivotDetailsQuery, type PivotFieldItem } from '../cube/query';
import type { FieldMeta } from '../fields/pivot-field';
import { fromPublicKey, itemKeysEqual, itemKeyText, keyIdOf, publicKeyId, sameItem, type PivotItemKey } from '../items/item-key';
import type { PivotAggregation, PivotColumnRole, PivotRowRole, PivotShowValuesAs } from '../layout/pivot-enums';
import { itemKindOrder } from '../layout/pivot-enums';
import type { PivotFieldPlacement, PivotLayout, PivotValueField } from '../layout/pivot-layout';
import { elementAt } from '../must';
import { Slicer, checkedSlicing, inPieces, pieceUnits, runWhole, sortedSteps, type PivotSlicing } from '../source/slicing';
import { escapeDataString, foldedText, upperInvariant } from '../text/casing';
import { captionId, fillWord, pivotWordIds } from '../words/pivot-words';
import { ItemLabels, ItemOrder, type ResolvedOptions } from './labels';
import { pivotValueOf, type PivotValue } from './value';

// ---- Value Field captions -----------------------------------------------------------------------

/**
 * Each Value Field's caption (ADR-0060): a caption of its own, which must be non-empty, unique ignoring case
 * and no Pivot Field's caption; or the default, `Sum of Amount` in the report's words, numbered 2, 3 … to stay
 * unique.
 */
export function valueCaptions(values: readonly PivotValueField[], fields: ReadonlyMap<string, FieldMeta>, options: ResolvedOptions): readonly string[] {
    const captions: (string | undefined)[] = values.map(() => undefined);
    const taken = new Set<string>();
    const fieldCaptions = new Set([...fields.values()].map(meta => foldedText(meta.field.caption)));
    values.forEach((value, i) => {
        const own = value.caption;
        if (own === undefined) return;
        if (own.length === 0) throw new Error(`The Value Field of '${value.field}' has an empty caption (ADR-0060).`);
        const folded = foldedText(own);
        if (taken.has(folded)) throw new Error(`Two Value Fields are captioned '${own}' (ADR-0060).`);
        taken.add(folded);
        if (fieldCaptions.has(folded)) {
            throw new Error(`The Value Field of '${value.field}' is captioned '${own}', which is a Pivot Field's caption (ADR-0060).`);
        }
        captions[i] = own;
    });
    values.forEach((value, i) => {
        if (captions[i] !== undefined) return;
        const meta = fields.get(value.field);
        const fieldCaption = meta === undefined ? value.field : meta.field.caption;
        const basis = fillWord(options.word(captionId(value.aggregation)), fieldCaption);
        let caption = basis;
        for (let n = 2; taken.has(foldedText(caption)) || fieldCaptions.has(foldedText(caption)); n++) caption = basis + String(n);
        taken.add(foldedText(caption));
        captions[i] = caption;
    });
    return Object.freeze(captions.map(caption => {
        if (caption === undefined) throw new Error('Internal: a Value Field was left without a caption.');
        return caption;
    }));
}

// ---- Cells --------------------------------------------------------------------------------------

interface ValueFieldPlan {
    readonly source: number;
    readonly aggregation: PivotAggregation;
    readonly caption: string;
    readonly showValuesAs: PivotShowValuesAs;
    readonly numberFormat: string | undefined;
}

/** Reads a Value Field at a row and a column, and shows it as its Show Values As says. */
class CellReader {
    readonly #cube: PivotCube;
    readonly values: readonly ValueFieldPlan[];

    constructor(cube: PivotCube, values: readonly ValueFieldPlan[]) {
        this.#cube = cube;
        this.values = values;
    }

    read(row: AxisNode, column: AxisNode, vf: number): AggregateValue {
        const plan = elementAt(this.values, vf);
        return readCell(cubeParts(this.#cube), row, column, plan.source, plan.aggregation);
    }

    shown(row: AxisNode, column: AxisNode, vf: number): AggregateValue {
        const value = this.read(row, column, vf);
        const plan = elementAt(this.values, vf);
        if (plan.showValuesAs === 'noCalculation' || value.kind !== 'number') return value;
        let divisor: AggregateValue;
        switch (plan.showValuesAs) {
            case 'percentOfGrandTotal': divisor = this.read(cubeParts(this.#cube).rows.root, cubeParts(this.#cube).columns.root, vf); break;
            case 'percentOfColumnTotal': divisor = this.read(cubeParts(this.#cube).rows.root, column, vf); break;
            case 'percentOfRowTotal': divisor = this.read(row, cubeParts(this.#cube).columns.root, vf); break;
        }
        if (divisor.kind !== 'number' || divisor.number === 0) return divideByZeroValue;
        const shown = value.number / divisor.number;
        return Number.isFinite(shown) ? doubleValue(shown) : numberErrorValue;
    }
}

// ---- What a report is made of -------------------------------------------------------------------

/** The button beside an outer row Item, which collapses or expands it. */
export interface PivotToggle {
    /** The row field whose Item it is. */
    readonly field: string;
    /** The Item it collapses or expands. */
    readonly item: PivotItemKey;
    /** The Item's label, for the button's accessible name. */
    readonly itemLabel: string;
    /** Whether the Item is collapsed now. */
    readonly isCollapsed: boolean;
}

/** A row's label in one label column: its text, its indent in levels, and its button. */
export interface PivotRowLabel {
    /** The text, or `undefined` for an empty label cell. */
    readonly text: string | undefined;
    /** The indent, in levels. */
    readonly indent: number;
    /** The collapse button, beside an outer Item that has Items under it. */
    readonly toggle?: PivotToggle;
    /** The number a number Item's label shows, in full, which its copy carries. */
    readonly number?: number;
}

/** Whether two labels are alike as C#'s records compare them: text, indent, number and toggle, the toggle's Item as
 * it is spelt. */
export function rowLabelsAlike(a: PivotRowLabel, b: PivotRowLabel): boolean {
    if (a.text !== b.text || a.indent !== b.indent || a.number !== b.number) return false;
    if (a.toggle === undefined || b.toggle === undefined) return a.toggle === b.toggle;
    return a.toggle.field === b.toggle.field && a.toggle.item.kind === b.toggle.item.kind && a.toggle.item.value === b.toggle.item.value
        && a.toggle.itemLabel === b.toggle.itemLabel && a.toggle.isCollapsed === b.toggle.isCollapsed;
}

/** A row's role, Value Field and Items written in one form, each Item as Items match: two keys that stand for one
 * row are one text, however their Items are spelt. */
export function rowIdOf(role: PivotRowRole, valueField: number, items: readonly PivotItemKey[]): string {
    return `${role}\u0001${valueField}\u0001${items.map(publicKeyId).join('\u0001')}`;
}

const noLabel: PivotRowLabel = Object.freeze({ text: undefined, indent: 0 });

function rowLabel(text: string, indent = 0, toggle?: PivotToggle, number?: number): PivotRowLabel {
    const label: { -readonly [K in keyof PivotRowLabel]: PivotRowLabel[K] } = { text, indent };
    if (toggle !== undefined) label.toggle = toggle;
    if (number !== undefined) label.number = number;
    return Object.freeze(label);
}

/** A label column: `row-labels` in the Compact form, `row:<field>` a field in the others. */
export interface PivotLabelColumn {
    /** Its name, stable across reports, so a width survives. */
    readonly name: string;
    /** Its header's text. */
    readonly header: string;
    /** The row field it labels, or `undefined` for Compact's one column and for Σ Values's. */
    readonly field: string | undefined;
}

/** A Header Group span: a label over `columnCount` value columns from `firstColumn`, at a tier of the
 * header, `tierSpan` tiers tall. */
export interface PivotHeaderSpan {
    /** The text over the span. */
    readonly label: string;
    /** The first value column under it. */
    readonly firstColumn: number;
    /** How many value columns it spans. */
    readonly columnCount: number;
    /** Its tier, from 0 at the top. */
    readonly tier: number;
    /** How many tiers tall it is. */
    readonly tierSpan: number;
}

let makeColumn: (name: string, header: string, role: PivotColumnRole, valueField: number, node: AxisNode, headerNumber?: number) => PivotReportColumn;
let columnNode: (column: PivotReportColumn) => AxisNode;

/** A value column of a report. */
export class PivotReportColumn {
    /** Its role, its Items and its Value Field's caption: stable across reports, so a width survives. */
    readonly name: string;
    /** Its leaf header's text. */
    readonly header: string;
    /** An Item's column, a subtotal's, or the grand total's. */
    readonly role: PivotColumnRole;
    /** The Value Field it shows, or −1 when its rows say. */
    readonly valueField: number;
    /** The number a number column Item's leaf header shows, in full, for its copy. */
    readonly headerNumber: number | undefined;
    readonly #node: AxisNode;

    private constructor(name: string, header: string, role: PivotColumnRole, valueField: number, node: AxisNode, headerNumber?: number) {
        this.name = name;
        this.header = header;
        this.role = role;
        this.valueField = valueField;
        this.#node = node;
        this.headerNumber = headerNumber;
        Object.freeze(this);
    }

    static {
        makeColumn = (name, header, role, valueField, node, headerNumber) => new PivotReportColumn(name, header, role, valueField, node, headerNumber);
        columnNode = column => column.#node;
    }
}

/** What a row of a report stands for, across reports: its role, its Value Field and its Items. */
export class PivotRowKey {
    /** The row's role. */
    readonly role: PivotRowRole;
    /** The Value Field the row shows, or −1 when its columns say. */
    readonly valueField: number;
    /** The row's Items, outermost first. */
    readonly items: readonly PivotItemKey[];

    /** A key of a row's role, Value Field and Items. */
    constructor(role: PivotRowRole, valueField: number, items: readonly PivotItemKey[]) {
        this.role = role;
        this.valueField = valueField;
        this.items = Object.freeze([...items]);
    }

    /** Whether two keys stand for one row: the same role, Value Field and Items. */
    equals(other: PivotRowKey): boolean {
        return this === other
            || (this.role === other.role && this.valueField === other.valueField && this.items.length === other.items.length
                && this.items.every((item, i) => itemKeysEqual(item, elementAt(other.items, i))));
    }

    /** `item -1 [Text:East / Text:Apples]`, for messages and logs. */
    toString(): string {
        return `${this.role} ${this.valueField} [${this.items.map(itemKeyText).join(' / ')}]`;
    }
}

const lineages = new WeakMap<PivotReportRow, object>();

let makeRow: (role: PivotRowRole, node: AxisNode, valueField: number, carriesValues: boolean, labels: readonly PivotRowLabel[], lineage: object) => PivotReportRow;
let rowNode: (row: PivotReportRow) => AxisNode;

/** A row of a report. */
export class PivotReportRow {
    /** A group row, an Item's, a subtotal's or the grand total's. */
    readonly role: PivotRowRole;
    /** The Value Field the row shows, or −1 when its columns say. */
    readonly valueField: number;
    /** Its label in each label column. */
    readonly labels: readonly PivotRowLabel[];
    /** Whether the row carries values: a group row whose subtotal stands at the bottom carries none. */
    readonly carriesValues: boolean;
    readonly #node: AxisNode;
    #key: PivotRowKey | undefined;

    private constructor(role: PivotRowRole, node: AxisNode, valueField: number, carriesValues: boolean, labels: readonly PivotRowLabel[], lineage: object) {
        this.role = role;
        this.#node = node;
        this.valueField = valueField;
        this.carriesValues = carriesValues;
        this.labels = Object.freeze([...labels]);
        lineages.set(this, lineage);
    }

    static {
        makeRow = (role, node, valueField, carriesValues, labels, lineage) => new PivotReportRow(role, node, valueField, carriesValues, labels, lineage);
        rowNode = row => row.#node;
    }

    /** What the row stands for, across reports. */
    get key(): PivotRowKey {
        if (this.#key === undefined) {
            const items: PivotItemKey[] = [];
            for (let at: AxisNode | undefined = this.#node; at?.item !== undefined; at = at.parent) items.unshift(at.item.publicKey);
            this.#key = new PivotRowKey(this.role, this.valueField, items);
        }
        return this.#key;
    }
}

function lineageOf(row: PivotReportRow): object | undefined {
    return lineages.get(row);
}

// ---- The report ---------------------------------------------------------------------------------

function sameRows(mine: PivotReport, other: PivotReport, from: number, to: number): boolean {
    for (let i = from; i < to; i++) {
        const row = elementAt(mine.rows, i);
        const theirs = elementAt(other.rows, i);
        if (row.role !== theirs.role || row.valueField !== theirs.valueField || !samePath(rowNode(row), rowNode(theirs))) return false;
    }
    return true;
}

/** Whether the other report's rows stand, one for one, for what this one's do, compared a piece at a time. */
export function* sameRowsSteps(mine: PivotReport, other: PivotReport | undefined): Generator<void, boolean> {
    if (other === undefined || other.rows.length !== mine.rows.length) return false;
    if (other === mine) return true;
    // A row is worth a unit for each level its path is compared at.
    const size = Math.max(1, Math.floor(pieceUnits / (mine.layout.rows.length + 1)));
    for (let from = 0; from < mine.rows.length; from += size) {
        if (from > 0) yield;
        if (!sameRows(mine, other, from, Math.min(mine.rows.length, from + size))) return false;
    }
    return true;
}

let reportOptions: (report: PivotReport) => ResolvedOptions;
let reportLineage: (report: PivotReport) => object;
let makeReport: (cube: PivotCube, layout: PivotLayout, options: ResolvedOptions, reader: CellReader, labelColumns: readonly PivotLabelColumn[],
    valueColumns: readonly PivotReportColumn[], headerSpans: readonly PivotHeaderSpan[], headerTierCount: number,
    rows: readonly PivotReportRow[], lineage: object) => PivotReport;

/** A report: its columns, its rows, and the value at each row and column. */
export class PivotReport {
    /** The layout it was laid out under. */
    readonly layout: PivotLayout;
    /** The label columns, left to right. */
    readonly labelColumns: readonly PivotLabelColumn[];
    /** The value columns, left to right. */
    readonly valueColumns: readonly PivotReportColumn[];
    /** The Header Group spans over the value columns. */
    readonly headerSpans: readonly PivotHeaderSpan[];
    /** How many tiers the column header has above the leaf headers. */
    readonly headerTierCount: number;
    /** The rows, top to bottom. */
    readonly rows: readonly PivotReportRow[];
    /** Each Value Field's caption. */
    readonly valueCaptions: readonly string[];
    /** The cube it was laid out from. */
    readonly cube: PivotCube;
    readonly #options: ResolvedOptions;
    readonly #reader: CellReader;
    readonly #lineage: object;

    private constructor(cube: PivotCube, layout: PivotLayout, options: ResolvedOptions, reader: CellReader, labelColumns: readonly PivotLabelColumn[],
        valueColumns: readonly PivotReportColumn[], headerSpans: readonly PivotHeaderSpan[], headerTierCount: number,
        rows: readonly PivotReportRow[], lineage: object) {
        this.cube = cube;
        this.layout = layout;
        this.#options = options;
        this.#reader = reader;
        this.labelColumns = Object.freeze([...labelColumns]);
        this.valueColumns = Object.freeze([...valueColumns]);
        this.headerSpans = Object.freeze([...headerSpans]);
        this.headerTierCount = headerTierCount;
        this.rows = Object.freeze([...rows]);
        this.#lineage = lineage;
        this.valueCaptions = Object.freeze(reader.values.map(v => v.caption));
    }

    static {
        makeReport = (...args) => new PivotReport(...args);
        reportOptions = report => report.#options;
        reportLineage = report => report.#lineage;
    }

    /** The locale the report is written in. */
    get locale(): string {
        return this.#options.culture.name;
    }

    /** Whether the layout places no field at all. */
    get isEmpty(): boolean {
        return this.layout.rows.length === 0 && this.layout.columns.length === 0 && this.layout.values.length === 0;
    }

    /** Whether the other report's rows stand, one for one, for what this one's do. */
    hasSameRowsAs(other: PivotReport | undefined): boolean {
        if (other === undefined || other.rows.length !== this.rows.length) return false;
        return other === this || sameRows(this, other, 0, this.rows.length);
    }

    /**
     * {@link PivotReport.hasSameRowsAs} in slices (ADR-0066, PV-40): the rows compared a piece at a time, yielding
     * between slices as `slicing` paces them — a refresh of a large report compares every row. Once `signal` is
     * aborted, it stops at the next slice and rejects with the signal's reason.
     */
    async hasSameRowsAsAsync(other: PivotReport | undefined, slicing?: PivotSlicing, signal?: AbortSignal): Promise<boolean> {
        const slicer = new Slicer(checkedSlicing(slicing), signal);
        return slicer.run(sameRowsSteps(this, other));
    }

    /** The row's Items, outermost first, each with its field. */
    rowPath(row: PivotReportRow): readonly PivotFieldItem[] {
        this.#own(row);
        return path(rowNode(row), this.layout.rows, cubeParts(this.cube).rows);
    }

    /** The value column's Items, outermost first, each with its field. */
    columnPath(valueColumn: number): readonly PivotFieldItem[] {
        return path(columnNode(elementAt(this.valueColumns, valueColumn)), this.layout.columns, cubeParts(this.cube).columns);
    }

    /** The question for the records behind a cell (ADR-0063): its row's and its column's Items, and every
     * placed field's Hidden Items, under the cube's Source Version. A column of −1 asks for the row's. */
    detailsQuery(row: PivotReportRow, valueColumn: number, start = 0, count: number = everyRecord): PivotDetailsQuery {
        if (!this.rows.includes(row)) throw new TypeError('The row belongs to another report.');
        if (!Number.isInteger(valueColumn) || valueColumn < -1 || valueColumn >= this.valueColumns.length) {
            throw new RangeError('Not a value column of the report, nor −1.');
        }
        return pivotDetailsQuery({
            sourceVersion: this.cube.sourceVersion,
            rowItems: this.rowPath(row),
            columnItems: valueColumn < 0 ? [] : this.columnPath(valueColumn),
            hiddenItems: placedFields(this.cube.query).filter(field => field.hiddenItems.length > 0),
            start,
            count,
        });
    }

    /** The Value Field a cell shows: its row's, its column's, the one Value Field, or −1 for none. */
    valueFieldAt(row: PivotReportRow, valueColumn: number): number {
        this.#own(row);
        if (row.valueField >= 0) return row.valueField;
        const column = elementAt(this.valueColumns, valueColumn);
        if (column.valueField >= 0) return column.valueField;
        return this.#reader.values.length === 1 ? 0 : -1;
    }

    /** The value at a row and a value column as the report shows it, or `undefined` for an empty cell. */
    valueAt(row: PivotReportRow, valueColumn: number): PivotValue | undefined {
        this.#own(row);
        if (!Number.isInteger(valueColumn) || valueColumn < 0 || valueColumn >= this.valueColumns.length) {
            throw new RangeError(`The report has ${this.valueColumns.length} value columns.`);
        }
        if (!row.carriesValues) return undefined;
        const vf = this.valueFieldAt(row, valueColumn);
        if (vf < 0) return undefined;
        const value = this.#reader.shown(rowNode(row), columnNode(elementAt(this.valueColumns, valueColumn)), vf);
        if (value.kind === 'empty') return undefined;
        const plan = elementAt(this.#reader.values, vf);
        return pivotValueOf(value, plan.numberFormat, plan.showValuesAs !== 'noCalculation', this.#options.culture);
    }

    // A row of this report's lineage, or the refusal that names it.
    #own(row: PivotReportRow): void {
        if (lineageOf(row) !== this.#lineage) {
            throw new TypeError(`The row ${row.key.toString()} is not a row of this report: it was laid out by another report, `
                + 'whose Value Fields and Items it names. Read a row through the report it came from.');
        }
    }
}

function samePath(one: AxisNode, other: AxisNode): boolean {
    let a: AxisNode | undefined = one;
    let b: AxisNode | undefined = other;
    while (a !== undefined && b !== undefined) {
        if (a.level !== b.level) return false;
        if (a.item === undefined) return b.item === undefined;
        if (b.item === undefined || !sameItem(a.item.key, b.item.key)) return false;
        a = a.parent;
        b = b.parent;
    }
    return a === b;
}

// A node's Items, outermost first, each spelled as the cube's version holds it.
function path(node: AxisNode, placements: readonly PivotFieldPlacement[], axis: CubeAxis): readonly PivotFieldItem[] {
    const steps: PivotFieldItem[] = [];
    for (let at: AxisNode | undefined = node; at?.item !== undefined; at = at.parent) {
        steps.unshift(Object.freeze({ field: elementAt(placements, at.level).field, item: itemOf(axis.nodeOf(at)).publicKey }));
    }
    return Object.freeze(steps);
}

// ---- Laying it out ------------------------------------------------------------------------------

/** Whether a placement's Item is collapsed, its Items compared as the engine tells them apart. */
class CollapseState {
    readonly #collapsed: boolean;
    readonly #toggled: ReadonlySet<string>;

    constructor(placement: PivotFieldPlacement) {
        this.#collapsed = placement.collapsed;
        this.#toggled = new Set(placement.toggledItems.map(key => keyIdOf(fromPublicKey(key))));
    }

    isCollapsed(item: ItemRef): boolean {
        return this.#collapsed !== this.#toggled.has(keyIdOf(item.key));
    }
}

/** Lays a report out from a cube and a layout the cube holds. */
export function buildReport(cube: PivotCube, layout: PivotLayout, options: ResolvedOptions): PivotReport {
    return runWhole(new ReportBuilder(cube, layout, options, false).build());
}

/** {@link buildReport}, a step at a time (PV-40): each tree walked a node at a time and a node's children
 * ordered a piece at a time, yielding once a piece of work is done, for a caller to slice. Siblings are merged
 * in order a piece at a time where {@link buildReport} sorts them at once, and both sort stably, so the two
 * give one order. */
export function buildReportSteps(cube: PivotCube, layout: PivotLayout, options: ResolvedOptions): Generator<void, PivotReport> {
    return new ReportBuilder(cube, layout, options, true).build();
}

// What a step of the walk is worth, in the units a piece counts: a node entered or left, each row it emits,
// and each column, whose name is written from its Items.
const rowUnits = 4;
const columnUnits = 8;

/** Lays a report out from a cube: the walk {@link buildReportSteps} takes, and the steps a computation lays a
 * report out with node by node (ADR-0153). Not part of the public surface. */
export class ReportBuilder {
    readonly #cube: PivotCube;
    readonly #layout: PivotLayout;
    readonly #options: ResolvedOptions;
    readonly #labels: ItemLabels;
    readonly #reader: CellReader;
    readonly #values: readonly ValueFieldPlan[];
    readonly #rowPlacements: readonly PivotFieldPlacement[];
    readonly #columnPlacements: readonly PivotFieldPlacement[];
    readonly #rowMeta: readonly FieldMeta[];
    readonly #columnMeta: readonly FieldMeta[];
    readonly #rowCollapse: readonly CollapseState[];
    readonly #columnCollapse: readonly CollapseState[];
    readonly #valuesOnRows: boolean;
    readonly #valuesOnColumns: boolean;
    readonly #labelColumnCount: number;
    readonly #pending: (AxisNode | undefined)[];
    readonly #lineage: object;
    readonly #sliced: boolean;
    readonly #rowAxis: CubeAxis;
    readonly #columnAxis: CubeAxis;

    /** A builder of `cube` under `layout`, which the cube holds; its rows are of `lineage`, a report's that it
     * lays out again, or of a lineage of their own. */
    constructor(cube: PivotCube, layout: PivotLayout, options: ResolvedOptions, sliced: boolean, lineage: object = {}) {
        this.#lineage = lineage;
        this.#cube = cube;
        this.#sliced = sliced;
        this.#rowAxis = cubeParts(cube).rows;
        this.#columnAxis = cubeParts(cube).columns;
        this.#layout = layout;
        this.#options = options;
        this.#labels = new ItemLabels(options);
        this.#rowPlacements = layout.rows;
        this.#columnPlacements = layout.columns;
        const metaOf = (field: string): FieldMeta => {
            const meta = cubeParts(cube).meta.get(field);
            if (meta === undefined) throw new Error(`Internal: '${field}' has no meta.`);
            return meta;
        };
        this.#rowMeta = layout.rows.map(p => metaOf(p.field));
        this.#columnMeta = layout.columns.map(p => metaOf(p.field));
        this.#rowCollapse = layout.rows.map(p => new CollapseState(p));
        this.#columnCollapse = layout.columns.map(p => new CollapseState(p));
        const captions = valueCaptions(layout.values, cubeParts(cube).meta, options);
        this.#values = layout.values.map((value, i) => Object.freeze({
            source: cubeParts(cube).sources.indexOf(value.field),
            aggregation: value.aggregation,
            caption: elementAt(captions, i),
            showValuesAs: value.showValuesAs,
            numberFormat: value.numberFormat,
        }));
        this.#reader = new CellReader(cube, this.#values);
        this.#valuesOnRows = this.#values.length >= 2 && layout.valuesAxis === 'rows';
        this.#valuesOnColumns = this.#values.length >= 2 && layout.valuesAxis === 'columns';
        this.#labelColumnCount = this.#countLabelColumns();
        this.#pending = layout.rows.map(() => undefined);
    }

    get #rowLevels(): number {
        return this.#rowPlacements.length;
    }

    get #columnLevels(): number {
        return this.#columnPlacements.length;
    }

    get #valueCount(): number {
        return this.#values.length;
    }

    get #empty(): boolean {
        return this.#layout.rows.length === 0 && this.#layout.columns.length === 0 && this.#layout.values.length === 0;
    }

    #countLabelColumns(): number {
        if (this.#empty) return 0;
        if (this.#layout.form === 'compact') return this.#rowLevels > 0 || this.#valuesOnRows ? 1 : 0;
        return this.#rowLevels + (this.#valuesOnRows ? 1 : 0);
    }

    /** A builder that lays `cube` out again as `previous` was laid out: under its layout and options, its rows of
     * its lineage, so that a row it shares is the report's own (ADR-0153). */
    static again(cube: PivotCube, previous: PivotReport): ReportBuilder {
        return new ReportBuilder(cube, previous.layout, reportOptions(previous), true, reportLineage(previous));
    }

    /** The cube laid out. */
    get cube(): PivotCube {
        return this.#cube;
    }

    /** The layout laid out. */
    get layout(): PivotLayout {
        return this.#layout;
    }

    /** The report around `rows`, which this builder laid out node by node ({@link rowsOf}): the value columns and
     * their spans laid out as {@link build} lays them out. */
    *buildAround(rows: readonly PivotReportRow[]): Generator<void, PivotReport> {
        const columns: PivotReportColumn[] = [];
        const spans: PivotHeaderSpan[] = [];
        if (!this.#empty) yield* this.#emitColumns(columns, spans);
        return makeReport(this.#cube, this.#layout, this.#options, this.#reader, this.#labelColumnList(), columns, spans, this.#tiers(), rows, this.#lineage);
    }

    /** `previous` with `rows` laid out by this builder: its label columns, and its value columns and spans unless
     * the columns changed — laid out again then, each column `previous` showed alike kept. */
    *withRows(previous: PivotReport, rows: readonly PivotReportRow[], columnsChanged: boolean): Generator<void, PivotReport> {
        if (!columnsChanged) {
            return makeReport(this.#cube, this.#layout, this.#options, this.#reader, previous.labelColumns, previous.valueColumns, previous.headerSpans,
                previous.headerTierCount, rows, this.#lineage);
        }
        const columns: PivotReportColumn[] = [];
        const spans: PivotHeaderSpan[] = [];
        if (!this.#empty) yield* this.#emitColumns(columns, spans);
        const old = new Map(previous.valueColumns.map(column => [column.name, column]));
        const kept = columns.map(column => {
            const held = old.get(column.name);
            return held !== undefined && held.header === column.header ? held : column;
        });
        return makeReport(this.#cube, this.#layout, this.#options, this.#reader, previous.labelColumns, kept, spans, previous.headerTierCount, rows,
            this.#lineage);
    }

    /**
     * One node's own rows, as the walk lays the node out (ADR-0153): those before its children's, those after, and
     * whether its children are laid out. In the Tabular form an outer Item's label stands on the first row of its
     * block: `pendingFrom` is the first level whose ancestor's label is still to be shown — the node's own level
     * for a node that is not its parent's first child, its parent's for one that is.
     */
    rowsOf(node: AxisNode, pendingFrom: number): { readonly before: PivotReportRow[]; readonly after: PivotReportRow[]; readonly descend: boolean } {
        const before: PivotReportRow[] = [];
        const after: PivotReportRow[] = [];
        if (this.#empty) return { before, after, descend: false };
        if (node.item === undefined) {
            if (this.#rowLevels === 0) {
                runWhole(this.#emitRows(before));
                return { before, after, descend: false };
            }
            this.#grandTotalRows(after);
            return { before, after, descend: true };
        }
        this.#pending.fill(undefined);
        for (let level = pendingFrom; level < node.level; level++) this.#pending[level] = this.#ancestorAt(node, level);
        const tabular = this.#layout.form === 'tabular';
        const descend = (tabular ? this.#enterTabular(node, before) : this.#enterGrouped(node, before)) >= 0;
        this.#pending.fill(undefined);
        if (descend) {
            if (tabular) this.#leaveTabular(node, after);
            else this.#leaveGrouped(node, after);
        }
        return { before, after, descend };
    }

    /** A row node's children in their field's order, ordered a piece at a time. */
    orderedChildren(node: AxisNode): Generator<void, readonly AxisNode[]> {
        return this.#order(node, true);
    }

    #tiers(): number {
        return this.#columnLevels === 0 ? 0 : this.#valuesOnColumns ? this.#columnLevels : this.#columnLevels - 1;
    }

    *build(): Generator<void, PivotReport> {
        const columns: PivotReportColumn[] = [];
        const spans: PivotHeaderSpan[] = [];
        const rows: PivotReportRow[] = [];
        if (!this.#empty) {
            yield* this.#emitColumns(columns, spans);
            yield* this.#emitRows(rows);
        }
        return makeReport(this.#cube, this.#layout, this.#options, this.#reader, this.#labelColumnList(), columns, spans, this.#tiers(), rows, this.#lineage);
    }

    #word(id: string): string {
        return this.#options.word(id);
    }

    #label(node: AxisNode, meta: FieldMeta): string {
        return this.#labels.of(itemOf(node), meta);
    }

    #itemLabel(node: AxisNode, level: number, indent = 0, toggle?: PivotToggle): PivotRowLabel {
        const meta = elementAt(this.#rowMeta, level);
        return rowLabel(this.#label(node, meta), indent, toggle, ItemLabels.numberOf(itemOf(node), meta));
    }

    #labelColumnList(): PivotLabelColumn[] {
        if (this.#labelColumnCount === 0) return [];
        if (this.#layout.form === 'compact') return [Object.freeze({ name: 'row-labels', header: this.#word(pivotWordIds.rowLabels), field: undefined })];
        const columns: PivotLabelColumn[] = this.#rowPlacements.map((placement, i) =>
            Object.freeze({ name: `row:${placement.field}`, header: elementAt(this.#rowMeta, i).field.caption, field: placement.field }));
        if (this.#valuesOnRows) columns.push(Object.freeze({ name: 'row:#values', header: this.#word(pivotWordIds.values), field: undefined }));
        return columns;
    }

    // ---- The walk ----

    // Depth first, in the Items' order: each node entered, its children ordered and walked, then left. A
    // step — a node entered or left, or a piece of its children ordered — is worth a unit and each row or
    // column it emits more; once a piece's worth is done, the walk yields.
    *#walk(root: AxisNode, rows: boolean, enter: (node: AxisNode) => number, leave: (node: AxisNode, mark: number) => void,
        emitted: () => number, unitsEach: number): Generator<void, void> {
        if (!this.#sliced) {
            this.#walkAtOnce(root, rows, enter, leave);
            return;
        }
        const frames: WalkFrame[] = [];
        let units = 0;
        const entered = (node: AxisNode): number => {
            const before = emitted();
            const mark = enter(node);
            units += 1 + ((emitted() - before) * unitsEach);
            return mark;
        };
        const rootMark = entered(root);
        if (rootMark < 0) return;
        frames.push({ node: root, mark: rootMark, children: this.#atOnce(root, rows) ? this.#orderAtOnce(root, rows) : yield* this.#order(root, rows), next: 0 });
        for (let top = frames.at(-1); top !== undefined; top = frames.at(-1)) {
            const child = top.children[top.next];
            if (child === undefined) {
                const before = emitted();
                leave(top.node, top.mark);
                units += 1 + ((emitted() - before) * unitsEach);
                frames.pop();
            } else {
                top.next++;
                const mark = entered(child);
                if (mark >= 0) frames.push({ node: child, mark, children: this.#atOnce(child, rows) ? this.#orderAtOnce(child, rows) : yield* this.#order(child, rows), next: 0 });
            }
            if (units >= pieceUnits) {
                units = 0;
                yield;
            }
        }
    }

    // The same walk at once, for a report laid out at once.
    #walkAtOnce(node: AxisNode, rows: boolean, enter: (node: AxisNode) => number, leave: (node: AxisNode, mark: number) => void): void {
        const mark = enter(node);
        if (mark < 0) return;
        for (const child of this.#orderAtOnce(node, rows)) this.#walkAtOnce(child, rows, enter, leave);
        leave(node, mark);
    }

    // ---- Columns ----

    #tierOf(level: number): number {
        return this.#valuesOnColumns ? this.#columnLevels - level : this.#columnLevels - 1 - level;
    }

    *#emitColumns(columns: PivotReportColumn[], spans: PivotHeaderSpan[]): Generator<void, void> {
        const root = this.#columnAxis.root;
        if (this.#columnLevels === 0) {
            if (this.#valueCount === 0) return;
            if (this.#valuesOnColumns) {
                this.#values.forEach((plan, vf) => columns.push(this.#column('item', root, vf, plan.caption)));
            } else {
                // One value column: the one Value Field's caption, or "Values" when several stand in rows.
                columns.push(this.#column('item', root, -1, this.#valueCount === 1 ? elementAt(this.#values, 0).caption : this.#word(pivotWordIds.values)));
            }
            return;
        }
        yield* this.#walk(root, false, node => this.#enterColumn(node, columns, spans), (node, start) => this.#leaveColumn(node, start, columns, spans),
            () => columns.length + spans.length, columnUnits);
        if (this.#layout.grandTotalColumn && this.#valueCount > 0) {
            const label = this.#word(pivotWordIds.grandTotal);
            if (this.#valuesOnColumns) {
                const first = columns.length;
                this.#values.forEach((plan, vf) => columns.push(this.#column('grandTotal', root, vf, plan.caption)));
                const top = this.#tierOf(0);
                spans.push(span(label, first, this.#valueCount, top, top));
            } else {
                columns.push(this.#column('grandTotal', root, -1, label));
            }
        }
    }

    // An innermost or collapsed column Item emits its columns, and its children are not walked; any other is
    // left with the first column under it.
    #enterColumn(node: AxisNode, columns: PivotReportColumn[], spans: PivotHeaderSpan[]): number {
        if (node.item === undefined) return 0;
        const level = node.level;
        const innermost = level === this.#columnLevels - 1;
        const collapsed = !innermost && elementAt(this.#columnCollapse, level).isCollapsed(node.item);
        if (!innermost && !collapsed) return columns.length;
        const meta = elementAt(this.#columnMeta, level);
        const label = this.#label(node, meta);
        const tier = this.#tierOf(level);
        if (this.#valuesOnColumns) {
            const first = columns.length;
            this.#values.forEach((plan, vf) => columns.push(this.#column('item', node, vf, plan.caption)));
            spans.push(span(label, first, this.#valueCount, tier, tier));
        } else {
            columns.push(makeColumn(this.#columnName('item', node, -1), label, 'item', -1, node, ItemLabels.numberOf(node.item, meta)));
        }
        return -1;
    }

    #leaveColumn(node: AxisNode, start: number, columns: PivotReportColumn[], spans: PivotHeaderSpan[]): void {
        if (node.item === undefined) return;
        const level = node.level;
        const label = this.#label(node, elementAt(this.#columnMeta, level));
        const tier = this.#tierOf(level);
        spans.push(span(label, start, columns.length - start, tier, 1));
        if (elementAt(this.#columnPlacements, level).subtotals && this.#valueCount > 0) {
            const total = fillWord(this.#word(pivotWordIds.itemTotal), label);
            if (this.#valuesOnColumns) {
                const first = columns.length;
                this.#values.forEach((plan, vf) => columns.push(this.#column('subtotal', node, vf, plan.caption)));
                spans.push(span(total, first, this.#valueCount, tier, tier));
            } else {
                columns.push(this.#column('subtotal', node, -1, total));
            }
        }
    }

    #column(role: PivotColumnRole, node: AxisNode, vf: number, header: string): PivotReportColumn {
        return makeColumn(this.#columnName(role, node, vf), header, role, vf, node);
    }

    // A column's name is its role, its Items and its Value Field's caption — stable across reports for the
    // same column, so a width the user gave it survives a refresh (ADR-0016).
    #columnName(role: PivotColumnRole, node: AxisNode, vf: number): string {
        const parts: string[] = [];
        for (let at: AxisNode | undefined = node; at?.item !== undefined; at = at.parent) {
            const key = itemOf(this.#columnAxis.nodeOf(at)).publicKey;
            const text = key.kind === 'text' ? upperInvariant(key.value ?? '') : key.value ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- a Blank's key has no text, and its name's part is empty as C# writes it
            parts.unshift(`${itemKindOrder(key.kind)}:${escapeDataString(text)}`);
        }
        let name = `v:${role === 'item' ? 'i' : role === 'subtotal' ? 's' : 'g'}`;
        for (const part of parts) name += `/${part}`;
        if (vf >= 0) name += `#${escapeDataString(elementAt(this.#values, vf).caption)}`;
        return name;
    }

    // ---- Rows ----

    #row(role: PivotRowRole, node: AxisNode, valueField: number, carriesValues: boolean, labels: readonly PivotRowLabel[]): PivotReportRow {
        return makeRow(role, node, valueField, carriesValues, labels, this.#lineage);
    }

    *#emitRows(rows: PivotReportRow[]): Generator<void, void> {
        const root = this.#rowAxis.root;
        if (this.#rowLevels === 0) {
            if (this.#valueCount === 0) return;
            if (this.#valuesOnRows) {
                this.#values.forEach((plan, vf) => {
                    const labels = this.#newLabels();
                    labels[this.#labelColumnCount - 1] = rowLabel(plan.caption);
                    rows.push(this.#row('item', root, vf, true, labels));
                });
            } else {
                rows.push(this.#row('grandTotal', root, -1, true, this.#newLabels()));
            }
            return;
        }
        if (this.#layout.form === 'tabular') {
            yield* this.#walk(root, true, node => this.#enterTabular(node, rows), node => this.#leaveTabular(node, rows), () => rows.length, rowUnits);
        } else {
            yield* this.#walk(root, true, node => this.#enterGrouped(node, rows), node => this.#leaveGrouped(node, rows), () => rows.length, rowUnits);
        }
        this.#grandTotalRows(rows);
    }

    #grandTotalRows(rows: PivotReportRow[]): void {
        if (!this.#layout.grandTotalRow || this.#valueCount === 0) return;
        const root = this.#rowAxis.root;
        if (this.#valuesOnRows) {
            for (let vf = 0; vf < this.#valueCount; vf++) rows.push(this.#row('grandTotal', root, vf, true, this.#grandTotalLabels(vf)));
        } else {
            rows.push(this.#row('grandTotal', root, -1, true, this.#grandTotalLabels(-1)));
        }
    }

    #newLabels(): PivotRowLabel[] {
        return Array.from({ length: this.#labelColumnCount }, () => noLabel);
    }

    #caption(vf: number): string {
        return elementAt(this.#values, vf).caption;
    }

    #grandTotalLabels(vf: number): PivotRowLabel[] {
        const labels = this.#newLabels();
        if (this.#layout.form === 'compact') {
            labels[0] = rowLabel(vf < 0 ? this.#word(pivotWordIds.grandTotal) : fillWord(this.#word(pivotWordIds.totalOf), this.#caption(vf)));
            return labels;
        }
        labels[0] = rowLabel(this.#word(pivotWordIds.grandTotal));
        if (vf >= 0) labels[this.#labelColumnCount - 1] = rowLabel(this.#caption(vf));
        return labels;
    }

    #toggleOf(node: AxisNode): PivotToggle | undefined {
        const level = node.level;
        if (level >= this.#rowLevels - 1) return undefined;
        const item = itemOf(node);
        return Object.freeze({
            field: elementAt(this.#rowPlacements, level).field,
            item: item.publicKey,
            itemLabel: this.#label(node, elementAt(this.#rowMeta, level)),
            isCollapsed: elementAt(this.#rowCollapse, level).isCollapsed(item),
        });
    }

    // The node's ancestor at a level, as the cube's version holds it.
    #ancestorAt(node: AxisNode, level: number): AxisNode {
        let at = node;
        while (at.level > level) at = parentOf(at);
        return this.#rowAxis.nodeOf(at);
    }

    // The Compact and Outline forms: a group row heads each outer Item's block. An innermost or collapsed
    // Item is its rows, and its children are not walked; an outer one is its group row, then its children,
    // then — when they stand at the bottom — its subtotals.
    #enterGrouped(node: AxisNode, rows: PivotReportRow[]): number {
        if (node.item === undefined) return 0;
        const level = node.level;
        const innermost = level === this.#rowLevels - 1;
        const collapsed = !innermost && elementAt(this.#rowCollapse, level).isCollapsed(node.item);
        if (innermost || collapsed) {
            if (this.#valuesOnRows) {
                rows.push(this.#row('group', node, -1, false, this.#itemLabels(node)));
                for (let vf = 0; vf < this.#valueCount; vf++) rows.push(this.#row('item', node, vf, true, this.#captionLabels(node, vf)));
            } else {
                rows.push(this.#row(innermost ? 'item' : 'group', node, -1, true, this.#itemLabels(node)));
            }
            return -1;
        }
        const { atTop } = this.#subtotalsOf(level);
        rows.push(this.#row('group', node, -1, atTop, this.#itemLabels(node)));
        return 0;
    }

    #leaveGrouped(node: AxisNode, rows: PivotReportRow[]): void {
        if (node.item === undefined) return;
        const { subtotals, atTop } = this.#subtotalsOf(node.level);
        if (!subtotals || atTop) return;
        if (this.#valuesOnRows) {
            for (let vf = 0; vf < this.#valueCount; vf++) rows.push(this.#row('subtotal', node, vf, true, this.#subtotalLabels(node, vf)));
        } else {
            rows.push(this.#row('subtotal', node, -1, true, this.#subtotalLabels(node, -1)));
        }
    }

    #subtotalsOf(level: number): { readonly subtotals: boolean; readonly atTop: boolean } {
        const subtotals = elementAt(this.#rowPlacements, level).subtotals && this.#valueCount > 0;
        return { subtotals, atTop: subtotals && this.#layout.subtotalsAtTop && !this.#valuesOnRows };
    }

    #itemLabels(node: AxisNode): PivotRowLabel[] {
        const labels = this.#newLabels();
        const level = node.level;
        if (this.#layout.form === 'compact') {
            labels[0] = this.#itemLabel(node, level, level, this.#toggleOf(node));
            return labels;
        }
        labels[level] = this.#itemLabel(node, level, 0, this.#toggleOf(node));
        this.#repeatOuter(labels, node, level - 1);
        return labels;
    }

    #captionLabels(node: AxisNode, vf: number): PivotRowLabel[] {
        const labels = this.#newLabels();
        if (this.#layout.form === 'compact') {
            labels[0] = rowLabel(this.#caption(vf), node.level + 1);
            return labels;
        }
        labels[this.#labelColumnCount - 1] = rowLabel(this.#caption(vf));
        this.#repeatOuter(labels, node, node.level);
        return labels;
    }

    #subtotalLabels(node: AxisNode, vf: number): PivotRowLabel[] {
        const labels = this.#newLabels();
        const level = node.level;
        const label = this.#label(node, elementAt(this.#rowMeta, level));
        if (this.#layout.form === 'compact') {
            labels[0] = rowLabel(vf < 0
                ? fillWord(this.#word(pivotWordIds.itemTotal), label)
                : fillWord(this.#word(pivotWordIds.itemValue), label, this.#caption(vf)), level);
            return labels;
        }
        labels[level] = rowLabel(fillWord(this.#word(pivotWordIds.itemTotal), label));
        if (vf >= 0) labels[this.#labelColumnCount - 1] = rowLabel(this.#caption(vf));
        this.#repeatOuter(labels, node, level - 1);
        return labels;
    }

    #repeatOuter(labels: PivotRowLabel[], node: AxisNode, upToLevel: number): void {
        if (!this.#layout.repeatItemLabels) return;
        for (let level = 0; level <= upToLevel; level++) labels[level] = this.#itemLabel(this.#ancestorAt(node, level), level);
    }

    // The Tabular form: no group rows — an outer Item's label stands on the first row of its block, and its
    // subtotal at the bottom.
    #enterTabular(node: AxisNode, rows: PivotReportRow[]): number {
        if (node.item === undefined) return 0;
        const level = node.level;
        this.#pending[level] = node;
        const innermost = level === this.#rowLevels - 1;
        const collapsed = !innermost && elementAt(this.#rowCollapse, level).isCollapsed(node.item);
        if (!innermost && !collapsed) return 0;
        const role: PivotRowRole = innermost ? 'item' : 'group';
        if (this.#valuesOnRows) {
            for (let vf = 0; vf < this.#valueCount; vf++) {
                const labels = this.#tabularLabels(node, level);
                labels[this.#labelColumnCount - 1] = rowLabel(this.#caption(vf));
                rows.push(this.#row(role, node, vf, true, labels));
            }
        } else {
            rows.push(this.#row(role, node, -1, true, this.#tabularLabels(node, level)));
        }
        return -1;
    }

    #leaveTabular(node: AxisNode, rows: PivotReportRow[]): void {
        if (node.item === undefined) return;
        const level = node.level;
        if (!elementAt(this.#rowPlacements, level).subtotals || this.#valueCount === 0) return;
        const total = fillWord(this.#word(pivotWordIds.itemTotal), this.#label(node, elementAt(this.#rowMeta, level)));
        for (let vf = this.#valuesOnRows ? 0 : -1; vf < (this.#valuesOnRows ? this.#valueCount : 0); vf++) {
            const labels = this.#tabularLabels(node, level - 1);
            labels[level] = rowLabel(total);
            if (vf >= 0) labels[this.#labelColumnCount - 1] = rowLabel(this.#caption(vf));
            rows.push(this.#row('subtotal', node, vf, true, labels));
        }
    }

    #tabularLabels(node: AxisNode, upToLevel: number): PivotRowLabel[] {
        const labels = this.#newLabels();
        for (let level = 0; level <= upToLevel; level++) {
            const ancestor = this.#ancestorAt(node, level);
            if (this.#pending[level] === ancestor) {
                labels[level] = this.#itemLabel(ancestor, level, 0, this.#toggleOf(ancestor));
                this.#pending[level] = undefined;
            } else if (this.#layout.repeatItemLabels) {
                labels[level] = this.#itemLabel(ancestor, level);
            }
        }
        return labels;
    }

    // ---- Order ----

    // A node's children in their field's order (ADR-0060). A short list is ordered at once, and so is every
    // list when the report is laid out at once; a long one in slices is ordered a piece at a time — its Order
    // Keys asked, the values it is ordered by read, and the sort — yielding once a piece's worth is done. Both
    // sort stably, so the two give one order.
    *#order(node: AxisNode, rows: boolean): Generator<void, readonly AxisNode[]> {
        if (this.#atOnce(node, rows)) return this.#orderAtOnce(node, rows);
        const children = this.#childrenOf(node, rows);
        const ordering = this.#ordering(node, rows);
        yield* inPieces(children.length, 2, ordering.prepare);
        const order = yield* sortedSteps(children.map((_, i) => i), ordering.compare);
        return order.map(i => elementAt(children, i));
    }

    #atOnce(node: AxisNode, rows: boolean): boolean {
        return !this.#sliced || this.#childrenOf(node, rows).length <= pieceUnits / 8;
    }

    #childrenOf(node: AxisNode, rows: boolean): readonly AxisNode[] {
        return (rows ? this.#rowAxis : this.#columnAxis).childrenOf(node);
    }

    // At once, the order is made as directly as it can be: this is every node's order in a report laid out at
    // once, and a few hundred thousand nodes' in a large one.
    #orderAtOnce(node: AxisNode, rows: boolean): readonly AxisNode[] {
        const level = node.level + 1;
        const placement = elementAt(rows ? this.#rowPlacements : this.#columnPlacements, level);
        if (placement.sort.byValue !== undefined) {
            const children = this.#childrenOf(node, rows);
            const ordering = this.#ordering(node, rows);
            ordering.prepare(0, children.length);
            return children.map((_, i) => i).toSorted(ordering.compare).map(i => elementAt(children, i));
        }
        const meta = elementAt(rows ? this.#rowMeta : this.#columnMeta, level);
        const order = new ItemOrder(meta, this.#labels, this.#options.culture, placement.sort.direction === 'descending');
        const children = this.#childrenOf(node, rows);
        for (const child of children) order.prepare(itemOf(child));
        return children.toSorted((a, b) => order.compare(itemOf(a), itemOf(b)));
    }

    // How a node's children are ordered: what is read of each child first — its Order Key, or the value it is
    // ordered by — and the comparison of two children by their places among them.
    #ordering(node: AxisNode, rows: boolean): ChildOrdering {
        const level = node.level + 1;
        const placement = elementAt(rows ? this.#rowPlacements : this.#columnPlacements, level);
        const meta = elementAt(rows ? this.#rowMeta : this.#columnMeta, level);
        const children = this.#childrenOf(node, rows);
        const descending = placement.sort.direction === 'descending';
        const culture = this.#options.culture;
        const byValue = placement.sort.byValue;
        if (byValue !== undefined) {
            const other = rows ? this.#columnAxis.root : this.#rowAxis.root;
            const byLabel = new ItemOrder(meta, this.#labels, culture, false, false);
            const keys: (number | undefined)[] = [];
            return {
                prepare: (from, to) => {
                    for (let i = from; i < to; i++) {
                        const child = elementAt(children, i);
                        const value = rows ? this.#reader.shown(child, other, byValue) : this.#reader.shown(other, child, byValue);
                        keys.push(value.kind === 'number' ? value.number : undefined);
                    }
                },
                compare: (i, j) => {
                    const x = keys[i];
                    const y = keys[j];
                    const xItem = itemOf(elementAt(children, i));
                    const yItem = itemOf(elementAt(children, j));
                    if (x === undefined || y === undefined) {
                        if (x === undefined && y === undefined) return byLabel.compare(xItem, yItem);
                        return x === undefined ? 1 : -1;
                    }
                    let compared = x < y ? -1 : x > y ? 1 : 0;
                    if (descending) compared = -compared;
                    return compared !== 0 ? compared : byLabel.compare(xItem, yItem);
                },
            };
        }
        const order = new ItemOrder(meta, this.#labels, culture, descending);
        return {
            prepare: (from, to) => {
                for (let i = from; i < to; i++) order.prepare(itemOf(elementAt(children, i)));
            },
            compare: (i, j) => order.compare(itemOf(elementAt(children, i)), itemOf(elementAt(children, j))),
        };
    }
}

interface ChildOrdering {
    readonly prepare: (from: number, to: number) => void;
    readonly compare: (i: number, j: number) => number;
}

interface WalkFrame {
    readonly node: AxisNode;
    readonly mark: number;
    readonly children: readonly AxisNode[];
    next: number;
}

function span(label: string, firstColumn: number, columnCount: number, tier: number, tierSpan: number): PivotHeaderSpan {
    return Object.freeze({ label, firstColumn, columnCount, tier, tierSpan });
}

function itemOf(node: AxisNode): ItemRef {
    if (node.item === undefined) throw new Error('Internal: the root has no Item.');
    return node.item;
}

function parentOf(node: AxisNode): AxisNode {
    if (node.parent === undefined) throw new Error('Internal: the root has no parent.');
    return node.parent;
}
