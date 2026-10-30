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

/** A member of a vocabulary: its place in the table, and the class it adds. */
type Member = readonly [place: number, adds: string];

// Each vocabulary's members. The first of each adds nothing, so an ordinary cell is painted exactly as it was
// before the vocabularies existed (ADR-0006/0016/0068).
const states: Readonly<Record<CellState, Member>> = {
    normal: [0, ''], stale: [1, ' ex-state-stale'], missing: [2, ' ex-state-missing'], error: [3, ' ex-state-error'],
    modified: [4, ' ex-state-modified'],
};
const tones: Readonly<Record<CellTone, Member>> = { none: [0, ''], positive: [1, ' ex-tone-positive'], negative: [2, ' ex-tone-negative'] };
const aligns: Readonly<Record<CellAlign, Member>> = {
    auto: [0, ''], left: [1, ' ex-align-left'], center: [2, ' ex-align-center'], right: [3, ' ex-align-right'],
};
const marks: Readonly<Record<MatchMark, Member>> = {
    none: [0, ''], match: [1, ' ex-match-mark'], current: [2, ' ex-match-mark ex-match-mark-current'],
};
const stateCount = 5;
const toneCount = 3;
const alignCount = 4;

/** A member of its vocabulary. One nobody declared, which only an untyped caller can pass, is refused rather
 * than painted as the ordinary cell: it would hide a Consumer's mapping defect behind a cell that looks
 * perfectly ordinary, the one outcome ADR-0006 exists to prevent. */
function memberOf<T extends string>(vocabulary: Readonly<Record<T, Member>>, value: T, name: string): Member {
    if (typeof value !== 'string' || !Object.hasOwn(vocabulary, value)) {
        throw new RangeError(`${name} is one of ${Object.keys(vocabulary).join(', ')}, not ${JSON.stringify(value)}.`);
    }
    return vocabulary[value];
}

function indexOf(mark: number, changed: boolean, tone: number, state: number, align: number, numeric: boolean, pinned: boolean): number {
    const combination = (((mark * 2 + (changed ? 1 : 0)) * toneCount + tone) * stateCount + state) * alignCount + align;
    return combination * 4 + (numeric ? 2 : 0) + (pinned ? 1 : 0);
}

let composed: readonly string[] | undefined;

/** Every combination, composed on first asking: nothing runs at module load (`sideEffects: false`). */
function table(): readonly string[] {
    if (composed !== undefined) return composed;
    const all: string[] = [];
    for (const [m, mark] of Object.values(marks)) {
        for (const changed of [false, true]) {
            for (const [t, tone] of Object.values(tones)) {
                for (const [s, state] of Object.values(states)) {
                    for (const [a, align] of Object.values(aligns)) {
                        for (const numeric of [false, true]) {
                            for (const pinned of [false, true]) {
                                all[indexOf(m, changed, t, s, a, numeric, pinned)] = 'ex-cell'
                                    + (numeric ? ' ex-cell-numeric' : '')
                                    + (pinned ? ' ex-pinned' : '')
                                    + align + tone + state
                                    + (changed ? ' ex-changed' : '')
                                    + mark;
                            }
                        }
                    }
                }
            }
        }
    }
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
        memberOf(marks, mark, 'mark')[0], changed, memberOf(tones, tone, 'tone')[0], memberOf(states, state, 'state')[0],
        memberOf(aligns, align, 'align')[0], numeric, pinned);
    return elementAt(table(), index);
}
