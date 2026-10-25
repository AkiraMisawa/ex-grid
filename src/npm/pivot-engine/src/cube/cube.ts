// The cube a report is laid out from (ADR-0060/0066): the answer's leaves as the first cells, each axis's
// tree of Items, and every total merged from the leaves beneath it — never a total of totals. The C#
// engine's PivotCube, AxisNode and ItemRef. The order totals are merged in is C#'s, leaf by leaf, each
// leaf into its row's ancestors and its column's, so a double total agrees with C#'s to the last bit.

import { PartColumns, emptyValue, type AggregateValue } from '../aggregates/parts';
import { fromPublicKey, type ItemKey, type PivotItemKey } from '../items/item-key';
import type { FieldMeta, PivotOrderKeyValue } from '../fields/pivot-field';
import { sameKeys, hidingFilters, placedFields, type PivotQuery, type PivotQueryField } from './query';
import { answerColumns, type PivotAnswer, type PivotAnswerAxis } from './answer';
import type { PivotLayout, PivotFieldPlacement } from '../layout/pivot-layout';
import { pivotAggregations, type PivotAggregation } from '../layout/pivot-enums';
import { elementAt } from '../must';
import { inPieces, runWhole } from '../source/slicing';

/** An Item as the report reads it: its key, the value an Order Key and a format read, and its public key. */
export class ItemRef {
    readonly key: ItemKey;
    readonly publicKey: PivotItemKey;
    /** The text of a text Item, the number of a number, the ticks of a date, the Boolean of a Boolean;
     * `undefined` for a Blank and for #NUM!. */
    readonly firstValue: string | number | bigint | boolean | undefined;
    #orderKey: PivotOrderKeyValue | undefined;
    #hasOrderKey = false;

    constructor(key: ItemKey, publicKey: PivotItemKey) {
        this.key = key;
        this.publicKey = publicKey;
        switch (key.kind) {
            case 'text': this.firstValue = key.text; break;
            case 'number': this.firstValue = key.number; break;
            case 'date': this.firstValue = key.ticks; break;
            case 'boolean': this.firstValue = key.ticks === 1n; break;
            case 'error':
            case 'blank': this.firstValue = undefined; break;
        }
    }

    /** The Item a public key names. */
    static of(publicKey: PivotItemKey): ItemRef {
        return new ItemRef(fromPublicKey(publicKey), publicKey);
    }

    /** Whether the Order Key was asked for this Item. */
    get hasOrderKey(): boolean {
        return this.#hasOrderKey;
    }

    /** The Order Key's answer for this Item, `undefined` for none. */
    get orderKey(): PivotOrderKeyValue | undefined {
        return this.#orderKey;
    }

    /** Keeps the Order Key's answer, asked once. */
    setOrderKey(key: PivotOrderKeyValue | undefined): void {
        this.#orderKey = key;
        this.#hasOrderKey = true;
    }
}

/** A node of an axis's tree: the root, or an Item under its parent's. */
export class AxisNode {
    readonly id: number;
    /** −1 for the root. */
    readonly level: number;
    readonly parent: AxisNode | undefined;
    readonly item: ItemRef | undefined;
    readonly children: AxisNode[] = [];
    readonly #childIndex = new Map<number, AxisNode>();

    constructor(id: number, level: number, parent: AxisNode | undefined, item: ItemRef | undefined) {
        this.id = id;
        this.level = level;
        this.parent = parent;
        this.item = item;
    }

    /** The child of the Item at `index` among the next level's Items, made on first asking. */
    child(index: number, item: ItemRef, nodes: AxisNode[]): AxisNode {
        let child = this.#childIndex.get(index);
        if (child === undefined) {
            child = new AxisNode(nodes.length, this.level + 1, this, item);
            nodes.push(child);
            this.children.push(child);
            this.#childIndex.set(index, child);
        }
        return child;
    }
}

const nodeLimit = 67108864;

/** A cell by its row's and its column's node ids, each below 2^26, so the key is a safe integer: a cube of
 * more nodes than that holds more cells than a Map can. */
export function cellKey(row: number, column: number): number {
    if (row >= nodeLimit || column >= nodeLimit) throw new RangeError(`A cube holds at most ${nodeLimit} nodes an axis.`);
    return row * nodeLimit + column;
}

/** One axis of a cube as a report reads it: its root, and each node's children and present form. A cube built
 * from an answer holds its tree as it was made; a computation's later versions hold it under an overlay of the
 * nodes and child lists their updates wrote (ADR-0153). Not part of the public surface. */
export interface CubeAxis {
    readonly root: AxisNode;
    /** The node of an id. */
    node(id: number): AxisNode;
    /** The node as this version holds it: an Item relabelled since the node was made carries its new spelling. */
    nodeOf(node: AxisNode): AxisNode;
    /** A node's children as this version holds them, in the order they were made. */
    childrenOf(node: AxisNode): readonly AxisNode[];
}

/** A cube's cells, by the ids of their row's and their column's nodes. Not part of the public surface. */
export interface CubeCells {
    /** An Aggregation's answer at a cell, Empty where the cube holds none. */
    read(row: number, column: number, source: number, aggregation: PivotAggregation): AggregateValue;
}

/** What the report reads of a cube: not part of the public surface. */
export interface CubeParts {
    readonly meta: ReadonlyMap<string, FieldMeta>;
    readonly rows: CubeAxis;
    readonly columns: CubeAxis;
    /** Each field in Values, by the answer's order. */
    readonly sources: readonly string[];
    /** Every Item of a field over all the data, where the cube was made from records it can read again. */
    readonly allItems: ((field: string) => readonly ItemKey[]) | undefined;
    readonly cells: CubeCells;
}

/** What a cube's build made, for a computation that keeps the cube up to date (ADR-0153): each tree's nodes by
 * id, the nodes each of the answer's leaves stands at, in the answer's order, and the cells — the leaves first,
 * as the answer has them, then the totals. Nothing writes any of it once the cube is made. */
export interface CubeBuild {
    readonly rowNodes: readonly AxisNode[];
    readonly columnNodes: readonly AxisNode[];
    readonly rowLeaves: readonly AxisNode[];
    readonly columnLeaves: readonly AxisNode[];
    readonly cells: ReadonlyMap<number, number>;
    readonly values: readonly PartColumns[];
}

// An axis as its build made it.
class BuiltAxis implements CubeAxis {
    readonly root: AxisNode;
    readonly #nodes: readonly AxisNode[];

    constructor(nodes: readonly AxisNode[]) {
        this.root = elementAt(nodes, 0);
        this.#nodes = nodes;
    }

    node(id: number): AxisNode {
        return elementAt(this.#nodes, id);
    }

    nodeOf(node: AxisNode): AxisNode {
        return node;
    }

    childrenOf(node: AxisNode): readonly AxisNode[] {
        return node.children;
    }
}

// The cells as the build made them.
class BuiltCells implements CubeCells {
    readonly #cells: ReadonlyMap<number, number>;
    readonly #values: readonly PartColumns[];

    constructor(cells: ReadonlyMap<number, number>, values: readonly PartColumns[]) {
        this.#cells = cells;
        this.#values = values;
    }

    read(row: number, column: number, source: number, aggregation: PivotAggregation): AggregateValue {
        const cell = this.#cells.get(cellKey(row, column));
        return cell === undefined ? emptyValue : elementAt(this.#values, source).read(cell, aggregation);
    }
}

/** An Aggregation's answer at a row and a column of a cube. */
export function readCell(parts: CubeParts, row: AxisNode, column: AxisNode, source: number, aggregation: PivotAggregation): AggregateValue {
    return parts.cells.read(row.id, column.id, source, aggregation);
}

let partsOfCube: (cube: PivotCube) => CubeParts;
let makeCube: (query: PivotQuery, sourceVersion: string, included: number, parts: CubeParts, values: readonly PartColumns[]) => PivotCube;
let withCells: (cube: PivotCube, sourceVersion: string, included: number, rows: CubeAxis, columns: CubeAxis, cells: CubeCells) => PivotCube;

/** The cube a report is laid out from (ADR-0060/0066): the answer's leaves, each axis's tree of Items, and
 * every total merged from the leaves beneath it. Made by {@link pivotCube} or {@link aggregatePivot}. */
export class PivotCube {
    /** The question the cube answers. */
    readonly query: PivotQuery;
    /** The Source Version its answer was computed at. */
    readonly sourceVersion: string;
    /** How many records its leaves hold: those no Hidden Item left out. */
    readonly includedRecordCount: number;
    readonly #parts: CubeParts;
    readonly #values: readonly PartColumns[];

    private constructor(query: PivotQuery, sourceVersion: string, included: number, parts: CubeParts, values: readonly PartColumns[]) {
        this.query = query;
        this.sourceVersion = sourceVersion;
        this.includedRecordCount = included;
        this.#parts = parts;
        this.#values = values;
        Object.freeze(this);
    }

    static {
        partsOfCube = cube => cube.#parts;
        makeCube = (query, sourceVersion, included, parts, values) => new PivotCube(query, sourceVersion, included, parts, values);
        withCells = (cube, sourceVersion, included, rows, columns, cells) =>
            new PivotCube(cube.query, sourceVersion, included, Object.freeze({ ...cube.#parts, rows, columns, cells }), cube.#values);
    }

    /** Whether the cube was aggregated under the layout's placed fields, Hidden Items and Value Fields, with
     * the parts each of its Aggregations reads. */
    holds(layout: PivotLayout): boolean {
        if (!sameFields(layout.rows, this.query.rows, true)
            || !sameFields(layout.columns, this.query.columns, true)
            || !sameFields(hidingFilters(layout.filters), this.query.filters.filter(f => f.hiddenItems.length > 0), false)) {
            return false;
        }
        for (const value of layout.values) {
            const source = this.#parts.sources.indexOf(value.field);
            if (source < 0 || !pivotAggregations.includes(value.aggregation) || !elementAt(this.#values, source).answers(value.aggregation)) return false;
        }
        return true;
    }
}

/** The parts of a cube the report reads. */
export function cubeParts(cube: PivotCube): CubeParts {
    return partsOfCube(cube);
}

/** The cube a computation's update made of `cube` (ADR-0153): the same question, fields and Values, at another
 * Source Version, with the trees and cells the update holds. */
export function cubeWith(cube: PivotCube, sourceVersion: string, included: number, rows: CubeAxis, columns: CubeAxis, cells: CubeCells): PivotCube {
    return withCells(cube, sourceVersion, included, rows, columns, cells);
}

/** The cube of an answer to a question (ADR-0066): an answer to another question is refused by name, and so
 * is a field no declared Pivot Field names, or two leaves for one cell. */
export function buildCube(query: PivotQuery, answer: PivotAnswer, meta: ReadonlyMap<string, FieldMeta>,
    allItems?: (field: string) => readonly ItemKey[]): PivotCube {
    return runWhole(buildCubeSteps(query, answer, meta, allItems));
}

/** {@link buildCube}, a step at a time (PV-40): the trees, the leaves' cells, every total merged and every exact
 * value written in one form, each over its leaves or cells a piece at a time, yielding after each piece for a
 * caller to slice. The answer is only read. `made` is handed what the build made, for a computation that keeps
 * the cube up to date (ADR-0153). */
export function* buildCubeSteps(query: PivotQuery, answer: PivotAnswer, meta: ReadonlyMap<string, FieldMeta>,
    allItems?: (field: string) => readonly ItemKey[], made?: (build: CubeBuild) => void): Generator<void, PivotCube> {
    const mismatch = answer.mismatch(query);
    if (mismatch !== undefined) throw new Error(`The answer does not answer the question: ${mismatch} (ADR-0066).`);
    // The report from Level Aggregates, each sibling set checked against its parent, is not built yet.
    if (answer.isLevels) throw new Error('The answer holds Level Aggregates, which a cube is not made from yet (ADR-0370 section 3).');
    for (const field of [...placedFields(query).map(f => f.field), ...query.values.map(v => v.field)]) {
        if (!meta.has(field)) throw new Error(`The question names '${field}', and no Pivot Field of that name is declared (ADR-0060).`);
    }
    const leafCount = answer.leafCount;
    const rowTree = yield* tree(answer.rows, leafCount);
    const columnTree = yield* tree(answer.columns, leafCount);
    const rowLeaves = rowTree.leaves;
    const columnLeaves = columnTree.leaves;

    const sources = answer.values.map(v => v.field);
    const values: PartColumns[] = [];
    for (const answered of answer.values) {
        const held = answerColumns(answered);
        const columns = new PartColumns(held.parts);
        yield* inPieces(leafCount, 1, (from, to) => {
            for (let leaf = from; leaf < to; leaf++) columns.copyCell(held, leaf);
        });
        values.push(columns);
    }

    // The leaves are the first cells, as the source answered them.
    const cells = new Map<number, number>();
    let included = 0;
    yield* inPieces(leafCount, 2, (from, to) => {
        for (let leaf = from; leaf < to; leaf++) {
            const key = cellKey(elementAt(rowLeaves, leaf).id, elementAt(columnLeaves, leaf).id);
            if (cells.has(key)) {
                throw new Error(`The answer has two leaves for one cell (leaf ${leaf}); a source answers each combination of Items once (ADR-0066).`);
            }
            cells.set(key, leaf);
            included += answer.recordsAt(leaf);
        }
    });

    // Every total from its leaves (ADR-0060): each leaf is merged into every cell whose row and column
    // are its own or their ancestors. An ancestor's cell is never a leaf's, so nothing is counted twice.
    if (values.length > 0) {
        // What one leaf costs: a cell looked up, and every field merged into it, for each pair of its row and
        // column or their ancestors.
        const weight = (answer.rows.length + 1) * (answer.columns.length + 1) * (1 + values.length);
        yield* inPieces(leafCount, weight, (from, to) => {
            for (let leaf = from; leaf < to; leaf++) {
                const rowLeaf = elementAt(rowLeaves, leaf);
                const columnLeaf = elementAt(columnLeaves, leaf);
                for (let row: AxisNode | undefined = rowLeaf; row !== undefined; row = row.parent) {
                    for (let column: AxisNode | undefined = columnLeaf; column !== undefined; column = column.parent) {
                        if (row === rowLeaf && column === columnLeaf) continue;
                        const key = cellKey(row.id, column.id);
                        let cell = cells.get(key);
                        if (cell === undefined) {
                            cell = elementAt(values, 0).count;
                            cells.set(key, cell);
                            for (const columns of values) columns.addCell();
                        }
                        for (const columns of values) columns.merge(cell, columns, leaf);
                    }
                }
            }
        });
    }

    // One form for each exact value (ADR-0064): decimal addition keeps the larger scale.
    for (const columns of values) yield* inPieces(columns.count, 1, (from, to) => columns.canonicalize(from, to));
    const parts: CubeParts = Object.freeze({
        meta, sources, allItems,
        rows: new BuiltAxis(rowTree.nodes),
        columns: new BuiltAxis(columnTree.nodes),
        cells: new BuiltCells(cells, values),
    });
    made?.(Object.freeze({ rowNodes: rowTree.nodes, columnNodes: columnTree.nodes, rowLeaves, columnLeaves, cells, values }));
    return makeCube(query, answer.sourceVersion, included, parts, values);
}

function sameFields(placements: readonly PivotFieldPlacement[], held: readonly PivotQueryField[], ordered: boolean): boolean {
    if (placements.length !== held.length) return false;
    return placements.every((mine, i) => {
        const theirs = ordered ? held[i] : held.find(f => f.field === mine.field);
        return theirs !== undefined && theirs.field === mine.field && sameKeys(mine.hiddenItems, theirs.hiddenItems);
    });
}

// One axis's tree from the leaves' Items, level by level, a piece of leaves at a time: its nodes by id, and the
// node each leaf ends at.
function* tree(axes: readonly PivotAnswerAxis[], leafCount: number): Generator<void, { readonly nodes: AxisNode[]; readonly leaves: AxisNode[] }> {
    const root = new AxisNode(0, -1, undefined, undefined);
    const nodes: AxisNode[] = [root];
    const refs: ItemRef[][] = [];
    for (const axis of axes) {
        const level: ItemRef[] = [];
        yield* inPieces(axis.items.length, 1, (from, to) => {
            for (let item = from; item < to; item++) level.push(ItemRef.of(elementAt(axis.items, item)));
        });
        refs.push(level);
    }
    const leaves: AxisNode[] = [];
    yield* inPieces(leafCount, Math.max(1, axes.length), (from, to) => {
        for (let leaf = from; leaf < to; leaf++) {
            let node = root;
            axes.forEach((axis, level) => {
                const item = axis.itemAt(leaf);
                node = node.child(item, elementAt(elementAt(refs, level), item), nodes);
            });
            leaves.push(node);
        }
    });
    return { nodes, leaves };
}
