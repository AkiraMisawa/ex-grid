import { test, expect } from './fixtures.mjs';
import { sheet, cell } from './sheet-helpers.mjs';
import { sameColour, painted } from './pixels.mjs';

// VZ-16 (ADR-0090): at a devicePixelRatio between the stylesheet's resolution steps — a 150% display
// zoomed to 150% — the grid is told its Device Pixel, and a gridline is whole Device Pixels. Until
// ADR-0090 the stylesheet took the step below, 2, and a 1px gridline was drawn 2.25 Device Pixels
// wide, blended over three. Emulated for the whole file, so the screenshots read the same scale.
//
// A Border line is not read here. Under Playwright's emulated scale a thin line on /sheet blends
// over two Device Pixels even at 1.5, where the chrome-150 project's real display scale draws it in
// one (DC-59): the emulation, not the grid, decides those pixels. A thin line at a real 225% is the
// next Windows run's to read.

test.use({ deviceScaleFactor: 2.25 });

const WHITE = [255, 255, 255];
const GRIDLINE = [0xe0, 0xe0, 0xe0];

/**
 * The pixels that are not the white Paper, in device pixels, across an edge at `at` CSS px (an x for
 * axis 'x', a y for 'y'), read along `along`. The clip's origin is a multiple of 4 CSS px, which is
 * a whole 9 Device Pixels at 2.25, so no pixel is read half from its neighbour.
 */
async function runAcross(page, at, along, axis) {
    const start = Math.floor(at / 4) * 4 - 8;
    const line = Math.floor(along / 4) * 4;
    const region = axis === 'x'
        ? await painted(page, { x: start, y: line, width: 16, height: 4 })
        : await painted(page, { x: line, y: start, width: 4, height: 16 });
    const pixels = axis === 'x'
        ? region.across(line + 2, start, start + 16 - 0.01)
        : region.down(line + 2, start, start + 16 - 0.01);
    return pixels.filter((pixel) => !sameColour(pixel, WHITE, 2));
}

test('VZ-16: at a ratio of 2.25 a gridline is two whole Device Pixels, not the step\'s 2.25 blended over three (ADR-0090)', async ({ page }) => {
    await page.goto('/sheet?case=11');
    const grid = sheet(page);
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    await expect(page.locator('#sheet-positions .ex-row').first()).toBeVisible();
    await expect.poll(() => grid.evaluate((root) => /--ex-dp:\s*0\.4444/.test(root.getAttribute('style') ?? ''))).toBe(true);
    await page.mouse.move(0, 0);
    expect(await page.evaluate(() => devicePixelRatio)).toBe(2.25);

    // A gridline is its 1px rounded down to whole Device Pixels: two, as at 200%. Read on a clip
    // whose origin lies on a Device Pixel (4 CSS px are 9 at 2.25): a run the browser blended over a
    // third pixel, as the stylesheet's step drew it, shows there.
    const box = await cell(grid, 'E8').boundingBox();
    const right = await runAcross(page, box.x + box.width, box.y + box.height / 2, 'x');
    const bottom = await runAcross(page, box.y + box.height, box.x + box.width / 2, 'y');
    for (const [name, run] of [['right', right], ['bottom', bottom]]) {
        expect(run.length, `E8's ${name} gridline: ${JSON.stringify(run)}`).toBe(2);
        for (const pixel of run) expect(sameColour(pixel, GRIDLINE, 2), `E8's ${name} gridline: ${JSON.stringify(run)}`).toBe(true);
    }
});
