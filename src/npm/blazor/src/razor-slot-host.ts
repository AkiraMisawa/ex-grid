// Chrome slots filled with Razor, for any product's Blazor adapter (ADR-0410): the component renders each standing
// slot's fragment beside its root, in a hidden holder whose one child is the element the fragment is drawn in, and the
// host moves that child into the slot the core made once .NET has rendered it, and back into its holder when the core
// takes the slot away. What the contents' controls do is wired here, on the moved markup, by the slot's kind: nothing
// a key means waits on the circuit. Until the contents stand the product is not settled (section 5), and until the
// contents of a part the keyboard reaches first stand, every input waits in its order and is taken in them. Each
// product registers its kinds by the core's slot names; the host is part of the Shared Script (ADR-0411), so that every
// product's slots move, wait and hand the keyboard back alike.

import type { DotNetCalls } from './held-messages';

/** The attribute the element a slot's contents are drawn in carries: the number of the data .NET drew them from. */
export const drawnVersionAttribute = 'data-ex-version';

/** What .NET is handed of a slot's context, and what the contents drawn from it are read against. */
export interface RazorSlotData<Drawn, Memo> {
    /** The data .NET draws the contents from: data, never a function (ADR-0410 section 3). */
    readonly json: string;
    /** What the contents drawn from these data show, which a press on them is read against: {@link RazorSlotKind.wire}'s
     * `drawn` answers it by the number the contents carry. */
    readonly drawn: Drawn;
    /** What the next {@link RazorSlotKind.data} is handed: what the kind keeps from one sending to the next. */
    readonly memo: Memo;
}

/** One kind of slot a Razor Chrome fills: how its context crosses to .NET, and how the contents .NET draws are wired. */
export interface RazorSlotKind<Context, Drawn = undefined, Memo = undefined> {
    /** Whether the contents take the keyboard: until the contents the core mounted stand, every input waits. */
    readonly takesKeyboard: boolean;
    /** What .NET is handed of `context`, with `memo` what the last sending kept, or undefined at the first. */
    data(context: Context, memo: Memo | undefined): RazorSlotData<Drawn, Memo>;
    /** Hears each change the core makes under the contents, answering the function that stops it; nothing where the
     * context never changes under them, as where the core mounts the slot again for each change. */
    subscribe?(context: Context, again: () => void): () => void;
    /** Wires the contents drawn for `context`, answering the function that stops it. `answered` says the context's
     * focus request is not to be answered on them: it was, or the contents are not yet the ones it is for. `drawn`
     * answers what the contents show, by the number they carry. */
    wire(contents: HTMLElement, context: Context, answered: boolean, drawn: () => Drawn): () => void;
    /** Whether contents drawn from `drawn`, the data last sent, stand whole: until they do, the keyboard is not handed
     * to them and every input waits. Whole as drawn where not given. */
    standsWhole?(drawn: Drawn): boolean;
}

/** What the host is handed of the product: each optional where the product has no such thing. Each is handed the
 * slot it is asked for, so a product of several grids, a pivot's report and its Show Details grids, answers for the
 * grid the slot stands in. */
export interface RazorSlotHostOptions {
    /** Holds the product's Settled Mark until the function it answers is called. */
    readonly hold?: (slot: HTMLElement) => (() => void);
    /** Holds every input made in the product after it, in its order, until the promise it is handed settles (ADR-0240). */
    readonly order?: (until: Promise<void>, slot: HTMLElement) => void;
    /** Hands the keyboard to the product, from contents that held it as their slot went. */
    readonly keyboardBack?: (slot: HTMLElement) => void;
}

/** A kind as the host keeps it, whose types the host does not know: it is handed only the contexts the core mounts its
 * slot with, and only what its own `data` answered. */
export type HostedSlotKind = RazorSlotKind<unknown, unknown, unknown>;

/** A slot the core mounted, and its contents once .NET has rendered them. */
interface Standing {
    readonly id: number;
    readonly name: string;
    readonly kind: HostedSlotKind;
    readonly slot: HTMLElement;
    context: unknown;
    /** The data .NET was last handed, and its number: a render of an earlier one leaves the Settled Mark held. */
    sent: string;
    version: number;
    /** The latest number .NET has said it drew. */
    drawn: number;
    /** What the contents drawn under each number show, from the one the contents may still show. */
    readonly views: Map<number, unknown>;
    memo: unknown;
    /** Stops hearing the context's changes. */
    unsubscribe: (() => void) | undefined;
    /** Lets the inputs held behind the contents go, while a part the keyboard reaches owes them. */
    letInputsGo: (() => void) | undefined;
    /** The element the fragment is drawn in, and the holder it came from, once rendered. */
    contents: HTMLElement | undefined;
    holder: HTMLElement | undefined;
    unwire: (() => void) | undefined;
    /** The context whose focus request the contents answered: wired again for it, they take no keyboard. */
    answered: unknown;
    /** Lets the Settled Mark go, while the contents are owed. */
    release: (() => void) | undefined;
    /** The core took the slot away; it goes from .NET at the end of the task, unless the core mounts it again. */
    leaving: boolean;
}

/**
 * The Razor Chrome of one product instance: mount functions for the slots .NET fills, and the contents .NET renders
 * for each. .NET is told `MountSlot(id, version, name, json)`, `UpdateSlot(id, version, json)` and `UnmountSlot(id)`,
 * and tells back {@link RazorSlotHost.rendered}.
 */
export class RazorSlotHost {
    readonly #dotnet: DotNetCalls;
    readonly #kinds: ReadonlyMap<string, HostedSlotKind>;
    readonly #hold: RazorSlotHostOptions['hold'];
    readonly #order: RazorSlotHostOptions['order'];
    readonly #keyboardBack: RazorSlotHostOptions['keyboardBack'];
    readonly #standing = new Map<number, Standing>();
    readonly #bySlot = new Map<HTMLElement, Standing>();
    #ids = 0;
    #disposed = false;
    #mounts: { readonly names: string; readonly mounts: Readonly<Record<string, (slot: HTMLElement, context: unknown) => () => void>> } | undefined;

    /** `kinds` are the product's, by the core's slot names. A kind whose contents take the keyboard needs `order`, to
     * hold the inputs until they stand, and `keyboardBack`, to hand the keyboard on as they go: without them it would
     * run quietly wrong, and is refused. */
    constructor(dotnet: DotNetCalls, kinds: ReadonlyMap<string, HostedSlotKind>, options: RazorSlotHostOptions = {}) {
        for (const [name, kind] of kinds) {
            if (kind.takesKeyboard && (options.order === undefined || options.keyboardBack === undefined)) {
                throw new TypeError(`Internal: the slot '${name}' takes the keyboard, and the product gave no Input Order or no way to hand the keyboard back.`);
            }
        }
        this.#dotnet = dotnet;
        this.#kinds = kinds;
        this.#hold = options.hold;
        this.#order = options.order;
        this.#keyboardBack = options.keyboardBack;
    }

    /** Whether `name` is a slot of a kind the product registered. */
    fills(name: unknown): name is string {
        return typeof name === 'string' && this.#kinds.has(name);
    }

    /** The core's mount functions for the slots `names`, each asking .NET for the slot's contents. The same object
     * while the slots are the same, so that a standing part is not drawn again for an update. */
    mounts(names: readonly string[]): Readonly<Record<string, (slot: HTMLElement, context: unknown) => () => void>> {
        const key = names.join(' ');
        if (this.#mounts?.names === key) return this.#mounts.mounts;
        const mounts: Record<string, (slot: HTMLElement, context: unknown) => () => void> = {};
        for (const name of names) {
            const kind = this.#kinds.get(name);
            if (kind === undefined) throw new TypeError(`Internal: .NET named '${name}' as a slot its Chrome fills, of no kind the product registered.`);
            mounts[name] = (slot, context) => this.#mount(name, kind, slot, context);
        }
        this.#mounts = { names: key, mounts };
        return mounts;
    }

    /** .NET rendered the contents of slot `id` for the data numbered `version` in `contents`: they move into the slot,
     * or are wired again with the context that slot now holds. Contents of a slot already gone stay in their holder,
     * which .NET removes. The Settled Mark stays held until the data last sent stand. */
    rendered(id: number, version: number, drawnIn: HTMLElement | null): void {
        const standing = this.#standing.get(id);
        if (standing === undefined || this.#disposed) return;
        // Blazor finds the element by searching the document: contents already moved into a slot the core has not put
        // in the page (a report filter band with no filters) are not found, and are the contents standing there.
        // Contents in the page that Blazor did not find are not those it drew.
        const contents = drawnIn ?? (standing.contents?.isConnected === false ? standing.contents : undefined);
        if (contents === undefined) throw new Error(`Internal: .NET rendered no element for the slot ${String(id)}.`);
        if (standing.contents !== contents) {
            const holder = contents.parentElement;
            if (holder === null) throw new Error(`Internal: the contents of the slot ${String(id)} stand in no holder.`);
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

    /** The product is going: every slot's contents go back to their holders, and nothing is asked of .NET again. */
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

    #mount(name: string, kind: HostedSlotKind, slot: HTMLElement, context: unknown): () => void {
        if (this.#disposed) throw new Error('Internal: a product destroyed mounted a Razor slot.');
        // The same slot mounted again in the task that took it away, for a focus request or a change of its context:
        // the contents stay, wired to the new context at once, and .NET renders them again for it.
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
            id: this.#ids, name, kind, slot, context, sent: '', version: 0, drawn: 0, views: new Map(), memo: undefined, unsubscribe: undefined,
            letInputsGo: undefined, contents: undefined, holder: undefined, unwire: undefined, answered: undefined, release: undefined, leaving: false,
        };
        this.#standing.set(standing.id, standing);
        this.#bySlot.set(slot, standing);
        if (kind.takesKeyboard) this.#holdInputs(standing);
        this.#hear(standing);
        this.#send(standing, 'MountSlot');
        return () => { this.#leave(standing); };
    }

    // Every input made in the product from now waits, in its order, until the slot's latest contents stand (ADR-0240).
    #holdInputs(standing: Standing): void {
        if (standing.letInputsGo !== undefined || this.#order === undefined) return;
        this.#order(new Promise<void>(resolve => { standing.letInputsGo = resolve; }), standing.slot);
    }

    #letInputsGo(standing: Standing): void {
        standing.letInputsGo?.();
        standing.letInputsGo = undefined;
    }

    // .NET is handed the slot's data where they changed, under the next number, and the product is not settled until
    // they are drawn. Where .NET fails to take them, nothing it would draw holds the product: the failure is said.
    #send(standing: Standing, call: 'MountSlot' | 'UpdateSlot'): void {
        const data = standing.kind.data(standing.context, standing.memo);
        standing.memo = data.memo;
        if (data.json === standing.sent) return;
        standing.sent = data.json;
        standing.version += 1;
        standing.views.set(standing.version, data.drawn);
        standing.release ??= this.#hold?.(standing.slot);
        const asked = call === 'MountSlot'
            ? this.#dotnet.invokeMethodAsync(call, standing.id, standing.version, standing.name, data.json)
            : this.#dotnet.invokeMethodAsync(call, standing.id, standing.version, data.json);
        void asked.catch((error: unknown) => { // oxlint-disable-line ex-grid/no-catch -- the hold and the inputs are let go, and the failure rethrown
            standing.release?.();
            standing.release = undefined;
            this.#letInputsGo(standing);
            throw error;
        });
    }

    // The context changes under the contents, a choice made, an answer arrived or a step's outcome said: .NET draws it
    // again.
    #hear(standing: Standing): void {
        standing.unsubscribe?.();
        standing.unsubscribe = standing.kind.subscribe?.(standing.context, () => {
            if (!standing.leaving && !this.#disposed) this.#send(standing, 'UpdateSlot');
        });
    }

    // The core takes the slot away: the contents stop answering at once, and go back to their holder at the end of the
    // task, as .NET is told, unless the core mounted the slot again meanwhile, as a focus request does: contents moved
    // out and back would drop the keyboard they hold. Contents that held it as the slot went, a menu whose command
    // opened a panel in its place, hand it to the product where it fell on nothing: the panel's field takes it only
    // once its own Razor stands, and the keys typed meanwhile wait for it in the product (ADR-0240), never reaching the
    // page.
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
            if (held && (active === null || active === doc.body || standing.contents?.contains(active) === true)) this.#keyboardBack?.(standing.slot);
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
    // would take away under the keyboard, nor on contents not yet whole: until the contents stand whole, the request
    // waits, and so does every input after it.
    #wire(standing: Standing): void {
        standing.unwire?.();
        standing.unwire = undefined;
        const { contents, context } = standing;
        if (contents === undefined) return;
        const current = standsWhole(standing);
        const answered = standing.answered === context || !current;
        const unwire = standing.kind.wire(contents, context, answered, () => drawnView(standing, contents));
        const native = keepTogglesNative(contents);
        standing.unwire = () => { unwire(); native(); };
        if (current) standing.answered = context;
        else if (standing.answered !== context && standing.kind.takesKeyboard) this.#holdInputs(standing);
    }
}

// The keys a component library's checkbox or radio takes in its own .NET handler, toggling the value it draws without
// the browser's `change` that a kind hears: MudBlazor 9's MudCheckBox unticks on Space and Delete in .NET, and the box
// it draws then disagrees with the draft the script holds. Stopped above the control, they do what the browser's own
// control does, which for Space is a click and a `change`, and for Delete and Backspace nothing.
const toggleKeys: ReadonlySet<string> = new Set([' ', 'Delete', 'Backspace']);

function stopToggleKey(event: KeyboardEvent): void {
    const target = event.target;
    if (!(target instanceof HTMLInputElement) || (target.type !== 'checkbox' && target.type !== 'radio')) return;
    if (toggleKeys.has(event.key)) event.stopPropagation();
}

/** Keeps the keys that toggle a checkbox or a radio in `contents` the browser's own (ADR-0410 section 3), answering
 * the function that lets them go to the component library again. */
function keepTogglesNative(contents: HTMLElement): () => void {
    const types = ['keydown', 'keypress', 'keyup'] as const;
    for (const type of types) contents.addEventListener(type, stopToggleKey, true);
    return () => {
        for (const type of types) contents.removeEventListener(type, stopToggleKey, true);
    };
}

/** Whether the contents .NET drew from the data last sent stand whole, as the slot's kind judges them. */
function standsWhole(standing: Standing): boolean {
    if (standing.drawn !== standing.version) return false;
    if (standing.kind.standsWhole === undefined) return true;
    if (!standing.views.has(standing.version)) {
        throw new Error(`Internal: the slot ${standing.name} was drawn from the data numbered ${String(standing.version)}, which the script no longer holds.`);
    }
    return standing.kind.standsWhole(standing.views.get(standing.version));
}

/** What a slot's contents show: what .NET drew them from, whose number they carry. */
function drawnView(standing: Standing, contents: HTMLElement): unknown {
    const version = Number(contents.getAttribute(drawnVersionAttribute));
    if (!standing.views.has(version)) {
        throw new Error(`Internal: the contents of the slot ${standing.name} show the data numbered ${String(version)}, which the script no longer holds.`);
    }
    return standing.views.get(version);
}
