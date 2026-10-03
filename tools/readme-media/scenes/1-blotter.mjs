// The trade blotter: prices tick, a desk is chosen, a rectangle of P&L is selected and extended
// with the keyboard, then every desk again.
import { click, drag, press, pause, moveTo } from '../studio.mjs';
import { cellAt, rowsPainted } from '../grid.mjs';

export default {
  name: 'blotter',
  path: 'showcase/blotter',
  async ready(page) {
    await rowsPainted(page);
    await pause(page, 1500);
  },
  async run(page) {
    await pause(page, 1600);
    await click(page, page.getByText('Rates', { exact: true }).first(), { after: 1100 });
    await drag(page, await cellAt(page, 'Day P&L', 2), await cellAt(page, 'Total P&L', 7), { after: 700 });
    await press(page, 'Shift+ArrowDown');
    await press(page, 'Shift+ArrowDown');
    await press(page, 'Shift+ArrowDown', { after: 1300 });
    await click(page, page.getByText('All', { exact: true }).first(), { after: 900 });
    await moveTo(page, await cellAt(page, 'Last', 10));
    await pause(page, 1600);
  },
};
