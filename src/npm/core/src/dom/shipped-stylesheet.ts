// Written by tools/assets/minify.mjs (ADR-0123): do not edit. Edit src/ExGrid/Assets/ and run it
// again, and commit what it writes. test/shipped-stylesheet.test.ts checks it against the source.

/** The name of the stylesheet the grid draws against, by which its notice rules find the notice. */
export const stylesheetName = 'ex-grid.css';

/** The SHA-256 of that stylesheet's source: the stamp its own notice rule hides the notice for. */
export const stylesheetStamp = '87f976f0cf5a733be7ca87a3137f64218581c6ea9ccfccc5350943164960ef85';
