// Where things are in an ExChart, for the scenes and the figures: a chart on screen, and a point on its
// plot as a fraction of the plot's box, read from what is painted rather than from any internal state.

/** The chart on screen: the shown tab's, or the page's only one. */
export const shownChart = page => page.locator('.ex-chart').filter({ visible: true }).first();

/** Waits until a chart is on screen and none is still drawing. */
export async function chartsDrawn(page) {
  await shownChart(page).waitFor();
  await page.waitForFunction(() => document.querySelectorAll('.ex-chart[aria-busy="true"]').length === 0);
}

/** The point at fractions `fx` across and `fy` down a chart's plot, in its `panel`-th panel. */
export async function onPlot(chart, fx, fy, panel = 0) {
  const box = await chart.locator('.ex-chart-hit').nth(panel).boundingBox();
  if (!box) throw new Error(`No plot in panel ${panel}`);
  return { x: box.x + box.width * fx, y: box.y + box.height * fy };
}
