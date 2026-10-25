import { elementAt } from '../must';

// The class vocabulary a cell is painted with, one string per combination composed once. The core decides
// the meanings (is this a numeric presentation, ADR-0016; is this cell Stale, ADR-0006); this answers how
// they are written for a browser. The class names are the seam a replacement Chrome (ADR-0010) and a theme
// paint against, and they are the same whichever Framework Adapter mounts the grid (ADR-0350). Composing
// two or three names per cell would make a string for each of the ~800 cells on every render, on the path
// ADR-0004 caps; a few hundred strings held for the life of the page are the cheaper end.

/**
 * The generic vocabulary for "this cell is in an unusual state" (ADR-0006), which cannot be derived from
 * the value and is supplied from outside: nothing unusual, Stale, Missing, in Error, or Modified and not
 * yet saved. `normal` paints exactly as a cell nobody said anything about.
 */
export type CellState = 'normal' | 'stale' | 'missing' | 'error' | 'modified';

/** The meaning a Column's rule attaches to a value, a gain or a loss, for the theme to paint (ADR-0006): a
 * tone names a meaning, never a colour. `none` paints as an ordinary cell. */
export type CellTone = 'none' | 'positive' | 'negative';

/** A cell's horizontal alignment (ADR-0016/0029). `auto` derives from the column type, Number and Date
 * right, Text and Boolean left; an explicit value beats the derivation. There is no vertical alignment. */
export type CellAlign = 'auto' | 'left' | 'center' | 'right';

/** Whether a painted cell wears the Match Mark (ADR-0332 §4): not at all, as a match of what the find
 * panel holds, or as the current match, the Focus a found step moved to. */
export type MatchMark = 'none' | 'match' | 'current';

/** What a cell's class attribute is composed from. */
export interface CellClassOptions {
    /** The core's classification of the presentation as numeric (`hashesWhenOverflowing`), not a
     * re-derivation of it. */
    readonly numeric: boolean;
    /** Whether the cell is in a pinned column. */
    readonly pinned: boolean;
    /** The cell's state (ADR-0006). */
    readonly state: CellState;
    /** The cell's alignment; `auto` unless given. */
    readonly align?: CellAlign;
    /** The cell's tone; `none` unless given. */
    readonly tone?: CellTone;
    /** The Change Highlight: the Consumer said the cell's shown value changed less than the highlight's
     * duration ago (ADR-0068). False unless given. */
    readonly changed?: boolean;
    /** The Match Mark; `none` unless given. */
    readonly mark?: MatchMark;
}

// Each vocabulary in the order of its places in the table, and the class each member adds. The first
// member of each adds nothing, so an ordinary cell is painted exactly as it was before the vocabularies
// existed (ADR-0006/0016/0068).
const states: readonly CellState[] = ['normal', 'stale', 'missing', 'error', 'modified'];
const tones: readonly CellTone[] = ['none', 'positive', 'negative'];
const aligns: readonly CellAlign[] = ['auto', 'left', 'center', 'right'];
const marks: readonly MatchMark[] = ['none', 'match', 'current'];
const stateClass: Readonly<Record<CellState, string>> = {
    normal: '', stale: ' ex-state-stale', missing: ' ex-state-missing', error: ' ex-state-error', modified: ' ex-state-modified',
};
const toneClass: Readonly<Record<CellTone, string>> = { none: '', positive: ' ex-tone-positive', negative: ' ex-tone-negative' };
const alignClass: Readonly<Record<CellAlign, string>> = {
    auto: '', left: ' ex-align-left', center: ' ex-align-center', right: ' ex-align-right',
};
const markClass: Readonly<Record<MatchMark, string>> = {
    none: '', match: ' ex-match-mark', current: ' ex-match-mark ex-match-mark-current',
};

/** A member's place in its vocabulary. A member nobody declared, which only an untyped caller can pass, is
 * refused rather than painted as the ordinary cell: it would hide a Consumer's mapping defect behind a cell
 * that looks perfectly ordinary, the one outcome ADR-0006 exists to prevent. */
function placeOf<T extends string>(members: readonly T[], value: T, name: string): number {
    const place = members.indexOf(value);
    if (place < 0) throw new RangeError(`${name} is one of ${members.join(', ')}, not ${value}.`);
    return place;
}

function indexOf(mark: number, changed: boolean, tone: number, state: number, align: number, numeric: boolean, pinned: boolean): number {
    const combination = (((mark * 2 + (changed ? 1 : 0)) * tones.length + tone) * states.length + state) * aligns.length + align;
    return combination * 4 + (numeric ? 2 : 0) + (pinned ? 1 : 0);
}

let composed: readonly string[] | undefined;

/** Every combination, composed on first asking: nothing runs at module load (`sideEffects: false`). */
function table(): readonly string[] {
    if (composed !== undefined) return composed;
    const all: string[] = [];
    marks.forEach((mark, m) => {
        for (const changed of [false, true]) {
            tones.forEach((tone, t) => {
                states.forEach((state, s) => {
                    aligns.forEach((align, a) => {
                        for (const numeric of [false, true]) {
                            for (const pinned of [false, true]) {
                                all[indexOf(m, changed, t, s, a, numeric, pinned)] = 'ex-cell'
                                    + (numeric ? ' ex-cell-numeric' : '')
                                    + (pinned ? ' ex-pinned' : '')
                                    + alignClass[align]
                                    + toneClass[tone]
                                    + stateClass[state]
                                    + (changed ? ' ex-changed' : '')
                                    + markClass[mark];
                            }
                        }
                    });
                });
            });
        }
    });
    composed = Object.freeze(all);
    return composed;
}

/**
 * The full class attribute for one cell. `normal`, `auto`, a tone of `none`, an unchanged cell and no mark
 * add nothing at all; `changed` adds `ex-changed` (ADR-0068), a match `ex-match-mark`, and the current
 * match `ex-match-mark-current` as well (ADR-0332 §4).
 */
export function cellClasses(options: CellClassOptions): string {
    const { numeric, pinned, state, align = 'auto', tone = 'none', changed = false, mark = 'none' } = options;
    const index = indexOf(
        placeOf(marks, mark, 'mark'), changed, placeOf(tones, tone, 'tone'), placeOf(states, state, 'state'),
        placeOf(aligns, align, 'align'), numeric, pinned);
    return elementAt(table(), index);
}
