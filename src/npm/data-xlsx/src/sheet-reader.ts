// One worksheet's cells read as a stream (ADR-0330 §9), as ExGrid.Data.Xlsx's XlsxSheetReader reads them: `read` reads
// the next batch into `cells`, row by row as the file holds them, and between batches the read reports its progress and
// yields once a slice has spent its budget. The worksheet is never held as a document; only the batch at hand is, and
// the text it was read from.

import { formatCell, maxColumns, maxRows, parseCell } from './cell-reference';
import { parseDigits } from './invariant';
import { elementAt, unreachable } from './must';
import {
    elementMaxChildren, elementMaxDepth, elementMaxText,
    type XlsxCell, type XlsxCellType, type XlsxColumn, type XlsxElement, type XlsxFormula, type XlsxFormulaKind, type XlsxRow,
    type XlsxSheetInfo,
} from './model';
import { strictPrefix } from './names';
import { Package, type PartXml } from './package';
import { readText } from './string-item';
import type { XlsxSharedStrings } from './shared-strings';
import { low, runSteps, type StepEnd } from './steps';
import type { XlsxStyles } from './styles';
import { bool, double, int } from './values';
import { decodeXString } from './x-string';
import { XlsxError } from './xlsx-error';
import { XmlError } from './xml-reader';

/** The cells a batch holds at most. */
export const batchSize = 4096;

/** What a worksheet's reader asks of the workbook's. */
export interface SheetOwner {
    readonly styles: XlsxStyles;
    sharedStrings(): XlsxSharedStrings;
    isDynamicArray(cellMetadata: number): boolean;
    countCell(sheet: XlsxSheetInfo, row: number, column: number): void;
    cells(): number;
    rewindCells(to: number): void;
    countBytes(bytes: number): void;
    checkpoint(sheet: number): Promise<void>;
    report(sheet: number): void;
    closed(): void;
}

type State = 'start' | 'head' | 'cells' | 'tail' | 'finished';

/**
 * One worksheet's cells read as a stream (ADR-0330 §9): `read` reads the next batch into `cells`, row by row as the file
 * holds them, and between batches the read reports its progress and yields once a slice has spent its budget. Made by
 * `XlsxReader.openSheet`; close it before opening the next.
 */
export interface XlsxSheetReader {
    /** The sheet as the workbook lists it. */
    readonly sheet: XlsxSheetInfo;
    /** The cells of the batch the last `read` read, in the file's order. */
    readonly cells: readonly XlsxCell[];
    /** The rows the last `read` began that record a cell format of their own (`customFormat`), in the file's order. A row
     * comes in the batch that holds its first cell, or in an earlier one. */
    readonly rows: readonly XlsxRow[];
    /** The worksheet's column widths, complete once the first `read` has returned: they come before the cells. */
    readonly columns: readonly XlsxColumn[];
    /**
     * Every element of the worksheet the reader does not read, beside its cells and columns, in the file's order, complete
     * once `read` has returned false: `dimension`, `sheetViews` (of which only `frozenPanes` is read), `sheetFormatPr`,
     * `mergeCells`, `conditionalFormatting`, `dataValidations`, `hyperlinks`, `drawing`, `legacyDrawing` (notes),
     * `tableParts`, `sheetProtection`, `pageMargins`, `pageSetup`, `extLst` and the rest, each as far as `XlsxElement`
     * keeps it. None of it is judged here: whether an element holds anything that matters is for the reader of the file to
     * say.
     */
    readonly otherElements: readonly XlsxElement[];
    /** Whether a row read so far sets a height of its own. */
    readonly customRowHeights: boolean;
    /** Whether a row read so far is hidden. */
    readonly hiddenRows: boolean;
    /** Whether the sheet's view freezes panes; known once the first `read` has returned. */
    readonly frozenPanes: boolean;
    /** Reads the next batch of cells into `cells`; false when the worksheet has no more. Refuses the file by name
     * (`XlsxError`) on a cell it cannot read, or past the read's cells. */
    read(): Promise<boolean>;
    /** Closes the worksheet's part. */
    close(): Promise<void>;
}

export class SheetReader implements XlsxSheetReader {
    readonly #owner: SheetOwner;
    readonly #index: number;
    readonly #part: string;
    readonly #opened: PartXml;
    readonly #batch: XlsxCell[] = [];
    readonly #columns: XlsxColumn[] = [];
    readonly #rows: XlsxRow[] = [];
    readonly #other: XlsxElement[] = [];
    #state: State = 'start';
    #row = -1;
    #column = -1;
    #reported = 0;
    #customRowHeights = false;
    #hiddenRows = false;
    #frozenPanes = false;
    #closed = false;

    /** The sheet as the workbook lists it. */
    readonly sheet: XlsxSheetInfo;

    constructor(owner: SheetOwner, index: number, sheet: XlsxSheetInfo, part: string, opened: PartXml) {
        this.#owner = owner;
        this.#index = index;
        this.sheet = sheet;
        this.#part = part;
        this.#opened = opened;
    }

    /** The cells of the batch the last `read` read, in the file's order. */
    get cells(): readonly XlsxCell[] {
        return this.#batch;
    }

    /** The rows the last `read` began that record a cell format of their own (`customFormat`), in the file's order. A row
     * comes in the batch that holds its first cell, or in an earlier one. */
    get rows(): readonly XlsxRow[] {
        return this.#rows;
    }

    /** The worksheet's column widths, complete once the first `read` has returned: they come before the cells. */
    get columns(): readonly XlsxColumn[] {
        return this.#columns;
    }

    /**
     * Every element of the worksheet the reader does not read, beside its cells and columns, in the file's order, complete
     * once `read` has returned false: `dimension`, `sheetViews` (of which only `frozenPanes` is read), `sheetFormatPr`,
     * `mergeCells`, `conditionalFormatting`, `dataValidations`, `hyperlinks`, `drawing`, `legacyDrawing` (notes),
     * `tableParts`, `sheetProtection`, `pageMargins`, `pageSetup`, `extLst` and the rest, each as far as `XlsxElement`
     * keeps it. None of it is judged here: whether an element holds anything that matters is for the reader of the file to
     * say.
     */
    get otherElements(): readonly XlsxElement[] {
        return this.#other;
    }

    /** Whether a row read so far sets a height of its own. */
    get customRowHeights(): boolean {
        return this.#customRowHeights;
    }

    /** Whether a row read so far is hidden. */
    get hiddenRows(): boolean {
        return this.#hiddenRows;
    }

    /** Whether the sheet's view freezes panes; known once the first `read` has returned. */
    get frozenPanes(): boolean {
        return this.#frozenPanes;
    }

    /**
     * Reads the next batch of cells into `cells`; false when the worksheet has no more. Refuses the file by name
     * (`XlsxError`) on a cell it cannot read, or past the read's cells.
     */
    async read(): Promise<boolean> {
        if (this.#closed) throw new Error('The worksheet reader is closed.');
        this.#batch.length = 0;
        this.#rows.length = 0;
        if (this.#finished()) return false;
        await this.#owner.checkpoint(this.#index);
        const { xml, bytes } = this.#opened;
        try {
            await runSteps(xml, () => this.#save(), () => this.#step());
        } catch (error) {
            if (error instanceof XmlError) throw Package.refuse(error, this.#opened, this.#part, this.sheet.name);
            throw error;
        } finally {
            this.#owner.countBytes(bytes.bytesRead - this.#reported);
            this.#reported = bytes.bytesRead;
        }
        if (this.#finished()) {
            this.#owner.report(this.#index);
            await bytes.close();
        }
        // A last batch of formatted rows with no cell is still a batch: their formats are read.
        return this.#batch.length > 0 || this.#rows.length > 0 || !this.#finished();
    }

    #finished(): boolean {
        return this.#stateIs('finished');
    }

    /** The state, read where a call before may have changed it. */
    #stateIs(state: State): boolean {
        return this.#state === state;
    }

    /** Closes the worksheet's part. */
    async close(): Promise<void> {
        if (this.#closed) return;
        this.#closed = true;
        this.#owner.closed();
        await this.#opened.bytes.close();
    }

    /** What puts the reader back where a step began, for a step read again with more text. */
    #save(): () => void {
        const state = this.#state;
        const row = this.#row;
        const column = this.#column;
        const batch = this.#batch.length;
        const rows = this.#rows.length;
        const columns = this.#columns.length;
        const other = this.#other.length;
        const flags = [this.#customRowHeights, this.#hiddenRows, this.#frozenPanes] as const;
        const cells = this.#owner.cells();
        return () => {
            this.#state = state;
            this.#row = row;
            this.#column = column;
            this.#batch.length = batch;
            this.#rows.length = rows;
            this.#columns.length = columns;
            this.#other.length = other;
            [this.#customRowHeights, this.#hiddenRows, this.#frozenPanes] = flags;
            this.#owner.rewindCells(cells);
        };
    }

    #step(): StepEnd {
        if (this.#state === 'start') this.#readRoot();
        if (this.#state === 'head' && this.#readHead() === 'more') return 'more';
        if (this.#state === 'cells') {
            const end = this.#readCells();
            if (end !== 'done' || this.#stateIs('cells')) return end;
        }
        if (this.#state === 'tail' && this.#readTail() === 'more') return 'more';
        return 'done';
    }

    #where(cell?: string): { part: string; sheet: string; cell?: string } {
        return cell === undefined ? { part: this.#part, sheet: this.sheet.name } : { part: this.#part, sheet: this.sheet.name, cell };
    }

    /** Reads the root, and refuses a worksheet that is not one. */
    #readRoot(): void {
        const xml = this.#opened.xml;
        xml.moveToContent();
        if (xml.namespaceUri().startsWith(strictPrefix)) {
            throw new XlsxError('strictOpenXml', 'The worksheet is saved as Strict Open XML, which is not read.', this.#where());
        }
        if (xml.localName() !== 'worksheet') throw new XlsxError('malformed', `The worksheet part's root is <${xml.localName()}>, not <worksheet>.`, this.#where());
        if (xml.isEmptyElement()) {
            this.#state = 'tail';
            return;
        }
        xml.read();
        this.#state = 'head';
    }

    /** Reads the children before `<sheetData>`. */
    #readHead(): 'more' | 'done' {
        const xml = this.#opened.xml;
        while (!xml.eof() && !(xml.nodeType() === 'endElement' && xml.depth() === 0)) {
            if (xml.nodeType() !== 'element') {
                xml.read();
                continue;
            }
            if (low(xml)) return 'more';
            switch (xml.localName()) {
                case 'sheetData':
                    this.#state = xml.isEmptyElement() ? 'tail' : 'cells';
                    xml.read();
                    return 'done';
                case 'cols':
                    this.#readColumns();
                    break;
                case 'sheetViews': {
                    const views = this.#readElement(0);
                    this.#other.push(views);
                    this.#frozenPanes = views.children.some(view => view.children.some(pane => {
                        const state = pane.attributes.get('state');
                        return pane.name === 'pane' && (state === 'frozen' || state === 'frozenSplit');
                    }));
                    break;
                }
                default:
                    this.#other.push(this.#readElement(0));
                    break;
            }
        }
        this.#state = 'tail';
        return 'done';
    }

    #readColumns(): void {
        const xml = this.#opened.xml;
        if (xml.isEmptyElement()) {
            xml.read();
            return;
        }
        const depth = xml.depth();
        const sheet = this.sheet.name;
        const part = this.#part;
        xml.read();
        while (!xml.eof() && !(xml.nodeType() === 'endElement' && xml.depth() === depth)) {
            if (xml.nodeType() === 'element' && xml.localName() === 'col') {
                const a = xml.getAttribute('min');
                const b = xml.getAttribute('max');
                const min = a === undefined ? 0 : int(a, { part, sheet });
                const max = b === undefined ? min : int(b, { part, sheet });
                if (min < 1 || max < min || max > maxColumns) {
                    throw new XlsxError('malformed', `A column range ${String(min)} to ${String(max)} lies outside the sheet's columns.`, { part, sheet });
                }
                const w = xml.getAttribute('width');
                const width = w === undefined ? null : double(w, part);
                const hidden = bool(xml.getAttribute('hidden'));
                const s = xml.getAttribute('style');
                const style = s === undefined ? 0 : int(s, { part, sheet });
                if (style < 0 || style >= this.#owner.styles.cellFormats.length) {
                    throw new XlsxError('malformed',
                        `The columns ${String(min)} to ${String(max)} name the cell format ${String(style)}, which the styles do not hold.`, { part, sheet });
                }
                this.#columns.push(Object.freeze({
                    first: min - 1, last: max - 1, width, hidden, styleIndex: style, bestFit: bool(xml.getAttribute('bestFit')),
                    customWidth: width === null || bool(xml.getAttribute('customWidth')),
                }));
            }
            if (xml.nodeType() === 'element') xml.skip();
            else xml.read();
        }
        xml.read();
    }

    /** The element at the reader, kept as `XlsxElement` keeps it, `depth` levels below the worksheet's element; the reader
     * is left past its end. */
    #readElement(depth: number): XlsxElement {
        const xml = this.#opened.xml;
        const name = xml.localName();
        const attributes = new Map<string, string>();
        for (const attribute of xml.attributes()) {
            if (attribute.namespaceUri !== 'http://www.w3.org/2000/xmlns/' && !attributes.has(attribute.localName)) {
                attributes.set(attribute.localName, attribute.value);
            }
        }
        if (xml.isEmptyElement()) {
            xml.read();
            return Object.freeze({ name, attributes, children: Object.freeze([]), text: null, childrenCut: false });
        }
        const children: XlsxElement[] = [];
        let text: string | null = null;
        let cut = false;
        const end = xml.depth();
        xml.read();
        while (!xml.eof() && !(xml.nodeType() === 'endElement' && xml.depth() === end)) {
            const node = xml.nodeType();
            switch (node) {
                case 'element':
                    if (depth < elementMaxDepth && children.length < elementMaxChildren) {
                        children.push(this.#readElement(depth + 1));
                    } else {
                        cut = true;
                        xml.skip();
                    }
                    break;
                case 'text':
                case 'cdata':
                case 'significantWhitespace': {
                    const kept: string = text === null ? '' : text;
                    text = kept + xml.value().slice(0, elementMaxText - kept.length);
                    xml.read();
                    break;
                }
                case 'whitespace':
                case 'endElement':
                case 'none':
                    xml.read();
                    break;
                default:
                    unreachable(node);
            }
        }
        xml.read();
        return Object.freeze({ name, attributes, children: Object.freeze(children), text, childrenCut: cut });
    }

    /** Reads cells until the batch is full, `<sheetData>` ends, or the text ahead runs low. */
    #readCells(): 'more' | 'done' {
        const xml = this.#opened.xml;
        // Formatted rows count toward the batch, so that rows formatted with no cell are not gathered without end.
        while (this.#batch.length + this.#rows.length < batchSize) {
            if (xml.eof()) throw new XlsxError('malformed', 'The worksheet ends inside its cells.', this.#where());
            if (low(xml)) return 'more';
            const node = xml.nodeType();
            switch (node) {
                case 'element':
                    if (xml.localName() === 'row') {
                        this.#startRow();
                        if (xml.isEmptyElement()) xml.skip();
                        else xml.read();
                    } else if (xml.localName() === 'c') {
                        this.#readCell();
                    } else {
                        xml.skip();
                    }
                    break;
                case 'endElement':
                    if (xml.localName() === 'sheetData') {
                        xml.read();
                        this.#state = 'tail';
                        return 'done';
                    }
                    xml.read();
                    break;
                case 'text':
                case 'cdata':
                case 'whitespace':
                case 'significantWhitespace':
                case 'none':
                    xml.read();
                    break;
                default:
                    unreachable(node);
            }
        }
        return 'done';
    }

    #startRow(): void {
        const xml = this.#opened.xml;
        const where = this.#where();
        let r = this.#row + 1;
        let style: string | undefined;
        let customFormat = false;
        for (const attribute of xml.attributes()) {
            switch (attribute.localName) {
                case 'r':
                    r = int(attribute.value, where) - 1;
                    break;
                case 'customHeight':
                    if (bool(attribute.value)) this.#customRowHeights = true;
                    break;
                case 'hidden':
                    if (bool(attribute.value)) this.#hiddenRows = true;
                    break;
                case 's':
                    style = attribute.value;
                    break;
                case 'customFormat':
                    customFormat = bool(attribute.value);
                    break;
                default:
                    break;
            }
        }
        if (r < 0 || r >= maxRows) throw new XlsxError('malformed', `Row ${String(r + 1)} lies outside the sheet's rows.`, where);
        if (r <= this.#row) {
            throw new XlsxError('malformed', `Row ${String(r + 1)} comes after row ${String(this.#row + 1)}: a worksheet's rows are in order, each once.`, where);
        }
        this.#row = r;
        this.#column = -1;
        if (!customFormat) return;
        // A row's s counts only beside customFormat (ECMA-376 Part 1, §18.3.1.73); without it the row records no format.
        const index = style === undefined ? 0 : int(style, where);
        if (index < 0 || index >= this.#owner.styles.cellFormats.length) {
            throw new XlsxError('malformed', `Row ${String(r + 1)} names the cell format ${String(index)}, which the styles do not hold.`, where);
        }
        this.#rows.push(Object.freeze({ row: r, styleIndex: index }));
    }

    #readCell(): void {
        const xml = this.#opened.xml;
        let reference: string | undefined;
        let type: string | undefined;
        let style: string | undefined;
        let metadata: string | undefined;
        for (const attribute of xml.attributes()) {
            switch (attribute.localName) {
                case 'r': reference = attribute.value; break;
                case 't': type = attribute.value; break;
                case 's': style = attribute.value; break;
                case 'cm': metadata = attribute.value; break;
                default: break;
            }
        }
        let r: number;
        let c: number;
        if (reference === undefined) {
            r = this.#row < 0 ? 0 : this.#row;
            c = this.#column + 1;
            if (c >= maxColumns) throw new XlsxError('malformed', 'A cell lies past the sheet\'s last column.', this.#where());
        } else {
            const place = parseCell(reference);
            if (place === undefined) throw new XlsxError('malformed', `'${reference}' is not a cell of the sheet.`, this.#where());
            r = place.row;
            c = place.column;
            if (this.#row >= 0 && r !== this.#row) {
                throw new XlsxError('malformed', `The cell is written inside row ${String(this.#row + 1)}, not its own.`, this.#where(reference));
            }
            if (c <= this.#column) {
                throw new XlsxError('malformed',
                    `The cell comes after ${formatCell(r, this.#column)}: a row's cells are in order, each once.`, this.#where(reference));
            }
        }
        this.#row = r;
        this.#column = c;
        this.#owner.countCell(this.sheet, r, c);
        const at = (): { part: string; sheet: string; cell?: string } => this.#where(formatCell(r, c));
        const cellType = cellTypeOf(type, at);
        const styleIndex = style === undefined ? 0 : int(style, at());
        if (styleIndex < 0 || styleIndex >= this.#owner.styles.cellFormats.length) {
            throw new XlsxError('malformed', `The cell names the cell format ${String(styleIndex)}, which the styles do not hold.`, at());
        }
        let value: string | null = null;
        let text: string | null = null;
        let formula: XlsxFormula | null = null;
        let rich = false;
        if (xml.isEmptyElement()) {
            xml.read();
        } else {
            const depth = xml.depth();
            xml.read();
            while (!xml.eof() && !(xml.nodeType() === 'endElement' && xml.depth() === depth)) {
                if (xml.nodeType() !== 'element') {
                    xml.read();
                    continue;
                }
                switch (xml.localName()) {
                    case 'v':
                        value = xml.readElementContentAsString();
                        break;
                    case 'f':
                        formula = this.#readFormula(metadata, at);
                        break;
                    case 'is':
                        [text, rich] = readText(xml);
                        xml.read();
                        break;
                    default:
                        xml.skip();
                        break;
                }
            }
            xml.read();
        }
        switch (cellType) {
            case 'sharedString': {
                const strings = this.#owner.sharedStrings();
                const index = value === null ? undefined : parseDigits(value);
                if (index === undefined || index >= strings.count) {
                    throw new XlsxError('malformed', `The cell names the shared string '${value === null ? '' : value}', which the workbook does not hold.`, at());
                }
                text = elementAt(strings.texts, index);
                rich = strings.isRich(index);
                break;
            }
            case 'inlineString':
                if (text === null) text = '';
                break;
            case 'formulaString':
                if (value !== null) value = decodeXString(value);
                break;
            case 'number':
            case 'boolean':
            case 'error':
            case 'date':
                break;
            default:
                return unreachableType(cellType);
        }
        this.#batch.push(Object.freeze({ row: r, column: c, type: cellType, value, text, formula, styleIndex, isRichText: rich }));
    }

    #readFormula(metadata: string | undefined, at: () => { part: string; sheet: string; cell?: string }): XlsxFormula {
        const xml = this.#opened.xml;
        let kind: string | undefined;
        let reference: string | undefined;
        let shared: string | undefined;
        for (const attribute of xml.attributes()) {
            switch (attribute.localName) {
                case 't': kind = attribute.value; break;
                case 'ref': reference = attribute.value; break;
                case 'si': shared = attribute.value; break;
                default: break;
            }
        }
        const formulaKind = formulaKindOf(kind, at);
        const sharedIndex = shared === undefined ? null : int(shared, at());
        if (formulaKind === 'shared' && sharedIndex === null) throw new XlsxError('malformed', 'A shared formula names no group (si).', at());
        const cm = metadata === undefined ? undefined : parseDigits(metadata);
        const isDynamicArray = formulaKind === 'array' && cm !== undefined && this.#owner.isDynamicArray(cm);
        const text = decodeXString(xml.readElementContentAsString());
        return Object.freeze({ text, kind: formulaKind, reference: reference === undefined ? null : reference, sharedIndex, isDynamicArray });
    }

    /** Reads what follows the cells, to the part's end. */
    #readTail(): 'more' | 'done' {
        const xml = this.#opened.xml;
        while (!xml.eof()) {
            if (xml.nodeType() === 'element') {
                if (low(xml)) return 'more';
                this.#other.push(this.#readElement(0));
            } else {
                xml.read();
            }
        }
        this.#state = 'finished';
        return 'done';
    }
}

function cellTypeOf(type: string | undefined, at: () => { part: string; sheet: string; cell?: string }): XlsxCellType {
    switch (type) {
        case undefined:
        case 'n': return 'number';
        case 's': return 'sharedString';
        case 'inlineStr': return 'inlineString';
        case 'str': return 'formulaString';
        case 'b': return 'boolean';
        case 'e': return 'error';
        case 'd': return 'date';
        default: throw new XlsxError('malformed', `'${type}' is not a cell type.`, at());
    }
}

function formulaKindOf(kind: string | undefined, at: () => { part: string; sheet: string; cell?: string }): XlsxFormulaKind {
    switch (kind) {
        case undefined:
        case 'normal': return 'normal';
        case 'shared': return 'shared';
        case 'array': return 'array';
        case 'dataTable': return 'dataTable';
        default: throw new XlsxError('malformed', `'${kind}' is not a kind of Formula.`, at());
    }
}

function unreachableType(type: never): never {
    throw new Error(`Internal: ${String(type)} is not a cell's type.`);
}
