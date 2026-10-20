// The EURUSD volatility surface: turned with a drag and with the arrow keys, then sliced by a click on a
// tenor's line and on a delta's, each slice drawn as a 2D chart beside it: a smile, then a term structure.
import { click, drag, moveTo, pause, press } from '../studio.mjs';
import { rowsPainted } from '../grid.mjs';
import { chartsDrawn } from '../chart.mjs';

/**
 * The page point of the surface's quote `index`. Its dots stand tenor by tenor, each tenor's deltas in
 * order, a quote the page filled or left a hole taking no dot: 1W's five are 0 to 4, 1M's five 5 to 9.
 */
async function quote(page, index) {
  const box = await page.locator('.ex-chart-surface-box circle.ex-chart-surface-point').nth(index).boundingBox();
  return { x: box.x + box.width / 2, y: box.y + box.height / 2 };
}

/** Halfway between two quotes, on the line through both: a click there slices along that line. */
async function between(page, a, b) {
  const [p, q] = [await quote(page, a), await quote(page, b)];
  return { x: (p.x + q.x) / 2, y: (p.y + q.y) / 2 };
}

export default {
  name: 'surface',
  path: 'showcase/fx-volatility',
  chromes: ['builtin'],
  // Wide enough for the slice to stand beside the surface and the surface beside the quotes.
  viewport: { width: 1440, height: 810 },
  gif: { width: 800, fps: 6, colours: 64, dither: 'none' },
  async ready(page) {
    await rowsPainted(page);
    await page.locator('.ex-chart-surface-box polygon.ex-chart-surface-face').first().waitFor();
    await pause(page, 1200);
  },
  async run(page) {
    const box = await page.locator('.ex-chart-surface-box').boundingBox();
    const centre = { x: box.x + box.width / 2, y: box.y + box.height * 0.55 };
    await pause(page, 400);
    // Turned with a drag, then with the arrow keys.
    await drag(page, centre, { x: centre.x + 50, y: centre.y + 8 }, { steps: 24, after: 500 });
    await drag(page, { x: centre.x + 30, y: centre.y }, { x: centre.x - 40, y: centre.y - 8 }, { steps: 24, after: 500 });
    await press(page, 'ArrowRight', { after: 250 });
    await press(page, 'ArrowRight', { after: 250 });
    await press(page, 'ArrowRight', { after: 250 });
    await press(page, 'ArrowRight', { after: 700 });

    // A click on a short tenor's line, between 10P and 25P, slices its smile; one on ATM's line, between
    // 1W and 1M, the term structure at the money. Turned this way, a tenor's line and the next lie apart.
    await click(page, await between(page, 5, 6), { after: 900 });
    await chartsDrawn(page);
    await pause(page, 900);
    await click(page, await between(page, 2, 7), { after: 900 });
    await chartsDrawn(page);
    await pause(page, 1400);
  },
};
