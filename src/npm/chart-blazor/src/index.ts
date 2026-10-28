// The script Blazor's ExChart and ExSurface import (ADR-0370 section 1, ADR-0350 section 9): ExChart's Script Core and
// the bridge to .NET, bundled into one module the ExChart package ships, so a Blazor Consumer installs nothing from npm.

export { attachChart, attachSurface } from './blazor-chart';

/** The SHA-256 of what this module was built from, which the component compares with the one it was built
 * with before it attaches anything (ADR-0123). The build writes it in. */
declare const EX_SOURCE_SHA256: string;

/** Answers the module's stamp (ADR-0123). */
export function stamp(): string {
    return EX_SOURCE_SHA256;
}
