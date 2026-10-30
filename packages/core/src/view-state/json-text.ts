// JSON written as the C# core's System.Text.Json writes it under its default encoder, so that a document both
// cores write is one text, byte for byte (ADR-0207, ADR-0370 section 5). Strings are written by @ex-grid/data's
// writer, the one a Board Diff is written with.

import { jsonStringText } from '@ex-grid/data';

/** A string as Utf8JsonWriter writes it under the default encoder: @ex-grid/data's writer, which a Board Diff around
 * a View State writes with too. */
export const jsonString = jsonStringText;

/** An object written from members already written, in order, as `{"name":value,…}`. */
export function jsonObject(members: readonly (readonly [string, string])[]): string {
    return `{${members.map(([name, value]) => `${jsonString(name)}:${value}`).join(',')}}`;
}

/** An array written from entries already written. */
export function jsonArray(entries: readonly string[]): string {
    return `[${entries.join(',')}]`;
}
