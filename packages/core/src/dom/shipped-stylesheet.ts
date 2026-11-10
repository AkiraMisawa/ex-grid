// Written by tools/assets/minify.mjs (ADR-0123): do not edit. Edit dotnet/src/ExGrid/Assets/ and run it
// again, and commit what it writes. test/shipped-stylesheet.test.ts checks it against the source.

/** The name of the stylesheet the grid draws against, by which its notice rules find the notice. */
export const stylesheetName = 'ex-grid.css';

/** The SHA-256 of that stylesheet's source: the stamp its own notice rule hides the notice for. */
export const stylesheetStamp = '09e302db6741968417bbac0934bf814e5d73eb7a084357650e8d727b0268e67c';
