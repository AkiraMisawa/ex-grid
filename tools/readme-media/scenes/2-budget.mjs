// The budget sheet: a month's figure is typed and every total follows; a variance shows its
// Formula in the Formula Bar; a block is made bold from the toolbar, then undone.
import { click, press, type, pause, moveTo } from '../studio.mjs';
import { rowsPainted } from '../grid.mjs';

export default {
  name: 'budget',
  path: 'showcase/budget',
  async ready(page) {
    await rowsPainted(page);
    await pause(page, 1200);
  },
  async run(page) {
    const cell = text => page.locator('.ex-cell').filter({ hasText: new RegExp(`^${text}$`) }).first();
    await pause(page, 900);
    await click(page, cell('423'), { after: 600 });
    await type(page, '515', { after: 250 });
    await press(page, 'Enter', { after: 1400 });
    await click(page, cell('\\(51\\)|41'), { after: 1200 });
    await press(page, 'Shift+ArrowRight');
    await press(page, 'Shift+ArrowDown');
    await press(page, 'Shift+ArrowDown', { after: 500 });
    await press(page, 'Control+B', { after: 1200 });
    await press(page, 'Control+Z', { after: 900 });
    await press(page, 'Control+Z', { after: 1200 });
    await moveTo(page, { x: 900, y: 420 });
    await pause(page, 900);
  },
};
