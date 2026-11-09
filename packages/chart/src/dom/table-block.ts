// "View as table" (ADR-0275) drawn in place: the block stands while the table is open, and a redraw
// rewrites only the rows and cells whose text changed, as the drawing's parts are (ADR-0252). A block
// built again on every redraw — a series toggled from the legend, new data, a range narrowed — sent a reader
// scrolled down a long table back to its first row, and dropped the keyboard of one who had tabbed into the
// scroller to the page's body (#549).

/** A cell of a table drawn in place: its tag, its text, and the scope a heading cell has. */
export interface TableCell {
    readonly tag: 'th' | 'td';
    readonly text: string;
    readonly scope?: 'col' | 'row';
}

/** What a table block shows: its caption, its head row and its body rows. */
export interface TableContent {
    readonly caption: string;
    readonly head: readonly TableCell[];
    readonly rows: readonly (readonly TableCell[])[];
}

/** An empty table block, to the DOM contract ex-chart.css and layer 3 read: a scroller holding the table. */
export function tableBlock(doc: Document): HTMLDivElement {
    const wrap = doc.createElement('div');
    wrap.className = 'ex-chart-table-wrap';
    const table = doc.createElement('table');
    table.className = 'ex-chart-table';
    const head = doc.createElement('thead');
    head.append(doc.createElement('tr'));
    table.append(doc.createElement('caption'), head, doc.createElement('tbody'));
    wrap.append(table);
    return wrap;
}

/** Draws `content` into a block `tableBlock` made, touching only what differs from what it shows. */
export function drawTable(block: Element, content: TableContent): void {
    const table = part(block, 0, 'table');
    setText(part(table, 0, 'caption'), content.caption);
    drawRow(part(part(table, 1, 'thead'), 0, 'tr'), content.head);
    const body = part(table, 2, 'tbody');
    const doc = block.ownerDocument;
    content.rows.forEach((cells, r) => {
        drawRow(body.children.item(r) ?? body.appendChild(doc.createElement('tr')), cells);
    });
    trim(body, content.rows.length);
}

function drawRow(row: Element, cells: readonly TableCell[]): void {
    const doc = row.ownerDocument;
    cells.forEach((cell, c) => {
        const standing = row.children.item(c);
        const element = standing !== null && standing.localName === cell.tag ? standing : doc.createElement(cell.tag);
        if (element !== standing) {
            if (standing === null) {
                row.append(element);
            } else {
                standing.replaceWith(element);
            }
        }
        if (cell.scope === undefined) {
            element.removeAttribute('scope');
        } else if (element.getAttribute('scope') !== cell.scope) {
            element.setAttribute('scope', cell.scope);
        }
        setText(element, cell.text);
    });
    trim(row, cells.length);
}

// Removes the children of `parent` past its first `count`.
function trim(parent: Element, count: number): void {
    for (let last = parent.lastElementChild; last !== null && parent.children.length > count; last = parent.lastElementChild) {
        last.remove();
    }
}

// The child of `parent` at `index`, which the block was built with.
function part(parent: Element, index: number, tag: string): Element {
    const child = parent.children.item(index);
    if (child?.localName !== tag) {
        throw new Error(`Internal: the table block has no ${tag} where it was built.`);
    }
    return child;
}

/** Writes `text` into `element` only when it differs, so a part that did not change is not touched. */
export function setText(element: Element, text: string): void {
    if (element.textContent !== text) {
        element.textContent = text;
    }
}
