import { productError } from '../dotnet';

/**
 * A number carried as the unevaluated sum of two doubles, about 106 bits, normalised so that `high` is the
 * double nearest the sum. Every operation is the standard error-free one (Dekker, Knuth; the QD library's
 * accurate forms), so the result is the same on every platform. `CorrectlyRounded` computes its exponential and
 * logarithm in it before it rounds them.
 */
export class Wide {
    readonly high: number;
    readonly low: number;

    constructor(high: number, low = 0) {
        this.high = high;
        this.low = low;
    }

    /** a + b exactly (Knuth's two-sum). */
    static sum(a: number, b: number): Wide {
        const sum = a + b;
        const b2 = sum - a;
        return new Wide(sum, (a - (sum - b2)) + (b - b2));
    }

    /** a + b exactly, where |a| is at least |b| or a is 0. */
    static quickSum(a: number, b: number): Wide {
        const sum = a + b;
        return new Wide(sum, b - (sum - a));
    }

    /** a * b exactly, the rounding error recovered as a fused multiply-add recovers it. */
    static product(a: number, b: number): Wide {
        return new Wide(a * b, productError(a, b));
    }

    negate(): Wide {
        return new Wide(-this.high, -this.low);
    }

    plus(other: Wide | number): Wide {
        const b = typeof other === 'number' ? new Wide(other) : other;
        const high = Wide.sum(this.high, b.high);
        const low = Wide.sum(this.low, b.low);
        const sum = Wide.quickSum(high.high, high.low + low.high);
        return Wide.quickSum(sum.high, sum.low + low.low);
    }

    minus(other: Wide | number): Wide {
        const b = typeof other === 'number' ? new Wide(other) : other;
        return this.plus(b.negate());
    }

    times(other: Wide | number): Wide {
        const b = typeof other === 'number' ? new Wide(other) : other;
        const product = Wide.product(this.high, b.high);
        return Wide.quickSum(product.high, product.low + ((this.high * b.low) + (this.low * b.high)));
    }

    dividedBy(other: Wide | number): Wide {
        const b = typeof other === 'number' ? new Wide(other) : other;
        const first = this.high / b.high;
        let rest = this.minus(b.times(first));
        const second = rest.high / b.high;
        rest = rest.minus(b.times(second));
        const third = rest.high / b.high;
        return Wide.quickSum(first, second).plus(third);
    }
}
