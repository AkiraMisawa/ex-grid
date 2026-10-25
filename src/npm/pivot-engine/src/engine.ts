// The pivot engine (ADR-0060/0066): a Pivot Source answers a question with the Leaf Aggregates; the engine
// builds from them the row and column trees and every total, and lays them out into a Pivot Report. Pure,
// with no UI. The C# engine's PivotEngine.

import type { PivotAnswer } from './cube/answer';
import { buildCube, buildCubeSteps, cubeParts, ItemRef, type PivotCube } from './cube/cube';
import { placedFields, queryForLayout, type PivotQuery } from './cube/query';
import { fieldMeta, fieldMetas, numberFormatProblem, type FieldMeta, type PivotField, type PivotRecordField } from './fields/pivot-field';
import { fromPublicKey, keyIdOf, toPublicKey, type PivotItemKey } from './items/item-key';
import { pivotAggregations, type PivotArea } from './layout/pivot-enums';
import { createPivotFieldPlacement, placementOf, placementsIn, type PivotLayout } from './layout/pivot-layout';
import { defined, elementAt } from './must';
import { ItemLabels, ItemOrder, resolveOptions, type PivotOptions } from './report/labels';
import { buildReport, buildReportSteps, type PivotReport, type PivotReportRow } from './report/report';
import { allItemsOf, RecordPivotSource, type PivotItemPage } from './source/pivot-source';
import { Slicer, checkedSlicing, type PivotSlicing } from './source/slicing';

/** One Item as Filter… lists it (ADR-0061): its key as a layout writes it, what it is painted as, and
 * whether the layout hides it now. */
export interface PivotItemInfo {
    /** The Item's key, as a layout writes it. */
    readonly key: PivotItemKey;
    /** What it is painted as. */
    readonly label: string;
    /** Whether the layout hides it now. */
    readonly isHidden: boolean;
}

const areaTitles: Readonly<Record<PivotArea, string>> = Object.freeze({ filters: 'Filters', columns: 'Columns', rows: 'Rows', values: 'Values' });
const directions: readonly string[] = ['ascending', 'descending'];
const showValuesAs: readonly string[] = ['noCalculation', 'percentOfGrandTotal', 'percentOfColumnTotal', 'percentOfRowTotal'];
const axes: readonly string[] = ['columns', 'rows'];
const forms: readonly string[] = ['compact', 'outline', 'tabular'];

/** Refuses by name a layout that names an undeclared field, stands a field in two of Filters, Rows and
 * Columns, orders by a Value Field it does not have, or carries a setting the engine does not know. */
export function validateLayout(layout: PivotLayout, declared: ReadonlyMap<string, unknown>): void {
    const standing = new Set<string>();
    for (const area of ['filters', 'columns', 'rows'] as const) {
        for (const placement of placementsIn(layout, area)) {
            if (!declared.has(placement.field)) {
                throw new Error(`The layout places '${placement.field}' in ${areaTitles[area]}, and no Pivot Field of that name is declared (ADR-0060).`);
            }
            if (standing.has(placement.field)) {
                throw new Error(`The layout places '${placement.field}' twice among Filters, Rows and Columns; a field stands in one of them at most (ADR-0061).`);
            }
            standing.add(placement.field);
            const byValue = placement.sort.byValue;
            if (byValue !== undefined && (byValue < 0 || byValue >= layout.values.length)) {
                throw new Error(`'${placement.field}' is ordered by Value Field ${byValue}, and the layout has ${layout.values.length} (ADR-0060).`);
            }
            if (!directions.includes(placement.sort.direction)) {
                throw new Error(`'${placement.field}' has an unknown sort direction (${placement.sort.direction}).`);
            }
        }
    }
    for (const value of layout.values) {
        if (!declared.has(value.field)) {
            throw new Error(`The layout places '${value.field}' in Values, and no Pivot Field of that name is declared (ADR-0060).`);
        }
        if (!pivotAggregations.includes(value.aggregation)) {
            throw new Error(`The Value Field of '${value.field}' has an unknown Aggregation (${value.aggregation}).`);
        }
        if (!showValuesAs.includes(value.showValuesAs)) {
            throw new Error(`The Value Field of '${value.field}' has an unknown Show Values As (${value.showValuesAs}).`);
        }
        const format = value.numberFormat;
        const problem = format === undefined ? undefined : numberFormatProblem(format);
        if (problem !== undefined) {
            throw new Error(`The Value Field of '${value.field}' has the number format '${format}', which ${problem} (ADR-0060).`);
        }
    }
    if (!axes.includes(layout.valuesAxis)) throw new Error(`Unknown ValuesAxis (${layout.valuesAxis}).`);
    if (!forms.includes(layout.form)) throw new Error(`Unknown report form (${layout.form}).`);
}

/**
 * The cube of a layout over records in memory, on the calling thread: the bundled source answers the
 * layout's question with no cap on leaves, and the cube is built from its leaves. Refuses by name what
 * {@link validateLayout} refuses, and a field list that declares a name twice.
 */
export function aggregatePivot<TRecord>(records: readonly TRecord[], fields: readonly PivotRecordField<TRecord>[], layout: PivotLayout): PivotCube {
    const source = new RecordPivotSource(records, fields);
    const meta = fieldMetas(fields);
    validateLayout(layout, meta);
    const query = queryForLayout(layout, Number.MAX_SAFE_INTEGER);
    const answer = source.aggregateNow(query);
    if (answer.refusal !== undefined) throw new Error(answer.refusal.message);
    return buildCube(query, answer, meta, field => allItemsOf(source, field));
}

/**
 * The cube of a Pivot Source's answer (ADR-0066): the row and column trees its leaves form, and every
 * subtotal and grand total merged from the leaves' parts — each total from its records, never from the
 * totals below it (ADR-0060). Refuses by name an answer to another question, a refusal, and a field
 * neither declared nor named by the question.
 */
export function pivotCube(query: PivotQuery, answer: PivotAnswer, fields: readonly PivotField[]): PivotCube {
    return buildCube(query, answer, fieldMetas(fields));
}

/**
 * {@link pivotCube} in slices (PV-40): the same steps over the answer's leaves and cells, a piece at a time,
 * yielding between slices as `slicing` paces them, so that a large answer's cube leaves the page painting. It
 * gives the cube {@link pivotCube} gives, and rejects with what it throws. Once `signal` is aborted, it stops at
 * the next slice and rejects with the signal's reason. A call begins a slice of its own, and its last slice ends
 * without a yield, as a source's question does: a caller that asks, makes the cube and lays out the report one
 * after another yields between them where the page must paint.
 */
export async function pivotCubeAsync(query: PivotQuery, answer: PivotAnswer, fields: readonly PivotField[],
    slicing?: PivotSlicing, signal?: AbortSignal): Promise<PivotCube> {
    const slicer = new Slicer(checkedSlicing(slicing), signal);
    return slicer.run(buildCubeSteps(query, answer, fieldMetas(fields)));
}

/**
 * Lays a cube out under a layout (ADR-0060): the rows, the label columns, the value columns and their
 * Header Group spans. Cheap — nothing is asked of the source. Refuses a layout the cube does not hold.
 */
export function pivotReport(cube: PivotCube, layout: PivotLayout, options?: PivotOptions): PivotReport {
    checkHolds(cube, layout);
    return buildReport(cube, layout, resolveOptions(options));
}

/**
 * {@link pivotReport} in slices (PV-40): each tree walked a node at a time and thousands of siblings ordered
 * a piece at a time, yielding between slices as `slicing` paces them. It gives the report {@link pivotReport}
 * gives, and rejects with what it throws, an Order Key that fails included. Once `signal` is aborted, it stops at
 * the next slice and rejects with the signal's reason.
 */
export async function pivotReportAsync(cube: PivotCube, layout: PivotLayout, options?: PivotOptions,
    slicing?: PivotSlicing, signal?: AbortSignal): Promise<PivotReport> {
    const slicer = new Slicer(checkedSlicing(slicing), signal);
    checkHolds(cube, layout);
    return slicer.run(buildReportSteps(cube, layout, resolveOptions(options)));
}

function checkHolds(cube: PivotCube, layout: PivotLayout): void {
    validateLayout(layout, cubeParts(cube).meta);
    if (!cube.holds(layout)) {
        throw new Error('The cube was aggregated under other placed fields, Hidden Items or Value Fields than this layout '
            + 'has, or without the parts one of its Aggregations reads; ask again (aggregatePivot, or the '
            + 'source with queryForLayout) before laying it out (ADR-0060/0066).');
    }
}

/** Aggregates and lays out in one step: {@link aggregatePivot} then {@link pivotReport}. */
export function computePivot<TRecord>(records: readonly TRecord[], fields: readonly PivotRecordField<TRecord>[], layout: PivotLayout,
    options?: PivotOptions): PivotReport {
    return pivotReport(aggregatePivot(records, fields, layout), layout, options);
}

/**
 * Every Item of a placed field over all the records, in the field's order — what Filter… lists (ADR-0061)
 * — each with its label and whether it is hidden now. For a cube built by {@link aggregatePivot}; a cube
 * built from a source's answer asks the source for a page and lays it out with {@link itemsOfPage}.
 */
export function itemsOfCube(cube: PivotCube, layout: PivotLayout, field: string, options?: PivotOptions): readonly PivotItemInfo[] {
    if (placementOf(layout, field) === undefined && !placedFields(cube.query).some(f => f.field === field)) {
        throw new TypeError(`'${field}' stands in none of Filters, Rows and Columns of the layout.`);
    }
    const allItems = cubeParts(cube).allItems;
    if (allItems === undefined) {
        throw new Error("This cube was built from a Pivot Source's answer, which carries only the Items its leaves have; ask the "
            + "source for the field's Items (PivotSource.items) under the cube's Source Version (ADR-0066).");
    }
    const meta = defined(cubeParts(cube).meta.get(field), `the field '${field}' has its meta`);
    return itemInfos(allItems(field).map(key => new ItemRef(key, toPublicKey(key))), meta, layout, field, options);
}

/**
 * A page of a field's Items as Filter… and the report filter band list them (ADR-0061): in the field's
 * order under the layout — its declared Items first, then by kind and label under the report's locale,
 * descending when the field is — each with its label and whether the layout hides it now.
 */
export function itemsOfPage(page: PivotItemPage, layout: PivotLayout, field: PivotField, options?: PivotOptions): readonly PivotItemInfo[] {
    return itemInfos(page.items.map(key => ItemRef.of(key)), fieldMeta(field), layout, field.name, options);
}

function itemInfos(items: readonly ItemRef[], meta: FieldMeta, layout: PivotLayout, field: string, options: PivotOptions | undefined): readonly PivotItemInfo[] {
    const at = placementOf(layout, field);
    const placement = at === undefined ? createPivotFieldPlacement(field) : elementAt(placementsIn(layout, at.area), at.index);
    const resolved = resolveOptions(options);
    const labels = new ItemLabels(resolved);
    const hidden = new Set(placement.hiddenItems.map(key => keyIdOf(fromPublicKey(key))));
    const order = new ItemOrder(meta, labels, resolved.culture, placement.sort.direction === 'descending' && placement.sort.byValue === undefined);
    for (const item of items) order.prepare(item);
    return Object.freeze(items.toSorted((x, y) => order.compare(x, y)).map(item => Object.freeze({
        key: item.publicKey, label: labels.of(item, meta), isHidden: hidden.has(keyIdOf(item.key)),
    })));
}

/**
 * The records behind one cell of a report — Show Details (ADR-0063) — in their order: those no Hidden Item
 * leaves out, carrying the row's Items and the column's. A label cell is value column −1, and is every
 * record of its row.
 */
export function recordsBehind<TRecord>(records: readonly TRecord[], fields: readonly PivotRecordField<TRecord>[], report: PivotReport,
    row: PivotReportRow, valueColumn: number): readonly TRecord[] {
    return new RecordPivotSource(records, fields).recordsBehind(report.detailsQuery(row, valueColumn));
}
