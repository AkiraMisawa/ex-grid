// The keys of a Toolbar Item's list drawn by a Chrome (ADR-0100, ADR-0390), the built-in list's: the arrows, Home and
// End move among the choices that can be made, swatches ten across, and the list opens on the choice the Focus cell has.
// Every Wrapper's list moves alike: React's hook and the Blazor adapter's wiring read them from here.

import type { SheetToolbarItemChoice } from './sheet-toolbar-slot';

/** Swatches to a row, as Excel's palette lays them out and as the built-in list moves among them. */
export const toolbarSwatchesAcross = 10;

/** The index the list's `key` moves the keyboard to from the choice at `at` (-1 for none), or `undefined` where it
 * stays. */
export function toolbarChoiceMovedTo(key: string, at: number, choices: readonly SheetToolbarItemChoice[]): number | undefined {
    const can = (index: number): boolean => choices[index] !== undefined && choices[index].disabledReason === undefined;
    const enabled = choices.map((_, index) => index).filter(can);
    const first = enabled[0];
    const last = enabled.at(-1);
    if (first === undefined || last === undefined) return undefined;
    const swatch = choices[at]?.swatch !== undefined;
    const step = (by: number): number | undefined => {
        for (let next = at + by; next >= 0 && next < choices.length; next += by) if (can(next)) return next;
        return undefined;
    };
    switch (key) {
        case 'Home': return first;
        case 'End': return last;
        case 'ArrowDown': return step(swatch ? toolbarSwatchesAcross : 1) ?? step(1);
        case 'ArrowUp': return step(swatch ? -toolbarSwatchesAcross : -1) ?? step(-1);
        case 'ArrowRight': return swatch ? step(1) : undefined;
        case 'ArrowLeft': return swatch ? step(-1) : undefined;
        default: return undefined;
    }
}

/** The choice the list's keyboard starts on: the one the Focus cell has, else the first that can be made. */
export function toolbarChoiceStart(choices: readonly SheetToolbarItemChoice[]): number | undefined {
    const selected = choices.findIndex(choice => choice.selected && choice.disabledReason === undefined);
    if (selected >= 0) return selected;
    const first = choices.findIndex(choice => choice.disabledReason === undefined);
    return first >= 0 ? first : undefined;
}

/** One row of a list, in the Sheet's order: the index of a choice drawn on a row of its own, or the indices of the
 * swatches drawn together, ten across, as the list's keys move among them. */
export type ToolbarChoiceRow = number | readonly number[];

/** The list's rows: a run of swatches is cut ten across, and every other choice stands on a row of its own. */
export function toolbarChoiceRows(choices: readonly SheetToolbarItemChoice[]): readonly ToolbarChoiceRow[] {
    const rows: ToolbarChoiceRow[] = [];
    let swatches: number[] = [];
    choices.forEach((choice, index) => {
        if (choice.swatch !== undefined) {
            swatches.push(index);
            if (swatches.length === toolbarSwatchesAcross) {
                rows.push(swatches);
                swatches = [];
            }
            return;
        }
        if (swatches.length > 0) rows.push(swatches);
        swatches = [];
        rows.push(index);
    });
    if (swatches.length > 0) rows.push(swatches);
    return rows;
}
