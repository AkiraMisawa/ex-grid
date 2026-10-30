// Written by tools/assets/minify.mjs (ADR-0123): do not edit. Edit dotnet/src/ExPivot/Assets/ and run it
// again, and commit what it writes. test/shipped-stylesheet.test.ts checks it against the source.

/** The name of the stylesheet the pivot draws against, by which its notice rules find the notice. */
export const stylesheetName = 'ex-pivot.css';

/** The SHA-256 of that stylesheet's source: the stamp its own notice rule hides the notice for. */
export const stylesheetStamp = '1a2532223e2af8d3d27b7aa2e8919df9378c1554121b47e778ced6ff8ae89dd3';
