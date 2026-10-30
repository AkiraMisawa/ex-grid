// A module of the page's own, which ExChart's Blazor component imports in the browser and calls as it draws
// (ADR-0420 section 3). The site serves it from wwwroot, as any other static file.

/** A tenor in years, written as a market quotes it: months below two years, years above. */
export function tenor(years) {
    const months = Math.round(years * 12);
    return months < 24 ? `${months}M` : Number.isInteger(years) ? `${years}Y` : `${years.toFixed(1)}Y`;
}

/** An expiry in years, written as a market quotes it: weeks below a month, months below a year, whole years above. */
export function quotedTenor(years) {
    const days = Math.round(years * 365);
    if (days < 28) return `${Math.max(1, Math.round(days / 7))}W`;
    const months = Math.round(years * 12);
    if (Math.abs(years * 12 - months) < 1e-6) return months % 12 === 0 ? `${months / 12}Y` : `${months}M`;
    return `${Number(years.toFixed(2))}Y`;
}

/** A rate in percent, written in basis points from 3%. */
export function fromThree(rate) {
    const bp = Math.round((Number(rate) - 3) * 100);
    return `${bp >= 0 ? '+' : '−'}${Math.abs(bp)}bp`;
}

/** A Nelson–Siegel zero curve: a level, a slope and a hump, in percent. */
export function nelsonSiegel(years) {
    const decay = years / 1.8;
    const loading = (1 - Math.exp(-decay)) / decay;
    return 3.7 - 0.44 * loading - 1.63 * (loading - Math.exp(-decay));
}

/** sin 2πx, the curve the scatter's points were drawn from. */
export function sinTwoPi(x) {
    return Math.sin(2 * Math.PI * x);
}
