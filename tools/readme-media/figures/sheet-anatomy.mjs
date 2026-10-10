// The parts of a sheet, each named by its term in the glossary (CONTEXT.md), for the ExSheet
// Overview and the README. The page opens an expenses claim beside last quarter's; the figure opens
// the total's Formula with F2, as a user would, so the Cell Editor, the Formula Bar and the
// Reference Outline show together.
import { cellAt, cellBox, rowsPainted } from '../grid.mjs';

export default {
  name: 'sheet-anatomy',
  path: 'figure/sheet-anatomy',
  overview: 'exsheet',
  alt: "An ExSheet expenses claim beside last quarter's, with the total's Formula open in the Cell Editor; each part the list names is ringed and numbered",
  frame: '.anatomy-sheet',
  async ready(page) {
    await rowsPainted(page);
    await page.locator('.ex-sheet-toolbar').waitFor();
  },
  async prepare(page) {
    const total = await cellAt(page, 'D', 6);
    await page.mouse.click(total.x, total.y);
    await page.keyboard.press('F2');
    await page.locator('.ex-reference-outline').waitFor();
  },
  parts: [
    // Its line comes down on the left, where the first button stands.
    { term: 'Sheet Toolbar', note: 'opt-in buttons that act on the Selection', side: 'top', at: 0.02, target: page => page.locator('.ex-sheet-toolbar') },
    // The Name Box starts at the frame's edge: its ring stands outside it.
    { term: 'Name Box', note: 'where the Focus is', side: 'left', ring: 3, target: page => page.locator('.ex-name-box') },
    { term: 'Formula Bar', note: "the Focus cell's Entry, editable here too", side: 'right',
      target: page => page.locator('.ex-formula-bar-text') },
    { term: 'Headings', note: 'column letters and row numbers', side: 'left', target: page => ({ all: page.locator('.ex-row-heading') }),
      within: page => page.locator('.ex-scroller') },
    // Down through the empty rows under the total.
    { term: 'Cell Editor', note: 'the uncommitted text, over its cell', side: 'below', target: page => page.locator('.ex-viewport .ex-editor') },
    // Along the line between the first two rows, clear of the words in them.
    { term: 'Reference Outline', note: 'the cells a Reference in the Formula names', side: 'right', at: 0, ring: 4,
      target: page => page.locator('.ex-reference-outline') },
    { term: 'Number Format', note: 'turns a Value into the text shown, here 0.0%', side: 'right', target: page => cellBox(page, 'F', 2) },
    { term: 'Error Value', note: 'an error, carried as data; here a division by nothing', side: 'right', target: page => cellBox(page, 'F', 5) },
    // Along the line under the total's row, clear of the change beside it.
    { term: 'Value', note: "what a cell's Entry evaluates to", side: 'right', at: 1, target: page => cellBox(page, 'E', 6) },
  ],
};
