// ExGrid's Chrome slots filled with Razor (ADR-0410): the grid's kinds over the Shared Script's RazorSlotHost, which
// moves the contents in and out and holds the Settled Mark and the inputs.

import {
    drawnVersionAttribute, gridRazorSlotKinds, RazorSlotHost, type GridRazorSlotName, type HostedSlotKind, type RazorSlotHostOptions, type RazorSlotKind,
} from '@ex-grid/blazor-shared';
import type { GridChrome, GridChromeStylesheet, GridTemplateCell } from '@ex-grid/core';
import type { ClaimsHeard } from './consumer-claims';
import type { DotNetHandle } from './dotnet-source';
import type { BlazorRow } from './wire-rows';

export { drawnVersionAttribute };

/** The slot a Template cell's content is drawn in, as .NET names it. */
const templateCellSlotName = 'templateCell';

// Each row object the bridge made, numbered as first drawn: a row whose values changed is another object (ADR-0003),
// which the core mounts its cells again for, and .NET draws them again from the row it holds then.
const rowDrawings = new WeakMap<BlazorRow, number>();
let rowsDrawn = 0;

function drawingOf(row: BlazorRow): number {
    const known = rowDrawings.get(row);
    if (known !== undefined) return known;
    rowsDrawn += 1;
    rowDrawings.set(row, rowsDrawn);
    return rowsDrawn;
}

/** A Template cell's kind (ADR-0020, ADR-0410 section 6): the row by its id, the row object's drawing, the column and
 * the focus request as data, and nothing wired: the content's controls are the Consumer's, and one that takes the
 * keyboard on a request asks the grid for it from .NET, as over the C# core (ADR-0037, ADR-0204). */
// A Template cell's content is the Consumer's markup: its controls' claims are heard (ADR-0441 section 4).
function templateCellKind(claims: ClaimsHeard): RazorSlotKind<GridTemplateCell<BlazorRow>> {
    return {
        takesKeyboard: false,
        data: cell => ({
            json: JSON.stringify({ row: cell.row.id, drawing: drawingOf(cell.row), column: cell.column, focusRequest: cell.focusRequest }),
            drawn: undefined,
            memo: undefined,
        }),
        wire: contents => claims.wire(contents),
    };
}

/** The slot the Consumer's popover's contents are drawn in, as .NET names it (ADR-0441 section 4). */
const consumerPopoverSlotName = 'consumerPopover';

/** What the Consumer's popover's slot is mounted with: the opening .NET numbered, whose contents it draws. */
interface ConsumerPopoverSlot {
    readonly opening: number;
}

// What Tab reaches, as the core's popover frame finds it (consumer-popover.ts): a control that is not disabled, and not
// taken out of the tab order, and none of the frame's own wraps.
const focusable = ['button', 'input', 'select', 'textarea'].map(tag => `${tag}:not(:disabled):not([tabindex="-1"])`)
    .concat('[tabindex]:not([tabindex="-1"]):not(.ex-focus-wrap)').join(', ');

// The Consumer's popover's contents take the inputs made after the opening, which wait until they stand; their first
// control then takes the keyboard where the frame, which took it as the popover opened, still holds it.
function consumerPopoverKind(claims: ClaimsHeard): RazorSlotKind<ConsumerPopoverSlot> {
    return {
        takesKeyboard: true,
        data: popover => ({ json: JSON.stringify({ opening: popover.opening }), drawn: undefined, memo: undefined }),
        wire: (contents, _popover, answered) => {
            const frame = contents.closest('.ex-popover-consumer');
            if (!answered && frame !== null && contents.ownerDocument.activeElement === frame) {
                contents.querySelector<HTMLElement>(focusable)?.focus({ preventScroll: true });
            }
            return claims.wire(contents);
        },
    };
}

/**
 * The Razor Chrome of one grid: mount functions for the slots .NET fills, and the contents .NET renders for each.
 */
export class RazorSlots {
    readonly #host: RazorSlotHost;
    // The Chrome last handed out, the same object while neither its slots nor its stylesheets change (ADR-0390).
    #dressed: { readonly mounts: GridChrome<BlazorRow>; readonly key: string; readonly chrome: GridChrome<BlazorRow> } | undefined;

    /** `options` hold the grid's Settled Mark and its inputs, and hand it the keyboard (ADR-0240, ADR-0410 section 5);
     * `claims` number the inputs on the Consumer's own markup (ADR-0441 section 4). */
    constructor(dotnet: DotNetHandle, options: RazorSlotHostOptions, claims: ClaimsHeard) {
        const kinds: ReadonlyMap<string, HostedSlotKind> = new Map([
            ...gridRazorSlotKinds,
            [templateCellSlotName, templateCellKind(claims)],
            [consumerPopoverSlotName, consumerPopoverKind(claims)],
        ]);
        this.#host = new RazorSlotHost(dotnet, kinds, options);
    }

    /** The core's Chrome for the slots .NET fills, each a mount function that asks .NET for the slot's contents, and
     * the stylesheets the Consumer's Chrome brings, whose notices the grid's root renders (ADR-0123). The same object
     * while neither changes, so that a standing part is not drawn again for an update. */
    chrome(names: readonly GridRazorSlotName[], stylesheets: readonly GridChromeStylesheet[] | undefined): GridChrome<BlazorRow> {
        const mounts = this.#host.mounts(names);
        const key = stylesheets === undefined ? '' : JSON.stringify(stylesheets);
        if (this.#dressed?.mounts === mounts && this.#dressed.key === key) return this.#dressed.chrome;
        const chrome: GridChrome<BlazorRow> = stylesheets === undefined ? mounts : { ...mounts, stylesheets };
        this.#dressed = { mounts, key, chrome };
        return chrome;
    }

    /** Mounts a Template cell's content, which .NET draws from the column's fragment and the row (ADR-0020), answering
     * the function that takes it away. */
    template(slot: HTMLElement, cell: GridTemplateCell<BlazorRow>): () => void {
        return this.#host.mount(templateCellSlotName, slot, cell);
    }

    /** Mounts the Consumer's popover's contents for the opening .NET numbered, which .NET draws from the Consumer's
     * fragment (ADR-0441 section 4), answering the function that takes them away. */
    popover(slot: HTMLElement, opening: number): () => void {
        return this.#host.mount(consumerPopoverSlotName, slot, { opening });
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

