import type { GridHeaderGroup } from '@ex-grid/core';

/**
 * The whole column order after the grid reported a new order of the columns it shows. Each hidden column keeps its
 * place inside the Header Groups it belongs to, beside a column of its own group, so that showing it again puts it
 * where the groups still stand together: a column dropped at the end would split its group, which the grid refuses.
 * `was` is the order before, every column in it; `groups` are every group, hidden members included.
 */
export function reordered(was: readonly string[], shown: readonly string[], groups: readonly GridHeaderGroup[]): string[] {
    const order = [...shown];
    // The groups a column moves with, innermost first, then the whole grid.
    const nested = (name: string): (readonly string[])[] => [
        ...groups.filter(group => group.columns.includes(name)).sort((a, b) => a.columns.length - b.columns.length).map(group => group.columns),
        was,
    ];
    // What moves as one inside `around`: the largest group of `name` within it, or the column alone.
    const unitOf = (name: string, around: readonly string[]): readonly string[] =>
        nested(name).filter(group => group.length < around.length && group.every(member => around.includes(member))).at(-1) ?? [name];
    was.forEach((name, at) => {
        if (order.includes(name)) return;
        for (const around of nested(name)) {
            const kin = (other: string): boolean => order.includes(other) && around.includes(other);
            const before = was.slice(0, at).findLast(kin);
            if (before !== undefined) {
                order.splice(Math.max(...unitOf(before, around).map(member => order.indexOf(member))) + 1, 0, name);
                return;
            }
            const after = was.slice(at + 1).find(kin);
            if (after !== undefined) {
                order.splice(Math.min(...unitOf(after, around).filter(member => order.includes(member)).map(member => order.indexOf(member))), 0, name);
                return;
            }
        }
        // Nothing else is shown: the column stands alone.
        order.push(name);
    });
    return order;
}
