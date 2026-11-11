// Written by tools/assets/minify.mjs (ADR-0123): do not edit. Edit dotnet/src/ExGrid/Assets/ and run it
// again, and commit what it writes. test/shipped-stylesheet.test.ts checks it against the source.

/** The name of the stylesheet the grid draws against, by which its notice rules find the notice. */
export const stylesheetName = 'ex-grid.css';

/** The SHA-256 of that stylesheet's source: the stamp its own notice rule hides the notice for. */
export const stylesheetStamp = 'b60af7e9b8193ff0f175e842314451577daefbf0a4e7a6c3b8b6efa7c239c7d7';
