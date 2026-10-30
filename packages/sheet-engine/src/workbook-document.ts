// The serialisable form of a Workbook (C#'s WorkbookDocument.cs; CONTEXT.md, ADR-0331), read and written as the C#
// engine reads and writes it.
import type { Culture } from './culture';
import { equalsIgnoreCase } from './dotnet';
import { JsonWriter, jsonInt32, type JsonElement } from './json-element';
import { parseDocument, property, SheetDocument } from './sheet-document';
import { SheetDocumentError, type SheetDocumentTable } from './sheet-document-parts';

/**
 * The serialisable form of a Workbook (CONTEXT.md, ADR-0331): its Sheets in order, the Sheet shown, its culture and its
 * Linked Tables' declarations, and for each Sheet what a Sheet Document records of it — never a Value: opening one
 * computes every Value again. The Consumer persists it; ExSheet never does.
 *
 * The document is a format with a version, beside the Sheet Document's. A reader meeting a version it does not know
 * refuses the document rather than guess at it, and so does a reader meeting anything it does not understand
 * (ADR-0048). This engine writes version 1, whose Sheets are recorded as a Sheet Document of the current version
 * records a Sheet. `fromJson` also reads a Sheet Document of every version `SheetDocument.fromJson` reads, as a
 * Workbook of that one Sheet.
 */
export class WorkbookDocument {
    /** The version this engine writes. */
    static readonly currentVersion = 1;

    /** The oldest version this engine reads. */
    static readonly oldestReadableVersion = 1;

    /** The name of the Workbook's declared culture, such as `en-US`; empty for the invariant culture, as .NET names it. */
    readonly culture: string;

    /** The name of the Sheet shown, one of `sheets`. */
    readonly shown: string;

    /** The Linked Tables declared on the Workbook, in the order they were declared: names, columns and keys only (ADR-0049). */
    readonly linkedTables: readonly SheetDocumentTable[];

    /** The Sheets, in the order their tabs stand, each under the Workbook's culture and declaring no Linked Table of its own. */
    readonly sheets: readonly SheetDocument[];

    /** @internal */
    constructor(
        culture: string,
        shown: string,
        linkedTables: readonly SheetDocumentTable[],
        sheets: readonly SheetDocument[],
    ) {
        this.culture = culture;
        this.shown = shown;
        this.linkedTables = linkedTables;
        this.sheets = sheets;
    }

    /**
     * A Workbook Document built from its Sheets, as a reader of another format builds one (ADR-0330 §3): `sheets` in the
     * order their tabs stand, the one named `shown` shown, under `culture`, declaring no Linked Table. Throws a
     * RangeError when there is no Sheet, two share a name, a Sheet records another culture or declares a Linked Table,
     * or no Sheet is named `shown`. Nothing is guessed.
     */
    static of(culture: Culture, shown: string, sheets: Iterable<SheetDocument>): WorkbookDocument {
        const list: SheetDocument[] = [];
        for (const sheet of sheets) {
            if (list.some(s => equalsIgnoreCase(s.name, sheet.name))) throw new RangeError(`Two Sheets are named '${sheet.name}'.`);
            if (sheet.culture !== culture.name) {
                throw new RangeError(`The Sheet '${sheet.name}' records the culture '${sheet.culture}', not the Workbook's '${culture.name}'.`);
            }
            if (sheet.linkedTables.length > 0) {
                throw new RangeError(`The Sheet '${sheet.name}' declares a Linked Table; a Workbook's Linked Tables are declared on the Workbook.`);
            }
            list.push(sheet);
        }
        if (list.length === 0) throw new RangeError('A Workbook holds at least one Sheet.');
        const shownSheet = list.find(s => equalsIgnoreCase(s.name, shown));
        if (shownSheet === undefined) throw new RangeError(`The Sheet shown, '${shown}', is not one of the Sheets.`);
        return new WorkbookDocument(culture.name, shownSheet.name, [], list);
    }

    /** The format version the document is written in: `currentVersion`, whatever it was read from. */
    get version(): number {
        return WorkbookDocument.currentVersion;
    }

    /** Writes the document as JSON, as the C# engine writes it. */
    toJson(): string {
        const json = new JsonWriter();
        json.startObject();
        json.number('workbookVersion', WorkbookDocument.currentVersion);
        json.string('culture', this.culture);
        json.string('shown', this.shown);
        SheetDocument.writeTables(json, this.linkedTables);
        json.startArray('sheets');
        for (const sheet of this.sheets) {
            json.startObject();
            json.string('name', sheet.name);
            sheet.writeBody(json);
            json.endObject();
        }
        json.endArray();
        json.endObject();
        return json.toString();
    }

    /**
     * Reads a document written by `toJson`, or by the C# engine, or a Sheet Document of any version
     * `SheetDocument.fromJson` reads, as a Workbook of that one Sheet, shown, with its Linked Tables declared on the
     * Workbook (ADR-0331). Throws a SheetDocumentError when the version is not one this engine reads, the document holds
     * anything its version does not define, it holds no Sheet or two of one name, or the Sheet it shows is not one of
     * them. Nothing is guessed.
     */
    static fromJson(text: string): WorkbookDocument {
        const root = parseDocument(text, 'Workbook Document');
        if (root.kind !== 'object') throw new SheetDocumentError('A Workbook Document is a JSON object.');
        const version = property(root, 'workbookVersion');
        if (version === undefined) {
            // No Workbook version: a Sheet Document, read as one, or refused as one is.
            const sheet = SheetDocument.fromJson(text);
            return new WorkbookDocument(sheet.culture, sheet.name, sheet.linkedTables,
                [new SheetDocument(sheet.culture, sheet.name, [], sheet.cells, sheet)]);
        }
        const number = jsonInt32(version);
        if (number === undefined) throw new SheetDocumentError("The Workbook Document's version is not a number.");
        if (number < WorkbookDocument.oldestReadableVersion || number > WorkbookDocument.currentVersion) {
            throw new SheetDocumentError(
                `The Workbook Document is version ${String(number)}; this engine reads versions ${String(WorkbookDocument.oldestReadableVersion)} to ${String(WorkbookDocument.currentVersion)}.`,
                { documentVersion: number });
        }
        let culture: string | undefined;
        let shown: string | undefined;
        let tables: SheetDocumentTable[] = [];
        let sheetsElement: (JsonElement & { kind: 'array' }) | undefined;
        for (const [key, value] of root.properties) {
            switch (key) {
                case 'workbookVersion':
                    break;
                case 'culture':
                    if (value.kind !== 'string') throw new SheetDocumentError('The culture is not a string.');
                    culture = value.value;
                    break;
                case 'shown':
                    if (value.kind !== 'string') throw new SheetDocumentError('The Sheet shown is not a string.');
                    shown = value.value;
                    break;
                case 'linkedTables':
                    tables = SheetDocument.readTables(value, SheetDocument.currentVersion);
                    break;
                case 'sheets':
                    if (value.kind !== 'array') throw new SheetDocumentError('The Sheets are not an array.');
                    sheetsElement = value;
                    break;
                default:
                    throw new SheetDocumentError(`'${key}' is not part of a version ${String(number)} Workbook Document.`);
            }
        }
        if (culture === undefined) throw new SheetDocumentError('The Workbook Document records no culture.');
        SheetDocument.resolveCulture(culture);
        if (sheetsElement === undefined) throw new SheetDocumentError('The Workbook Document records no Sheets.');
        const sheets: SheetDocument[] = [];
        for (const element of sheetsElement.items) {
            const sheet = SheetDocument.readSheetOfWorkbook(element, culture);
            if (sheets.some(s => equalsIgnoreCase(s.name, sheet.name))) throw new SheetDocumentError(`Two Sheets of the Workbook Document are named '${sheet.name}'.`);
            sheets.push(sheet);
        }
        if (sheets.length === 0) throw new SheetDocumentError('The Workbook Document records no Sheets.');
        if (shown === undefined) throw new SheetDocumentError('The Workbook Document records no Sheet shown.');
        const wanted = shown;
        const shownSheet = sheets.find(s => equalsIgnoreCase(s.name, wanted));
        if (shownSheet === undefined) throw new SheetDocumentError(`The Sheet shown, '${shown}', is not a Sheet of the Workbook Document.`);
        return new WorkbookDocument(culture, shownSheet.name, tables, sheets);
    }
}
