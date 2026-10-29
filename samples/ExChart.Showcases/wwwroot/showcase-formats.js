// The showcases' own module, which ExChart.Script.ExChart imports in the browser and calls as it draws (ADR-0420
// section 3). It ships in this Razor class library, at _content/ExChart.Showcases/, as a Consumer's library would.

// RateData's pillars: each tenor as quoted, at its position in years.
const tenors = ["ON", "1W", "1M", "2M", "3M", "6M", "1Y", "2Y", "3Y", "4Y", "5Y", "7Y", "10Y", "12Y", "15Y", "20Y", "25Y", "30Y", "40Y", "50Y"];
const years = [0.00277778, 0.01944444, 0.08333333, 0.16666667, 0.25, 0.5, 1, 2, 3, 4, 5, 7, 10, 12, 15, 20, 25, 30, 40, 50];

/** A position on the rate curve's axis: a pillar as quoted, any other position in years. */
export function rateTenor(position) {
    const at = years.indexOf(position);
    return at >= 0 ? tenors[at] : `${Number(position.toFixed(4))}Y`;
}
