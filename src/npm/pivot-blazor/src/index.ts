// The script Blazor's ExPivot imports (ADR-0370 section 1, ADR-0350 section 9): ExPivot's Script Core, the grid it
// draws through and the bridge to .NET, bundled into one module the ExPivot package ships, so a Blazor Consumer
// installs nothing from npm.

export { attach } from './blazor-pivot';

/** The SHA-256 of what this module was built from, which the component compares with the one it was built
 * with before it attaches anything (ADR-0123). The build writes it in. */
declare const EX_SOURCE_SHA256: string;

/** Answers the module's stamp (ADR-0123). */
export function stamp(): string {
    return EX_SOURCE_SHA256;
}
