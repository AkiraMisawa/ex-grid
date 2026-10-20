// The Exposure profile under MudBlazor: the Date and EPE's three columns are selected by their headers,
// charted from the grid's context menu into a new tab of the Chart Dock, and read with the pointer.
import { badge, click, moveTo, pause } from '../studio.mjs';
import { rowsPainted } from '../grid.mjs';
import { chartsDrawn, onPlot, shownChart } from '../chart.mjs';

const header = (page, text, nth = 0) =>
  page.locator('.ex-header .ex-header-cell').filter({ hasText: new RegExp(`^\\W*${text}\\W*$`) }).nth(nth);

export default {
  name: 'exposure',
  path: 'showcase/exposure',
  chromes: ['mud'],
  gif: { width: 800, fps: 6, colours: 48, dither: 'none' },
  async ready(page) {
    await rowsPainted(page);
    await chartsDrawn(page);
    await pause(page, 1200);
  },
  async run(page) {
    await pause(page, 600);
    // The Date's header, then Shift and EPE's Diff: four whole columns.
    await click(page, header(page, 'Date'), { after: 500 });
    await badge(page, 'Shift + click');
    await page.keyboard.down('Shift');
    await click(page, header(page, 'Diff'), { after: 700 });
    await page.keyboard.up('Shift');

    // The context menu over the Selection, and its Chart.
    const cell = await page.locator('.ex-grid [role=gridcell]').filter({ hasText: '2,238,064.61' }).first().boundingBox();
    const at = await moveTo(page, { x: cell.x + cell.width / 2, y: cell.y + cell.height / 2 });
    await pause(page, 200);
    await page.mouse.click(at.x, at.y, { button: 'right' });
    await pause(page, 700);
    await click(page, page.locator('[role=menu] [role=menuitem]').filter({ hasText: /^Chart$/ }), { after: 600 });
    await page.locator('.mud-ex-chart-dock-panel .mud-tab', { hasText: 'Chart 2' }).waitFor();
    await chartsDrawn(page);

    // Down to the new chart, the wheel turned over the Chart Dock rather than the grid, and along it.
    await moveTo(page, await onPlot(shownChart(page), 0.75, 0.2), { steps: 16 });
    for (let i = 0; i < 8; i++) {
      await page.mouse.wheel(0, 50);
      await pause(page, 40);
    }
    await pause(page, 600);
    const chart = shownChart(page);
    await moveTo(page, await onPlot(chart, 0.12, 0.4));
    await moveTo(page, await onPlot(chart, 0.62, 0.4), { steps: 40 });
    await pause(page, 1600);
  },
};
