// Ten years of an index as candles: too many sessions for the plot's pixels, so they merge into months;
// a drag across a few months narrows the range and the candles come back as days, then the whole range
// merges them again.
import { click, drag, moveTo, pause } from '../studio.mjs';
import { rowsPainted } from '../grid.mjs';
import { chartsDrawn, onPlot, shownChart } from '../chart.mjs';

export default {
  name: 'candles',
  path: 'showcase/equity-index',
  chromes: ['builtin'],
  gif: { width: 800, fps: 6, colours: 48, dither: 'none' },
  async ready(page) {
    await rowsPainted(page);
    await chartsDrawn(page);
    await pause(page, 1200);
  },
  async run(page) {
    const chart = shownChart(page);
    await pause(page, 400);
    // A merged candle's tooltip names its month and its rows.
    await moveTo(page, await onPlot(chart, 0.3, 0.6));
    await moveTo(page, await onPlot(chart, 0.55, 0.6), { steps: 30 });
    await pause(page, 1100);

    // Narrowed to a few months, the candles are days again.
    await drag(page, await onPlot(chart, 0.5, 0.75), await onPlot(chart, 0.525, 0.75), { steps: 16, after: 900 });
    await chartsDrawn(page);
    await moveTo(page, await onPlot(chart, 0.3, 0.5), { steps: 20 });
    await moveTo(page, await onPlot(chart, 0.6, 0.5), { steps: 30 });
    await pause(page, 1100);

    // The whole range merges them again.
    await click(page, page.locator('.xs-reset'), { after: 1500 });
  },
};
