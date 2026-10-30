// The page's own module, which ExChart.Components.ExChart imports in the browser and calls as it draws (ADR-0420
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

/** sin 2πx, the curve /scatter's points were drawn from. */
export function sinTwoPi(x) {
    return Math.sin(2 * Math.PI * x);
}

/** A tenor in years written as the market writes it, as the chart writes a tenor axis: 6M, 18M, 1Y, 2.5Y. */
export function tenor(years) {
    const months = years * 12;
    if (Math.abs(months - Math.round(months)) < 1e-6) {
        const m = Math.round(months);
        return m % 12 === 0 ? `${m / 12}Y` : `${m}M`;
    }
    const weeks = years * 52;
    if (years < 1 / 12 && Math.abs(weeks - Math.round(weeks)) < 1e-6) return `${Math.round(weeks)}W`;
    return `${Number(years.toFixed(2))}Y`;
}
