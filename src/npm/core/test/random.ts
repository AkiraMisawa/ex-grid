// A seeded random source for the property tests: the same seed answers the same sequence on every run.
import { itemAt } from '../src/must';

/** mulberry32, so a failure is reproduced by running the test again. */
export class Random {
    #state: number;

    constructor(start: number) {
        this.#state = start;
    }

    /** A whole number in [low, high). */
    next(low: number, high: number): number {
        this.#state = (this.#state + 0x6D2B79F5) | 0;
        let t = this.#state;
        t = Math.imul(t ^ (t >>> 15), t | 1);
        t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
        return low + Math.floor((((t ^ (t >>> 14)) >>> 0) / 4294967296) * (high - low));
    }

    below(high: number): number {
        return this.next(0, high);
    }

    pick<T>(values: readonly T[]): T {
        return itemAt(values, this.below(values.length));
    }

    shuffled<T>(values: readonly T[]): T[] {
        const out = [...values];
        for (let i = out.length - 1; i > 0; i--) {
            const j = this.below(i + 1);
            [out[i], out[j]] = [itemAt(out, j), itemAt(out, i)];
        }
        return out;
    }
}
