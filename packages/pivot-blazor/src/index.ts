// The script Blazor's ExPivot imports (ADR-0370 section 1, ADR-0350 section 9): ExPivot's Script Core, the grid it
// draws through and the bridge to .NET, bundled into one module the ExPivot package ships, so a Blazor Consumer
// installs nothing from npm.

import { stampAgainst } from '@ex-grid/blazor-shared';

export { attach, labelIds } from './blazor-pivot';

/** The SHA-256 of what this module was built from, the Shared Script's stamp among it, which the component compares
 * with the one it was built with before it attaches anything (ADR-0123). The build writes it in. */
declare const EX_SOURCE_SHA256: string;

/** The stamp of the Shared Script this module was built against (ADR-0411). The build writes it in. */
declare const EX_SHARED_EXPECTED_SHA256: string;

/** Answers the module's stamp (ADR-0123), or, when the Shared Script the browser handed it is another build's, a text
 * that is no stamp, so that the component refuses it as it refuses a module of another version (ADR-0411). */
export function stamp(): string {
    return stampAgainst(EX_SOURCE_SHA256, EX_SHARED_EXPECTED_SHA256);
}
