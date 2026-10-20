// The parts of a chart, each named by its term in the glossary (CONTEXT.md) or the ADR that decided it,
// for the ExChart Overview and the README. The page draws two years of an index's sessions as candles
// with a 25-day average, the volume in a panel below; the figure rests the pointer on the plot, as a
// user reading it would, so the tooltip and the guide stand where they are read.
import { chartsDrawn, onPlot, shownChart } from '../chart.mjs';

/** The candle the guide stands on: the box of the first panel's candle subpaths that cross the guide's line. */
async function candleUnderGuide(page) {
  return page.locator('.anatomy-chart svg.ex-chart-svg').evaluate(svg => {
    const at = Number(svg.querySelector('.ex-chart-guide-line').getAttribute('x1'));
    const frame = svg.getBoundingClientRect();
    // Only the first panel's candles: the volume below is drawn by the same series.
    const plot = svg.querySelector('rect.ex-chart-plot');
    const floor = Number(plot.getAttribute('y')) + Number(plot.getAttribute('height'));
    let box = null;
    for (const path of svg.querySelectorAll('path.ex-chart-candles')) {
      // Each subpath is one candle's body or wick, drawn by absolute moves, and horizontal and vertical lines.
      for (const sub of path.getAttribute('d').split(/(?=M)/)) {
        const xs = [];
        let x = 0, y = 0;
        for (const [, command, a, b] of sub.matchAll(/([MHV])(-?[\d.]+)(?:,(-?[\d.]+))?/g)) {
          if (command === 'M') [x, y] = [Number(a), Number(b)];
          else if (command === 'H') x = Number(a);
          else y = Number(a);
          xs.push([x, y]);
        }
        if (xs.length === 0) continue;
        const left = Math.min(...xs.map(p => p[0])), right = Math.max(...xs.map(p => p[0]));
        const top = Math.min(...xs.map(p => p[1])), bottom = Math.max(...xs.map(p => p[1]));
        if (left - 1.5 > at || right + 1.5 < at || top > floor) continue;
        box = box === null ? { left, right, top, bottom } : {
          left: Math.min(box.left, left), right: Math.max(box.right, right),
          top: Math.min(box.top, top), bottom: Math.max(box.bottom, bottom),
        };
      }
    }
    if (box === null) return null;
    return { x: frame.x + box.left - 2, y: frame.y + box.top - 2, width: box.right - box.left + 4, height: box.bottom - box.top + 4 };
  });
}

export default {
  name: 'chart-anatomy',
  path: 'figure/chart-anatomy',
  overview: 'exchart',
  alt: 'An ExChart of two years of an equity index as weekly candles with a 25-day average and the volume in a panel below, the pointer resting on the plot; each part the list names is ringed and numbered',
  frame: '.anatomy-chart',
  async ready(page) {
    await chartsDrawn(page);
  },
  async prepare(page) {
    // The pointer comes to rest near the plot's right end, as a reader's would, and the tooltip turns to its left.
    const at = await onPlot(shownChart(page), 0.86, 0.5);
    await page.mouse.move(at.x - 80, at.y, { steps: 4 });
    await page.mouse.move(at.x, at.y, { steps: 4 });
    await page.locator('.ex-chart-tooltip').waitFor();
    await page.locator('.ex-chart-guide-line').waitFor({ state: 'attached' });
  },
  parts: [
    { term: 'Chart Filters', note: 'the series shown and the range, raised to your application, which holds them', side: 'top',
      target: page => page.locator('.ex-chart-filters-toggle') },
    { term: 'Export', note: 'the CSV, SVG and PNG of what the chart draws, each saved on one click', side: 'top',
      target: page => page.locator('.ex-chart-downloads') },
    { term: 'Legend', note: 'a click on an entry hides its series', side: 'left',
      target: page => page.locator('g.ex-chart-legend') },
    { term: 'Tooltip', note: 'every series of the panel, at the position the pointer is over', side: 'left', at: 0.3,
      target: page => page.locator('.ex-chart-tooltip') },
    { term: 'Break mark', note: 'the axis does not start at zero, and says so at its foot', side: 'left',
      target: page => page.locator('path.ex-chart-break').first() },
    { term: 'Merged candle', note: "too many sessions for the plot's pixels: each candle is a week, and the tooltip names its rows", side: 'right', ring: 3,
      target: page => candleUnderGuide(page) },
    { term: 'Guide', note: "the pointer's position, drawn down every panel", side: 'right', at: 0.85,
      target: page => page.locator('g.ex-chart-guide') },
    { term: 'Panel', note: 'the volume, below its candles on a value axis of its own', side: 'right',
      target: page => page.locator('rect.ex-chart-plot').nth(1) },
    { term: 'Notes', note: "what the chart did to fit, said in words, with the Consumer's own notes", side: 'below',
      target: page => page.locator('ul.ex-chart-notes') },
  ],
};
