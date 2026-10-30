// What crosses between .NET and the Sheet in the browser, for Blazor's adapter (ADR-0370 section 1): a Sheet Edit, a
// Cell Format change and a Linked Table's rows read from the JSON ExSheet.Engine's SheetWire writes, and a Cell Format
// and a refused snapshot written as it reads them. The vocabulary is the one the shared cases of both engines write
// edits, changes and Values in (tests/cases/sheet), so an edit means the same on both sides; the shared cases read
// their edits through this reader.
import { CellAddress } from './cell-address';
import { borderLineStyles, BorderLine, CellBorders, CellColour, CellFill, CellFont, type BorderLineStyle, type CellFormat, type HorizontalAlignment } from './cell-format';
import { BorderChange, CellFormatChange, CellState } from './cell-format-change';
import { CellRange } from './cell-range';
import { Entry } from './entry';
import { FormatError } from './format-error';
import { JsonSyntaxError, parseJson, type JsonElement } from './json-element';
import { NumberFormat } from './number-format';
import { SheetBlock, type EntryOfCell, type FillDirection, type TypedCell } from './sheet';
import type { RepeatedKeyError, SheetChange } from './sheet-change';
import { SheetEdit } from './sheet-edit';
import { parseWrittenError, Value } from './value';

type JsonObject = JsonElement & { kind: 'object' };

const alignments: readonly HorizontalAlignment[] = ['general', 'left', 'center', 'right'];
const directions: readonly FillDirection[] = ['down', 'right', 'up', 'left'];
const changeParts = new Set(['numberFormat', 'alignment', 'fontColour', 'bold', 'italic', 'underline', 'strikethrough', 'fill', 'borders']);
const edges = new Set(['top', 'bottom', 'left', 'right', 'insideHorizontal', 'insideVertical']);

/**
 * A Sheet Edit read from its JSON: an object of one key, the factory, and its arguments as the factory takes them
 * (ADR-0370 section 1). The factory checks them as it checks any caller's; what is not an edit so written is refused
 * with a `FormatError`.
 */
export function readSheetEdit(json: string): SheetEdit {
    return editOf(parse(json));
}

/** A Cell Format change read from its JSON: each part it sets, by its name. */
export function readCellFormatChange(json: string): CellFormatChange {
    return changeOf(parse(json));
}

/** A Linked Table's snapshot read from its JSON: rows of Values, a blank as `null`. */
export function readLinkedRows(json: string): (Value | undefined)[][] {
    return items(parse(json), 'the rows').map(row => items(row, 'a row').map(valueOf));
}

/**
 * A Linked Table's snapshot as JSON, rows of Values, a blank as `null` and a refusal as one, as `readLinkedRows` and
 * .NET's `SheetWire.ReadRows` read it.
 */
export function writeLinkedRows(rows: Iterable<readonly (Value | undefined)[]>): string {
    return JSON.stringify([...rows].map(row => row.map(value => value === undefined ? null : rowValueJson(value))));
}

/** A Cell Format as JSON: its Number Format's code, its alignment, its whole Font, its Fill and its four Borders. */
export function writeCellFormat(format: CellFormat): string {
    return JSON.stringify({
        numberFormat: format.numberFormat.code,
        alignment: format.alignment,
        font: fontJson(format.font),
        fill: fillText(format.fill),
        borders: bordersJson(format.borders),
    });
}

/**
 * A refused snapshot as JSON (ADR-0049): the table, its key's columns, the key as its first and its second row hold
 * it, the two rows, and what the table's going back to waiting changed, so that .NET raises the refusal as the C#
 * Sheet raises it.
 */
export function writeRepeatedKey(error: RepeatedKeyError): string {
    return JSON.stringify({
        table: error.table,
        keyColumns: error.keyColumns,
        keys: error.keys.map(valueJson),
        firstRow: error.firstRow,
        repeats: error.repeats.map(valueJson),
        secondRow: error.secondRow,
        change: changeJson(error.change),
    });
}

function parse(json: string): JsonElement {
    // A text that is not JSON is refused as the reader refuses a text that is not an edit.
    try {
        return parseJson(json);
    } catch (error) {
        if (error instanceof JsonSyntaxError) throw new FormatError(`Not JSON: ${error.message}`);
        throw error;
    }
}

function editOf(element: JsonElement): SheetEdit {
    const edit = object(element, 'an edit');
    const only = edit.properties.length === 1 ? edit.properties[0] : undefined;
    if (only === undefined) throw new FormatError(`An edit names one operation: ${edit.raw}.`);
    const [name, argument] = only;
    switch (name) {
        case 'enter':
            return SheetEdit.enterMany(typedOf(argument));
        case 'setEntries':
            return SheetEdit.setEntries(entriesOf(argument));
        case 'setNumberFormat': {
            const args = object(argument, name);
            const code = member(args, 'format');
            const format = code.kind === 'null' ? undefined : NumberFormat.parse(text(code, 'format'));
            const at = member(args, 'at');
            return at.kind === 'array'
                ? SheetEdit.setNumberFormatOf(at.items.map(addressOf), format)
                : SheetEdit.setNumberFormat(rangeOf(at), format);
        }
        case 'setAlignment': {
            const args = object(argument, name);
            const alignment = oneOf(alignments, text(member(args, 'alignment'), 'alignment'), 'an alignment');
            const at = member(args, 'at');
            return at.kind === 'array'
                ? SheetEdit.setAlignmentOf(at.items.map(addressOf), alignment)
                : SheetEdit.setAlignment(rangeOf(at), alignment);
        }
        case 'setCellFormat': {
            const args = object(argument, name);
            return SheetEdit.setCellFormat(items(member(args, 'ranges'), 'ranges').map(rangeOf), changeOf(member(args, 'change')));
        }
        case 'insertRows':
            return SheetEdit.insertRows(...placeAndCount(argument));
        case 'deleteRows':
            return SheetEdit.deleteRows(...placeAndCount(argument));
        case 'insertColumns':
            return SheetEdit.insertColumns(...placeAndCount(argument));
        case 'deleteColumns':
            return SheetEdit.deleteColumns(...placeAndCount(argument));
        case 'setColumnWidth': {
            const args = object(argument, name);
            const width = member(args, 'width');
            return SheetEdit.setColumnWidth(rangeOf(member(args, 'columns')), width.kind === 'null' ? undefined : number(width, 'width'));
        }
        case 'setAutomaticColumnWidth': {
            const args = object(argument, name);
            return SheetEdit.setAutomaticColumnWidth(rangeOf(member(args, 'columns')), number(member(args, 'width'), 'width'));
        }
        case 'fill':
        case 'fillCopy': {
            const args = object(argument, name);
            const source = rangeOf(member(args, 'source'));
            const target = rangeOf(member(args, 'target'));
            const direction = oneOf(directions, text(member(args, 'direction'), 'direction'), 'a fill direction');
            return name === 'fill' ? SheetEdit.fill(source, target, direction) : SheetEdit.fillCopy(source, target, direction);
        }
        case 'paste': {
            const args = object(argument, name);
            const block = blockOf(member(args, 'block'));
            const at = member(args, 'at');
            if (at.kind === 'array') return SheetEdit.paste(block, at.items.map(rangeOf));
            const target = text(at, 'at');
            return SheetEdit.paste(block, target.includes(':') ? CellRange.parse(target) : CellAddress.parse(target));
        }
        case 'pasteText': {
            const args = object(argument, name);
            return SheetEdit.pasteText(items(member(args, 'fields'), 'fields').map(row => items(row, 'a row').map(f => text(f, 'a field'))),
                addressOf(member(args, 'at')));
        }
        case 'pasteFields': {
            const args = object(argument, name);
            const entries = optional(args, 'entries');
            return SheetEdit.pasteFields(typedOf(member(args, 'typed')), entries === undefined ? undefined : entriesOf(entries));
        }
        case 'enterInto': {
            const args = object(argument, name);
            return SheetEdit.enterInto(items(member(args, 'targets'), 'targets').map(rangeOf), addressOf(member(args, 'at')), text(member(args, 'typed'), 'typed'));
        }
        case 'rename':
            return SheetEdit.rename(text(argument, 'rename'));
        case 'insertSheet': {
            const args = object(argument, name);
            const sheetName = optional(args, 'name');
            const before = optional(args, 'before');
            return SheetEdit.insertSheet(sheetName === undefined || sheetName.kind === 'null' ? undefined : text(sheetName, 'name'),
                before === undefined ? false : flag(before, 'before'));
        }
        case 'deleteSheet':
            return SheetEdit.deleteSheet();
        case 'moveSheet':
            return SheetEdit.moveSheet(whole(argument, 'moveSheet'));
        case 'copySheet':
            return SheetEdit.copySheet(argument.kind === 'number' ? whole(argument, 'copySheet') : undefined);
        default:
            throw new FormatError(`'${name}' is no Sheet Edit.`);
    }
}

function changeOf(element: JsonElement): CellFormatChange {
    const parts = object(element, 'a Cell Format change');
    for (const [key] of parts.properties) {
        if (!changeParts.has(key)) throw new FormatError(`'${key}' is no part of a Cell Format change.`);
    }
    const part = (key: string): JsonElement | undefined => optional(parts, key);
    const numberFormat = part('numberFormat');
    const alignment = part('alignment');
    const fontColour = part('fontColour');
    const fill = part('fill');
    const borders = part('borders');
    const emphasis = (key: string): boolean | undefined => {
        const given = part(key);
        return given === undefined ? undefined : flag(given, key);
    };
    return new CellFormatChange({
        numberFormat: numberFormat === undefined ? undefined : NumberFormat.parse(text(numberFormat, 'numberFormat')),
        alignment: alignment === undefined ? undefined : oneOf(alignments, text(alignment, 'alignment'), 'an alignment'),
        fontColour: fontColour === undefined ? undefined : colourOf(fontColour),
        bold: emphasis('bold'),
        italic: emphasis('italic'),
        underline: emphasis('underline'),
        strikethrough: emphasis('strikethrough'),
        fill: fill === undefined ? undefined : fillOf(fill),
        borders: borders === undefined ? undefined : borderChangeOf(borders),
    });
}

function borderChangeOf(element: JsonElement): BorderChange {
    if (element.kind === 'string' && element.value === 'none') return BorderChange.none;
    const given = object(element, 'a Border change');
    const outline = optional(given, 'outline');
    if (given.properties.length === 1 && outline !== undefined) return BorderChange.outline(lineOf(outline));
    const inside = optional(given, 'inside');
    if (given.properties.length === 1 && inside !== undefined) return BorderChange.inside(lineOf(inside));
    for (const [key] of given.properties) {
        if (!edges.has(key)) throw new FormatError(`'${key}' is no edge of a Border change.`);
    }
    const edge = (key: string): BorderLine | undefined => {
        const line = optional(given, key);
        return line === undefined ? undefined : lineOf(line);
    };
    return new BorderChange({
        top: edge('top'), bottom: edge('bottom'), left: edge('left'), right: edge('right'),
        insideHorizontal: edge('insideHorizontal'), insideVertical: edge('insideVertical'),
    });
}

// A block copied inside ExSheet: the range it came from, and each cell as copied, rows of columns, each part of its Cell
// Format written only where the cell records it (ADR-0048, ADR-0071).
function blockOf(element: JsonElement): SheetBlock {
    const block = object(element, 'a block');
    const source = rangeOf(member(block, 'source'));
    const rows = items(member(block, 'cells'), 'the cells');
    if (rows.length !== source.rowCount) throw new FormatError(`A block copied from ${source.toString()} holds ${String(source.rowCount)} row(s), not ${String(rows.length)}.`);
    const cells = rows.flatMap(row => {
        const columns = items(row, 'a row of the block');
        if (columns.length !== source.columnCount) throw new FormatError(`A row of a block copied from ${source.toString()} holds ${String(source.columnCount)} cell(s), not ${String(columns.length)}.`);
        return columns.map(stateOf);
    });
    return new SheetBlock(source, cells);
}

function stateOf(element: JsonElement): CellState {
    const cell = object(element, 'a cell of a block');
    for (const [key] of cell.properties) {
        if (!['entry', 'numberFormat', 'alignment', 'font', 'fill', 'borders'].includes(key)) throw new FormatError(`'${key}' is no part of a copied cell.`);
    }
    const entry = optional(cell, 'entry');
    const numberFormat = optional(cell, 'numberFormat');
    const alignment = optional(cell, 'alignment');
    const font = optional(cell, 'font');
    const fill = optional(cell, 'fill');
    const borders = optional(cell, 'borders');
    return new CellState(
        entry === undefined ? undefined : entryOf(entry),
        undefined,
        numberFormat === undefined ? undefined : NumberFormat.parse(text(numberFormat, 'numberFormat')),
        alignment === undefined ? undefined : oneOf(alignments, text(alignment, 'alignment'), 'an alignment'),
        font === undefined ? undefined : fontOf(font),
        fill === undefined ? undefined : fillOf(fill),
        borders === undefined ? undefined : bordersOf(borders));
}

function fontOf(element: JsonElement): CellFont {
    const font = object(element, 'a Font');
    return new CellFont({
        colour: colourOf(member(font, 'colour')),
        bold: flag(member(font, 'bold'), 'bold'),
        italic: flag(member(font, 'italic'), 'italic'),
        underline: flag(member(font, 'underline'), 'underline'),
        strikethrough: flag(member(font, 'strikethrough'), 'strikethrough'),
    });
}

function bordersOf(element: JsonElement): CellBorders {
    const sides = object(element, 'the Borders');
    return new CellBorders({
        top: lineOf(member(sides, 'top')),
        bottom: lineOf(member(sides, 'bottom')),
        left: lineOf(member(sides, 'left')),
        right: lineOf(member(sides, 'right')),
    });
}

function lineOf(element: JsonElement): BorderLine {
    if (element.kind === 'string' && element.value === 'none') return BorderLine.none;
    const line = object(element, 'a line');
    const style: BorderLineStyle = oneOf(borderLineStyles, text(member(line, 'style'), 'style'), 'a line style');
    const colour = optional(line, 'colour');
    return new BorderLine(style, colour === undefined ? CellColour.automatic : colourOf(colour));
}

function colourOf(element: JsonElement): CellColour {
    const written = text(element, 'a colour');
    if (written === 'Automatic') return CellColour.automatic;
    const colour = CellColour.fromHex(written);
    if (colour === undefined) throw new FormatError(`'${written}' is no colour: Automatic, or #RRGGBB.`);
    return colour;
}

function fillOf(element: JsonElement): CellFill {
    return element.kind === 'string' && element.value === 'none' ? CellFill.none : CellFill.solid(colourOf(element));
}

function typedOf(element: JsonElement): TypedCell[] {
    // Each cell in its place in the list, as .NET wrote it: a cell named twice is typed twice, in order.
    return object(element, 'typed cells').properties.map(([at, typed]) => [CellAddress.parse(at), text(typed, at)] as const);
}

function entriesOf(element: JsonElement): EntryOfCell[] {
    return object(element, 'Entries').properties.map(([at, entry]) => [CellAddress.parse(at), entry.kind === 'null' ? undefined : entryOf(entry)] as const);
}

function entryOf(element: JsonElement): Entry {
    const entry = object(element, 'an Entry');
    const formula = optional(entry, 'formula');
    if (formula !== undefined && entry.properties.length === 1) return Entry.fromFormula(text(formula, 'formula'));
    const constant = optional(entry, 'constant');
    if (constant !== undefined && entry.properties.length === 1) {
        const value = valueOf(constant);
        if (value === undefined) throw new FormatError('A constant is not blank.');
        return Entry.fromValue(value);
    }
    throw new FormatError(`${entry.raw} is no Entry: {"formula": text} or {"constant": value}.`);
}

function valueOf(element: JsonElement): Value | undefined {
    switch (element.kind) {
        case 'null':
            return undefined;
        case 'string':
            return Value.fromText(element.value);
        case 'true':
            return Value.fromBoolean(true);
        case 'false':
            return Value.fromBoolean(false);
        case 'number':
            return Value.fromNumber(number(element, 'a number'));
        case 'object': {
            const written = optional(element, 'error');
            const refused = optional(element, 'refused');
            const code = written === undefined ? undefined : text(written, 'an Error Value');
            if (code !== undefined && refused !== undefined && element.properties.length === 2 && refused.kind === 'true') {
                // A refusal, as a Consumer holds one read from a refused cell: the engine refuses it where it is no data.
                if (code === '#VALUE!') return Value.refusal;
                if (code === '#NAME?') return Value.refusedName;
            }
            const error = code === '#CIRC!' || code === '#GETTING_DATA' ? code : code === undefined ? undefined : parseWrittenError(code);
            if (error !== undefined && element.properties.length === 1) return Value.fromError(error);
            throw new FormatError(`${element.raw} is no Value.`);
        }
        case 'array':
            throw new FormatError(`${element.raw} is no Value.`);
        default:
            throw new FormatError('Internal: no kind of JSON.');
    }
}

// A Value of a snapshot: a refusal a Consumer read from a refused cell crosses as one.
function rowValueJson(value: Value): unknown {
    return value.isRefused ? { error: value.error, refused: true } : valueJson(value);
}

function valueJson(value: Value): unknown {
    switch (value.kind) {
        case 'number':
            return value.number;
        case 'text':
            return value.text;
        case 'boolean':
            return value.boolean;
        case 'error':
            return { error: value.error };
        default:
            throw new Error('Internal: no kind of Value.');
    }
}

function changeJson(change: SheetChange): unknown {
    return {
        valueChanges: change.valueChanges.map(a => a.toString()),
        recalculated: change.recalculated.map(a => a.toString()),
        rows: change.rows,
        columns: change.columns,
        reformatsUnnamedRows: change.reformatsUnnamedRows,
    };
}

function fontJson(font: CellFont): unknown {
    return { colour: font.colour.toString(), bold: font.bold, italic: font.italic, underline: font.underline, strikethrough: font.strikethrough };
}

function fillText(fill: CellFill): string {
    return fill.colour === undefined ? 'none' : fill.colour.toString();
}

function lineJson(line: BorderLine): unknown {
    return line.isNone ? 'none' : { style: line.style, colour: line.colour.toString() };
}

function bordersJson(borders: CellBorders): unknown {
    return { top: lineJson(borders.top), bottom: lineJson(borders.bottom), left: lineJson(borders.left), right: lineJson(borders.right) };
}

function placeAndCount(element: JsonElement): [number, number] {
    const pair = items(element, 'a place and count');
    const [place, count] = pair;
    if (place === undefined || pair.length > 2) throw new FormatError(`${raw(element)} is no place and count.`);
    return [whole(place, 'a place'), count === undefined ? 1 : whole(count, 'a count')];
}

function raw(element: JsonElement): string {
    return element.kind === 'object' || element.kind === 'array' ? element.raw : JSON.stringify(element);
}

function oneOf<T extends string>(names: readonly T[], written: string, what: string): T {
    const found = names.find(name => name === written);
    if (found === undefined) throw new RangeError(`'${written}' is not ${what}.`);
    return found;
}

function addressOf(element: JsonElement): CellAddress {
    return CellAddress.parse(text(element, 'a cell'));
}

function rangeOf(element: JsonElement): CellRange {
    return CellRange.parse(text(element, 'a range'));
}

function object(element: JsonElement, what: string): JsonObject {
    if (element.kind !== 'object') throw new FormatError(`${what} is not an object.`);
    return element;
}

// A named argument, refused when it is written twice: one of the two would be ignored.
function optional(element: JsonObject, name: string): JsonElement | undefined {
    const found = element.properties.filter(([key]) => key === name);
    if (found.length > 1) throw new FormatError(`'${name}' is written twice in ${element.raw}.`);
    return found[0]?.[1];
}

function member(element: JsonObject, name: string): JsonElement {
    const found = optional(element, name);
    if (found === undefined) throw new FormatError(`'${name}' is missing from ${element.raw}.`);
    return found;
}

function items(element: JsonElement, what: string): readonly JsonElement[] {
    if (element.kind !== 'array') throw new FormatError(`${what} is not a list.`);
    return element.items;
}

function text(element: JsonElement, what: string): string {
    if (element.kind !== 'string') throw new FormatError(`${what} is not text.`);
    return element.value;
}

function flag(element: JsonElement, what: string): boolean {
    if (element.kind === 'true') return true;
    if (element.kind === 'false') return false;
    throw new FormatError(`${what} is not true or false.`);
}

function number(element: JsonElement, what: string): number {
    if (element.kind !== 'number') throw new FormatError(`${what} is not a number.`);
    const read = Number(element.raw);
    // A number JSON writes is finite; one written past a double's range is no Value .NET sent.
    if (!Number.isFinite(read)) throw new FormatError(`${element.raw} is no number a double holds.`);
    return read;
}

function whole(element: JsonElement, what: string): number {
    const read = number(element, what);
    if (!Number.isSafeInteger(read)) throw new FormatError(`${what} is not a whole number.`);
    return read;
}
