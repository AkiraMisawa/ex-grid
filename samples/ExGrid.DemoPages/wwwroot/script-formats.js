// The page's own module, which ExChart.Script.ExChart imports in the browser and calls as it draws (ADR-0420
// section 3). It ships in this Razor class library, at _content/ExGrid.DemoPages/, as a Consumer's library would.

/** A position in years. */
export function years(x) {
    return `${x.toFixed(1)} yrs`;
}

/** A value in basis points. */
export function bp(v) {
    return `${Math.round(Number(v) * 10000)} bp`;
}

/** A survival curve, in the browser. */
export function survival(x) {
    return Math.exp(-0.02 * x);
}
