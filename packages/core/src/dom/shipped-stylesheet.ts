// Written by tools/assets/minify.mjs (ADR-0123): do not edit. Edit dotnet/src/ExGrid/Assets/ and run it
// again, and commit what it writes. test/shipped-stylesheet.test.ts checks it against the source.

/** The name of the stylesheet the grid draws against, by which its notice rules find the notice. */
export const stylesheetName = 'ex-grid.css';

/** The SHA-256 of that stylesheet's source: the stamp its own notice rule hides the notice for. */
export const stylesheetStamp = 'e5e5a6806cb644b0af9adafdf3022a78c4d2e0af4ffd0244ba1ec52655068eb5';
