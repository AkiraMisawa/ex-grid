// The Shared Script (ADR-0411): the Script Core and the data it reads, every export of each, bundled once into the
// module ExGrid's package ships beside its bridge. Every product's Blazor module imports `@ex-grid/core` and
// `@ex-grid/data` from it by one address, so a page with several products loads them once, and their state is one.
// A product's module names this file `@ex-grid/blazor-shared`, which no package publishes: src/npm/scripts/build-blazor.mjs
// resolves it to the Shared Script's address, and each adapter's tsconfig and Vitest config to this file. A name both
// packages exported would be left out of `export *`; TypeScript refuses that ambiguity here (TS2308).

export * from '@ex-grid/core';
export * from '@ex-grid/data';

/** The SHA-256 of what this module was built from, which each product's module compares with the one it was built
 * with (ADR-0123). The build writes it in. */
declare const EX_SHARED_SHA256: string;

/** Answers the Shared Script's stamp (ADR-0123, ADR-0411). */
export function sharedStamp(): string {
    return EX_SHARED_SHA256;
}

/** A product module's stamp, `own`, while this Shared Script is the one it was built against, `expected`; otherwise a
 * text that is no stamp, so that the component refuses the module as it refuses another version's (ADR-0411). */
export function stampAgainst(own: string, expected: string): string {
    const shared = sharedStamp();
    return shared === expected ? own : `the Shared Script ${shared}, not ${expected}`;
}

export { crossingCalls, HeldMessages, inlineBytes, mostHeldBytes, type DotNetCalls, type WireHeld } from './held-messages';
export { HeldSnapshots, type DotNetStream, type SnapshotsRead } from './snapshots';
export { awaitPointingScope, holdPointingScope, type PagePointingScope } from './pointing-scopes';
export {
    drawnVersionAttribute, RazorSlotHost, type HostedSlotKind, type RazorSlotData, type RazorSlotHostOptions, type RazorSlotKind,
} from './razor-slot-host';
