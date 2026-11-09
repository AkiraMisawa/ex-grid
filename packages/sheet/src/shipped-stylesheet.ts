// Written by tools/assets/minify.mjs (ADR-0123): do not edit. Edit dotnet/src/ExSheet/Assets/ and run it
// again, and commit what it writes. test/shipped-stylesheet.test.ts checks it against the source.

/** The name of the stylesheet the sheet draws against, by which its notice rules find the notice. */
export const stylesheetName = 'ex-sheet.css';

/** The SHA-256 of that stylesheet's source: the stamp its own notice rule hides the notice for. */
export const stylesheetStamp = '1008b0af94139e01643800cc5a1204d2eb3c28ca25264d9c60d88deb0155229f';
