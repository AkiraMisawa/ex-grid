// Fit helpers a Consumer may call (ADR-0291), as ExChart.Engine's `Fit` does. The chart never calls them: a
// Consumer calls one, explicitly, and hands the result in as a series, so no fit sits on the drawing's path.
// Each result names its method, its coefficients and the space it was fitted in, and its series carries that
// as a Segment, so the Notes say it. None extrapolates: the series has values only between the smallest and
// the largest x it was fitted on. The arithmetic is the C# engine's, step for step, so both engines answer
// the same coefficients (tests/cases/chart/fitting.json).
import { roundTrip, twoPlaces, upToPlaces } from './invariant-text';
import type { ChartSeriesOptions } from './model';
import { elementAt } from './must';
import { greatest, least } from './numbers';

/** The space a fit is made in (ADR-0291): y against x, ln y against x (an exponential), or ln y against ln x (a power law). */
export type FitSpace = 'linear' | 'logY' | 'logLog';

const spaces: ReadonlySet<string> = new Set(['linear', 'logY', 'logLog']);

/** What a fitted curve is made of: the C# record's members. */
export interface FittedCurveOptions {
    /** The method's name, such as `OLS` or `Theil–Sen`. */
    readonly method: string;
    /** The polynomial's coefficients from the constant up: for a line, the intercept then the slope. */
    readonly coefficients: readonly number[];
    /** The share of the variance it explains, in the space it was fitted in; below zero where it does worse than the mean. */
    readonly rSquared: number;
    /** How many points it was fitted on. */
    readonly count: number;
    /** The smallest x it was fitted on. */
    readonly from: number;
    /** The largest. */
    readonly to: number;
    /** The space it was fitted in; `linear` where absent. */
    readonly space?: FitSpace;
}

/**
 * A curve fitted by one of the fits (`fitLeastSquares`, `fitTheilSen`, `fitPolynomial`): its method, its
 * coefficients in the space it was fitted in, R² there, how many points it was fitted on, and the range of x
 * they span (ADR-0291).
 */
export class FittedCurve {
    readonly #method: string;
    readonly #coefficients: readonly number[];
    readonly #rSquared: number;
    readonly #count: number;
    readonly #from: number;
    readonly #to: number;
    readonly #space: FitSpace;

    /** A curve of the given coefficients; refused when it has none, since a polynomial has a constant at least. */
    constructor(options: FittedCurveOptions) {
        const { space = 'linear' } = options;
        if (options.coefficients.length === 0) {
            throw new RangeError('A fitted curve has one coefficient at least, its constant.');
        }
        if (!spaces.has(space)) {
            throw new TypeError(`"${space}" is not a FitSpace; it is linear, logY or logLog.`);
        }
        this.#method = options.method;
        this.#coefficients = Object.freeze([...options.coefficients]);
        this.#rSquared = options.rSquared;
        this.#count = options.count;
        this.#from = options.from;
        this.#to = options.to;
        this.#space = space;
    }

    /** The method's name, such as `OLS` or `Theil–Sen`. */
    get method(): string {
        return this.#method;
    }

    /** The polynomial's coefficients from the constant up: for a line, the intercept then the slope. */
    get coefficients(): readonly number[] {
        return this.#coefficients;
    }

    /** The share of the variance it explains, in the space it was fitted in; below zero where it does worse than the mean. */
    get rSquared(): number {
        return this.#rSquared;
    }

    /** How many points it was fitted on. */
    get count(): number {
        return this.#count;
    }

    /** The smallest x it was fitted on. */
    get from(): number {
        return this.#from;
    }

    /** The largest x it was fitted on. */
    get to(): number {
        return this.#to;
    }

    /** The space it was fitted in. */
    get space(): FitSpace {
        return this.#space;
    }

    /** The intercept: the constant coefficient. */
    get intercept(): number {
        return elementAt(this.#coefficients, 0);
    }

    /** The slope: the first-degree coefficient, or zero for a constant. */
    get slope(): number {
        return this.#coefficients.length > 1 ? elementAt(this.#coefficients, 1) : 0;
    }

    /** The polynomial's degree. */
    get degree(): number {
        return this.#coefficients.length - 1;
    }

    /**
     * Its Note: one line naming the method and the space, the equation for a line or the degree for a higher
     * polynomial, R² and n (ADR-0291), such as `OLS: y = 41.2x + 88.5, R² = 0.63, n = 212`.
     */
    get note(): string {
        const [y, x] = this.#space === 'logY' ? ['ln y', 'x'] : this.#space === 'logLog' ? ['ln y', 'ln x'] : ['y', 'x'];
        const on = this.#space === 'linear' ? '' : ` on ${y} vs ${x}`;
        const degree = this.degree;
        const intercept = this.intercept;
        const shape = degree === 0
            ? `${y} = ${coefficientText(intercept)}`
            : degree === 1
                ? `${y} = ${coefficientText(this.slope)}${x} ${intercept < 0 ? '−' : '+'} ${coefficientText(Math.abs(intercept))}`
                : `degree ${String(degree)}`;
        return `${this.#method}${on}: ${shape}, R² = ${twoPlaces(this.#rSquared)}, n = ${String(this.#count)}`;
    }

    /** The fitted value at `x`, back in y, or `null` outside the range it was fitted over or where it is not a finite number. */
    valueAt(x: number): number | null {
        if (!(x >= this.#from && x <= this.#to)) {
            return null;
        }
        const t = this.#space === 'logLog' ? Math.log(x) : x;
        let v = 0;
        for (let k = this.#coefficients.length - 1; k >= 0; k--) {
            v = v * t + elementAt(this.#coefficients, k);
        }
        if (this.#space !== 'linear') {
            v = Math.exp(v);
        }
        return Number.isFinite(v) ? v : null;
    }

    /**
     * The curve as a Level series over the range it was fitted on, sampled by the chart as it draws, read away
     * from zero, with one Segment named by its Note, so the Notes say it (ADR-0291).
     */
    series(id: string, name: string): ChartSeriesOptions {
        const from = this.#from;
        const to = this.#to;
        return Object.freeze({
            id, name, kind: 'level', fromZero: false,
            segments: Object.freeze([Object.freeze({ from, to, method: this.note, extrapolated: false })]),
            sampler: Object.freeze({ from, to, valueAt: (x: number) => this.valueAt(x) }),
        });
    }
}

// A coefficient in a Note, as C# writes it with "0.###".
function coefficientText(v: number): string {
    return upToPlaces(v, 3);
}

/** The most points `fitTheilSen` takes: it weighs every pair, which past this is too many to run. */
export const theilSenLimit = 3_000;

/**
 * Ordinary least squares (ADR-0291): the line that minimises the squared vertical distances, in `space`. A
 * point whose x or y is not a finite number, or whose logarithm the space cannot take, is refused.
 */
export function fitLeastSquares(xs: readonly number[], ys: readonly number[], space: FitSpace = 'linear'): FittedCurve {
    return fitted(xs, ys, 1, space, 'OLS');
}

/** Least squares of a polynomial of `degree` (ADR-0291), solved by Householder QR; degree 1 is `fitLeastSquares`. */
export function fitPolynomial(xs: readonly number[], ys: readonly number[], degree: number, space: FitSpace = 'linear'): FittedCurve {
    return fitted(xs, ys, degree, space, degree === 1 ? 'OLS' : 'Least squares');
}

/**
 * Theil–Sen (ADR-0291): the median of the slopes between every pair of points with different x, and the
 * median of yᵢ − slope·xᵢ as the intercept. A single point far off does not drag it. More than
 * `theilSenLimit` points are refused.
 */
export function fitTheilSen(xs: readonly number[], ys: readonly number[], space: FitSpace = 'linear'): FittedCurve {
    const { x, y } = prepared(xs, ys, space, 2);
    if (x.length > theilSenLimit) {
        throw new TypeError(`Theil–Sen weighs every pair of points; ${String(x.length)} points are more than the ${String(theilSenLimit)} it takes.`);
    }
    const slopes: number[] = [];
    for (let i = 0; i < x.length; i++) {
        const xi = elementAt(x, i);
        const yi = elementAt(y, i);
        for (let j = i + 1; j < x.length; j++) {
            const xj = elementAt(x, j);
            if (xi !== xj) {
                slopes.push((elementAt(y, j) - yi) / (xj - xi));
            }
        }
    }
    if (slopes.length === 0) {
        throw new TypeError('Every point has the same x: no slope can be fitted.');
    }
    const slope = median(slopes);
    const intercept = median(x.map((xi, i) => elementAt(y, i) - slope * xi));
    return curve('Theil–Sen', [intercept, slope], x, y, xs, space);
}

function fitted(xs: readonly number[], ys: readonly number[], degree: number, space: FitSpace, method: string): FittedCurve {
    if (!Number.isInteger(degree) || degree < 0) {
        throw new RangeError(`A polynomial's degree is a whole number not below zero, not ${roundTrip(degree)}.`);
    }
    const { x, y } = prepared(xs, ys, space, degree + 1);
    if (new Set(x).size <= degree) {
        throw new TypeError(`A polynomial of degree ${String(degree)} needs ${String(degree + 1)} different x at least.`);
    }
    // Centred and scaled x keeps the columns of the design matrix comparable, so degree 9 on [0, 1] is solved
    // as accurately as degree 1; the coefficients are mapped back afterwards.
    const lo = least(x);
    const hi = greatest(x);
    const mid = (lo + hi) / 2;
    const half = Math.max((hi - lo) / 2, Number.MIN_VALUE);
    const n = degree + 1;
    const a: number[] = [];
    for (const xi of x) {
        const t = (xi - mid) / half;
        let p = 1;
        for (let k = 0; k <= degree; k++, p *= t) {
            a.push(p);
        }
    }
    const scaled = solveLeastSquares(a, x.length, n, y);
    return curve(method, unscale(scaled, mid, half), x, y, xs, space);
}

// R² over the points in the space the fit was made in, the curve's range over the points as handed in.
function curve(method: string, c: readonly number[], x: readonly number[], y: readonly number[], xs: readonly number[], space: FitSpace): FittedCurve {
    // A mean as .NET's Average takes it: summed in order, then divided.
    let sum = 0;
    for (const v of y) {
        sum += v;
    }
    const mean = sum / y.length;
    let res = 0;
    let tot = 0;
    for (let i = 0; i < x.length; i++) {
        const xi = elementAt(x, i);
        const yi = elementAt(y, i);
        let f = 0;
        for (let k = c.length - 1; k >= 0; k--) {
            f = f * xi + elementAt(c, k);
        }
        res += (yi - f) * (yi - f);
        tot += (yi - mean) * (yi - mean);
    }
    const rSquared = tot === 0 ? (res === 0 ? 1 : 0) : 1 - res / tot;
    return new FittedCurve({ method, coefficients: c, rSquared, count: x.length, from: least(xs), to: greatest(xs), space });
}

// The points in the space the fit is made in: ln y, and ln x for a power law. A value a logarithm cannot take
// is refused by name, never dropped (ADR-0290); so is a point that is not a finite number.
function prepared(xs: readonly number[], ys: readonly number[], space: FitSpace, fewest: number): { x: number[]; y: number[] } {
    if (!Array.isArray(xs) || !Array.isArray(ys)) {
        throw new TypeError('A fit takes its xs and its ys as lists of numbers.');
    }
    if (!spaces.has(space)) {
        throw new TypeError(`"${space}" is not a FitSpace; it is linear, logY or logLog.`);
    }
    if (xs.length !== ys.length) {
        throw new TypeError('Each x needs one y.');
    }
    if (xs.length < fewest) {
        throw new TypeError(`The method needs ${String(fewest)} points at least.`);
    }
    const x: number[] = [];
    const y: number[] = [];
    for (let i = 0; i < xs.length; i++) {
        // Read as what an untyped caller may have handed in, and checked.
        const xi: unknown = xs[i];
        const yi: unknown = ys[i];
        if (typeof xi !== 'number' || typeof yi !== 'number' || !Number.isFinite(xi) || !Number.isFinite(yi)) {
            throw new TypeError(`Point ${String(i)} is not a finite number.`);
        }
        if (space !== 'linear' && !(yi > 0)) {
            throw new TypeError(`Point ${String(i)} has y = ${roundTrip(yi)}, whose logarithm a fit on ln y cannot take.`);
        }
        if (space === 'logLog' && !(xi > 0)) {
            throw new TypeError(`Point ${String(i)} has x = ${roundTrip(xi)}, whose logarithm a fit on ln x cannot take.`);
        }
        x.push(space === 'logLog' ? Math.log(xi) : xi);
        y.push(space === 'linear' ? yi : Math.log(yi));
    }
    return { x, y };
}

// min ‖Ac − b‖ by Householder QR, then back substitution; `a` is m rows of n, row by row.
function solveLeastSquares(a: readonly number[], m: number, n: number, b: readonly number[]): number[] {
    const r = [...a];
    const q = [...b];
    const at = (i: number, j: number): number => elementAt(r, i * n + j);
    for (let k = 0; k < n; k++) {
        let norm = 0;
        for (let i = k; i < m; i++) {
            norm += at(i, k) * at(i, k);
        }
        norm = Math.sqrt(norm);
        if (norm === 0) {
            throw new TypeError('The points do not determine the polynomial.');
        }
        const alpha = at(k, k) > 0 ? -norm : norm;
        const v = Array.from({ length: m }, () => 0);
        v[k] = at(k, k) - alpha;
        for (let i = k + 1; i < m; i++) {
            v[i] = at(i, k);
        }
        let vv = 0;
        for (let i = k; i < m; i++) {
            vv += elementAt(v, i) * elementAt(v, i);
        }
        if (vv === 0) {
            continue;
        }
        for (let j = k; j < n; j++) {
            let d = 0;
            for (let i = k; i < m; i++) {
                d += elementAt(v, i) * at(i, j);
            }
            d = 2 * d / vv;
            for (let i = k; i < m; i++) {
                r[i * n + j] = at(i, j) - d * elementAt(v, i);
            }
        }
        let e = 0;
        for (let i = k; i < m; i++) {
            e += elementAt(v, i) * elementAt(q, i);
        }
        e = 2 * e / vv;
        for (let i = k; i < m; i++) {
            q[i] = elementAt(q, i) - e * elementAt(v, i);
        }
    }
    const c = Array.from({ length: n }, () => 0);
    for (let k = n - 1; k >= 0; k--) {
        let s = elementAt(q, k);
        for (let j = k + 1; j < n; j++) {
            s -= at(k, j) * elementAt(c, j);
        }
        c[k] = s / at(k, k);
    }
    return c;
}

// Coefficients in t = (x − mid) / half mapped back to coefficients in x: Σ sₖ ((x − mid)/half)^k, expanded by
// the binomial theorem.
function unscale(scaled: readonly number[], mid: number, half: number): number[] {
    const n = scaled.length;
    const c = Array.from({ length: n }, () => 0);
    for (let k = 0; k < n; k++) {
        const coeff = elementAt(scaled, k) / half ** k;
        let binom = 1;
        for (let j = 0; j <= k; j++) {
            c[j] = elementAt(c, j) + coeff * binom * (-mid) ** (k - j);
            binom = binom * (k - j) / (j + 1);
        }
    }
    return c;
}

function median(values: readonly number[]): number {
    const sorted = values.toSorted((a, b) => a - b);
    const n = sorted.length;
    const middle = Math.floor(n / 2);
    return n % 2 === 1 ? elementAt(sorted, middle) : (elementAt(sorted, middle - 1) + elementAt(sorted, middle)) / 2;
}
