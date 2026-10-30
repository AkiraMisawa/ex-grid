// The pages' own functions for ExGrid.Components.ExGrid, which the grid imports in the browser and calls by position as it
// paints or decides a key (ADR-0440). It ships in this Razor class library, at _content/ExGrid.DemoPages/, as a
// Consumer's library would.

/** A Row Heading: the row's position from 1, as Excel numbers rows. */
export function headingNumber(row) {
    return String(row + 1);
}
