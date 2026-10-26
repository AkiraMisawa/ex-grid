// Written by tools/assets/minify.mjs (ADR-0123): do not edit. Edit src/ExGrid/Assets/ and run it
// again, and commit what it writes. test/shipped-stylesheet.test.ts checks it against the source.

/** The name of the stylesheet the grid draws against, by which its notice rules find the notice. */
export const stylesheetName = 'ex-grid.css';

/** The SHA-256 of that stylesheet's source: the stamp its own notice rule hides the notice for. */
export const stylesheetStamp = '13b1ff606023a782703da1ec1e0b55d26309e181f3b9b70fb0c3de69f1d5b7a9';
