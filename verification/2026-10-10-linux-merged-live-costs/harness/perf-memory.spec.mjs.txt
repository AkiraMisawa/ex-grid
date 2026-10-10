// Disposable, never committed: LV-23's loop for reviewing PR #70. 20 live redraws of a 101,001-row
// report in a published WebAssembly build; after each, a full collection and the managed heap.
import { test, expect } from '@playwright/test';
import fs from 'node:fs';

test('LV-23 live ExPivot managed heap over redraws', async ({ page }) => {
  test.setTimeout(1_800_000);
  const out = { inner: Number(process.env.PERF_B ?? 100), redraws: Number(process.env.PERF_N ?? 20), samples: [], errors: [], browser: page.context().browser().version() };
  const save = () => fs.writeFileSync(process.env.PERF_OUT, JSON.stringify(out, null, 2));
  page.on('console', (m) => { if (m.type() === 'error') { out.errors.push(m.text()); save(); } });
  page.on('pageerror', (e) => { out.errors.push(String(e)); save(); });
  const linear = () => page.evaluate(() => { const rt = globalThis.getDotnetRuntime?.(0); return rt?.Module?.HEAPU8?.length ?? null; });
  const collect = async (n) => {
    await page.click('#perf-gc');
    await expect(page.locator('#perf-mem')).toContainText(`#${n}`, { timeout: 600_000 });
    return Number((await page.locator('#perf-mem').textContent()).split(' ')[0]);
  };
  const t0 = Date.now();
  await page.goto(`/perf-live-memory?b=${out.inner}`);
  await page.locator('.ex-pivot .ex-row').first().waitFor({ timeout: 600_000 });
  out.firstReportSeconds = (Date.now() - t0) / 1000;
  out.samples.push({ redraw: 0, managedMB: await collect(0), linearMB: Math.round((await linear()) / 1048576) });
  save();
  for (let i = 1; i <= out.redraws; i++) {
    const before = await page.locator('.ex-pivot .ex-viewport').textContent();
    const t = Date.now();
    await page.click('#perf-tick');
    await expect(page.locator('#perf-status')).toHaveText(`Batch ${i}`, { timeout: 600_000 });
    await page.waitForFunction((old) => document.querySelector('.ex-pivot .ex-viewport')?.textContent !== old, before, { timeout: 600_000 });
    const redrawMs = Date.now() - t;
    out.samples.push({ redraw: i, redrawMs, managedMB: await collect(i), linearMB: Math.round((await linear()) / 1048576) });
    save();
  }
  expect(out.errors).toEqual([]);
});
