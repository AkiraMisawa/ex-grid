// What is typed in Chart Filters (ADR-0273, ADR-0281), held for a design system's inputs: the range's ends as text,
// replaced by the chart's whenever the chart's range changes (Chart Filters opened, a drag, Reset range) and held
// across every other context the chart hands, so a pointer moving over the plot never takes away what is typed.

import { useState } from 'react';
import type { ChartFiltersSlotContext } from '@ex-grid/chart';

/** Chart Filters' typed range, from {@link useChartFilterText}. */
export interface ChartFilterText {
    /** The start as typed. */
    readonly from: string;
    /** The end as typed. */
    readonly to: string;
    /** Takes what is typed as the start. */
    readonly setFrom: (text: string) => void;
    /** Takes what is typed as the end. */
    readonly setTo: (text: string) => void;
    /** Hands the chart the range as typed; the chart says what it could not read. */
    readonly apply: () => void;
}

interface Typed {
    readonly held: readonly [string | null, string | null];
    readonly from: string;
    readonly to: string;
}

// An end the chart holds none of is an empty field, which `apply` reads back as the axis' own end.
const fieldOf = (end: string | null): string => end ?? ''; // oxlint-disable-line ex-grid/no-stand-in-default -- an empty field is the axis' own end, as the chart reads it back

const typedOf = (filters: ChartFiltersSlotContext): Typed => ({ held: [filters.from, filters.to], from: fieldOf(filters.from), to: fieldOf(filters.to) });

/** Holds what is typed in Chart Filters for the inputs a Chrome draws them with. */
export function useChartFilterText(filters: ChartFiltersSlotContext): ChartFilterText {
    const [typed, setTyped] = useState(() => typedOf(filters));
    let shown = typed;
    if (typed.held[0] !== filters.from || typed.held[1] !== filters.to) {
        shown = typedOf(filters);
        setTyped(shown);
    }
    const { from, to } = shown;
    return {
        from, to,
        setFrom: text => { setTyped(t => ({ ...t, from: text })); },
        setTo: text => { setTyped(t => ({ ...t, to: text })); },
        apply: () => { filters.apply(from, to); },
    };
}
