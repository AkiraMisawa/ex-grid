import { elementAt } from '../must';

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

    /**
     * Declares one rectangle. Refused here: no member, an empty or repeated member name, and a span reaching
     * below the leaf row. Adjacency is refused at resolution, where the order is known (ADR-0032).
     */
    constructor(options: { readonly label: string; readonly columns: readonly string[]; readonly tier?: number; readonly tierSpan?: number }) {
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
        this.label = label;
        this.columns = Object.freeze([...columns]);
        this.tier = tier;
        this.tierSpan = tierSpan;
    }
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
    static resolve(options: { readonly groups: readonly HeaderGroup[]; readonly columnNames: readonly string[]; readonly pinnedCount: number }): HeaderGroupLayout {
        const { groups, columnNames, pinnedCount } = options;
        if (!Number.isSafeInteger(pinnedCount) || pinnedCount < 0) {
            throw new RangeError(`A pinned count is a whole number, not negative. (pinnedCount: ${pinnedCount})`);
        }
        if (groups.length === 0) {
            return HeaderGroupLayout.empty;
        }
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
