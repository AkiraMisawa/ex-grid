// What every part of a Board's reader asks of an object alike, in the words C#'s BoardSpec uses.

import { BoardRefusedError, jsonMember, jsonMemberNames, type JsonObject } from '@ex-grid/data';

/** Refuses a member of `element` that is not one of `keys`, naming it under `path`. */
export function members(element: JsonObject, keys: readonly string[], path: string, what: string): void {
    for (const name of jsonMemberNames(element)) {
        if (!keys.includes(name)) throw new BoardRefusedError(path === '' ? name : `${path}.${name}`, `${what} has no member '${name}'.`);
    }
}

/** The text of an optional member, empty where it is not written; anything but text is refused. */
export function optionalText(element: JsonObject, name: string, path: string, what: string): string {
    const value = jsonMember(element, name);
    if (value === undefined) return '';
    if (typeof value !== 'string') throw new BoardRefusedError(path, `${what} is text.`);
    return value;
}
