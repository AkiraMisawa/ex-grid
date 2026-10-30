// A module of the page's own, which ExGrid's Blazor component over the TypeScript core imports in the browser and
// asks as the Focus moves (ADR-0440). The site serves it from wwwroot, as any other static file.

/** The Name Box's text for the Focus cell: its row, counted from 1. */
export function rowName(cell) {
    return `Row ${cell.row + 1}`;
}
