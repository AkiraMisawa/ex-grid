// A computation's report structure (ADR-0153). Only the working index is mutable: a published part and its
// children's sequence reach immutable rows and parts, never this index or an earlier report.
//
// The first report of a layout is laid out as one array of rows, node by node as the walk lays it out: its
// structure is the base, an entry a node saying where its rows lie. An update lays a node out again only where
// the data asks it to; the node is then split out of the part it lies in — its own rows, and a part for each
// child — and the versions share every part an update did not touch. Each version's rows are its parts read in
// order. The C# engine's ComputationReport.

import { cubeParts, type AxisNode, type PivotCube } from '../cube/cube';
import type { PivotLayout } from '../layout/pivot-layout';
import { defined, elementAt } from '../must';
import { ReportBuilder, rowLabelsAlike, type PivotReport, type PivotReportRow, type PivotRowKey, type PivotRowLabel } from '../report/report';
import { Pace, type ComputationCube } from './cube';

// A node laid out.
interface Laid {
    readonly pendingFrom: number;
    readonly order: readonly number[];
    // Where the node stands among its parent's children, as the parent last ordered them.
    position: number;
    // Its subtree's rows: its own before its children's, then its own after them, and all of them.
    readonly before: number;
    readonly after: number;
    readonly rows: number;
    // The part that is its subtree; `undefined` while that lies within an ancestor's first part.
    published: Part | undefined;
}

type Part = FirstLayout | Branch;

// A subtree as it was first laid out: its rows in order, in an array of its own.
class FirstLayout {
    readonly rows: readonly PivotReportRow[];

    constructor(rows: readonly PivotReportRow[]) {
        this.rows = rows;
    }

    get count(): number {
        return this.rows.length;
    }

    readInto(rows: PivotReportRow[]): void {
        for (const row of this.rows) rows.push(row);
    }
}

// A subtree an update laid out: its own rows, and its children's parts between them.
class Branch {
    readonly before: readonly PivotReportRow[];
    readonly children: Children | undefined;
    readonly after: readonly PivotReportRow[];
    readonly count: number;

    constructor(before: readonly PivotReportRow[], children: Children | undefined, after: readonly PivotReportRow[]) {
        this.before = before;
        this.children = children;
        this.after = after;
        this.count = before.length + (children === undefined ? 0 : children.rows) + after.length;
    }

    readInto(rows: PivotReportRow[]): void {
        for (const row of this.before) rows.push(row);
        this.children?.readInto(rows);
        for (const row of this.after) rows.push(row);
    }
}

// A balanced sequence of siblings with their subtrees' row counts. Replacing one child's part shares every
// sibling and copies only its logarithmic path.
class Children {
    readonly left: Children | undefined;
    readonly value: Part;
    readonly right: Children | undefined;
    readonly count: number;
    readonly rows: number;

    constructor(left: Children | undefined, value: Part, right: Children | undefined) {
        this.left = left;
        this.value = value;
        this.right = right;
        this.count = 1 + (left === undefined ? 0 : left.count) + (right === undefined ? 0 : right.count);
        this.rows = value.count + (left === undefined ? 0 : left.rows) + (right === undefined ? 0 : right.rows);
    }

    static of(values: readonly Part[], start: number, count: number): Children | undefined {
        if (count === 0) return undefined;
        const half = Math.floor(count / 2);
        return new Children(Children.of(values, start, half), elementAt(values, start + half), Children.of(values, start + half + 1, count - half - 1));
    }

    set(index: number, value: Part): Children {
        const before = this.left === undefined ? 0 : this.left.count;
        if (index < before) return new Children(defined(this.left, 'the left of a balanced list').set(index, value), this.value, this.right);
        if (index > before) return new Children(this.left, this.value, defined(this.right, 'the right of a balanced list').set(index - before - 1, value));
        return this.value === value ? this : new Children(this.left, value, this.right);
    }

    readInto(rows: PivotReportRow[]): void {
        this.left?.readInto(rows);
        this.value.readInto(rows);
        this.right?.readInto(rows);
    }
}

/** One layout's report, kept up to date with its computation's cube. */
export class ComputationReport {
    // By node id, which the cube hands out densely.
    readonly #nodes: (Laid | undefined)[] = [];
    readonly #dirtyChildren = new Map<number, Set<number>>();
    readonly #reorder = new Set<number>();
    readonly #relabel = new Set<number>();
    readonly #labels: PivotReportRow[] = [];
    readonly #removed: PivotRowKey[] = [];
    #builder: ReportBuilder | undefined;
    #pace = new Pace();
    #rowSequenceChanged = false;

    /** The rows the last update added, or whose labels or whether they carry values changed. */
    get labelChanges(): readonly PivotReportRow[] {
        return this.#labels;
    }

    /** The keys of the rows the last update took away. */
    get removedRows(): readonly PivotRowKey[] {
        return this.#removed;
    }

    /** Whether the last report laid out, or updated, moved the rows' sequence. */
    get rowSequenceChanged(): boolean {
        return this.#rowSequenceChanged;
    }

    #prepare(builder: ReportBuilder): void {
        this.#builder = builder;
        this.#pace = new Pace();
        this.#dirtyChildren.clear();
        this.#reorder.clear();
        this.#relabel.clear();
        this.#labels.length = 0;
        this.#removed.length = 0;
        this.#rowSequenceChanged = false;
    }

    get #made(): ReportBuilder {
        if (this.#builder === undefined) throw new Error('Internal: the report structure was laid out by no builder.');
        return this.#builder;
    }

    get #layout(): PivotLayout {
        return this.#made.layout;
    }

    #rowNode(id: number): AxisNode {
        return cubeParts(this.#made.cube).rows.node(id);
    }

    /**
     * Lays out the first report of a layout, and the structure its updates follow, in one walk of the row tree:
     * each node's own rows and its children in their order — the rows a node lays out when an update lays it out
     * again. `builder` holds the cube and the layout, checked already.
     */
    *initialize(builder: ReportBuilder): Generator<void, PivotReport> {
        this.#prepare(builder);
        const rows: PivotReportRow[] = [];
        const root = cubeParts(builder.cube).rows.root;
        yield* this.#layOut(root, 0, 0, rows);
        const first = new FirstLayout(rows);
        defined(this.#nodes[root.id], 'the root laid out').published = first;
        this.#rowSequenceChanged = true;
        return yield* builder.buildAround(rows);
    }

    // A node's subtree as first laid out, appended to `rows`: its own rows before its children's, its children in
    // their order, then its own rows after them.
    *#layOut(node: AxisNode, position: number, pendingFrom: number, rows: PivotReportRow[]): Generator<void, void> {
        const start = rows.length;
        const own = this.#made.rowsOf(node, pendingFrom);
        for (const row of own.before) rows.push(row);
        let order: readonly number[] = [];
        if (own.descend) {
            const ordered = yield* this.#made.orderedChildren(node);
            order = ordered.map(child => child.id);
            for (let i = 0; i < ordered.length; i++) {
                yield* this.#layOut(elementAt(ordered, i), i, i === 0 ? pendingFrom : node.level + 1, rows);
                if (this.#pace.spent(1)) yield;
            }
        }
        for (const row of own.after) rows.push(row);
        this.#nodes[node.id] = {
            pendingFrom, order, position, before: own.before.length, after: own.after.length, rows: rows.length - start, published: undefined,
        };
        if (this.#pace.spent(1 + own.before.length + own.after.length)) yield;
    }

    #dirty(node: AxisNode): void {
        for (let current = node; current.parent !== undefined; current = current.parent) {
            let children = this.#dirtyChildren.get(current.parent.id);
            if (children === undefined) {
                children = new Set();
                this.#dirtyChildren.set(current.parent.id, children);
            }
            children.add(current.id);
        }
    }

    /** The report of `cube`, which the computation updated from `previous`'s: only the nodes the update touched
     * laid out again, and every other row shared with `previous`. Run to its end once begun. */
    *update(previous: PivotReport, cube: PivotCube, computation: ComputationCube): Generator<void, PivotReport> {
        this.#prepare(ReportBuilder.again(cube, previous));
        const layout = this.#layout;
        for (const id of computation.changedRowNodes) {
            const node = this.#rowNode(id);
            this.#relabel.add(id);
            this.#reorder.add(id);
            if (node.parent !== undefined) this.#reorder.add(node.parent.id);
            this.#dirty(node);
        }
        // A value sort follows the number shown. Percentages of the column or the grand total may move every
        // sibling order; otherwise only the ancestors of a leaf changed need an order.
        for (const id of computation.valueRows) {
            const node = this.#rowNode(id);
            if (node.parent !== undefined && elementAt(layout.rows, node.level).sort.byValue !== undefined) {
                this.#reorder.add(node.parent.id);
                this.#dirty(this.#rowNode(node.parent.id));
            }
        }
        if (computation.valueRows.size > 0 && layout.rows.some(p => p.sort.byValue !== undefined)
            && layout.values.some(v => v.showValuesAs === 'percentOfGrandTotal' || v.showValuesAs === 'percentOfColumnTotal')) {
            for (let id = 0; id < this.#nodes.length; id++) {
                if (this.#nodes[id] === undefined) continue;
                const node = this.#rowNode(id);
                const next = layout.rows[node.level + 1];
                if (next?.sort.byValue !== undefined) {
                    this.#reorder.add(id);
                    this.#dirty(node);
                }
            }
        }
        const root = yield* this.#updateNode(cubeParts(cube).rows.root, 0, 0, false);
        const columnsChanged = computation.changedColumnNodes.size > 0 || computation.removedColumnNodes.size > 0
            || (computation.valueColumns.size > 0 && layout.columns.some(p => p.sort.byValue !== undefined));
        const rows: PivotReportRow[] = [];
        root.readInto(rows);
        return yield* this.#made.withRows(previous, rows, columnsChanged);
    }

    *#updateNode(node: AxisNode, position: number, pendingFrom: number, ancestorLabelChanged: boolean): Generator<void, Part> {
        const layout = this.#layout;
        const held = this.#nodes[node.id];
        const tabular = layout.form === 'tabular';
        const boundary = held !== undefined && held.pendingFrom !== pendingFrom && tabular;
        const relabelled = this.#relabel.has(node.id);
        const labelsChanged = held === undefined || boundary || relabelled || ancestorLabelChanged;
        const needsOrder = held === undefined || this.#reorder.has(node.id);
        const dirtyChildren = this.#dirtyChildren.get(node.id);
        if (held !== undefined && !labelsChanged && !needsOrder && dirtyChildren === undefined) {
            held.position = position;
            return defined(held.published, 'a node\'s rows as published');
        }
        // Laid out again: split out of the part it was first laid out in.
        const branch = held === undefined ? undefined : this.#split(node.id);
        const own = this.#made.rowsOf(node, pendingFrom);
        const before = this.#keep(own.before, branch?.before);
        const after = this.#keep(own.after, branch?.after);
        const forceChildren = (ancestorLabelChanged || relabelled) && layout.repeatItemLabels;
        const firstLabelChanged = (ancestorLabelChanged || relabelled) && tabular && pendingFrom <= node.level;
        let children = branch?.children;
        let order: readonly number[] = held === undefined ? [] : held.order;
        if (own.descend) {
            const ordered = needsOrder ? yield* this.#made.orderedChildren(node) : undefined;
            const next = ordered === undefined ? order : ordered.map(child => child.id);
            const reordered = order.length !== next.length || order.some((id, i) => id !== next[i]);
            if (held === undefined || reordered) {
                this.#rowSequenceChanged = true;
                if (order.length > 0) {
                    const surviving = new Set(next);
                    for (const old of order) if (!surviving.has(old)) this.#remove(old);
                }
                const orderedChildren = defined(ordered, 'a node\'s children in order');
                const parts: Part[] = [];
                for (let i = 0; i < orderedChildren.length; i++) {
                    // The children as ordered: each the node the cube holds now.
                    parts.push(yield* this.#updateNode(elementAt(orderedChildren, i), i, i === 0 ? pendingFrom : node.level + 1,
                        forceChildren || (i === 0 && firstLabelChanged)));
                    if (this.#pace.spent(1)) yield;
                }
                children = Children.of(parts, 0, parts.length);
                order = next;
            } else {
                const affected = new Set<number>(forceChildren ? order : []);
                if (!forceChildren && dirtyChildren !== undefined) for (const id of dirtyChildren) affected.add(id);
                if ((boundary || firstLabelChanged) && order.length > 0) affected.add(elementAt(order, 0));
                for (const id of affected) {
                    // A child the order holds: one it no longer holds is not laid out.
                    const child = this.#nodes[id];
                    if (child === undefined || child.position >= order.length || order[child.position] !== id) continue;
                    const at = child.position;
                    const made = yield* this.#updateNode(this.#rowNode(id), at, at === 0 ? pendingFrom : node.level + 1,
                        forceChildren || (at === 0 && firstLabelChanged));
                    children = defined(children, 'a node\'s children as laid out').set(at, made);
                }
            }
        } else if (order.length > 0) {
            for (const old of order) this.#remove(old);
            children = undefined;
            order = [];
            this.#rowSequenceChanged = true;
        }
        const published = branch !== undefined && before === branch.before && after === branch.after && children === branch.children
            ? branch : new Branch(before, children, after);
        this.#nodes[node.id] = {
            pendingFrom, order, position, before: before.length, after: after.length, rows: published.count, published,
        };
        if (this.#pace.spent(1 + before.length + after.length)) yield;
        return published;
    }

    // A node first laid out lies in one part with its subtree. Laid out again, it is split: its own rows, and a
    // part for each child holding the child's subtree — copied out, so that a later version holds only the rows
    // it shows.
    #split(id: number): Branch {
        const held = defined(this.#nodes[id], 'a node laid out');
        const published = defined(held.published, 'a node\'s rows as published');
        if (published instanceof Branch) return published;
        const rows = published.rows;
        let children: Children | undefined;
        if (held.order.length > 0) {
            const parts: Part[] = [];
            let at = held.before;
            for (const childId of held.order) {
                const child = defined(this.#nodes[childId], 'a child laid out');
                const part = new FirstLayout(rows.slice(at, at + child.rows));
                child.published = part;
                parts.push(part);
                at += child.rows;
            }
            children = Children.of(parts, 0, parts.length);
        }
        const made = new Branch(rows.slice(0, held.before), children, rows.slice(rows.length - held.after));
        held.published = made;
        return made;
    }

    // A node's own rows as laid out again: each the row held for its key where it shows alike, so the Window
    // keeps that row's instance; the rows that are new or show otherwise are the label changes.
    #keep(made: PivotReportRow[], held: readonly PivotReportRow[] | undefined): readonly PivotReportRow[] {
        if (held === undefined) {
            // A node laid out for the first time: its rows are new.
            for (const row of made) this.#labels.push(row);
            return made;
        }
        let identical = held.length === made.length;
        for (let i = 0; i < made.length; i++) {
            const row = elementAt(made, i);
            const prior = held.find(candidate => candidate.key.equals(row.key));
            // Its Items spelt alike too: a row whose Item took another spelling stands for it as it is now spelt.
            if (prior !== undefined && prior.carriesValues === row.carriesValues && sameLabels(prior.labels, row.labels)
                && spelledAlike(prior.key, row.key)) made[i] = prior;
            else this.#labels.push(row);
            identical &&= held[i] === made[i];
        }
        return identical ? held : made;
    }

    #remove(id: number): void {
        const held = defined(this.#nodes[id], 'a node laid out');
        const published = defined(held.published, 'a node\'s rows as published');
        if (published instanceof FirstLayout) {
            this.#removeFirst(id, published.rows, 0);
            return;
        }
        for (const row of published.before) this.#removed.push(row.key);
        for (const row of published.after) this.#removed.push(row.key);
        for (const child of held.order) this.#remove(child);
        this.#nodes[id] = undefined;
    }

    // A subtree as first laid out, within `rows` from `start`: its own rows, then its children's.
    #removeFirst(id: number, rows: readonly PivotReportRow[], start: number): void {
        const held = defined(this.#nodes[id], 'a node laid out');
        for (let i = start; i < start + held.before; i++) this.#removed.push(elementAt(rows, i).key);
        for (let i = start + held.rows - held.after; i < start + held.rows; i++) this.#removed.push(elementAt(rows, i).key);
        let at = start + held.before;
        for (const child of held.order) {
            const count = defined(this.#nodes[child], 'a child laid out').rows;
            this.#removeFirst(child, rows, at);
            at += count;
        }
        this.#nodes[id] = undefined;
    }
}

// Two keys of one row, whose Items are spelt alike.
function spelledAlike(a: PivotRowKey, b: PivotRowKey): boolean {
    return a.items.every((item, i) => item.kind === elementAt(b.items, i).kind && item.value === elementAt(b.items, i).value);
}

function sameLabels(a: readonly PivotRowLabel[], b: readonly PivotRowLabel[]): boolean {
    return a.length === b.length && a.every((label, i) => rowLabelsAlike(label, elementAt(b, i)));
}

