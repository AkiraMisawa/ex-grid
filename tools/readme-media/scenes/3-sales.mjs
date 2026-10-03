// The sales analysis: a region is collapsed and opened again, a field is added to the report's
// rows from the Fields pane and taken off again.
import { click, pause, moveTo } from '../studio.mjs';
import { rowsPainted } from '../grid.mjs';

export default {
  name: 'sales',
  path: 'showcase/sales',
  async ready(page) {
    await rowsPainted(page);
    await page.locator('.ex-pivot-toggle').first().waitFor();
    await pause(page, 1200);
  },
  async run(page) {
    await pause(page, 900);
    const europe = page.locator('.ex-row').filter({ hasText: 'Europe' }).locator('.ex-pivot-toggle').first();
    await click(page, europe, { after: 1200 });
    await click(page, page.locator('.ex-row').filter({ hasText: 'Europe' }).locator('.ex-pivot-toggle').first(), { after: 1100 });
    const field = name => page.getByRole('checkbox', { name, exact: true }).first();
    await click(page, field('Channel'), { after: 1800 });
    await click(page, field('Channel'), { after: 1300 });
    await moveTo(page, { x: 520, y: 420 });
    await pause(page, 900);
  },
};
