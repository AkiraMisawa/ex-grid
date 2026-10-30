// The Workbook Documents a Sheet raises, written for an adapter as what changed since the one written before (ADR-0370
// section 1's note of 2026-10-30), which ExSheet.Engine's DocumentChangesReader reads against the document it read
// before. A document of thousands of cells changes a few at an edit; written whole, every edit crossed all of them
// and .NET read every Formula again.
import { SheetDocument } from './sheet-document';
import type { SheetDocumentCell } from './sheet-document-parts';
import { WorkbookDocument } from './workbook-document';

/**
 * Writes each Workbook Document raised as what changed since the one it wrote before: an object of three keys,
 * `"number"`, counting the documents written from 1, `"workbook"`, a Workbook Document recording every part whole but
 * the cells, and `"emptied"`, one entry per Sheet in the same order. A Sheet whose entry is `null` holds all its cells;
 * any other holds only the cells that changed since the Sheet of its name was written, in the document numbered one
 * less, and its entry lists the addresses of the cells emptied since. The first document, and the first after
 * `forget`, are written whole.
 */
export class DocumentChanges {
    // Each Sheet last written, by name, and its cells, by their address's key.
    #written = new Map<string, ReadonlyMap<number, SheetDocumentCell>>();
    #number = 0;

    /** Forgets what was written, as when its reader stops reading: the next document is written whole. */
    forget(): void {
        this.#written = new Map();
    }

    /** `document` as what changed since the document written before it, as JSON. */
    write(document: WorkbookDocument): string {
        const written = new Map<string, ReadonlyMap<number, SheetDocumentCell>>();
        const emptied: (readonly string[] | null)[] = [];
        const sheets = document.sheets.map(sheet => {
            const before = this.#written.get(sheet.name);
            const now = new Map<number, SheetDocumentCell>();
            const changed: SheetDocumentCell[] = [];
            for (const cell of sheet.cells) {
                now.set(cell.address.key, cell);
                const was = before?.get(cell.address.key);
                if (was === undefined || !recordsAlike(was, cell)) changed.push(cell);
            }
            written.set(sheet.name, now);
            if (before === undefined) {
                emptied.push(null);
                return sheet;
            }
            const gone: string[] = [];
            for (const [key, cell] of before) if (!now.has(key)) gone.push(cell.address.toString());
            emptied.push(gone);
            return new SheetDocument(sheet.culture, sheet.name, sheet.linkedTables, changed, sheet);
        });
        this.#written = written;
        const changes = new WorkbookDocument(document.culture, document.shown, document.linkedTables, sheets);
        this.#number += 1;
        return `{"number":${String(this.#number)},"workbook":${changes.toJson()},"emptied":${JSON.stringify(emptied)}}`;
    }
}

// Whether two cells of one address record the same: at once where they hold the same parts, as a cell the Sheet left
// alone does, and otherwise by what each writes.
function recordsAlike(a: SheetDocumentCell, b: SheetDocumentCell): boolean {
    if (a.entry === b.entry && a.numberFormat === b.numberFormat && a.alignment === b.alignment && a.font === b.font && a.fill === b.fill
        && a.borders === b.borders) {
        return true;
    }
    return recorded(a) === recorded(b);
}

// What a cell writes, written once for each cell object.
const cellTexts = new WeakMap<SheetDocumentCell, string>();
function recorded(cell: SheetDocumentCell): string {
    let text = cellTexts.get(cell);
    if (text === undefined) {
        text = SheetDocument.cellJson(cell);
        cellTexts.set(cell, text);
    }
    return text;
}
