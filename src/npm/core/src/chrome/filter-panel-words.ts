// The words of a column's filter panel (ADR-0009, ADR-0044), by id (ADR-0370 section 10): its buttons, the value
// list's, the operators' names and why a typed value does not read. The built-in panel resolves them as the menus
// resolve theirs, through the grid's `commandLabel`, falling back to the built-in English, and a Chrome slot is
// handed the same resolution. A word with `{name}` is a template, its parts put in by the panel.

import type { FilterOperator } from '../query/filter';

/** The filter panel's label ids. */
export const filterPanelLabelIds = Object.freeze({
    /** The button that applies the panel. */
    ok: 'filter-ok',
    /** The button that closes it, applying nothing. */
    cancel: 'filter-cancel',
    /** The value list's search box, named and shown as its placeholder. */
    search: 'filter-search',
    /** Excel's option that adds what a search shows to the values already filtered by. */
    addToCurrent: 'filter-add-to-current',
    /** The box over every value the list shows. */
    selectAll: 'filter-select-all',
    /** The same box while a search stands. */
    selectAllSearchResults: 'filter-select-all-search-results',
    /** The Blank, as the value list shows it. */
    blanks: 'filter-blanks',
    /** Two conditions joined so both must hold. */
    and: 'filter-and',
    /** Two conditions joined so either may. */
    or: 'filter-or',
    /** The second condition's operator, unset. */
    none: 'filter-none',
    /** A typed date that reads as two days: `{text}` typed, `{first}` and `{second}` the two. */
    readsTwoDates: 'filter-reads-two-dates',
    /** A typed number that reads two ways: `{text}` typed, `{first}` as the column's culture reads it, `{second}` as
     * the invariant culture does, and `{other}` the second in the column's culture's form. */
    readsTwoNumbers: 'filter-reads-two-numbers',
    /** A typed value that is not one of the column's type: `{text}` typed, `{culture}` the culture's name. */
    notReadable: 'filter-not-readable',
    /** A day typed with a time, for a column of days: `{text}` typed, `{day}` the day alone. */
    dayHasTime: 'filter-day-has-time',
    /** A day typed with an offset, for a column of days: `{text}` typed, `{day}` the day alone. */
    dayHasOffset: 'filter-day-has-offset',
    /** A date and time typed with an offset, for a column whose dates have none: `{text}` typed, `{date}` it
     * without the offset. */
    dateTimeHasOffset: 'filter-date-time-has-offset',
    /** A moment typed without an offset, for a column of instants: `{text}` typed, `{day}` and `{time}` it. */
    instantHasNoOffset: 'filter-instant-has-no-offset',
} as const);

/** The id of an operator's name, as the panel's operator lists show it. */
export function operatorLabelId(operator: FilterOperator): string {
    return operatorIdOf(operator);
}

// The one place the form of an operator's id is written: the English below is keyed by the record's own keys.
const operatorIdOf = (operator: string): string => `filter-operator-${operator}`;

const operatorWords: Readonly<Record<FilterOperator, string>> = Object.freeze({
    equals: 'Equals',
    notEquals: 'NotEquals',
    greaterThan: 'GreaterThan',
    greaterThanOrEqual: 'GreaterThanOrEqual',
    lessThan: 'LessThan',
    lessThanOrEqual: 'LessThanOrEqual',
    contains: 'Contains',
    doesNotContain: 'DoesNotContain',
    startsWith: 'StartsWith',
    endsWith: 'EndsWith',
    in: 'In',
    isBlank: 'IsBlank',
    isNotBlank: 'IsNotBlank',
});

/** The built-in English of the filter panel, by id. The operators keep the names the C# core's enum gives them,
 * which its built-in panel shows. */
export const filterPanelWords: ReadonlyMap<string, string> = new Map([
    [filterPanelLabelIds.ok, 'OK'],
    [filterPanelLabelIds.cancel, 'Cancel'],
    [filterPanelLabelIds.search, 'Search'],
    [filterPanelLabelIds.addToCurrent, 'Add current selection to filter'],
    [filterPanelLabelIds.selectAll, '(Select All)'],
    [filterPanelLabelIds.selectAllSearchResults, '(Select All Search Results)'],
    [filterPanelLabelIds.blanks, '(Blanks)'],
    [filterPanelLabelIds.and, 'And'],
    [filterPanelLabelIds.or, 'Or'],
    [filterPanelLabelIds.none, '(none)'],
    [filterPanelLabelIds.readsTwoDates, '“{text}” reads two ways: as {first}, and as {second}. Type the one you mean as {first} or {second}.'],
    [filterPanelLabelIds.readsTwoNumbers,
        '“{text}” reads two ways: as {first}, and as {second}. Type {first} without separators, or {other} for the other.'],
    [filterPanelLabelIds.notReadable, '“{text}” is not a value of this column as {culture} writes it.'],
    [filterPanelLabelIds.dayHasTime, '“{text}” has a time, and this column holds days. Type the day alone, as {day}.'],
    [filterPanelLabelIds.dayHasOffset, '“{text}” has an offset, and this column holds days. Type the day alone, as {day}.'],
    [filterPanelLabelIds.dateTimeHasOffset, '“{text}” has an offset, and this column\'s dates have none. Type it without one, as {date}.'],
    [filterPanelLabelIds.instantHasNoOffset,
        '“{text}” has no offset, and this column holds moments. Type it with one, as {day}T{time}+hh:mm, or with Z for UTC.'],
    ...Object.entries(operatorWords).map(([operator, words]) => [operatorIdOf(operator), words] as const),
]);

/** The built-in English for `id`, one of the panel's. */
export function builtInFilterWord(id: string): string {
    const words = filterPanelWords.get(id);
    if (words === undefined) throw new RangeError(`'${id}' is not one of the filter panel's word ids.`);
    return words;
}
