// Chrome slots filled with Razor (ADR-0410): the component renders each standing slot's fragment beside its root,
// in a hidden holder whose one child is the element the fragment is drawn in, and this moves that child into the
// slot the core made once .NET has rendered it, and back into its holder when the core takes the slot away. What
// the contents' controls do is wired here, on the moved markup, from the attributes the contents write (section 2):
// nothing a key means waits on the circuit. Until the contents stand the grid is not settled (section 5), and until
// the contents of a part the keyboard reaches first stand, every input waits in its order and is taken in them.

import {
    filterPanelLabelIds, findPanelLabelIds, operatorLabelId, wireFilterPanel, wireFindPanel, wireMenuItems, type FilterPanelCondition,
    type FilterPanelView, type FindPanelView, type GridChrome, type GridColumnMenuSlotContext, type GridContextMenuSlotContext, type GridFilterPanelSlotContext,
    type GridFindPanelSlotContext, type GridLoadingIndicatorSlotContext, type GridMenuSlotContext,
} from '@ex-grid/core';
import type { DotNetHandle } from './dotnet-source';
import type { BlazorRow } from './wire-rows';

/** The slots a Razor Chrome fills, as the core names them. */
export type RazorSlotName = 'columnMenu' | 'contextMenu' | 'filterPanel' | 'findPanel' | 'loadingIndicator';

const razorSlotNames: readonly RazorSlotName[] = Object.freeze(['columnMenu', 'contextMenu', 'filterPanel', 'findPanel', 'loadingIndicator']);

/** The slots whose contents take the keyboard: until the contents the core mounted stand, every input waits. */
const keyboardSlots: ReadonlySet<RazorSlotName> = new Set(['columnMenu', 'contextMenu', 'filterPanel', 'findPanel']);

/** The attribute the element a slot's contents are drawn in carries: the number of the data .NET drew them from. */
export const drawnVersionAttribute = 'data-ex-version';

/** Whether `name` is a slot a Razor Chrome fills. */
export function isRazorSlotName(name: unknown): name is RazorSlotName {
    return typeof name === 'string' && (razorSlotNames as readonly string[]).includes(name);
}

type SlotContext = GridColumnMenuSlotContext | GridContextMenuSlotContext<BlazorRow> | GridFilterPanelSlotContext | GridFindPanelSlotContext
    | GridLoadingIndicatorSlotContext;

/** A slot the core mounted, and its contents once .NET has rendered them. */
interface Standing {
    readonly id: number;
    readonly name: RazorSlotName;
    readonly slot: HTMLElement;
    context: SlotContext;
    /** The data .NET was last handed, and its number: a render of an earlier one leaves the Settled Mark held. */
    sent: string;
    version: number;
    /** The latest number .NET has said it drew. */
    drawn: number;
    /** A filter panel's views by the number .NET was handed each under, from the one its contents may still show. */
    readonly views: Map<number, FilterPanelView>;
    /** The texts a find panel's fields open with, as .NET is handed them: what the core held as the panel was mounted,
     * and the replacement as Replace was last shown. Typed after, a text is the field's own and crosses nothing. */
    findOpens: FindOpens | undefined;
    /** Stops hearing a panel's view. */
    unsubscribe: (() => void) | undefined;
    /** Lets the inputs held behind the contents go, while a part the keyboard reaches owes them. */
    letInputsGo: (() => void) | undefined;
    /** The element the fragment is drawn in, and the holder it came from, once rendered. */
    contents: HTMLElement | undefined;
    holder: HTMLElement | undefined;
    unwire: (() => void) | undefined;
    /** The context whose focus request the contents answered: wired again for it, they take no keyboard. */
    answered: SlotContext | undefined;
    /** Lets the Settled Mark go, while the contents are owed. */
    release: (() => void) | undefined;
    /** The core took the slot away; it goes from .NET at the end of the task, unless the core mounts it again. */
    leaving: boolean;
}

/**
 * The Razor Chrome of one grid: mount functions for the slots .NET fills, and the contents .NET renders for each.
 * `hold` holds the grid's Settled Mark until the function it answers is called.
 */
export class RazorSlots {
    readonly #dotnet: DotNetHandle;
    readonly #hold: () => (() => void);
    readonly #order: (until: Promise<void>) => void;
    readonly #keyboardBack: () => void;
    readonly #standing = new Map<number, Standing>();
    readonly #bySlot = new Map<HTMLElement, Standing>();
    #ids = 0;
    #disposed = false;
    #chrome: { readonly names: string; readonly chrome: GridChrome<BlazorRow> } | undefined;

    /** `hold` holds the grid's Settled Mark until the function it answers is called; `order` holds every input made in
     * the grid after it, in its order, until the promise it is handed settles (ADR-0240); `keyboardBack` hands the
     * keyboard to the grid. */
    constructor(dotnet: DotNetHandle, hold: () => (() => void), order: (until: Promise<void>) => void, keyboardBack: () => void) {
        this.#dotnet = dotnet;
        this.#hold = hold;
        this.#order = order;
        this.#keyboardBack = keyboardBack;
    }

    /** The core's Chrome for the slots .NET fills: each a mount function that asks .NET for the slot's contents. The
     * same object while the slots are the same, so that a standing part is not drawn again for an update. */
    chrome(names: readonly RazorSlotName[]): GridChrome<BlazorRow> {
        const key = names.join(' ');
        if (this.#chrome?.names === key) return this.#chrome.chrome;
        const chrome: Record<string, (slot: HTMLElement, context: SlotContext) => () => void> = {};
        for (const name of names) chrome[name] = (slot, context) => this.#mount(name, slot, context);
        this.#chrome = { names: key, chrome };
        return chrome;
    }

    /** .NET rendered the contents of slot `id` for the data numbered `version` in `contents`: they move into the slot,
     * or are wired again with the context that slot now holds. Contents of a slot already gone stay in their holder,
     * which .NET removes. The Settled Mark stays held until the data last sent stands. */
    rendered(id: number, version: number, contents: HTMLElement | null): void {
        const standing = this.#standing.get(id);
        if (standing === undefined || this.#disposed) return;
        if (contents === null) throw new Error(`Internal: .NET rendered no element for the slot ${id}.`);
        if (standing.contents !== contents) {
            const holder = contents.parentElement;
            if (holder === null) throw new Error(`Internal: the contents of the slot ${id} stand in no holder.`);
            standing.holder = holder;
            standing.contents = contents;
        }
        standing.drawn = Math.max(standing.drawn, version);
        if (!standing.leaving) {
            if (contents.parentElement !== standing.slot) standing.slot.append(contents);
            this.#wire(standing);
            // The inputs held wait for the contents drawn from the data last sent, which take the keyboard.
            if (standsWhole(standing)) this.#letInputsGo(standing);
        }
        for (const drawn of standing.views.keys()) if (drawn < version) standing.views.delete(drawn);
        if (version !== standing.version) return;
        standing.release?.();
        standing.release = undefined;
    }

    /** The grid is going: every slot's contents go back to their holders, and nothing is asked of .NET again. */
    dispose(): void {
        if (this.#disposed) return;
        this.#disposed = true;
        for (const standing of this.#standing.values()) {
            this.#takeBack(standing);
            this.#letInputsGo(standing);
        }
        this.#standing.clear();
        this.#bySlot.clear();
    }

    #mount(name: RazorSlotName, slot: HTMLElement, context: SlotContext): () => void {
        if (this.#disposed) throw new Error('Internal: a grid destroyed mounted a Razor slot.');
        // The same slot mounted again in the task that took it away, for a focus request or a change of its context:
        // the contents stay, wired to the new context at once, and .NET renders them again for it.
        // A focus request draws nothing new: its data are the same, and the script answers it on the contents standing.
        const again = this.#bySlot.get(slot);
        if (again?.leaving === true && again.name === name) {
            again.leaving = false;
            again.context = context;
            this.#hear(again);
            this.#send(again, 'UpdateSlot');
            if (again.contents !== undefined) {
                // Still in the slot, since they go back to their holder only at the end of the task: moved, they would
                // drop the keyboard they hold.
                if (again.contents.parentElement !== again.slot) again.slot.append(again.contents);
                this.#wire(again);
            }
            return () => { this.#leave(again); };
        }
        this.#ids += 1;
        const standing: Standing = {
            id: this.#ids, name, slot, context, sent: '', version: 0, drawn: 0, views: new Map(), findOpens: undefined, unsubscribe: undefined, letInputsGo: undefined,
            contents: undefined, holder: undefined, unwire: undefined, answered: undefined, release: undefined, leaving: false,
        };
        this.#standing.set(standing.id, standing);
        this.#bySlot.set(slot, standing);
        if (keyboardSlots.has(name)) this.#holdInputs(standing);
        this.#hear(standing);
        this.#send(standing, 'MountSlot');
        return () => { this.#leave(standing); };
    }

    // Every input made in the grid from now waits, in its order, until the slot's latest contents stand (ADR-0240).
    #holdInputs(standing: Standing): void {
        if (standing.letInputsGo !== undefined) return;
        this.#order(new Promise<void>(resolve => { standing.letInputsGo = resolve; }));
    }

    #letInputsGo(standing: Standing): void {
        standing.letInputsGo?.();
        standing.letInputsGo = undefined;
    }

    // .NET is handed the slot's data where they changed, under the next number, and the grid is not settled until
    // they are drawn. Where .NET fails to take them, nothing it would draw holds the grid: the failure is said.
    #send(standing: Standing, call: 'MountSlot' | 'UpdateSlot'): void {
        const view = standing.name === 'filterPanel' ? filterContext(standing.context).view() : undefined;
        if (standing.name === 'findPanel') standing.findOpens = findOpensOf(findContext(standing.context).view(), standing.findOpens);
        const sent = contextJson(standing.name, standing.context, view, standing.findOpens);
        if (sent === standing.sent) return;
        standing.sent = sent;
        standing.version += 1;
        if (view !== undefined) standing.views.set(standing.version, view);
        standing.release ??= this.#hold();
        const asked = call === 'MountSlot'
            ? this.#dotnet.invokeMethodAsync(call, standing.id, standing.version, standing.name, sent)
            : this.#dotnet.invokeMethodAsync(call, standing.id, standing.version, sent);
        void asked.catch((error: unknown) => { // oxlint-disable-line ex-grid/no-catch -- the hold and the inputs are let go, and the failure rethrown
            standing.release?.();
            standing.release = undefined;
            this.#letInputsGo(standing);
            throw error;
        });
    }

    // A panel's view changes under its contents, a choice made, the value list arrived or a step's outcome said: .NET
    // draws it again.
    #hear(standing: Standing): void {
        standing.unsubscribe?.();
        const again = (): void => { if (!standing.leaving && !this.#disposed) this.#send(standing, 'UpdateSlot'); };
        standing.unsubscribe = standing.name === 'filterPanel' ? filterContext(standing.context).subscribe(again)
            : standing.name === 'findPanel' ? findContext(standing.context).subscribe(again)
            : undefined;
    }

    // The core takes the slot away: the contents stop answering at once, and go back to their holder at the end of the
    // task, as .NET is told, unless the core mounted the slot again meanwhile, as a focus request does: contents moved
    // out and back would drop the keyboard they hold. Contents that held it as the slot went, a menu whose command
    // opened a panel in its place, hand it to the grid where it fell on nothing: the panel's field takes it only once
    // its own Razor stands, and the keys typed meanwhile wait for it in the grid (ADR-0240), never reaching the page.
    #leave(standing: Standing): void {
        if (standing.leaving || this.#disposed) return;
        standing.leaving = true;
        const held = standing.contents?.contains(standing.contents.ownerDocument.activeElement) === true;
        this.#unwire(standing);
        queueMicrotask(() => {
            if (!standing.leaving || this.#disposed) return;
            this.#takeBack(standing);
            const doc = standing.slot.ownerDocument;
            const active = doc.activeElement;
            if (held && (active === null || active === doc.body || standing.contents?.contains(active) === true)) this.#keyboardBack();
            standing.release?.();
            standing.release = undefined;
            this.#letInputsGo(standing);
            this.#standing.delete(standing.id);
            if (this.#bySlot.get(standing.slot) === standing) this.#bySlot.delete(standing.slot);
            void this.#dotnet.invokeMethodAsync('UnmountSlot', standing.id);
        });
    }

    #unwire(standing: Standing): void {
        standing.unwire?.();
        standing.unwire = undefined;
        standing.unsubscribe?.();
        standing.unsubscribe = undefined;
    }

    #takeBack(standing: Standing): void {
        this.#unwire(standing);
        if (standing.contents !== undefined && standing.holder !== undefined) standing.holder.append(standing.contents);
    }

    // A focus request is answered on contents drawn from the data last sent, never on older ones a drawing still owed
    // would take away under the keyboard, nor on a filter panel whose value list is on its way: until the contents
    // stand whole, the request waits, and so does every input after it.
    #wire(standing: Standing): void {
        standing.unwire?.();
        standing.unwire = undefined;
        const { contents, context } = standing;
        if (contents === undefined) return;
        const current = standsWhole(standing);
        const answered = standing.answered === context || !current;
        if (standing.name === 'columnMenu' || standing.name === 'contextMenu') {
            // Wired again as .NET renders the contents again, the request already answered is not answered again: a
            // key moved the keyboard on meanwhile, and the render a round trip later must not take it back.
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- a menu slot is handed a menu's context, as the core mounts it
            standing.unwire = wireMenuItems(contents, context as GridMenuSlotContext, answered);
        } else if (standing.name === 'filterPanel') {
            standing.unwire = wireFilterPanel(contents, filterContext(context), () => drawnView(standing, contents), answered);
        } else if (standing.name === 'findPanel') {
            standing.unwire = wireFindPanel(contents, findContext(context), answered);
        } else {
            return;
        }
        if (current) standing.answered = context;
        else if (standing.answered !== context) this.#holdInputs(standing);
    }
}

// oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the filter slot is handed the filter panel's context, as the core mounts it
const filterContext = (context: SlotContext): GridFilterPanelSlotContext => context as GridFilterPanelSlotContext;

// oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the find slot is handed the find panel's context, as the core mounts it
const findContext = (context: SlotContext): GridFindPanelSlotContext => context as GridFindPanelSlotContext;

/** Whether the contents .NET drew from the data last sent stand whole: a filter panel's only once its value list has
 * arrived, since the list takes the place of the condition form shown meanwhile, and of anything typed or chosen in it.
 * Until then the keyboard is not handed to the panel, and every input waits (spine rule 6). */
function standsWhole(standing: Standing): boolean {
    return standing.drawn === standing.version && standing.views.get(standing.version)?.fetching !== true;
}

/** The view a filter panel's contents show: the one .NET drew them from, whose number they carry. */
function drawnView(standing: Standing, contents: HTMLElement): FilterPanelView {
    const version = Number(contents.getAttribute(drawnVersionAttribute));
    const view = standing.views.get(version);
    if (view === undefined) throw new Error(`Internal: the filter panel's contents show the data numbered ${String(version)}, which the script no longer holds.`);
    return view;
}

/** A condition as .NET is handed it. */
function conditionJson(condition: FilterPanelCondition): object {
    // JSON has no undefined: a refusal absent crosses as null, which .NET reads as none.
    return { operator: condition.operator, operand: condition.operand, takesOperand: condition.takesOperand, refusal: condition.refusal === undefined ? null : condition.refusal };
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

/** What .NET is handed of a slot's context: data, never a function (ADR-0410 section 3). */
function contextJson(name: RazorSlotName, context: SlotContext, filterView: FilterPanelView | undefined, findOpens: FindOpens | undefined): string {
    switch (name) {
        case 'columnMenu':
        case 'contextMenu': {
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- a menu slot is handed a menu's context, as the core mounts it
            const menu = context as GridColumnMenuSlotContext | GridContextMenuSlotContext<BlazorRow>;
            // Not the focus request, which the script answers: a request is the same menu, drawn as it stands.
            return JSON.stringify({
                column: menu.column,
                columnType: menu.columnType,
                commands: menu.commands.map(command => ({ id: command.id, label: menu.label(command.id), enabled: command.enabled })),
            });
        }
        case 'filterPanel': {
            const panel = filterContext(context);
            if (filterView === undefined) throw new Error('Internal: a filter panel\'s data were asked for without its view.');
            const view = filterView;
            const words = {
                ok: panel.label(filterPanelLabelIds.ok), cancel: panel.label(filterPanelLabelIds.cancel), search: panel.label(filterPanelLabelIds.search),
                addToCurrent: panel.label(filterPanelLabelIds.addToCurrent), selectAll: panel.label(filterPanelLabelIds.selectAll),
                selectAllSearchResults: panel.label(filterPanelLabelIds.selectAllSearchResults), and: panel.label(filterPanelLabelIds.and),
                or: panel.label(filterPanelLabelIds.or), none: panel.label(filterPanelLabelIds.none),
            };
            // Not the focus request, which the script answers; an entry's value stays here, and its checkbox names its index.
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
        case 'findPanel': {
            const panel = findContext(context);
            const view = panel.view();
            if (findOpens === undefined) throw new Error('Internal: a find panel\'s data were asked for without the texts its fields open with.');
            const words = {
                field: panel.label(findPanelLabelIds.field), matchCase: panel.label(findPanelLabelIds.matchCase),
                wholeCell: panel.label(findPanelLabelIds.wholeCell), next: panel.label(findPanelLabelIds.next),
                previous: panel.label(findPanelLabelIds.previous), replaceField: panel.label(findPanelLabelIds.replaceField),
                replace: panel.label(findPanelLabelIds.replace), replaceAll: panel.label(findPanelLabelIds.replaceAll),
            };
            // Not the focus request, which the script answers: a request is the same panel, drawn as it stands.
            return JSON.stringify({
                text: findOpens.text, matchCase: view.matchCase, wholeCell: view.wholeCell, replaceShown: view.replaceShown,
                replaceText: findOpens.replaceText, replaceWaiting: view.replaceWaiting, outcome: view.outcome, words,
            });
        }
        case 'loadingIndicator': {
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the loading slot is handed the loading context, as the core mounts it
            const loading = context as GridLoadingIndicatorSlotContext;
            return JSON.stringify({ loading: loading.loading, placeholderRows: loading.placeholderRows });
        }
        default: throw new Error(`Internal: a Razor slot named ${String(name satisfies never)}.`);
    }
}
