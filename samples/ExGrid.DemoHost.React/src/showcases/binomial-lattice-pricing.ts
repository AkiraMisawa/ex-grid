// A European or American option priced on a binomial lattice, as ExChart.Showcases' BinomialLattice.cs prices
// it: the Binomial lattice Showcase draws its nodes, and /chart-spec hands them to a Chart Spec as a table.

/** One node: step i, up-moves j, the underlying there, the option's value, what exercise pays, and whether an American option is exercised there early. */
export interface LatticeNode {
    readonly step: number;
    readonly up: number;
    readonly spot: number;
    readonly value: number;
    readonly exercise: number;
    readonly early: boolean;
}

export const keyOf = (x: LatticeNode): string => `${String(x.step)}:${String(x.up)}`;

export interface OptionTerms {
    readonly spot: number;
    readonly strike: number;
    readonly rate: number;
    readonly volatility: number;
    readonly maturity: number;
    readonly put: boolean;
    readonly american: boolean;
}

/** Prices `o` on an `n`-step lattice, returning the price and every node. */
// The lattice's price, and its nodes when `drawn`: the convergence chart prices every N up to 500 and draws none.
export function price(o: OptionTerms, n: number, drawn = true): { readonly price: number; readonly nodes: readonly LatticeNode[] } {
    const dt = o.maturity / n;
    const u = Math.exp(o.volatility * Math.sqrt(dt));
    const d = 1 / u;
    const p = (Math.exp(o.rate * dt) - d) / (u - d);
    const disc = Math.exp(-o.rate * dt);
    const payoff = (s: number): number => Math.max(o.put ? o.strike - s : s - o.strike, 0);
    const spotAt = (i: number, j: number): number => o.spot * (u ** j) * (d ** (i - j));
    const nodes: LatticeNode[] = drawn ? new Array<LatticeNode>((n + 1) * (n + 2) / 2) : [];
    const index = (i: number, j: number): number => i * (i + 1) / 2 + j;
    const v: number[] = new Array<number>(n + 1).fill(0);
    const held = (j: number): number => {
        const value = v[j];
        if (value === undefined) throw new Error(`Internal: the lattice holds no value at ${String(j)}.`);
        return value;
    };
    for (let j = 0; j <= n; j++) {
        const value = payoff(spotAt(n, j));
        v[j] = value;
        if (drawn) nodes[index(n, j)] = { step: n, up: j, spot: spotAt(n, j), value, exercise: value, early: false };
    }
    for (let i = n - 1; i >= 0; i--) {
        for (let j = 0; j <= i; j++) {
            const hold = disc * (p * held(j + 1) + (1 - p) * held(j));
            const exercise = payoff(spotAt(i, j));
            const early = o.american && exercise > hold;
            const value = early ? exercise : hold;
            v[j] = value;
            if (drawn) nodes[index(i, j)] = { step: i, up: j, spot: spotAt(i, j), value, exercise, early };
        }
    }
    const first = held(0);
    return { price: first, nodes };
}
