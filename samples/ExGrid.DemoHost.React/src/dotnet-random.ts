// .NET's `new Random(seed)`, whose sequence for a seed is fixed (the subtractive generator .NET has kept for
// seeded instances since 1.0), so a page here paints the rows the Blazor hosts paint from the same seed and
// layer 3 reads the same values on all three hosts (ADR-0350 section 4).

const mbig = 2_147_483_647;
const mseed = 161_803_398;

export class DotNetRandom {
    readonly #seeds: number[] = Array.from({ length: 56 }, () => 0);
    #next = 0;
    #nextP = 21;

    constructor(seed: number) {
        const subtraction = Math.abs(seed);
        let mj = mseed - subtraction;
        this.#seeds[55] = mj;
        let mk = 1;
        for (let i = 1; i < 55; i++) {
            const ii = (21 * i) % 55;
            this.#seeds[ii] = mk;
            mk = mj - mk;
            if (mk < 0) mk += mbig;
            mj = this.#at(ii);
        }
        for (let k = 1; k < 5; k++) {
            for (let i = 1; i < 56; i++) {
                let n = i + 30;
                if (n >= 55) n -= 55;
                let value = this.#at(i) - this.#at(1 + n);
                if (value < 0) value += mbig;
                this.#seeds[i] = value;
            }
        }
    }

    /** `Next(maxValue)`, or `Next(minValue, maxValue)`. */
    next(first: number, second?: number): number {
        if (second === undefined) return Math.trunc(this.#sample() * first);
        return Math.trunc(this.#sample() * (second - first)) + first;
    }

    #sample(): number {
        let next = this.#next + 1;
        if (next >= 56) next = 1;
        let nextP = this.#nextP + 1;
        if (nextP >= 56) nextP = 1;
        let value = this.#at(next) - this.#at(nextP);
        if (value === mbig) value -= 1;
        if (value < 0) value += mbig;
        this.#seeds[next] = value;
        this.#next = next;
        this.#nextP = nextP;
        return value * (1 / mbig);
    }

    #at(index: number): number {
        const value = this.#seeds[index];
        if (value === undefined) throw new Error(`Internal: no seed at ${index}.`);
        return value;
    }
}
