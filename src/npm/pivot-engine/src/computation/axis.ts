// One axis of a computation (ADR-0153): the first cube's tree, by node id, under an overlay of the nodes and the
// child lists the updates since wrote. A version shares every page an update did not write; it reaches no
// version before it, and nothing writes the first tree. The C# engine's ComputationAxis and its Working index.

import { AxisNode, ItemRef, type CubeAxis } from '../cube/cube';
import { keyIdOf, type PivotItemKey } from '../items/item-key';
import { elementAt } from '../must';
import type { AggregationPass } from '../source/aggregation-pass';
import { PagedArray, type PagedWriter } from './paged';

/** One published version of an axis. A node's parents are this version's: a relabelled node's descendants are
 * made again under it. */
export class ComputationAxis implements CubeAxis {
    readonly root: AxisNode;
    readonly #initial: readonly AxisNode[];
    readonly #nodes: PagedArray<AxisNode>;
    readonly #children: PagedArray<readonly number[]>;
    readonly #relabelled: boolean;
    // Each child list read through the overlay, made once: a version never changes.
    readonly #read = new Map<number, readonly AxisNode[]>();

    constructor(initial: readonly AxisNode[], nodes: PagedArray<AxisNode>, children: PagedArray<readonly number[]>, relabelled: boolean) {
        this.#initial = initial;
        this.#nodes = nodes;
        this.#children = children;
        this.#relabelled = relabelled;
        this.root = this.node(0);
    }

    node(id: number): AxisNode {
        return this.#nodes.get(id) ?? elementAt(this.#initial, id);
    }

    nodeOf(node: AxisNode): AxisNode {
        return this.node(node.id);
    }

    childrenOf(node: AxisNode): readonly AxisNode[] {
        const ids = this.#children.get(node.id);
        // A list no update wrote, under no node an update relabelled, is the first tree's.
        if (ids === undefined && (!this.#relabelled || node.id >= this.#initial.length)) return node.id < this.#initial.length ? elementAt(this.#initial, node.id).children : [];
        let read = this.#read.get(node.id);
        if (read === undefined) {
            read = Object.freeze(ids === undefined ? elementAt(this.#initial, node.id).children.map(child => this.node(child.id)) : ids.map(id => this.node(id)));
            this.#read.set(node.id, read);
        }
        return read;
    }
}

/**
 * The working index of one axis, private to its computation. It starts as the first cube's tree, which it never
 * writes: a node an update makes, relabels or gives other children is written to the overlay it publishes. What
 * only an update reads — each parent's children by Item, and the nodes of each Item — is made the first time an
 * update reads it, so the first report costs what its own cube does.
 */
export class WorkingAxis {
    readonly #initial: readonly AxisNode[];
    #nodes: PagedArray<AxisNode> = PagedArray.empty();
    #children: PagedArray<readonly number[]> = PagedArray.empty();
    #nodeWriter: PagedWriter<AxisNode> | undefined;
    #childWriter: PagedWriter<readonly number[]> | undefined;
    #relabelled = false;
    // Each parent's children by their Item's key: made the first time an update looks for a child under it.
    readonly #paths = new Map<number, Map<string, number>>();
    // How many leaves each node is on the path of, by id; the root is not counted.
    #users: Int32Array;
    #next: number;
    // The label every node of an Item carries, by level and the pass's Item: an Item's nodes are labelled
    // alike, and relabelled together. And the nodes of each Item, made the first time an Item's label changes.
    readonly #labels: (PivotItemKey | undefined)[][];
    #byItem: Map<string, number[]> | undefined;
    /** The nodes an update made, relabelled, or gave other children, or that took a leaf on again. */
    readonly changed = new Set<number>();
    /** The nodes an update took the last leaf from. */
    readonly removed = new Set<number>();

    constructor(initial: readonly AxisNode[], levels: number) {
        this.#initial = initial;
        this.#next = initial.length;
        this.#users = new Int32Array(Math.max(16, initial.length + Math.floor(initial.length / 8)));
        this.#labels = Array.from({ length: levels }, () => []);
    }

    /** Begins an update: nothing changed yet. */
    begin(): void {
        this.changed.clear();
        this.removed.clear();
    }

    /** The version written so far, published. */
    freeze(): ComputationAxis {
        if (this.#nodeWriter !== undefined) this.#nodes = this.#nodeWriter.freeze();
        if (this.#childWriter !== undefined) this.#children = this.#childWriter.freeze();
        this.#nodeWriter = undefined;
        this.#childWriter = undefined;
        return new ComputationAxis(this.#initial, this.#nodes, this.#children, this.#relabelled);
    }

    /** The root, as the overlay holds it. */
    get root(): AxisNode {
        return this.#node(0);
    }

    #node(id: number): AxisNode {
        return (this.#nodeWriter === undefined ? this.#nodes.get(id) : this.#nodeWriter.get(id)) ?? elementAt(this.#initial, id);
    }

    #setNode(node: AxisNode): void {
        this.#nodeWriter ??= this.#nodes.writer();
        this.#nodeWriter.set(node.id, node);
    }

    #setChildren(parent: number, ids: readonly number[]): void {
        this.#childWriter ??= this.#children.writer();
        this.#childWriter.set(parent, Object.freeze(ids));
    }

    /** A leaf the first cube holds, at the node the cube made for it: every node on its path counts it, and each
     * Item on the path is labelled as the cube labelled it. `start` is the axis's first level among the pass's. */
    use(pass: AggregationPass, leaf: number, start: number, node: AxisNode): void {
        for (let at = node; at.parent !== undefined; at = at.parent) {
            this.#users[at.id] = elementAt(this.#users, at.id) + 1;
            const labels = elementAt(this.#labels, at.level);
            const item = pass.itemOfLeaf(start + at.level, leaf);
            labels[item] ??= itemOf(at).publicKey;
        }
    }

    /** The node a leaf the batches brought stands at, made where it is new, and counted on each node of its
     * path: a node that took its first leaf, or took one again, joins its parent's children. */
    addLeaf(pass: AggregationPass, leaf: number, start: number, count: number): AxisNode {
        let parent = this.root;
        for (let level = 0; level < count; level++) {
            const space = pass.axis(start + level);
            const item = pass.itemOfLeaf(start + level, leaf);
            const paths = this.#pathsUnder(parent.id);
            const key = keyIdOf(space.keyOf(item));
            let id = paths.get(key);
            if (id === undefined) {
                id = this.#next++;
                const label = space.publicKeyOf(item);
                this.#setNode(new AxisNode(id, level, parent, ItemRef.of(label)));
                paths.set(key, id);
                this.#ensureUsers(id);
                // The Item's first node sets the label its nodes carry. A node made with a newer spelling than
                // the others carry came with a batch that touched its Item, so relabel brings the others to it.
                const labels = elementAt(this.#labels, level);
                labels[item] ??= label;
                if (this.#byItem !== undefined) nodesOf(this.#byItem, level, key).push(id);
            }
            const users = elementAt(this.#users, id);
            this.#users[id] = users + 1;
            if (users === 0) {
                const siblings = this.#childIds(parent.id);
                if (!siblings.includes(id)) this.#setChildren(parent.id, [...siblings, id]);
                this.changed.add(id);
                this.changed.add(parent.id);
            }
            parent = this.#node(id);
        }
        return parent;
    }

    /** Takes a leaf off each node of its path: a node left with none leaves its parent's children. */
    removeLeaf(leaf: AxisNode): void {
        for (let node = leaf; node.parent !== undefined; node = node.parent) {
            const users = elementAt(this.#users, node.id) - 1;
            this.#users[node.id] = users;
            if (users !== 0) continue;
            const parent = node.parent.id;
            this.#setChildren(parent, this.#childIds(parent).filter(id => id !== node.id));
            this.removed.add(node.id);
            this.changed.add(parent);
        }
    }

    /** Relabels the nodes of each Item a batch touched whose spelling changed: a text Item is labelled by its
     * first spelling among the records present (ADR-0060). */
    relabel(pass: AggregationPass, start: number, count: number): void {
        for (let level = 0; level < count; level++) {
            const space = pass.axis(start + level);
            const labels = elementAt(this.#labels, level);
            for (const item of space.changedItems) {
                // An Item no node carries has no label to change.
                const label = labels[item];
                if (label === undefined) continue;
                const key = space.publicKeyOf(item);
                if (label.kind === key.kind && label.value === key.value) continue;
                labels[item] = key;
                const ids = this.#nodesByItem().get(itemId(level, keyIdOf(space.keyOf(item))));
                if (ids === undefined) continue;
                for (const id of ids) {
                    const before = this.#node(id);
                    const held = itemOf(before).publicKey;
                    if (held.kind === key.kind && held.value === key.value) continue;
                    const node = new AxisNode(id, before.level, before.parent, ItemRef.of(key));
                    this.#setNode(node);
                    this.#relabelled = true;
                    if (elementAt(this.#users, id) > 0) this.changed.add(id);
                    this.#reparent(node);
                }
            }
        }
    }

    // Every node ever made under a relabelled one, made again under it: a node's path, read up its parents, is
    // then spelled as this version spells it, wherever it is read from.
    #reparent(parent: AxisNode): void {
        for (const id of this.#pathsUnder(parent.id).values()) {
            const child = this.#node(id);
            const node = new AxisNode(id, child.level, parent, child.item);
            this.#setNode(node);
            if (elementAt(this.#users, id) > 0) this.changed.add(id);
            this.#reparent(node);
        }
    }

    #ensureUsers(id: number): void {
        if (id < this.#users.length) return;
        const grown = new Int32Array(Math.max(id + 1, this.#users.length * 2));
        grown.set(this.#users);
        this.#users = grown;
    }

    #childIds(parent: number): readonly number[] {
        const written = this.#childWriter === undefined ? this.#children.get(parent) : this.#childWriter.get(parent);
        if (written !== undefined) return written;
        if (parent >= this.#initial.length) return [];
        return elementAt(this.#initial, parent).children.map(child => child.id);
    }

    #pathsUnder(parent: number): Map<string, number> {
        let paths = this.#paths.get(parent);
        if (paths !== undefined) return paths;
        paths = new Map();
        if (parent < this.#initial.length) {
            for (const child of elementAt(this.#initial, parent).children) paths.set(keyIdOf(itemOf(child).key), child.id);
        }
        this.#paths.set(parent, paths);
        return paths;
    }

    // Every node but the root, by its level and Item: the first cube's and the updates'.
    #nodesByItem(): Map<string, number[]> {
        if (this.#byItem !== undefined) return this.#byItem;
        const index = new Map<string, number[]>();
        for (let id = 1; id < this.#next; id++) {
            const node = this.#node(id);
            if (node.item !== undefined) nodesOf(index, node.level, keyIdOf(node.item.key)).push(id);
        }
        this.#byItem = index;
        return index;
    }
}

function itemId(level: number, key: string): string {
    return `${level}\u0001${key}`;
}

function nodesOf(index: Map<string, number[]>, level: number, key: string): number[] {
    const id = itemId(level, key);
    let nodes = index.get(id);
    if (nodes === undefined) {
        nodes = [];
        index.set(id, nodes);
    }
    return nodes;
}

function itemOf(node: AxisNode): ItemRef {
    if (node.item === undefined) throw new Error('Internal: the root has no Item.');
    return node.item;
}
