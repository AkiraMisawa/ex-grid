// ExGrid's Chrome slots filled with Razor (ADR-0410): the kinds of the grid's slots, over the Shared Script's
// RazorSlotHost, which moves the contents in and out and holds the Settled Mark and the inputs. Here is what each slot's
// context crosses to .NET as, and how the contents' controls are wired from the attributes they write (section 2), so
// nothing a key means waits on the circuit.

import { drawnVersionAttribute, RazorSlotHost, type HostedSlotKind, type RazorSlotKind } from '@ex-grid/blazor-shared';
import {
    filterPanelLabelIds, findPanelLabelIds, operatorLabelId, wireFilterPanel, wireFindPanel, wireMenuItems, type FilterPanelCondition,
    type FilterPanelView, type FindPanelView, type GridChrome, type GridColumnMenuSlotContext, type GridContextMenuSlotContext, type GridFilterPanelSlotContext,
    type GridFindPanelSlotContext, type GridLoadingIndicatorSlotContext,
} from '@ex-grid/core';
import type { DotNetHandle } from './dotnet-source';
import type { BlazorRow } from './wire-rows';

export { drawnVersionAttribute };

/** The slots a Razor Chrome fills, as the core names them. */
export type RazorSlotName = 'columnMenu' | 'contextMenu' | 'filterPanel' | 'findPanel' | 'loadingIndicator';

/** A menu's kind: the commands as data, and the keys wired on the items, which take the keyboard. */
const menuKind: RazorSlotKind<GridColumnMenuSlotContext | GridContextMenuSlotContext<BlazorRow>> = {
    takesKeyboard: true,
    // Not the focus request, which the script answers: a request is the same menu, drawn as it stands.
    data: menu => ({
        json: JSON.stringify({
            column: menu.column,
            columnType: menu.columnType,
            commands: menu.commands.map(command => ({ id: command.id, label: menu.label(command.id), enabled: command.enabled })),
        }),
        drawn: undefined,
        memo: undefined,
    }),
    // Wired again as .NET renders the contents again, the request already answered is not answered again: a key moved
    // the keyboard on meanwhile, and the render a round trip later must not take it back.
    wire: (contents, menu, answered) => wireMenuItems(contents, menu, answered),
};

/** The loading bar's kind: drawn, and nothing to wire. */
const loadingKind: RazorSlotKind<GridLoadingIndicatorSlotContext> = {
    takesKeyboard: false,
    data: loading => ({ json: JSON.stringify({ loading: loading.loading, placeholderRows: loading.placeholderRows }), drawn: undefined, memo: undefined }),
    wire: () => () => undefined,
};

/** The filter panel's kind: the view as data, drawn again at each change the core makes, and the choices read in the
 * browser against the view the contents carry the number of. */
const filterKind: RazorSlotKind<GridFilterPanelSlotContext, FilterPanelView> = {
    takesKeyboard: true,
    data: panel => {
        const view = panel.view();
        return { json: filterJson(panel, view), drawn: view, memo: undefined };
    },
    subscribe: (panel, again) => panel.subscribe(again),
    wire: (contents, panel, answered, drawn) => wireFilterPanel(contents, panel, drawn, answered),
    // Only once its value list has arrived, since the list takes the place of the condition form shown meanwhile, and of
    // anything typed or chosen in it. Until then the keyboard is not handed to the panel, and every input waits (spine
    // rule 6).
    standsWhole: view => !view.fetching,
};

/** The find panel's kind: the panel as data, its fields' texts as the panel opened, and the fields read in the browser. */
const findKind: RazorSlotKind<GridFindPanelSlotContext, undefined, FindOpens> = {
    takesKeyboard: true,
    data: (panel, before) => {
        const opens = findOpensOf(panel.view(), before);
        return { json: findJson(panel, opens), drawn: undefined, memo: opens };
    },
    subscribe: (panel, again) => panel.subscribe(again),
    wire: (contents, panel, answered) => wireFindPanel(contents, panel, answered),
};

// Every slot a Razor Chrome fills has its kind: a name added to RazorSlotName without one does not compile.
const gridKindsByName = {
    columnMenu: menuKind, contextMenu: menuKind, loadingIndicator: loadingKind, filterPanel: filterKind, findPanel: findKind,
} satisfies Record<RazorSlotName, HostedSlotKind>;

const gridKinds: ReadonlyMap<string, HostedSlotKind> = new Map(Object.entries(gridKindsByName));

/** Whether `name` is a slot a Razor Chrome fills. */
export function isRazorSlotName(name: unknown): name is RazorSlotName {
    return typeof name === 'string' && gridKinds.has(name);
}

/**
 * The Razor Chrome of one grid: mount functions for the slots .NET fills, and the contents .NET renders for each.
 * `hold` holds the grid's Settled Mark until the function it answers is called.
 */
export class RazorSlots {
    readonly #host: RazorSlotHost;

    /** `hold` holds the grid's Settled Mark until the function it answers is called; `order` holds every input made in
     * the grid after it, in its order, until the promise it is handed settles (ADR-0240); `keyboardBack` hands the
     * keyboard to the grid. */
    constructor(dotnet: DotNetHandle, hold: () => (() => void), order: (until: Promise<void>) => void, keyboardBack: () => void) {
        this.#host = new RazorSlotHost(dotnet, gridKinds, { hold, order, keyboardBack });
    }

    /** The core's Chrome for the slots .NET fills: each a mount function that asks .NET for the slot's contents. The
     * same object while the slots are the same, so that a standing part is not drawn again for an update. */
    chrome(names: readonly RazorSlotName[]): GridChrome<BlazorRow> {
        return this.#host.mounts(names);
    }

    /** .NET rendered the contents of slot `id` for the data numbered `version` in `contents`. */
    rendered(id: number, version: number, contents: HTMLElement | null): void {
        this.#host.rendered(id, version, contents);
    }

    /** The grid is going: every slot's contents go back to their holders, and nothing is asked of .NET again. */
    dispose(): void {
        this.#host.dispose();
    }
}

/** A condition as .NET is handed it. */
function conditionJson(condition: FilterPanelCondition): object {
    // JSON has no undefined: a refusal absent crosses as null, which .NET reads as none.
    return { operator: condition.operator, operand: condition.operand, takesOperand: condition.takesOperand, refusal: condition.refusal === undefined ? null : condition.refusal };
}

/** The filter panel as .NET is handed it. Not the focus request, which the script answers; an entry's value stays
 * here, and its checkbox names its index. */
function filterJson(panel: GridFilterPanelSlotContext, view: FilterPanelView): string {
    const words = {
        ok: panel.label(filterPanelLabelIds.ok), cancel: panel.label(filterPanelLabelIds.cancel), search: panel.label(filterPanelLabelIds.search),
        addToCurrent: panel.label(filterPanelLabelIds.addToCurrent), selectAll: panel.label(filterPanelLabelIds.selectAll),
        selectAllSearchResults: panel.label(filterPanelLabelIds.selectAllSearchResults), and: panel.label(filterPanelLabelIds.and),
        or: panel.label(filterPanelLabelIds.or), none: panel.label(filterPanelLabelIds.none),
    };
    const form = view.kind === 'valueList'
        ? {
            valueList: {
                search: view.search, addOffered: view.addOffered, addToCurrent: view.addToCurrent, selectAll: view.selectAll,
                entries: view.entries.map(entry => ({ text: entry.text, blank: entry.blank, checked: entry.checked })),
            },
        }
        : {
            conditions: {
                allowed: view.allowed.map(operator => ({ id: operator, label: panel.label(operatorLabelId(operator)) })),
                first: conditionJson(view.first), combinator: view.combinator, second: view.second === undefined ? null : conditionJson(view.second),
            },
        };
    return JSON.stringify({ column: panel.column, columnType: panel.columnType, canApply: view.canApply, fetching: view.fetching, words, ...form });
}

/** The texts a find panel's fields open with. */
interface FindOpens {
    readonly text: string;
    readonly replaceText: string;
    readonly replaceShown: boolean;
}

/** The texts a find panel's fields open with, for the view standing: the find field's as the panel was mounted, and
 * the Replace field's as Replace was last shown. A text typed changes nothing .NET draws, and is not sent again for
 * each key; one the core holds as its field is drawn is. */
function findOpensOf(view: FindPanelView, before: FindOpens | undefined): FindOpens {
    if (before === undefined) return { text: view.text, replaceText: view.replaceText, replaceShown: view.replaceShown };
    if (view.replaceShown === before.replaceShown) return before;
    return { text: before.text, replaceText: view.replaceShown ? view.replaceText : before.replaceText, replaceShown: view.replaceShown };
}

/** The find panel as .NET is handed it. Not the focus request, which the script answers: a request is the same panel,
 * drawn as it stands. */
function findJson(panel: GridFindPanelSlotContext, opens: FindOpens): string {
    const view = panel.view();
    const words = {
        field: panel.label(findPanelLabelIds.field), matchCase: panel.label(findPanelLabelIds.matchCase),
        wholeCell: panel.label(findPanelLabelIds.wholeCell), next: panel.label(findPanelLabelIds.next),
        previous: panel.label(findPanelLabelIds.previous), replaceField: panel.label(findPanelLabelIds.replaceField),
        replace: panel.label(findPanelLabelIds.replace), replaceAll: panel.label(findPanelLabelIds.replaceAll),
    };
    return JSON.stringify({
        text: opens.text, matchCase: view.matchCase, wholeCell: view.wholeCell, replaceShown: view.replaceShown,
        replaceText: opens.replaceText, replaceWaiting: view.replaceWaiting, outcome: view.outcome, words,
    });
}
