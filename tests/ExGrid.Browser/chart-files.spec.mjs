import { readFile } from 'node:fs/promises';
import { test, expect } from './fixtures.mjs';

// The files a chart saves on /chart (ADR-0282, CH-10, CH-12): CSV, SVG and PNG, each by one click,
// under the chart's file name. A PNG is drawn at the Device Pixel, not scaled up from 100%: its tests
// carry CH-10, so the chrome-150 project also runs them at a display scale of 150%. (Emulating the
// scale with deviceScaleFactor left Edge's pages unable to close after a download.)

const exposure = (page) => page.locator('#exposure .ex-chart');

test.beforeEach(async ({ page }) => {
    await page.goto('/chart');
    await expect(exposure(page).locator('g.ex-chart-series')).toHaveCount(5);
    await expect(exposure(page)).not.toHaveAttribute('aria-busy', 'true');
});

/** One click on a download button, and the file it saved. */
async function save(page, button) {
    const [download] = await Promise.all([
        page.waitForEvent('download'),
        exposure(page).locator(button).click(),
    ]);
    return { name: download.suggestedFilename(), bytes: await readFile(await download.path()) };
}

test('ADR-0282, CH-12: one click saves the CSV of the visible series', async ({ page }) => {
    const { name, bytes } = await save(page, '.ex-chart-download-csv');
    expect(name).toMatch(/\.csv$/);
    const lines = bytes.toString('utf8').replace(/^﻿/, '').trim().split(/\r?\n/);
    expect(lines).toHaveLength(122);
    expect(lines[1]).toMatch(/^2026-10-30,/);
});

test('ADR-0282, CH-12: one click saves the SVG, the drawing on screen with its stylesheet and the screen\'s colours inside', async ({ page }) => {
    const { name, bytes } = await save(page, '.ex-chart-download-svg');
    expect(name).toMatch(/\.svg$/);
    const text = bytes.toString('utf8');
    const parsed = await page.evaluate((svg) => {
        const doc = new DOMParser().parseFromString(svg, 'image/svg+xml');
        return {
            error: doc.querySelector('parsererror')?.textContent ?? null,
            series: doc.querySelectorAll('g.ex-chart-series').length,
            style: doc.querySelector('style')?.textContent.includes('.ex-chart-path') ?? false,
            width: doc.documentElement.getAttribute('width'),
            rootStyle: doc.documentElement.getAttribute('style'),
        };
    }, text);
    expect(parsed.error).toBeNull();
    expect(parsed.series).toBe(5);
    expect(parsed.style).toBe(true);
    expect(parsed.width).toBe(await exposure(page).locator('svg.ex-chart-svg').getAttribute('width'));
    // The tokens as the screen has them are written on the file's root (ADR-0282).
    const onScreen = await exposure(page).evaluate((root) => getComputedStyle(root).getPropertyValue('--_ex-chart-colour-0').trim());
    expect(parsed.rootStyle).toContain(`--ex-chart-colour-0:${onScreen};`);
});

test('ADR-0282, CH-10, CH-12: one click saves the PNG, painted at the Device Pixel', async ({ page }) => {
    const { name, bytes } = await save(page, '.ex-chart-download-png');
    expect(name).toMatch(/\.png$/);
    expect(bytes.subarray(1, 4).toString('latin1')).toBe('PNG');
    const svg = exposure(page).locator('svg.ex-chart-svg');
    const width = Number(await svg.getAttribute('width'));
    const height = Number(await svg.getAttribute('height'));
    const scale = await page.evaluate(() => window.devicePixelRatio);
    expect(bytes.readUInt32BE(16)).toBe(Math.round(width * scale));
    expect(bytes.readUInt32BE(20)).toBe(Math.round(height * scale));
});

test('ADR-0282, ADR-0274, CH-10: the 3D surface saves its drawing as it stands, as SVG and as PNG at the Device Pixel', async ({ page }) => {
    await page.goto('/surface');
    const surface = page.locator('#vol .ex-chart-surface');
    await expect(surface.locator('.ex-chart-download-png')).toBeEnabled();
    const take = async (button) => {
        const [download] = await Promise.all([page.waitForEvent('download'), surface.locator(button).click()]);
        return { name: download.suggestedFilename(), bytes: await readFile(await download.path()) };
    };
    const svg = await take('.ex-chart-download-svg');
    expect(svg.name).toBe('EURUSD volatility.svg');
    const parsed = await page.evaluate((text) => {
        const doc = new DOMParser().parseFromString(text, 'image/svg+xml');
        return { error: doc.querySelector('parsererror')?.textContent ?? null, painted: [...doc.querySelectorAll('[style]')].filter((e) => /fill:rgb/.test(e.getAttribute('style'))).length };
    }, svg.bytes.toString('utf8'));
    expect(parsed.error).toBeNull();
    // The colours are written on each element, as the screen computed them, so the file needs no stylesheet.
    expect(parsed.painted).toBeGreaterThan(20);
    const png = await take('.ex-chart-download-png');
    expect(png.name).toBe('EURUSD volatility.png');
    expect(png.bytes.subarray(1, 4).toString('latin1')).toBe('PNG');
    const scale = await page.evaluate(() => window.devicePixelRatio);
    expect(png.bytes.readUInt32BE(16)).toBe(Math.round(640 * scale));
    expect(png.bytes.readUInt32BE(20)).toBe(Math.round(420 * scale));
});
