import { elementAt } from '../must';
import type { CellState } from '../paint/cell-classes';

/**
 * The mark a collapsed Header Group wears when a column it hides holds a Cell State (ADR-0400 section 6): painted as
 * a cell in that state is, with the Consumer's reason as its accessible description and its tooltip. It covers the
 * group, not a row; the Consumer decides it, since the grid does not look into a column it was not given.
 */
export interface HiddenMark {
    /** The Cell State the hidden columns hold, such as `'modified'` for a value the user set. */
    readonly state: Exclude<CellState, 'normal'>;
    /** What the mark says, such as "2 user-set fields in hidden columns". Never empty: the mark says what it hides. */
    readonly reason: string;
}

/**
 * What a press on a Header Group's toggle, or Alt+Shift+↓ or ↑ from one of its members, notifies (ADR-0400 section 5):
 * the group, and the opposite of the state it showed when the input was taken in its turn.
 */
export interface HeaderGroupToggle {
    /** The group's `id`. */
    readonly id: string;
    /** The state the input asks for: true to expand, false to collapse. */
    readonly expanded: boolean;
    /** The Focus's column when the input was taken, by name, if there was a Focus. */
    readonly focusColumn?: string;
}

const markedStates: ReadonlySet<unknown> = new Set<Exclude<CellState, 'normal'>>(['stale', 'missing', 'error', 'modified']);

function isMarkedState(state: unknown): state is HiddenMark['state'] {
    return markedStates.has(state);
}

/**
 * A labelled rectangle over adjacent leaf columns in the tiers above the header row, declared by member
 * column names (ADR-0032): colspan and rowspan without a column tree. Membership is a set: the painted
 * left-to-right order is the flat column order's, never this declaration's.
 *
 * Names, not indices, deliberately: an index declaration survives a reorder formally valid while labelling
 * the wrong columns; names turn the same mistake into a refusal the grid can make (members no longer
 * adjacent), and they record the intent no reorder changes.
 */
export class HeaderGroup {
    /** The caption; empty for a spacer rectangle. */
    readonly label: string;
    /** The member column names, as a set: the painted order is the flat order's, never this list's. */
    readonly columns: readonly string[];
    /** The rectangle's top tier, 1 sitting directly above the leaf row. */
    readonly tier: number;
    /** How many tiers the rectangle spans downward from {@link HeaderGroup.tier}: the rowspan. */
    readonly tierSpan: number;
    /** What names the group in the toggle's notification (ADR-0400); none unless given. */
    readonly id: string | undefined;
    /** Whether the group shows open: given, the group draws its toggle; none, it draws none (ADR-0400). */
    readonly expanded: boolean | undefined;
    /** The Hidden Mark a collapsed group wears (ADR-0400 section 6); none unless given. */
    readonly hiddenMark: HiddenMark | undefined;

    /**
     * Declares one rectangle. Refused here: no member, an empty or repeated member name, and a span reaching
     * below the leaf row. Adjacency is refused at resolution, where the order is known (ADR-0032). And, by
     * ADR-0400 section 2: `expanded` without `id`, `expanded` on a spacer, a `hiddenMark` on a group not
     * collapsed, and a `hiddenMark` with no `reason`.
     */
    constructor(options: {
        readonly label: string; readonly columns: readonly string[]; readonly tier?: number; readonly tierSpan?: number;
        readonly id?: string; readonly expanded?: boolean; readonly hiddenMark?: HiddenMark;
    }) {
        const { label, columns, tier = 1, tierSpan = 1 } = options;
        if (typeof label !== 'string') {
            throw new TypeError(`A Header Group's label is text, empty for a spacer. (label: ${String(label)})`);
        }
        const given: unknown = columns;
        if (!Array.isArray(given)) {
            throw new TypeError(`Header Group '${label}' names its member columns in a list.`);
        }
        if (columns.length === 0) {
            throw new TypeError(`Header Group '${label}' names no member columns.`);
        }
        if (columns.some(name => typeof name !== 'string' || name === '')) {
            throw new TypeError(`Header Group '${label}' names a missing or empty member column.`);
        }
        if (new Set(columns).size !== columns.length) {
            throw new TypeError(`Header Group '${label}' names a member column twice.`);
        }
        if (!Number.isSafeInteger(tier) || tier < 1) {
            throw new RangeError(`Header Group '${label}' stands on tier ${tier}; tier 1 is directly above the leaf row.`);
        }
        if (!Number.isSafeInteger(tierSpan) || tierSpan < 1) {
            throw new RangeError(`Header Group '${label}' spans ${tierSpan} tiers; it spans at least one.`);
        }
        if (tierSpan > tier) {
            throw new RangeError(`Header Group '${label}' spans ${tierSpan} tiers downward from tier ${tier}, past the leaf row.`);
        }
        const { id, expanded, hiddenMark }: { readonly id?: unknown; readonly expanded?: unknown; readonly hiddenMark?: unknown } = options;
        if (id !== undefined && (typeof id !== 'string' || id === '')) {
            throw new TypeError(`Header Group '${label}' is named by an id that is not text, or is empty (ADR-0400).`);
        }
        if (expanded !== undefined && typeof expanded !== 'boolean') {
            throw new TypeError(`Header Group '${label}' says whether it is expanded with true or false, not a ${typeof expanded} (ADR-0400).`);
        }
        if (expanded !== undefined && id === undefined) {
            throw new TypeError(`Header Group '${label}' is declared expanded or collapsed without an id: its toggle's notification would `
                + 'name no group (ADR-0400 section 2).');
        }
        if (expanded !== undefined && label === '') {
            throw new TypeError(`A spacer Header Group (empty label) over [${columns.join(', ')}] is declared expanded or collapsed: its `
                + 'toggle would name no group, and a spacer is no group a reader opens (ADR-0400 section 2).');
        }
        this.label = label;
        this.columns = Object.freeze([...columns]);
        this.tier = tier;
        this.tierSpan = tierSpan;
        this.id = id;
        this.expanded = expanded;
        this.hiddenMark = hiddenMark === undefined ? undefined : markOf(label, expanded, hiddenMark);
    }

    /**
     * What an input on this group's toggle notifies (ADR-0400 section 5): the opposite of the `expanded` it shows, and
     * the Focus's column when there was a Focus. Throws for a group that draws no toggle.
     */
    toggled(focusColumn?: string): HeaderGroupToggle {
        const { id, expanded } = this;
        if (id === undefined || expanded === undefined) {
            throw new Error(`Header Group '${this.label}' draws no toggle: it was not declared expanded or collapsed (ADR-0400).`);
        }
        return Object.freeze(focusColumn === undefined ? { id, expanded: !expanded } : { id, expanded: !expanded, focusColumn });
    }
}

// A Hidden Mark checked (ADR-0400 section 2): only on a group shown collapsed, in a Cell State, saying what it hides.
function markOf(label: string, expanded: boolean | undefined, given: unknown): HiddenMark {
    if (typeof given !== 'object' || given === null) {
        throw new TypeError(`Header Group '${label}' is given a Hidden Mark that is not one: { state, reason } (ADR-0400).`);
    }
    if (expanded !== false) {
        throw new TypeError(`Header Group '${label}' wears a Hidden Mark while it is not collapsed: an open group's cells show their own `
            + 'Cell State, so the mark is for a collapsed group alone (ADR-0400 section 2).');
    }
    const { state, reason }: { readonly state?: unknown; readonly reason?: unknown } = given;
    if (!isMarkedState(state)) {
        throw new TypeError(`Header Group '${label}' wears a Hidden Mark in ${typeof state === 'string' ? `'${state}'` : `a ${typeof state}`}, which is no Cell State a mark says: `
            + 'stale, missing, error or modified (ADR-0400).');
    }
    if (typeof reason !== 'string' || reason.trim() === '') {
        throw new TypeError(`Header Group '${label}' wears a Hidden Mark with no reason: the mark must say what it hides (ADR-0400 section 2).`);
    }
    return Object.freeze({ state, reason });
}

/** One declared rectangle resolved against the current column order (ADR-0032). */
export interface ResolvedHeaderGroup {
    /** The declaration, unchanged: membership is gesture-proof. */
    readonly group: HeaderGroup;
    /** The declaration's caption. */
    readonly label: string;
    /** The leftmost member's index in the flat order. */
    readonly firstColumn: number;
    /** The colspan. */
    readonly memberCount: number;
    /** The rectangle's top tier, 1 directly above the leaf row. */
    readonly topTier: number;
    /** The rowspan: the rectangle occupies tiers `bottomTier` to `topTier`. */
    readonly tierSpan: number;
    /** The lowest tier the rectangle occupies, `topTier - tierSpan + 1`. */
    readonly bottomTier: number;
    /** Whether the whole rectangle sits inside the pinned block; a straddle was refused at resolution. */
    readonly pinned: boolean;
}

/**
 * The Header Groups resolved against the current column order, once per push and off the render path
 * (ADR-0032). Every refusal is by name at resolution: an unknown member, members no longer adjacent,
 * overlapping rectangles, a rectangle straddling the pinned boundary. A dishonest header over money is the
 * failure this design refuses everywhere.
 */
export class HeaderGroupLayout {
    /** No tiers: the band is the one header row it always was. */
    static readonly empty: HeaderGroupLayout = new HeaderGroupLayout([], 0, []);

    /** In declaration order; the positions are the flat order's. */
    readonly groups: readonly ResolvedHeaderGroup[];
    /** How many tiers stand above the leaf row; the band is `(1 + tierCount) × headerHeight` tall. */
    readonly tierCount: number;
    readonly #leafTierSpans: readonly number[];

    private constructor(groups: readonly ResolvedHeaderGroup[], tierCount: number, leafTierSpans: readonly number[]) {
        this.groups = Object.freeze(groups);
        this.tierCount = tierCount;
        this.#leafTierSpans = Object.freeze(leafTierSpans);
    }

    /**
     * Resolves the declarations against the columns' names in the current flat order and the pinned count.
     * An unknown member, members no longer adjacent, a rectangle straddling the pinned boundary and two
     * overlapping rectangles each throw, naming the group (ADR-0032). No declarations resolve to
     * {@link HeaderGroupLayout.empty}.
     */
    static resolve(options: {
        readonly groups: readonly HeaderGroup[]; readonly columnNames: readonly string[]; readonly pinnedCount: number; readonly toggleHeard?: boolean;
    }): HeaderGroupLayout {
        const { groups, columnNames, pinnedCount, toggleHeard } = options;
        if (!Number.isSafeInteger(pinnedCount) || pinnedCount < 0) {
            throw new RangeError(`A pinned count is a whole number, not negative. (pinnedCount: ${pinnedCount})`);
        }
        if (groups.length === 0) {
            return HeaderGroupLayout.empty;
        }
        HeaderGroupLayout.refuseToggles(groups, toggleHeard === true);
        const indexOf = new Map(columnNames.map((name, i) => [name, i]));
        const resolved = groups.map(group => resolveOne(group, indexOf, pinnedCount));
        resolved.forEach((one, a) => {
            for (const two of resolved.slice(a + 1)) {
                const columnsMeet = one.firstColumn <= two.firstColumn + two.memberCount - 1
                    && two.firstColumn <= one.firstColumn + one.memberCount - 1;
                const tiersMeet = one.bottomTier <= two.topTier && two.bottomTier <= one.topTier;
                if (columnsMeet && tiersMeet) {
                    throw new Error(`Header Groups '${one.label}' and '${two.label}' overlap: their columns and tiers both `
                        + 'intersect, and one rectangle would paint over the other (ADR-0032).');
                }
            }
        });
        const tierCount = Math.max(...resolved.map(group => group.topTier));
        // The lowest covered tier per column decides how far its leaf header stretches: up to that
        // rectangle's underside, or the full band when nothing covers it.
        const leafSpans = columnNames.map((_, c) => {
            const covering = resolved.filter(group => c >= group.firstColumn && c < group.firstColumn + group.memberCount);
            return covering.length === 0 ? 1 + tierCount : Math.min(...covering.map(group => group.bottomTier));
        });
        return new HeaderGroupLayout(resolved, tierCount, leafSpans);
    }

    /**
     * The toggles' refusals that look across the declaration (ADR-0400 section 2): two groups under one `id`, and a
     * group declared expanded or collapsed on a grid nobody hears a toggle on, whose toggle would do nothing when
     * pressed. Throws a TypeError naming the group.
     */
    static refuseToggles(groups: readonly HeaderGroup[], toggleHeard: boolean): void {
        const named = new Map<string, HeaderGroup>();
        for (const group of groups) {
            if (group.expanded !== undefined && !toggleHeard) {
                throw new TypeError(`Header Group '${group.label}' is declared expanded or collapsed, but no onHeaderGroupToggled hears its `
                    + 'toggle: a toggle that does nothing when pressed is not drawn (ADR-0400 section 2).');
            }
            if (group.id === undefined) continue;
            const other = named.get(group.id);
            if (other !== undefined) {
                throw new TypeError(`Header Groups '${other.label}' and '${group.label}' are both named '${group.id}': a toggle's `
                    + 'notification would not say which (ADR-0400 section 2).');
            }
            named.set(group.id, group);
        }
    }

    /**
     * The group Alt+Shift+↓ (`expand`) or Alt+Shift+↑ acts on from a cell in `column` (ADR-0400 section 4): the
     * lowest-tier group over the column that shows collapsed, to expand, or expanded, to collapse; none where no
     * group over the column answers, and the key is then the page's.
     */
    toggleOver(column: number, expand: boolean): ResolvedHeaderGroup | undefined {
        if (!Number.isSafeInteger(column) || column < 0) {
            throw new RangeError(`A column is a whole number, not negative. (column: ${column})`);
        }
        let lowest: ResolvedHeaderGroup | undefined;
        for (const group of this.groups) {
            if (column < group.firstColumn || column >= group.firstColumn + group.memberCount) continue;
            if (group.group.expanded !== !expand) continue;
            if (lowest === undefined || group.bottomTier < lowest.bottomTier) lowest = group;
        }
        return lowest;
    }

    /**
     * Where the Focus goes back after a toggle (ADR-0011's section "A Header Group's toggle keeps the Focus"), in
     * this layout, which is the columns push that answered it: the column named `toggle.focusColumn` when it is
     * among `columnNames`, otherwise the toggled group's first member in the order as pushed. None when the push
     * does not show the group as the toggle asked, or the group is gone, and the Focus is then dropped as before.
     */
    focusColumnAfter(toggle: HeaderGroupToggle, columnNames: readonly string[]): number | undefined {
        const shown = this.groups.find(group => group.group.id === toggle.id);
        if (shown === undefined || shown.group.expanded !== toggle.expanded) return undefined;
        const focusColumn = toggle.focusColumn === undefined ? -1 : columnNames.indexOf(toggle.focusColumn);
        return focusColumn >= 0 ? focusColumn : shown.firstColumn;
    }

    /**
     * How many tiers tall one column's leaf header is: 1 under the lowest rectangle covering it, more where
     * tiers below that rectangle are uncovered, and the full band, `1 + tierCount`, for a column no tier
     * covers; 1 with nothing declared (ADR-0032).
     */
    leafTierSpanOf(column: number): number {
        if (!Number.isSafeInteger(column) || column < 0) {
            throw new RangeError(`A column is a whole number, not negative. (column: ${column})`);
        }
        return this.tierCount === 0 || column >= this.#leafTierSpans.length ? 1 : elementAt(this.#leafTierSpans, column);
    }

    /**
     * Whether `pinnedCount` pinned columns would leave every rectangle wholly on one side of the boundary:
     * the straddle {@link HeaderGroupLayout.resolve} refuses, asked before it would have to be refused, so
     * the column menu never offers a pin the grid would then reject (ADR-0032/0010).
     */
    allowsPinnedCount(pinnedCount: number): boolean {
        if (!Number.isSafeInteger(pinnedCount)) {
            throw new RangeError(`A pinned count is a whole number. (pinnedCount: ${pinnedCount})`);
        }
        return this.groups.every(group => !(group.firstColumn < pinnedCount && group.firstColumn + group.memberCount > pinnedCount));
    }
}

function resolveOne(group: HeaderGroup, indexOf: ReadonlyMap<string, number>, pinnedCount: number): ResolvedHeaderGroup {
    const positions = group.columns.map(member => {
        const index = indexOf.get(member);
        if (index === undefined) {
            throw new Error(`Header Group '${group.label}' names the member column '${member}', which no column carries (ADR-0032).`);
        }
        return index;
    });
    const first = Math.min(...positions);
    const last = Math.max(...positions);
    if (last - first + 1 !== group.columns.length) {
        throw new Error(`Header Group '${group.label}' members are not adjacent in the current column order: they span `
            + `positions ${first}-${last} with columns between them that are not members. A rectangle over non-adjacent `
            + 'columns would label strangers (ADR-0032).');
    }
    if (first < pinnedCount && last >= pinnedCount) {
        throw new Error(`Header Group '${group.label}' straddles the pinned boundary: members ${first}-${last} cross the `
            + `first ${pinnedCount} pinned columns. Half sticky, half scrolling cannot be drawn honestly (ADR-0032).`);
    }
    return Object.freeze({
        group,
        label: group.label,
        firstColumn: first,
        memberCount: group.columns.length,
        topTier: group.tier,
        tierSpan: group.tierSpan,
        bottomTier: group.tier - group.tierSpan + 1,
        pinned: last < pinnedCount,
    });
}
