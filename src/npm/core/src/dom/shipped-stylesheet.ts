// Written by tools/assets/minify.mjs (ADR-0123): do not edit. Edit src/ExGrid/Assets/ and run it
// again, and commit what it writes. test/shipped-stylesheet.test.ts checks it against the source.

/** The name of the stylesheet the grid draws against, by which its notice rules find the notice. */
export const stylesheetName = 'ex-grid.css';

/** The SHA-256 of that stylesheet's source: the stamp its own notice rule hides the notice for. */
export const stylesheetStamp = 'c11c835b7e92620702eb3503dbaa8e7a2dbd8017c00b1bb06ca34b66a6d76652';
