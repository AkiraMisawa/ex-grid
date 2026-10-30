// Written by tools/assets/minify.mjs (ADR-0123): do not edit. Edit src/ExSheet/Assets/ and run it
// again, and commit what it writes. test/shipped-stylesheet.test.ts checks it against the source.

/** The name of the stylesheet the sheet draws against, by which its notice rules find the notice. */
export const stylesheetName = 'ex-sheet.css';

/** The SHA-256 of that stylesheet's source: the stamp its own notice rule hides the notice for. */
export const stylesheetStamp = '17ce2ebd71ab40f9a773a2965946086201667a9c8f52525abd042a581c4c26f7';
