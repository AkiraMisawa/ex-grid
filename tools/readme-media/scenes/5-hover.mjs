// Reading a chart: the pointer moves along the Exposure profile, and the tooltip and the guide follow it;
// a legend entry hides a series and shows it again; a drag across the plot narrows the range, which
// Chart Filters show, and Reset range takes it back.
import { click, drag, moveTo, pause } from '../studio.mjs';
import { rowsPainted } from '../grid.mjs';
import { chartsDrawn, onPlot, shownChart } from '../chart.mjs';

export default {
  name: 'hover',
  path: 'showcase/exposure',
  chromes: ['builtin'],
  gif: { width: 800, fps: 6, colours: 48, dither: 'none' },
  async ready(page) {
    await rowsPainted(page);
    await chartsDrawn(page);
    // The chart stands below the grid: the page is scrolled to it before the recording starts.
    await shownChart(page).evaluate(el => el.scrollIntoView({ block: 'end' }));
    await pause(page, 1200);
  },
  async run(page) {
    const chart = shownChart(page);
    await pause(page, 400);
    await moveTo(page, await onPlot(chart, 0.08, 0.5));
    await moveTo(page, await onPlot(chart, 0.55, 0.45), { steps: 45 });
    await pause(page, 900);

    // A legend entry hides its series, and shows it again.
    const after = chart.locator('.ex-chart-legend-entry').nth(1);
    await click(page, after, { after: 900 });
    await click(page, after, { after: 700 });

    // A drag across the plot narrows the range; Chart Filters say what it is.
    await drag(page, await onPlot(chart, 0.15, 0.6), await onPlot(chart, 0.45, 0.6), { after: 900 });
    await moveTo(page, await onPlot(chart, 0.6, 0.4), { steps: 30 });
    await pause(page, 700);
    await click(page, chart.locator('.ex-chart-filters-toggle'), { after: 1300 });
    await click(page, chart.locator('.ex-chart-filters-toggle'), { after: 400 });
    await click(page, chart.locator('.ex-chart-reset'), { after: 1200 });
  },
};
