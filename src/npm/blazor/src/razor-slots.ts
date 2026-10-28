// Chrome slots filled with Razor (ADR-0410): the component renders each standing slot's fragment beside its root,
// in a hidden holder whose one child is the element the fragment is drawn in, and this moves that child into the
// slot the core made once .NET has rendered it, and back into its holder when the core takes the slot away. What
// the contents' controls do is wired here, on the moved markup, from the attributes the contents write (section 2):
// nothing a key means waits on the circuit. Until the contents stand the grid is not settled (section 5).

import { wireMenuItems, type GridChrome, type GridColumnMenuSlotContext, type GridContextMenuSlotContext, type GridLoadingIndicatorSlotContext, type GridMenuSlotContext } from '@ex-grid/core';
import type { DotNetHandle } from './dotnet-source';
import type { BlazorRow } from './wire-rows';

/** The slots a Razor Chrome fills, as the core names them. */
export type RazorSlotName = 'columnMenu' | 'contextMenu' | 'loadingIndicator';

const razorSlotNames: readonly RazorSlotName[] = Object.freeze(['columnMenu', 'contextMenu', 'loadingIndicator']);

/** Whether `name` is a slot a Razor Chrome fills. */
export function isRazorSlotName(name: unknown): name is RazorSlotName {
    return typeof name === 'string' && (razorSlotNames as readonly string[]).includes(name);
}

type SlotContext = GridColumnMenuSlotContext | GridContextMenuSlotContext<BlazorRow> | GridLoadingIndicatorSlotContext;

/** A slot the core mounted, and its contents once .NET has rendered them. */
interface Standing {
    readonly id: number;
    readonly name: RazorSlotName;
    readonly slot: HTMLElement;
    context: SlotContext;
    /** The data .NET was last handed, and its number: a render of an earlier one leaves the Settled Mark held. */
    sent: string;
    version: number;
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
    readonly #standing = new Map<number, Standing>();
    readonly #bySlot = new Map<HTMLElement, Standing>();
    #ids = 0;
    #disposed = false;
    #chrome: { readonly names: string; readonly chrome: GridChrome<BlazorRow> } | undefined;

    constructor(dotnet: DotNetHandle, hold: () => (() => void)) {
        this.#dotnet = dotnet;
        this.#hold = hold;
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
        if (!standing.leaving) {
            if (contents.parentElement !== standing.slot) standing.slot.append(contents);
            this.#wire(standing);
        }
        if (version !== standing.version) return;
        standing.release?.();
        standing.release = undefined;
    }

    /** The grid is going: every slot's contents go back to their holders, and nothing is asked of .NET again. */
    dispose(): void {
        if (this.#disposed) return;
        this.#disposed = true;
        for (const standing of this.#standing.values()) this.#takeBack(standing);
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
            if (again.contents !== undefined) {
                again.slot.append(again.contents);
                this.#wire(again);
            }
            const sent = contextJson(name, context);
            if (sent !== again.sent) {
                again.sent = sent;
                again.version += 1;
                again.release ??= this.#hold();
                void this.#dotnet.invokeMethodAsync('UpdateSlot', again.id, again.version, sent);
            }
            return () => { this.#leave(again); };
        }
        this.#ids += 1;
        const sent = contextJson(name, context);
        const standing: Standing = {
            id: this.#ids, name, slot, context, sent, version: 1, contents: undefined, holder: undefined, unwire: undefined, answered: undefined,
            release: this.#hold(), leaving: false,
        };
        this.#standing.set(standing.id, standing);
        this.#bySlot.set(slot, standing);
        void this.#dotnet.invokeMethodAsync('MountSlot', standing.id, standing.version, name, sent);
        return () => { this.#leave(standing); };
    }

    // The core takes the slot away: the contents go back to their holder at once, before the slot leaves the page,
    // and .NET is told at the end of the task, unless the core mounted the slot again meanwhile.
    #leave(standing: Standing): void {
        if (standing.leaving || this.#disposed) return;
        standing.leaving = true;
        this.#takeBack(standing);
        queueMicrotask(() => {
            if (!standing.leaving || this.#disposed) return;
            standing.release?.();
            standing.release = undefined;
            this.#standing.delete(standing.id);
            if (this.#bySlot.get(standing.slot) === standing) this.#bySlot.delete(standing.slot);
            void this.#dotnet.invokeMethodAsync('UnmountSlot', standing.id);
        });
    }

    #takeBack(standing: Standing): void {
        standing.unwire?.();
        standing.unwire = undefined;
        if (standing.contents !== undefined && standing.holder !== undefined) standing.holder.append(standing.contents);
    }

    #wire(standing: Standing): void {
        standing.unwire?.();
        standing.unwire = undefined;
        const { contents } = standing;
        if (contents === undefined) return;
        if (standing.name === 'columnMenu' || standing.name === 'contextMenu') {
            // Wired again as .NET renders the contents again, the request already answered is not answered again: a
            // key moved the keyboard on meanwhile, and the render a round trip later must not take it back.
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- a menu slot is handed a menu's context, as the core mounts it
            standing.unwire = wireMenuItems(contents, standing.context as GridMenuSlotContext, standing.answered === standing.context);
            standing.answered = standing.context;
        }
    }
}

/** What .NET is handed of a slot's context: data, never a function (ADR-0410 section 3). */
function contextJson(name: RazorSlotName, context: SlotContext): string {
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
        case 'loadingIndicator': {
            // oxlint-disable-next-line typescript/no-unsafe-type-assertion -- the loading slot is handed the loading context, as the core mounts it
            const loading = context as GridLoadingIndicatorSlotContext;
            return JSON.stringify({ loading: loading.loading, placeholderRows: loading.placeholderRows });
        }
        default: throw new Error(`Internal: a Razor slot named ${String(name satisfies never)}.`);
    }
}
