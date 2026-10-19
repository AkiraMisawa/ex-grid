import { readFile } from 'node:fs/promises';
import { test, expect } from './fixtures.mjs';

// The files a chart saves on /chart (ADR-0282, CH-10, CH-12): CSV, SVG and PNG, each by one click,
// under the chart's file name. Emulated at 150% for the whole file, so the PNG is read at that
// scale: it is drawn at the Device Pixel, not scaled up from 100%.

test.use({ deviceScaleFactor: 1.5 });

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

test('ADR-0282, CH-12: one click saves the SVG, the drawing on screen with its stylesheet inside', async ({ page }) => {
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
        };
    }, text);
    expect(parsed.error).toBeNull();
    expect(parsed.series).toBe(5);
    expect(parsed.style).toBe(true);
    expect(parsed.width).toBe(await exposure(page).locator('svg.ex-chart-svg').getAttribute('width'));
});

test('ADR-0282, CH-10, CH-12: one click saves the PNG, painted at the Device Pixel of 150%', async ({ page }) => {
    const { name, bytes } = await save(page, '.ex-chart-download-png');
    expect(name).toMatch(/\.png$/);
    expect(bytes.subarray(1, 4).toString('latin1')).toBe('PNG');
    const svg = exposure(page).locator('svg.ex-chart-svg');
    const width = Number(await svg.getAttribute('width'));
    const height = Number(await svg.getAttribute('height'));
    expect(bytes.readUInt32BE(16)).toBe(Math.round(width * 1.5));
    expect(bytes.readUInt32BE(20)).toBe(Math.round(height * 1.5));
});
