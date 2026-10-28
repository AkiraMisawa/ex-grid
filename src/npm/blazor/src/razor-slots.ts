// ExGrid's Chrome slots filled with Razor (ADR-0410): the grid's kinds over the Shared Script's RazorSlotHost, which
// moves the contents in and out and holds the Settled Mark and the inputs.

import { drawnVersionAttribute, gridRazorSlotKinds, RazorSlotHost, type GridRazorSlotName, type RazorSlotHostOptions } from '@ex-grid/blazor-shared';
import type { GridChrome } from '@ex-grid/core';
import type { DotNetHandle } from './dotnet-source';
import type { BlazorRow } from './wire-rows';

export { drawnVersionAttribute };

/**
 * The Razor Chrome of one grid: mount functions for the slots .NET fills, and the contents .NET renders for each.
 */
export class RazorSlots {
    readonly #host: RazorSlotHost;

    /** `options` hold the grid's Settled Mark and its inputs, and hand it the keyboard (ADR-0240, ADR-0410 section 5). */
    constructor(dotnet: DotNetHandle, options: RazorSlotHostOptions) {
        this.#host = new RazorSlotHost(dotnet, gridRazorSlotKinds, options);
    }

    /** The core's Chrome for the slots .NET fills: each a mount function that asks .NET for the slot's contents. The
     * same object while the slots are the same, so that a standing part is not drawn again for an update. */
    chrome(names: readonly GridRazorSlotName[]): GridChrome<BlazorRow> {
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

