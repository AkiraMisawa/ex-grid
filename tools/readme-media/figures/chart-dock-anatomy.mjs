// The Chart Dock beside the grid it charts, each part named by its term in the glossary (CONTEXT.md) or
// the ADR that decided it, for the ExChart Overview and the README. The figure makes a Selection of a
// curve history, charts it from the grid's context menu as a user would, and opens the menu over the
// Selection again, so the commands that make a chart and the tab they made stand side by side.
import { cellAt, rowsPainted } from '../grid.mjs';
import { chartsDrawn } from '../chart.mjs';

const menuItem = (page, text) => page.locator('[role=menu] [role=menuitem]').filter({ hasText: text });
const panel = page => page.locator('.mud-ex-chart-dock-panel');

export default {
  name: 'chart-dock-anatomy',
  path: 'figure/chart-dock-anatomy',
  overview: 'exchart',
  alt: 'A grid of a curve history under MudBlazor with a range selected and its context menu open, beside the Chart Dock holding the chart made from that range; each part the list names is ringed and numbered',
  frame: '.anatomy-dock',
  async ready(page) {
    await rowsPainted(page);
  },
  async prepare(page) {
    // The Date and the four tenors over every row, charted with a series a column.
    const from = await cellAt(page, 'Date', 0);
    const to = await cellAt(page, '10Y', 9);
    await page.mouse.click(from.x, from.y);
    await page.keyboard.down('Shift');
    await page.mouse.click(to.x, to.y);
    await page.keyboard.up('Shift');
    await page.mouse.click(to.x, to.y, { button: 'right' });
    await menuItem(page, /^Series in columns$/).click();
    await panel(page).locator('.mud-tab', { hasText: 'Chart 1' }).waitFor();
    await chartsDrawn(page);
    // The menu again, over the Selection, left open.
    const at = await cellAt(page, '2Y', 4);
    await page.mouse.click(at.x, at.y, { button: 'right' });
    await menuItem(page, /^Add to chart/).waitFor();
    await page.mouse.move(at.x, at.y + 400);
  },
  parts: [
    // The Selection is painted in two pieces, the Pinned Column's and the rest; the ring stands round both.
    { term: 'Selection', note: 'the cells to chart, the Date beside the figures', side: 'left', at: 0.12, ring: 4,
      target: page => ({ all: page.locator('.ex-range') }) },
    { term: 'Chart commands', note: "in the grid's context menu: a new chart with a series a column or a row, or the series added to a chart already open", side: 'left',
      target: page => ({ all: menuItem(page, /^(Series in|Add to chart)/) }) },
    { term: 'Chart Dock', note: "the MudBlazor Wrapper's place for charts made from the grid: beside it or below it, never over it", side: 'top', at: 0.85, ring: 4,
      target: page => panel(page) },
    { term: 'Tab', note: 'one a chart, which keeps following the rows it was made from', side: 'top',
      target: page => panel(page).locator('.mud-tab', { hasText: 'Chart 1' }) },
    { term: 'Toolbar', note: "Chart Filters, the table and the files, in MudBlazor's own buttons and menus", side: 'right', at: 0.3,
      target: page => panel(page).locator('.mud-ex-chart-toolbar') },
    { term: 'Legend', note: "a series a tenor, coloured from the MudBlazor theme's palette", side: 'right',
      target: page => panel(page).locator('g.ex-chart-legend') },
  ],
};
