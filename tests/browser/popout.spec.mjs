import { test, expect } from './fixtures.mjs';

// VZ-16, issue #565 (ADR-0090's note of 2026-11-12): a grid drawn in a window the page opened, by the page's own
// script, is told that window's Device Pixel. Until the note the report read the script's own window, so such a
// grid drew its lines on the opener's Device Pixel and never heard its own window's change. Only a browser has
// two windows whose scales differ, and delivers one window's media query to a listener of the other's script;
// layer 2 holds the same rule over a window it fakes (browser-reports.test.ts).
//
// The second window's scale is CDP's override, as a display of another scale gives it. Under the override a
// change of scale alone evaluates no media query (VZ-16's own note, definition-of-done.md), so the width changes
// with it, as a window's does when it is dragged to a display of another scale.

/** What the grid's root says one Device Pixel is, in CSS pixels: the inline token the lines read (ADR-0090). */
function devicePixelOf(grid) {
    return grid.evaluate((root) => /--ex-dp:\s*([^;]+)/.exec(root.getAttribute('style') ?? '')?.[1]);
}

test('VZ-16 / #565: a grid in a window the page opened is told that window\'s Device Pixel, at its mount and as it changes (ADR-0090) @behaviour', async ({ page, context }) => {
    await page.goto('/popout');
    const [popup] = await Promise.all([page.waitForEvent('popup'), page.locator('#open-window').click()]);
    const said = [];
    popup.on('console', (message) => said.push(message.text()));
    popup.on('pageerror', (error) => said.push(error.message));
    const session = await context.newCDPSession(popup);
    const display = (scale, width) => session.send('Emulation.setDeviceMetricsOverride', { width, height: 440, deviceScaleFactor: scale, mobile: false });

    // The window stands on a display of 200% before anything is mounted in it.
    await display(2, 760);
    await expect.poll(() => popup.evaluate(() => devicePixelRatio)).toBe(2);
    const opener = await page.evaluate(() => devicePixelRatio);
    expect([2, 1.25], 'the opener is at a scale the second window never takes').not.toContain(opener);

    // At its mount the grid reads the window its element is in: half a CSS pixel, not the opener's.
    await page.locator('#toggle-grid').click();
    const grid = popup.locator('.ex-grid');
    await expect(grid.locator('.ex-row').first()).toBeVisible();
    await expect.poll(() => devicePixelOf(grid), { message: 'the Device Pixel at the mount, on a display of 200%' }).toBe('0.5px');

    // Dragged to a display of 125%: the change is that window's to tell, and the grid hears it.
    await display(1.25, 800);
    await expect.poll(() => devicePixelOf(grid), { message: 'the Device Pixel after the window moved to a display of 125%' }).toBe('0.8px');
    expect(await page.evaluate(() => devicePixelRatio), 'the opener\'s scale never moved').toBe(opener);

    // The window closed takes its grid with it, and nothing complains.
    await popup.close();
    await expect(page.locator('#popout-status')).toHaveText('Window: closed. Grid: not mounted.');
    expect(said).toEqual([]);
});
