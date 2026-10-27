// A word with `{name}` is a template (ADR-0370 section 10): the Chrome puts its parts in, a count, a column's name, the
// text a user typed. The parts go in in one pass, so a part that itself holds braces is put in as it is, never read as
// another part's place.

/** `template` with each `{name}` that `parts` holds put in, in one pass, so a part that holds braces, typed text say,
 * is put in as it is. Braces naming no part stay as written. */
export function filledWords(template: string, parts: Readonly<Record<string, string>>): string {
    return template.replaceAll(/\{(\w+)\}/gu, (written: string, name: string) => (Object.hasOwn(parts, name) ? String(parts[name]) : written));
}
