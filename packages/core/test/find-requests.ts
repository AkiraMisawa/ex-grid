// The Find and Replace requests a case writes, read for the "find", "replace" and "source" runners.
import type { GridFindRequest } from '../src/finding/find';
import type { GridReplaceRequest } from '../src/finding/replace';
import { readCell, readRanges, type CellText, type RangeText } from './cases';

export interface WrittenFind {
    readonly text: string;
    readonly from?: CellText;
    readonly backward?: boolean;
    readonly matchCase?: boolean;
    readonly wholeCell?: boolean;
    readonly scope?: readonly RangeText[];
    readonly columns?: readonly string[];
}

export interface WrittenReplace {
    readonly text: string;
    readonly replacement: string;
    readonly matchCase?: boolean;
    readonly wholeCell?: boolean;
    readonly scope?: readonly RangeText[];
    readonly columns?: readonly string[];
    readonly editable?: readonly string[];
    readonly version?: number;
}

/** A request as a case writes it: a key left out is off, the file's columns, or none. */
export function findRequestOf(written: WrittenFind, columns: readonly string[]): GridFindRequest {
    return {
        text: written.text,
        backward: flag(written.backward, 'backward'),
        matchCase: flag(written.matchCase, 'matchCase'),
        wholeCell: flag(written.wholeCell, 'wholeCell'),
        from: written.from === undefined ? undefined : readCell(written.from),
        scope: written.scope === undefined ? undefined : readRanges(written.scope),
        columns: written.columns === undefined ? columns : written.columns,
        rowSequenceVersion: 0,
    };
}

/** A Replace All as a case writes it: a key left out is off, the file's columns, every one of them
 * Editable, none, or `version` as given. */
export function replaceRequestOf(written: WrittenReplace, columns: readonly string[], version: number): GridReplaceRequest {
    return {
        text: written.text,
        replacement: written.replacement,
        matchCase: flag(written.matchCase, 'matchCase'),
        wholeCell: flag(written.wholeCell, 'wholeCell'),
        scope: written.scope === undefined ? undefined : readRanges(written.scope),
        columns: written.columns === undefined ? columns : written.columns,
        editableColumns: written.editable === undefined ? columns : written.editable,
        rowSequenceVersion: written.version === undefined ? version : written.version,
    };
}

/** A flag a case may leave out, which is then off. Anything but true or false is a defect in the case,
 * refused as C#'s GetBoolean refuses it, never read as off. */
function flag(value: unknown, key: string): boolean {
    if (value === undefined) return false;
    if (typeof value !== 'boolean') throw new Error(`A case's "${key}" is true or false, not ${JSON.stringify(value)}.`);
    return value;
}

/** What a case takes for a number: the same pattern both runners read. */
export const numberText = /^-?[0-9]+(\.[0-9]+)?$/u;
