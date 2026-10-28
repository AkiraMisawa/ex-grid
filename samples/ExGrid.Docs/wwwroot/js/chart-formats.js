// A module of the page's own, which ExChart's Blazor component imports in the browser and calls as it draws
// (ADR-0420 section 3). The site serves it from wwwroot, as any other static file.

/** A tenor in years, written as a market quotes it: months below two years, years above. */
export function tenor(years) {
    const months = Math.round(years * 12);
    return months < 24 ? `${months}M` : Number.isInteger(years) ? `${years}Y` : `${years.toFixed(1)}Y`;
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
