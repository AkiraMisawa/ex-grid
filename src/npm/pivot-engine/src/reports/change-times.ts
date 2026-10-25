// When the Consumer first showed each change of the data a report source marks — the time a Change Highlight
// starts (ADR-0068) — stamped on the Consumer's own clock as it adopts a Window, never on the source's, which
// may run in a Worker or on a server whose clock need not agree. The C# engine's PivotChangeTimes.

import type { PivotDisplayRow, PivotReportState } from './contract';

/**
 * When this Consumer first showed each change a report source marks. A source marks each changed cell with
 * the Report Version whose data changed it ({@link PivotDisplayRow.changedIn}) and lists the changes it still
 * marks ({@link PivotReportMetadata.changeMarks}). A listed change is stamped the first time a Window of a
 * report listing it is adopted — whether or not that Window holds one of its cells — so a row first asked for
 * later, by a scroll, shows it as of that first time, and its highlight ends with the others of the change. A
 * change no longer listed is never stamped; a stamp is kept while the source lists its change or its highlight
 * has not ended, and once forgotten the change is never listed again, so none is stamped twice. One instance
 * serves a view for its lifetime, across new sources.
 */
export class PivotChangeTimes {
    readonly #stamps = new Map<string, number>();

    /** Takes a Window up as it is shown at `now`, on the Consumer's clock in milliseconds: each change its
     * report lists that is not stamped yet is stamped `now`, and each stamp whose change is no longer listed
     * and whose highlight of `highlightMs` has ended is forgotten. */
    adopt(state: PivotReportState, now: number, highlightMs: number): void {
        const listed = state.metadata.changeMarks;
        for (const [mark, at] of Array.from(this.#stamps)) {
            if (now - at >= highlightMs && !listed.includes(mark)) this.#stamps.delete(mark);
        }
        for (const mark of listed) {
            if (!this.#stamps.has(mark)) this.#stamps.set(mark, now);
        }
    }

    /** When the change `mark` names was first shown, or `undefined` for no mark or one not stamped. */
    at(mark: string | undefined): number | undefined {
        return mark === undefined ? undefined : this.#stamps.get(mark);
    }

    /** When the shown text of a row's value cell last changed with the data, as this Consumer first showed
     * it; `undefined` when it did not change recently. */
    changedAt(row: PivotDisplayRow, valueColumn: number): number | undefined {
        return valueColumn >= 0 && valueColumn < row.changedIn.length ? this.at(row.changedIn[valueColumn]) : undefined;
    }
}
