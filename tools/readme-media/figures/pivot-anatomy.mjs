// The parts of a pivot, each named by its term in the glossary (CONTEXT.md), for the ExPivot
// Overview and the README. The page lays out a report with every part on screen at once: a report
// filter, a Header Group over two Value Fields, a group row with its Items and its subtotal, and
// the grand total.
import { rowsPainted } from '../grid.mjs';

/** The report's row whose label reads exactly `label`, a toggle aside. */
const row = (page, label) => page.locator('.ex-row').filter({
  has: page.locator('.ex-cell.ex-pinned').filter({ hasText: new RegExp(`^\\W*${label}\\s*$`) }),
});

export default {
  name: 'pivot-anatomy',
  path: 'figure/pivot-anatomy',
  frame: '.anatomy-pivot',
  // Neither the report nor the pane may scroll: every part is shown whole.
  whole: ['.ex-scroller', '.ex-pivot-pane-body'],
  async ready(page) {
    await rowsPainted(page);
    await page.locator('.ex-pivot-areas').waitFor();
    await row(page, 'Grand Total').waitFor();
  },
  parts: [
    // The filter's caption starts at the frame's edge: its ring stands outside it.
    { term: 'Report filter', note: 'a field in Filters', side: 'left', ring: 4, target: page => page.locator('.ex-pivot-filter') },
    // Both lines come down between the report filter and the Layout button.
    { term: 'Value Field', note: 'Revenue, with its Aggregation, Sum', side: 'top', at: 0.32,
      target: page => page.locator('.ex-header-cell', { hasText: 'Sum of Revenue' }) },
    { term: 'Header Group', note: 'a column Item over its columns', side: 'top', at: 0.5,
      target: page => page.locator('.ex-header-group', { hasText: '2025' }) },
    { term: 'Group row', note: 'an outer Item, heading its Items', side: 'left', target: page => row(page, 'Europe') },
    { term: 'Item', note: 'one value of a field in Rows', side: 'left', target: page => row(page, 'Italy').locator('.ex-cell.ex-pinned') },
    { term: 'Subtotal', note: 'of the group, at its foot', side: 'left', target: page => row(page, 'Europe Total') },
    { term: 'Grand total', note: 'of everything the report counts', side: 'left', target: page => row(page, 'Grand Total') },
    // A number stands at the right of its cell, so the line meets the cell at its left.
    { term: 'Cell', note: 'one per combination of Items, computed from its Leaf Aggregate', side: 'below', at: 0.12,
      of: page => row(page, 'Grand Total'), target: page => row(page, 'United Kingdom').locator('.ex-cell-numeric').first() },
    // In the room under the report, its line crossing only the pane's edge.
    { term: 'Areas', note: 'Filters, Columns, Rows and Values', side: 'left', at: 0.88,
      of: page => page.locator('.ex-pivot-field-list'), target: page => page.locator('.ex-pivot-areas') },
  ],
};
