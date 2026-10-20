// CDS spread against leverage: a rectangle across the crowded lower left narrows both axes, the whole
// range comes back, and a click on the distressed name's mark goes to its row in the grid.
import { click, drag, moveTo, pause } from '../studio.mjs';
import { rowsPainted } from '../grid.mjs';
import { chartsDrawn, onPlot, shownChart } from '../chart.mjs';

/** The page point of the highest mark of the names: the distressed DSTR. */
async function highestMark(chart) {
  const svg = await chart.locator('svg.ex-chart-svg').first().boundingBox();
  const d = await chart.locator('g.ex-chart-series[data-series="names"] path.ex-chart-markers').first().getAttribute('d');
  // Each marker starts with an absolute move to a point on its outline, a few pixels from its centre.
  let best = null;
  for (const [, x, y] of d.matchAll(/M(-?[\d.]+),(-?[\d.]+)/g))
    if (best === null || Number(y) < best.y) best = { x: Number(x), y: Number(y) };
  return { x: svg.x + best.x + 2, y: svg.y + best.y + 2 };
}

export default {
  name: 'scatter',
  path: 'showcase/cds-spread',
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
    // A rectangle narrows both axes.
    await drag(page, await onPlot(chart, 0.04, 0.98), await onPlot(chart, 0.45, 0.75), { steps: 30, after: 900 });
    await chartsDrawn(page);
    await moveTo(page, await onPlot(chart, 0.4, 0.5), { steps: 30 });
    await pause(page, 1200);
    await click(page, page.locator('.xs-reset'), { after: 900 });

    // The outlier's mark: its tooltip names it, and a click goes to its row.
    await click(page, await highestMark(chart), { after: 1800 });
  },
};
