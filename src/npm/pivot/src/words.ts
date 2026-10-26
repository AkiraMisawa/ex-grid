// The words ExPivot's screen paints, by id (ADR-0370 section 10): the Consumer's label function first, then
// the engine's English, and an id neither knows painted as itself, a missing word seen, never a blank. The
// tables are the engine's, so a report's own words and the Field List's come from one place.

import { fillPivotWord, pivotWord, type PivotArea, type PivotLabel, type PivotLayoutChoice, type PivotSourceRefusal } from '@ex-grid/pivot-engine';
import { unreachable } from './must';

/** The words of one pivot, under the label function it was given. */
export class PivotScreenWords {
    readonly #label: PivotLabel | undefined;

    constructor(label: PivotLabel | undefined) {
        this.#label = label;
    }

    /** The label function the words were made with: the one the report is laid out with too. */
    get label(): PivotLabel | undefined {
        return this.#label;
    }

    /** The word for `id`. */
    word(id: string): string {
        return pivotWord(id, this.#label);
    }

    /** The template word for `id`, filled as .NET fills one. */
    fill(id: string, ...args: readonly string[]): string {
        return fillPivotWord(this.word(id), ...args);
    }

    /** What a source that threw says where the user sees it. */
    failed(error: unknown): string {
        return this.fill('source-failed', messageOf(error));
    }

    /** What a Stale Report says of a source that threw while asked for newer data (ADR-0067). */
    staleFailed(error: unknown): string {
        return this.fill('stale-source-failed', messageOf(error));
    }

    /** What a Pivot Source's refusal says where the user sees it: data that moved on under the report asks for a
     * refresh (ADR-0066), and any other refusal is said in the source's words. */
    sourceRefused(refusal: PivotSourceRefusal): string {
        return refusal.kind === 'sourceVersionNotHeld' ? this.word('data-changed') : this.fill('source-refused', refusal.message);
    }

    /** An Area's title: `Filters`, `Columns`, `Rows`, `Values`. */
    area(area: PivotArea): string {
        return this.word(areaWordId(area));
    }
}

/** The id of an Area's title. */
export function areaWordId(area: PivotArea): string {
    switch (area) {
        case 'filters': return 'area-filters';
        case 'columns': return 'area-columns';
        case 'rows': return 'area-rows';
        case 'values': return 'area-values';
        default: return unreachable(area);
    }
}

/** The id of a Layout menu choice's command, which is also the id of its word, as the Razor component's
 * `PivotCommandIds.LayoutChoice` names it (ADR-0061). */
export function layoutChoiceId(choice: PivotLayoutChoice): string {
    switch (choice) {
        case 'doNotShowSubtotals': return 'subtotals-do-not-show';
        case 'showSubtotalsAtBottom': return 'subtotals-at-bottom';
        case 'showSubtotalsAtTop': return 'subtotals-at-top';
        case 'grandTotalsOff': return 'grand-totals-off';
        case 'grandTotalsOn': return 'grand-totals-on';
        case 'grandTotalsOnRowsOnly': return 'grand-totals-rows-only';
        case 'grandTotalsOnColumnsOnly': return 'grand-totals-columns-only';
        case 'compactForm': return 'form-compact';
        case 'outlineForm': return 'form-outline';
        case 'tabularForm': return 'form-tabular';
        case 'repeatItemLabels': return 'repeat-item-labels';
        case 'doNotRepeatItemLabels': return 'do-not-repeat-item-labels';
        default: return unreachable(choice);
    }
}

// What a thrown value says: an Error's message, and anything else as it prints.
function messageOf(error: unknown): string {
    return error instanceof Error ? error.message : String(error);
}
