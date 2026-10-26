// The script Blazor's ExGrid imports (ADR-0350 sections 7 and 9): the Script Core and the bridge to .NET,
// bundled into one module the ExGrid package ships, so a Blazor Consumer installs nothing from npm.

export { attach } from './blazor-grid';

/** The SHA-256 of what this module was built from, which the component compares with the one it was built
 * with before it attaches anything (ADR-0123). The build writes it in. */
declare const EX_SOURCE_SHA256: string;

/** Answers the module's stamp (ADR-0123). */
export function stamp(): string {
    return EX_SOURCE_SHA256;
}
