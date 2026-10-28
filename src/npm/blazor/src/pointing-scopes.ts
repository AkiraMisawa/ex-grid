// The Pointing Scopes of a page drawn by Blazor's Script adapters (ADR-0430): each C# PointingScope a Consumer made is
// one Scope here, named by the identifier the C# object makes, and held while a member of it stands. ExSheet's module
// brings the Scope, since it is ExSheet's Script Core's object; ExGrid's module, which cannot make one, waits here for
// it and registers its grid once it stands. In the Shared Script (ADR-0411), so both modules reach one place; it joins
// only what the Consumer put in one Scope.

import type { GridPointedAt } from '@ex-grid/core';

/** A Pointing Scope as a grid's adapter registers in it: ExSheet's Script Core's Scope, behind what ExSheet's Blazor
 * module made of it. */
export interface PagePointingScope {
    /**
     * The declaration named `declaration`, registered once however often it is asked: the grid shows the Linked Table
     * `table`, its columns named otherwise than their table column are `tableColumns`, and `tableRow` answers a row's
     * Values as .NET wrote them for the table's row.
     */
    registerGrid<TRow>(
        declaration: string, table: string, tableColumns: Readonly<Record<string, string>>, tableRow: (row: TRow) => readonly unknown[],
    ): GridPointedAt<TRow>;
}

interface Held {
    scope: PagePointingScope | undefined;
    readonly waiting: Set<(scope: PagePointingScope) => void>;
    members: number;
}

const held = new Map<string, Held>();

function heldAs(id: string): Held {
    if (id.length === 0) throw new TypeError('Internal: .NET named a Pointing Scope with no name.');
    const known = held.get(id);
    if (known !== undefined) return known;
    const made: Held = { scope: undefined, waiting: new Set(), members: 0 };
    held.set(id, made);
    return made;
}

// A member lets go: the Scope goes with its last member, so a page left keeps none of its Scopes.
function letGo(id: string, scope: Held): () => void {
    let gone = false;
    return () => {
        if (gone) return;
        gone = true;
        scope.members -= 1;
        if (scope.members === 0 && held.get(id) === scope) held.delete(id);
    };
}

/**
 * A Sheet of the Scope `id` holds it: the Scope standing, or the one `make` makes, which every grid waiting for it is
 * then handed. What it answers lets go of it, once.
 */
export function holdPointingScope(id: string, make: () => PagePointingScope): { readonly scope: PagePointingScope; readonly release: () => void } {
    const scope = heldAs(id);
    const standing = scope.scope ?? make();
    scope.members += 1;
    const release = letGo(id, scope);
    if (scope.scope === undefined) {
        scope.scope = standing;
        const waiting = [...scope.waiting];
        scope.waiting.clear();
        // Every grid waiting is handed the Scope, though one fails: a grid's failure is not the next grid's. The Sheet
        // that brought it fails with them, and holds nothing.
        const failures: unknown[] = [];
        for (const then of waiting) {
            try {
                then(standing);
            } catch (failure) { // oxlint-disable-line ex-grid/no-catch -- gathered, and thrown once every grid has been handed the Scope
                failures.push(failure);
            }
        }
        if (failures.length > 0) {
            release();
            throw failures.length === 1 ? failures[0] : new AggregateError(failures, 'Grids of a Pointing Scope failed as they joined it.');
        }
    }
    return { scope: standing, release };
}

/**
 * A grid of the Scope `id` waits for it: `then` is handed it at once where it stands, or once a Sheet of it brings
 * it. What it answers lets go of it, and of the wait, once.
 */
export function awaitPointingScope(id: string, then: (scope: PagePointingScope) => void): () => void {
    const scope = heldAs(id);
    scope.members += 1;
    const release = letGo(id, scope);
    if (scope.scope !== undefined) {
        try {
            then(scope.scope);
        } catch (failure) {
            // The grid failed as it joined, and holds nothing.
            release();
            throw failure;
        }
        return release;
    }
    scope.waiting.add(then);
    return () => {
        scope.waiting.delete(then);
        release();
    };
}
