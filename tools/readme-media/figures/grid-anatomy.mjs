// The parts of a grid, each named by its term in the glossary (CONTEXT.md), for the ExGrid Overview
// and the README. The page lays out positions under Header Groups, three Pinned Columns with the
// Mark Column among them, and Row Stripes; the figure pans the grid a little, so the Pinned Columns
// stand over the ones that pass under them, marks two rows and makes a Selection, as a user would.
import { cellAt, cellBox, rowsPainted } from '../grid.mjs';

/** The painted row whose Id reads `id`. */
const row = (page, id) => page.locator('.ex-row:not(.ex-placeholder)').filter({
  has: page.locator('.ex-cell').filter({ hasText: new RegExp(`^${id}$`) }),
});

export default {
  name: 'grid-anatomy',
  path: 'figure/grid-anatomy',
  frame: '.anatomy-grid',
  async ready(page) {
    await rowsPainted(page);
    await row(page, 5012).waitFor();
  },
  async prepare(page) {
    // Pan right to the end, so the first column that is not pinned passes under the ones that are.
    const viewport = await page.locator('.ex-viewport').boundingBox();
    await page.mouse.move(viewport.x + viewport.width / 2, viewport.y + viewport.height / 2);
    await page.mouse.wheel(200, 0);
    await page.waitForFunction(() => {
      const scroller = document.querySelector('.ex-scroller');
      return scroller.scrollLeft > 0 && scroller.scrollLeft >= scroller.scrollWidth - scroller.clientWidth - 1;
    });
    // Two Row Marks, then a range from Price on the second row to P&L on the fifth.
    for (const id of [5007, 5009]) {
      const mark = await row(page, id).locator('.ex-cell').first().boundingBox();
      await page.mouse.click(mark.x + mark.width / 2, mark.y + mark.height / 2);
    }
    await page.locator('.ex-mark-count', { hasText: '2 marked' }).waitFor();
    const focus = await cellAt(page, 'Price', 1);
    await page.mouse.click(focus.x, focus.y);
    const extent = await cellAt(page, 'P&L', 4);
    await page.keyboard.down('Shift');
    await page.mouse.click(extent.x, extent.y);
    await page.keyboard.up('Shift');
    await page.locator('.ex-summary-figure', { hasText: 'Sum' }).waitFor();
  },
  parts: [
    { term: 'Header Group', note: 'a label over adjacent columns', side: 'top',
      target: page => page.locator('.ex-header-group', { hasText: 'Economics' }) },
    // Down the left of the Mark Column's header, where nothing is written.
    { term: 'Pinned Column', note: 'held at the edge while the others pan under it', side: 'top', at: 0.04, ring: 4,
      target: page => ({ all: page.locator('.ex-header .ex-pinned, .ex-row:not(.ex-placeholder) .ex-pinned') }),
      within: page => page.locator('.ex-scroller') },
    { term: 'Mark Column', note: 'its ticks are Row Marks, which your application holds', side: 'left',
      target: page => row(page, 5007).locator('.ex-cell').first() },
    { term: 'Row Stripe', note: 'on every second row of the result', side: 'left', target: page => row(page, 5010),
      within: page => page.locator('.ex-scroller') },
    // The Selection's ring stands outside it, clear of the Focus's and the Extent's.
    { term: 'Selection', note: 'rectangles of cells, by position', side: 'right', ring: 4, target: page => page.locator('.ex-range') },
    // Along the line between two rows, clear of the number beside it.
    { term: 'Focus', note: 'the active cell: typing enters it', side: 'right', at: 0, target: page => page.locator('.ex-focus') },
    { term: 'Extent', note: 'the end that moves as the range grows', side: 'right', target: page => cellBox(page, 'P&L', 4) },
    { term: 'Overflow', note: 'a number too wide shows ####, never a shorter number', side: 'right',
      target: page => row(page, 5006).locator('.ex-cell').filter({ has: page.locator('.ex-hashes') }) },
    { term: 'Selection Summary', note: "Excel's status-bar figures over the Selection", side: 'below',
      target: page => ({ all: page.locator('.ex-summary-figure') }) },
  ],
};
