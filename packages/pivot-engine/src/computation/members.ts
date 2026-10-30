// The leaves of one total, ordered by their first record, then their number: the order a fresh pass merges them
// in (ADR-0153). A set of (first record, leaf) pairs: the pairs the computation began with, sorted once, and the
// few that updates added or took away since — so beginning a computation costs two arrays a total, not an
// entry a leaf. The C# engine's ComputationCube.Members.

import { elementAt } from '../must';

export class Members {
    #firsts: Int32Array | number[] = [];
    #leaves: Int32Array | number[] = [];
    #count = 0;
    // Positions among the first pairs no longer members.
    #gone: Set<number> | undefined;
    // Members the first pairs do not hold, by `first,leaf`.
    #added: Map<string, readonly [number, number]> | undefined;

    /** How many leaves the total holds. */
    get count(): number {
        let count = this.#count;
        if (this.#gone !== undefined) count -= this.#gone.size;
        if (this.#added !== undefined) count += this.#added.size;
        return count;
    }

    /** A pair the computation begins with; {@link seal} orders them. */
    append(first: number, leaf: number): void {
        if (!Array.isArray(this.#firsts) || !Array.isArray(this.#leaves)) throw new Error('Internal: a total\'s first leaves are sealed (ADR-0153).');
        this.#firsts.push(first);
        this.#leaves.push(leaf);
        this.#count++;
    }

    /** Ends the first pairs: sorted, where they did not come in order. */
    seal(): void {
        const firsts = Int32Array.from(this.#firsts);
        const leaves = Int32Array.from(this.#leaves);
        let sorted = true;
        for (let i = 1; i < this.#count && sorted; i++) sorted = compare(elementAt(firsts, i - 1), elementAt(leaves, i - 1), elementAt(firsts, i), elementAt(leaves, i)) < 0;
        if (sorted) {
            this.#firsts = firsts;
            this.#leaves = leaves;
            return;
        }
        const order = Array.from({ length: this.#count }, (_, i) => i)
            .toSorted((a, b) => compare(elementAt(firsts, a), elementAt(leaves, a), elementAt(firsts, b), elementAt(leaves, b)));
        this.#firsts = Int32Array.from(order, i => elementAt(firsts, i));
        this.#leaves = Int32Array.from(order, i => elementAt(leaves, i));
    }

    // The position of a pair among the first ones, or −1.
    #find(first: number, leaf: number): number {
        let low = 0;
        let high = this.#count - 1;
        while (low <= high) {
            const middle = (low + high) >>> 1;
            const compared = compare(elementAt(this.#firsts, middle), elementAt(this.#leaves, middle), first, leaf);
            if (compared === 0) return middle;
            if (compared < 0) low = middle + 1;
            else high = middle - 1;
        }
        return -1;
    }

    /** Makes a leaf a member at its first record. */
    add(first: number, leaf: number): void {
        const at = this.#find(first, leaf);
        if (at >= 0) this.#gone?.delete(at);
        else (this.#added ??= new Map()).set(`${first},${leaf}`, [first, leaf]);
    }

    /** Takes a leaf at its first record out. */
    remove(first: number, leaf: number): void {
        if (this.#added?.delete(`${first},${leaf}`) === true) return;
        const at = this.#find(first, leaf);
        if (at >= 0) (this.#gone ??= new Set()).add(at);
    }

    /** The members' leaves, in order. */
    *leaves(): Generator<number, void> {
        const added = this.#added === undefined ? [] : [...this.#added.values()].toSorted((a, b) => compare(a[0], a[1], b[0], b[1]));
        let next = 0;
        for (let at = 0; at < this.#count; at++) {
            if (this.#gone?.has(at) === true) continue;
            const first = elementAt(this.#firsts, at);
            const leaf = elementAt(this.#leaves, at);
            for (let pair = added[next]; pair !== undefined && compare(pair[0], pair[1], first, leaf) < 0; pair = added[++next]) yield pair[1];
            yield leaf;
        }
        for (let pair = added[next]; pair !== undefined; pair = added[++next]) yield pair[1];
    }
}

function compare(firstA: number, leafA: number, firstB: number, leafB: number): number {
    return firstA !== firstB ? firstA - firstB : leafA - leafB;
}
