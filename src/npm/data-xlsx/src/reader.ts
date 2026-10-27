// An .xlsx workbook opened for reading (ADR-0330 §1, §9), as ExGrid.Data.Xlsx's XlsxReader opens it. Opening reads the
// package's parts that are small and whole — the workbook part, the shared strings, the styles and the theme — and
// refuses by name a file that is not a plain .xlsx, or that passes a limit. Each worksheet's cells are then read as a
// stream, a batch at a time, through `openSheet`; no worksheet is ever held as a document, nor is the file.

import { grouped, parseHex32 } from './invariant';
import { formatCell } from './cell-reference';
import type { XlsxCell, XlsxColumn, XlsxDefinedName, XlsxElement, XlsxRow, XlsxSheetInfo, XlsxSheetVisibility } from './model';
import {
    binaryContent, drawingNamespace, relationshipsNamespace, sharedStringsType, sheetMetadataType, strictPrefix, stylesType, templateContent,
    themeType, vbaProjectContent, workbookContent, worksheetType,
} from './names';
import { classifyOle, isOle, isZip, Package } from './package';
import { defaultMaxCells, defaultMaxUncompressedBytes, Pacer, type XlsxProgress, type XlsxReadOptions } from './pacer';
import { XlsxSharedStrings } from './shared-strings';
import { SheetReader, type SheetOwner, type XlsxSheetReader } from './sheet-reader';
import { low, runSteps, type StepEnd } from './steps';
import type { XlsxStyles } from './styles';
import { defaultStyles } from './styles';
import { readStyles } from './styles-reader';
import { XlsxTheme } from './theme';
import { foldCase } from './text-casing';
import { decodeXString } from './x-string';
import { readText } from './string-item';
import { XlsxError } from './xlsx-error';
import { XmlError } from './xml-reader';
import { byteSource } from './zip-read';
import { bool, int } from './values';
import { elementAt } from './must';

/** A worksheet read whole (`XlsxReader.readSheet`). */
export interface XlsxWorksheet {
    /** The sheet as the workbook lists it. */
    readonly sheet: XlsxSheetInfo;
    /** Its column widths. */
    readonly columns: readonly XlsxColumn[];
    /** Its cells, in the file's order: row by row, left to right. */
    readonly cells: readonly XlsxCell[];
    /** What else it holds, as `XlsxSheetReader.otherElements` lists it. */
    readonly otherElements: readonly XlsxElement[];
    /** The rows that record a cell format of their own, in order; empty for none. */
    readonly rows: readonly XlsxRow[];
    /** Whether a row sets a height of its own. */
    readonly customRowHeights: boolean;
    /** Whether a row is hidden. */
    readonly hiddenRows: boolean;
    /** Whether the sheet's view freezes panes. */
    readonly frozenPanes: boolean;
}

interface Relationship {
    readonly type: string;
    readonly target: string;
}

/** The shared strings read between two looks at the clock: a few microseconds' work. */
const sharedStringSlice = 256;

/**
 * An `.xlsx` workbook opened for reading (ADR-0330 §1, §9): its sheets, defined names, shared strings, styles and theme,
 * read when it is opened; each worksheet's cells read as a stream through `openSheet`. The model is the file's own: a
 * number is its text, a Formula its stored text with its prefixes, a style an index. Made by `openXlsx`.
 */
export interface XlsxReader {
    /** The workbook part's name, as `xl/workbook.xml`. */
    readonly workbookPart: string;
    /** The workbook's sheets, in its order: the order of its tabs. */
    readonly sheets: readonly XlsxSheetInfo[];
    /** The index into `sheets` of the sheet the workbook shows (`activeTab`), 0 when it names none. */
    readonly activeSheet: number;
    /** Whether the workbook counts dates from 1904 (`date1904`), whose serials are 1,462 less than 1900's for the same day. */
    readonly date1904: boolean;
    /** The workbook's defined names. */
    readonly definedNames: readonly XlsxDefinedName[];
    /** The shared strings, held whole. */
    readonly sharedStrings: XlsxSharedStrings;
    /** The styles, as far as ADR-0330 §4 carries them; `defaultStyles` when the package has no styles part. */
    readonly styles: XlsxStyles;
    /** The theme's colours; `XlsxTheme.office` when the package has no theme part (`hasTheme`). */
    readonly theme: XlsxTheme;
    /** Whether the package has a theme part of its own. */
    readonly hasTheme: boolean;
    /** Every part the package holds, by name, as `xl/comments1.xml`: what else it holds than what is read. */
    readonly parts: readonly string[];
    /** The cells read so far, every worksheet counted. */
    readonly cellsRead: number;
    /** The worksheets' uncompressed bytes, as the zip declares them. */
    readonly sheetBytes: number;
    /** Opens the worksheet at `index` for reading its cells as a stream. One worksheet is read at a time: close the reader
     * before opening the next. */
    openSheet(index: number): Promise<XlsxSheetReader>;
    /** Reads the worksheet at `index` whole: its columns, its cells and what else it holds. For a sheet that is small, or a
     * test; a large one is read a batch at a time through `openSheet`. */
    readSheet(index: number): Promise<XlsxWorksheet>;
}

class WorkbookReader implements XlsxReader {
    readonly #package: Package;
    readonly #pacer: Pacer;
    readonly #maxCells: number;
    readonly #dynamicArrayMetadata: ReadonlySet<number>;
    readonly #owner: SheetOwner;
    #cells = 0;
    #sheetBytesRead = 0;
    #sharedStrings = XlsxSharedStrings.empty;
    #sheetOpen = false;

    /** The workbook part's name, as `xl/workbook.xml`. */
    readonly workbookPart: string;
    /** The workbook's sheets, in its order: the order of its tabs. */
    readonly sheets: readonly XlsxSheetInfo[];
    /** The index into `sheets` of the sheet the workbook shows (`activeTab`), 0 when it names none. */
    readonly activeSheet: number;
    /** Whether the workbook counts dates from 1904 (`date1904`), whose serials are 1,462 less than 1900's for the same day. */
    readonly date1904: boolean;
    /** The workbook's defined names. */
    readonly definedNames: readonly XlsxDefinedName[];
    /** The styles, as far as ADR-0330 §4 carries them; `defaultStyles` when the package has no styles part. */
    readonly styles: XlsxStyles;
    /** The theme's colours; `XlsxTheme.office` when the package has no theme part (`hasTheme`). */
    readonly theme: XlsxTheme;
    /** Whether the package has a theme part of its own. */
    readonly hasTheme: boolean;
    /** The worksheets' uncompressed bytes, as the zip declares them. */
    readonly sheetBytes: number;

    private constructor(pkg: Package, options: XlsxReadOptions, read: WorkbookRead) {
        this.#package = pkg;
        this.#pacer = new Pacer(options);
        this.#maxCells = options.maxCells === undefined ? defaultMaxCells : options.maxCells;
        this.workbookPart = read.part;
        this.sheets = read.sheets;
        this.activeSheet = read.active;
        this.date1904 = read.date1904;
        this.definedNames = read.names;
        this.styles = read.styles;
        this.theme = read.theme;
        this.hasTheme = read.hasTheme;
        this.#dynamicArrayMetadata = read.dynamicArrayMetadata;
        this.sheetBytes = read.sheets.reduce((sum, s) => (s.part === null ? sum : sum + pkg.lengthOf(s.part)), 0);
        const progress = (sheet: number): XlsxProgress => ({
            sheet, sheets: this.sheets.length, cells: this.#cells, bytes: this.#sheetBytesRead, totalBytes: this.sheetBytes,
        });
        this.#owner = {
            styles: this.styles,
            sharedStrings: () => this.#sharedStrings,
            isDynamicArray: cm => this.#dynamicArrayMetadata.has(cm),
            countCell: (sheet, row, column) => {
                if (++this.#cells > this.#maxCells) {
                    throw new XlsxError('tooManyCells',
                        `The workbook holds more than the ${grouped(this.#maxCells)} cells a read is allowed (XlsxReadOptions.MaxCells).`,
                        { part: sheet.part === null ? undefined : sheet.part, sheet: sheet.name, cell: formatCell(row, column) });
                }
            },
            cells: () => this.#cells,
            rewindCells: to => {
                this.#cells = to;
            },
            countBytes: bytes => {
                this.#sheetBytesRead += bytes;
            },
            checkpoint: sheet => this.#pacer.checkpoint(() => progress(sheet)),
            report: sheet => this.#pacer.report(progress(sheet)),
            closed: () => {
                this.#sheetOpen = false;
            },
        };
    }

    /** Opens the workbook in `file`. */
    static async open(file: Uint8Array | ArrayBuffer | Blob, options: XlsxReadOptions = {}): Promise<WorkbookReader> {
        options.signal?.throwIfAborted();
        const max = options.maxUncompressedBytes === undefined ? defaultMaxUncompressedBytes : options.maxUncompressedBytes;
        const source = byteSource(file);
        const head = await source.read(0, 8);
        if (isOle(head)) throw await classifyOle(source);
        if (!isZip(head)) throw new XlsxError('notAZip', head.length === 0 ? 'The file is empty.' : 'The file is not a zip package, so it is not an .xlsx.');
        const pkg = await Package.open(source, max);
        const read = await readWorkbookParts(pkg);
        const reader = new WorkbookReader(pkg, options, read);
        if (read.sharedStringsPart !== undefined) await reader.#readSharedStrings(read.sharedStringsPart);
        return reader;
    }

    /** The shared strings, held whole. */
    get sharedStrings(): XlsxSharedStrings {
        return this.#sharedStrings;
    }

    /** Every part the package holds, by name, as `xl/comments1.xml`: what else it holds than what is read. */
    get parts(): readonly string[] {
        return this.#package.partNames;
    }

    /** The cells read so far, every worksheet counted. */
    get cellsRead(): number {
        return this.#cells;
    }

    /**
     * Opens the worksheet at `index` for reading its cells as a stream. One worksheet is read at a time: close the reader
     * before opening the next.
     */
    async openSheet(index: number): Promise<XlsxSheetReader> {
        if (!Number.isInteger(index) || index < 0 || index >= this.sheets.length) {
            throw new RangeError(`The workbook has ${String(this.sheets.length)} sheets; ${String(index)} is not the index of one.`);
        }
        const sheet = elementAt(this.sheets, index);
        if (sheet.part === null) throw new Error(`The sheet '${sheet.name}' is not a worksheet and holds no cells.`);
        if (this.#sheetOpen) throw new Error('Another worksheet is being read: close its reader first.');
        const opened = await this.#package.openXml(sheet.part, sheet.name);
        if (opened === undefined) {
            throw new XlsxError('malformed', 'The workbook names a worksheet part the package does not hold.', { part: sheet.part, sheet: sheet.name });
        }
        this.#sheetOpen = true;
        return new SheetReader(this.#owner, index, sheet, sheet.part, opened);
    }

    /**
     * Reads the worksheet at `index` whole: its columns, its cells and what else it holds. For a sheet that is small, or a
     * test; a large one is read a batch at a time through `openSheet`.
     */
    async readSheet(index: number): Promise<XlsxWorksheet> {
        const reader = await this.openSheet(index);
        try {
            const cells: XlsxCell[] = [];
            const rows: XlsxRow[] = [];
            while (await reader.read()) {
                cells.push(...reader.cells);
                rows.push(...reader.rows);
            }
            return Object.freeze({
                sheet: reader.sheet,
                columns: reader.columns,
                cells: Object.freeze(cells),
                otherElements: reader.otherElements,
                rows: Object.freeze(rows),
                customRowHeights: reader.customRowHeights,
                hiddenRows: reader.hiddenRows,
                frozenPanes: reader.frozenPanes,
            });
        } finally {
            await reader.close();
        }
    }

    /**
     * Reads the shared strings, the one part held whole, in slices as a worksheet is read (ADR-0330 §9): cancellation is
     * looked at, progress reported and the read yields between them. Its progress names no worksheet (`sheet` is -1) and
     * counts the part's bytes.
     */
    async #readSharedStrings(part: string): Promise<void> {
        const opened = await this.#package.openXml(part);
        if (opened === undefined) return;
        const { xml, bytes } = opened;
        const total = this.#package.lengthOf(part);
        const progress = (): XlsxProgress => ({ sheet: -1, sheets: this.sheets.length, cells: 0, bytes: bytes.bytesRead, totalBytes: total });
        const texts: string[] = [];
        const rich: number[] = [];
        let started = false;
        const step = (): StepEnd => {
            if (!started) {
                xml.moveToContent();
                started = true;
            }
            let slice = 0;
            for (;;) {
                if (low(xml)) return 'more';
                if (!xml.read()) return 'done';
                if (xml.nodeType() === 'element' && xml.localName() === 'si') {
                    const [text, isRich] = readText(xml);
                    if (isRich) rich.push(texts.length);
                    texts.push(text);
                    if (++slice === sharedStringSlice) return 'pause';
                }
            }
        };
        const save = (): (() => void) => {
            const count = texts.length;
            const richCount = rich.length;
            const wasStarted = started;
            return () => {
                texts.length = count;
                rich.length = richCount;
                started = wasStarted;
            };
        };
        try {
            while ((await runSteps(xml, save, step)) === 'pause') await this.#pacer.checkpoint(progress);
        } catch (error) {
            if (error instanceof XmlError) throw Package.refuse(error, opened, part);
            throw error;
        } finally {
            await bytes.close();
        }
        this.#pacer.report(progress());
        this.#sharedStrings = new XlsxSharedStrings(texts, new Set(rich));
    }
}

/**
 * Opens the workbook in `file`, reading its workbook part, shared strings, styles and theme, or refuses it by name
 * (`XlsxError`). A Blob — a File — is read where its bytes are asked for, through `slice`, and never whole.
 * @param file - The file's bytes, or a Blob.
 * @param options - The limits, progress and pacing; the defaults when left out.
 */
export function openXlsx(file: Uint8Array | ArrayBuffer | Blob, options?: XlsxReadOptions): Promise<XlsxReader> {
    return WorkbookReader.open(file, options);
}

// ---- the workbook's parts ---------------------------------------------------------------------

interface WorkbookRead {
    readonly part: string;
    readonly sheets: readonly XlsxSheetInfo[];
    readonly active: number;
    readonly date1904: boolean;
    readonly names: readonly XlsxDefinedName[];
    readonly styles: XlsxStyles;
    readonly theme: XlsxTheme;
    readonly hasTheme: boolean;
    readonly dynamicArrayMetadata: ReadonlySet<number>;
    readonly sharedStringsPart: string | undefined;
}

async function readWorkbookParts(pkg: Package): Promise<WorkbookRead> {
    const part = await workbookPartOf(pkg);
    const rels = await relationships(pkg, part);
    const { sheets, active, date1904, names } = await readWorkbook(pkg, part, rels);
    const target = (type: string): string | undefined => {
        for (const r of rels.values()) if (r.type === type) return r.target;
        return undefined;
    };
    const stylesPart = target(stylesType);
    const themePart = target(themeType);
    const metadataPart = target(sheetMetadataType);
    return {
        part, sheets, active, date1904, names,
        styles: stylesPart === undefined ? defaultStyles : await readStyles(pkg, stylesPart),
        theme: themePart === undefined ? XlsxTheme.office : await readTheme(pkg, themePart),
        hasTheme: themePart !== undefined,
        dynamicArrayMetadata: metadataPart === undefined ? new Set() : await readDynamicArrayMetadata(pkg, metadataPart),
        sharedStringsPart: target(sharedStringsType),
    };
}

function strict(): XlsxError {
    return new XlsxError('strictOpenXml', 'The workbook is saved as Strict Open XML, which is not read. Save it from Excel as an Excel Workbook (.xlsx).');
}

/** The extension of a part's name, without its point, as `Path.GetExtension` finds it; '' for none. */
function extensionOf(name: string): string {
    const dot = name.lastIndexOf('.');
    return dot < 0 || dot < name.lastIndexOf('/') ? '' : name.slice(dot + 1);
}

/** The workbook part, from the content types and the package's relationships; refused by name when the package is not
 * a plain transitional workbook. */
async function workbookPartOf(pkg: Package): Promise<string> {
    const { defaults, overrides } = await readContentTypes(pkg);
    const root = await relationships(pkg, '');
    let office: Relationship | undefined;
    for (const r of root.values()) {
        if (r.type.endsWith('/officeDocument')) {
            office = r;
            break;
        }
    }
    if (office === undefined) {
        if ([...root.values()].some(r => r.type.startsWith(strictPrefix))) throw strict();
        throw new XlsxError('notAWorkbook', 'The zip package holds no document, so it is not an .xlsx.');
    }
    if (office.type.startsWith(strictPrefix)) throw strict();
    const part = office.target;
    const contentType = overrides.get(foldCase(part)) ?? defaults.get(foldCase(extensionOf(part))) ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- ExGrid.Data.Xlsx: a document with no content type is refused below by that absence
    if (contentType === binaryContent || foldCase(part).endsWith('.bin')) {
        throw new XlsxError('binary', 'The file is an Excel Binary Workbook (.xlsb), not an .xlsx. Save it from Excel as an Excel Workbook (.xlsx).', { part });
    }
    if (foldCase(contentType).includes(foldCase('macroEnabled')) || [...overrides.values(), ...defaults.values()].includes(vbaProjectContent)) {
        throw new XlsxError('macroEnabled',
            'The file is a workbook with macros (.xlsm), which is not read. Save it from Excel as an Excel Workbook (.xlsx), which leaves the macros out.', { part });
    }
    if (contentType !== workbookContent && contentType !== templateContent) {
        throw new XlsxError('notAWorkbook', contentType === ''
            ? "The package's document has no content type, so it is not an .xlsx."
            : `The package's document is '${contentType}', not a workbook: the file is not an .xlsx.`, { part });
    }
    if (!pkg.has(part)) throw new XlsxError('malformed', 'The package names a workbook part it does not hold.', { part });
    return part;
}

/** The content types: the defaults by extension and the overrides by part, each keyed in any case. */
async function readContentTypes(pkg: Package): Promise<{ defaults: Map<string, string>; overrides: Map<string, string> }> {
    const part = '[Content_Types].xml';
    const defaults = new Map<string, string>();
    const overrides = new Map<string, string>();
    const opened = await pkg.readXml(part);
    if (opened === undefined) throw new XlsxError('notAWorkbook', 'The zip package has no [Content_Types].xml, so it is not an Office document.');
    const xml = opened.xml;
    try {
        while (xml.read()) {
            if (xml.nodeType() !== 'element') continue;
            if (xml.localName() === 'Default') {
                const ext = xml.getAttribute('Extension');
                const type = xml.getAttribute('ContentType');
                if (ext !== undefined && type !== undefined) defaults.set(foldCase(ext), type);
            } else if (xml.localName() === 'Override') {
                const name = xml.getAttribute('PartName');
                const type = xml.getAttribute('ContentType');
                if (name !== undefined && type !== undefined) overrides.set(foldCase(name.replace(/^\/+/, '')), type);
            }
        }
    } catch (error) {
        if (error instanceof XmlError) throw Package.refuse(error, opened, part);
        throw error;
    }
    return { defaults, overrides };
}

/** The relationships of `source` (the package itself when empty), by id, each target resolved to a part name. */
async function relationships(pkg: Package, source: string): Promise<Map<string, Relationship>> {
    const slash = source.lastIndexOf('/');
    const dir = slash < 0 ? '' : source.slice(0, slash);
    const part = `${dir === '' ? '' : `${dir}/`}_rels/${source.slice(slash + 1)}.rels`;
    const found = new Map<string, Relationship>();
    const opened = await pkg.readXml(part);
    if (opened === undefined) return found;
    const xml = opened.xml;
    try {
        while (xml.read()) {
            if (xml.nodeType() !== 'element' || xml.localName() !== 'Relationship') continue;
            const id = xml.getAttribute('Id');
            const type = xml.getAttribute('Type');
            const target = xml.getAttribute('Target');
            if (id === undefined || type === undefined || target === undefined || xml.getAttribute('TargetMode') === 'External') continue;
            found.set(id, { type, target: resolve(dir, target) });
        }
    } catch (error) {
        if (error instanceof XmlError) throw Package.refuse(error, opened, part);
        throw error;
    }
    return found;
}

/** `Uri.UnescapeDataString`: each run of `%XX` escapes that spells UTF-8 decoded, and any other `%` left as it is. */
function unescapeData(text: string): string {
    return text.replace(/(?:%[0-9A-Fa-f]{2})+/g, run => {
        const bytes = Uint8Array.from(run.match(/%[0-9A-Fa-f]{2}/g) ?? [], h => Number.parseInt(h.slice(1), 16)); // oxlint-disable-line ex-grid/no-stand-in-default -- the run matched the pattern, so it holds at least one escape
        let out = '';
        let at = 0;
        while (at < bytes.length) {
            const length = utf8Length(bytes, at);
            if (length === 0) {
                out += run.slice(at * 3, at * 3 + 3);
                at++;
            } else {
                out += new TextDecoder().decode(bytes.subarray(at, at + length));
                at += length;
            }
        }
        return out;
    });
}

/** The length of the well-formed UTF-8 sequence at `at`, or 0 when none starts there. */
function utf8Length(bytes: Uint8Array, at: number): number {
    const b = elementAt(bytes, at);
    const length = b < 0x80 ? 1 : b >= 0xC2 && b < 0xE0 ? 2 : b >= 0xE0 && b < 0xF0 ? 3 : b >= 0xF0 && b < 0xF5 ? 4 : 0;
    if (length === 0 || at + length > bytes.length) return 0;
    for (let i = 1; i < length; i++) {
        if ((elementAt(bytes, at + i) & 0xC0) !== 0x80) return 0;
    }
    const second = length > 1 ? elementAt(bytes, at + 1) : 0;
    if ((b === 0xE0 && second < 0xA0) || (b === 0xED && second >= 0xA0) || (b === 0xF0 && second < 0x90) || (b === 0xF4 && second >= 0x90)) return 0;
    return length;
}

function resolve(dir: string, given: string): string {
    const target = unescapeData(given.replaceAll('\\', '/'));
    const path = target.startsWith('/') ? target.replace(/^\/+/, '') : dir === '' ? target : `${dir}/${target}`;
    const parts: string[] = [];
    for (const segment of path.split('/')) {
        if (segment === '' || segment === '.') continue;
        if (segment === '..') {
            parts.pop();
            continue;
        }
        parts.push(segment);
    }
    return parts.join('/');
}

interface Workbook {
    readonly sheets: readonly XlsxSheetInfo[];
    readonly active: number;
    readonly date1904: boolean;
    readonly names: readonly XlsxDefinedName[];
}

async function readWorkbook(pkg: Package, part: string, rels: ReadonlyMap<string, Relationship>): Promise<Workbook> {
    const sheets: XlsxSheetInfo[] = [];
    const names: XlsxDefinedName[] = [];
    let active = 0;
    let date1904 = false;
    const opened = await pkg.readXml(part);
    if (opened === undefined) throw new Error(`Internal: the package was checked to hold '${part}'.`);
    const xml = opened.xml;
    try {
        xml.moveToContent();
        if (xml.namespaceUri().startsWith(strictPrefix)) throw strict();
        if (xml.localName() !== 'workbook') throw new XlsxError('malformed', `The workbook part's root is <${xml.localName()}>, not <workbook>.`, { part });
        // A defined name's text is read with readElementContentAsString, which leaves the reader on the node after it:
        // that node is looked at before reading on.
        let more = xml.read();
        while (more) {
            if (xml.nodeType() !== 'element') {
                more = xml.read();
                continue;
            }
            let advanced = false;
            switch (xml.localName()) {
                case 'workbookPr':
                    date1904 = bool(xml.getAttribute('date1904'));
                    break;
                case 'workbookView': {
                    const tab = xml.getAttribute('activeTab');
                    if (tab !== undefined) active = int(tab, { part });
                    break;
                }
                case 'sheet': {
                    const name = xml.getAttribute('name');
                    if (name === undefined) throw new XlsxError('malformed', 'A sheet has no name.', { part });
                    const id = xml.getAttributeNs('id', relationshipsNamespace);
                    const state = xml.getAttribute('state');
                    const visibility: XlsxSheetVisibility = state === 'hidden' ? 'hidden' : state === 'veryHidden' ? 'veryHidden' : 'visible';
                    const sid = xml.getAttribute('sheetId');
                    const sheetId = sid === undefined ? sheets.length + 1 : int(sid, { part });
                    const rel = id === undefined ? undefined : rels.get(id);
                    sheets.push(Object.freeze(rel !== undefined && rel.type === worksheetType
                        ? { name, sheetId, visibility, part: rel.target, kind: 'worksheet' as const }
                        : { name, sheetId, visibility, part: null, kind: 'other' as const }));
                    break;
                }
                case 'definedName': {
                    const given = xml.getAttribute('name');
                    const name = given === undefined ? '' : given;
                    const l = xml.getAttribute('localSheetId');
                    const localSheet = l === undefined ? null : int(l, { part });
                    const hidden = bool(xml.getAttribute('hidden'));
                    names.push(Object.freeze({ name, text: decodeXString(xml.readElementContentAsString()), localSheet, hidden }));
                    advanced = true;
                    break;
                }
                default:
                    break;
            }
            more = advanced ? !xml.eof() : xml.read();
        }
    } catch (error) {
        if (error instanceof XmlError) throw Package.refuse(error, opened, part);
        throw error;
    }
    if (sheets.length === 0) throw new XlsxError('malformed', 'The workbook lists no sheets.', { part });
    if (active < 0 || active >= sheets.length) active = 0;
    return { sheets: Object.freeze(sheets), active, date1904, names: Object.freeze(names) };
}

const schemeOrder = ['dk1', 'lt1', 'dk2', 'lt2', 'accent1', 'accent2', 'accent3', 'accent4', 'accent5', 'accent6', 'hlink', 'folHlink'];

async function readTheme(pkg: Package, part: string): Promise<XlsxTheme> {
    const colors = [...XlsxTheme.office.schemeColors];
    const opened = await pkg.readXml(part);
    if (opened === undefined) return XlsxTheme.office;
    const xml = opened.xml;
    try {
        if (!xml.readToFollowing('clrScheme', drawingNamespace)) return XlsxTheme.office;
        const depth = xml.depth();
        let slot = -1;
        while (xml.read() && xml.depth() > depth) {
            if (xml.nodeType() !== 'element') continue;
            if (xml.depth() === depth + 1) {
                slot = schemeOrder.indexOf(xml.localName());
                continue;
            }
            if (slot < 0) continue;
            const value = xml.localName() === 'srgbClr' ? xml.getAttribute('val') : xml.localName() === 'sysClr' ? xml.getAttribute('lastClr') : undefined;
            const rgb = value === undefined ? undefined : parseHex32(value);
            if (rgb !== undefined) colors[slot] = (0xFF000000 | (rgb & 0xFFFFFF)) >>> 0;
        }
    } catch (error) {
        if (error instanceof XmlError) throw Package.refuse(error, opened, part);
        throw error;
    }
    return new XlsxTheme(colors);
}

/** The cell metadata indexes (`cm`, from one) that mark a dynamic array: a `cellMetadata` block whose record points at an
 * `XLDAPR` future-metadata block with `fDynamic="1"`. */
async function readDynamicArrayMetadata(pkg: Package, part: string): Promise<ReadonlySet<number>> {
    const dynamic = new Set<number>();
    const opened = await pkg.readXml(part);
    if (opened === undefined) return dynamic;
    const xml = opened.xml;
    const types: string[] = [];
    const futures = new Map<string, boolean[]>();
    const cellBlocks: [number, number][] = [];
    try {
        let future: boolean[] | null = null;
        let inCellMetadata = false;
        let pendingRecord = false;
        while (xml.read()) {
            if (xml.nodeType() === 'endElement') {
                if (xml.localName() === 'futureMetadata') future = null;
                else if (xml.localName() === 'cellMetadata') inCellMetadata = false;
                continue;
            }
            if (xml.nodeType() !== 'element') continue;
            const name = xml.localName();
            if (name === 'metadataType') {
                const typeName = xml.getAttribute('name');
                types.push(typeName === undefined ? '' : typeName);
            } else if (name === 'futureMetadata') {
                const futureName = xml.getAttribute('name');
                const blocks: boolean[] = [];
                futures.set(futureName === undefined ? '' : futureName, blocks);
                future = xml.isEmptyElement() ? null : blocks;
            } else if (name === 'bk' && future !== null) {
                future.push(false);
            } else if (name === 'dynamicArrayProperties' && future !== null && future.length > 0) {
                future[future.length - 1] = bool(xml.getAttribute('fDynamic'));
            } else if (name === 'cellMetadata') {
                inCellMetadata = !xml.isEmptyElement();
            } else if (name === 'bk' && inCellMetadata) {
                pendingRecord = true;
                cellBlocks.push([-1, -1]);
            } else if (name === 'rc' && inCellMetadata && pendingRecord) {
                pendingRecord = false;
                const t = xml.getAttribute('t');
                const v = xml.getAttribute('v');
                cellBlocks[cellBlocks.length - 1] = [int(t === undefined ? '0' : t, { part }), int(v === undefined ? '0' : v, { part })];
            }
        }
    } catch (error) {
        if (error instanceof XmlError) throw Package.refuse(error, opened, part);
        throw error;
    }
    const blocks = futures.get('XLDAPR');
    cellBlocks.forEach(([type, value], i) => {
        if (type < 1 || type > types.length || types[type - 1] !== 'XLDAPR') return;
        if (blocks !== undefined && value >= 0 && value < blocks.length && blocks[value] === true) dynamic.add(i + 1);
    });
    return dynamic;
}
