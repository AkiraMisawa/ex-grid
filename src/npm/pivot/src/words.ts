// The words ExPivot's screen paints, by id (ADR-0370 section 10): the Consumer's label function first, then
// the engine's English, and an id neither knows painted as itself, a missing word seen, never a blank. The
// tables are the engine's, so a report's own words and the Field List's come from one place.

import { fillPivotWord, pivotWord, type PivotArea, type PivotLabel } from '@ex-grid/pivot-engine';
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
