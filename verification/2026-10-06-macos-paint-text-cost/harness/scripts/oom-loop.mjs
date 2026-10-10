// OOM feedback loop: load /pivot-live-costs at A x B, press "Apply one batch" N times, and after
// each redraw record the WebAssembly linear memory and whether "Out of memory" reached the console.
// usage: node loop.mjs <baseUrl> <a> <b> <redraws> [extraQuery]
import { createRequire } from 'node:module';
const require = createRequire('/Users/akira338/github/ex-grid/.claude/worktrees/live-data-next-cc/tests/ExGrid.Browser/package.json');
const { chromium } = require('playwright');
const [base, a, b, n, extra = '', collect = '1'] = process.argv.slice(2);
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1400, height: 1100 } });
let oom = null;
const errors = [];
page.on('console', (m) => { if (m.type() === 'error') { errors.push(m.text()); if (/Out of memory/i.test(m.text())) oom ??= m.text().split('\n').slice(0, 4).join(' | '); } });
page.on('pageerror', (e) => errors.push(String(e)));
let gcs = 0;
const gc = async () => {
  if (collect !== '1') return null;
  const before = await page.locator('#pivot-live-costs-mem').textContent();
  await page.click('#pivot-live-costs-gc');
  await page.waitForFunction((b) => document.querySelector('#pivot-live-costs-mem').textContent !== b || false, before, { timeout: 60000 }).catch(() => {});
  return (await page.locator('#pivot-live-costs-mem').textContent()).trim();
};
const mem = () => page.evaluate(() => {
  const rt = globalThis.getDotnetRuntime?.(0) ?? globalThis.Blazor?.runtime;
  const heap = rt?.Module?.HEAPU8?.length ?? rt?.localHeapViewU8?.()?.length ?? null;
  return { heapMB: heap == null ? null : Math.round(heap / 1048576), managedMB: document.querySelector('#pivot-live-costs-mem')?.textContent ?? null };
});
const t0 = Date.now();
await page.goto(`${base}/pivot-live-costs?a=${a}&b=${b}&batch=1&step=1${extra}`);
await page.locator('#pivot-live-costs-made').filter({ hasText: 'records made in' }).waitFor({ timeout: 300000 });
const report = page.locator('#pivot-live-costs .ex-grid .ex-viewport .ex-row').first();
await report.waitFor({ timeout: 300000 }).catch(() => {});
const g0 = await gc();
console.log(JSON.stringify({ step: 'loaded', s: (Date.now() - t0) / 1000, managedAfterGcMB: g0, ...(await mem()), oom }));
for (let i = 1; i <= Number(n) && !oom; i++) {
  const t = Date.now();
  await page.click('#pivot-live-costs-step');
  await page.locator('#pivot-live-costs-status').filter({ hasText: `Batch ${i}:` }).waitFor({ timeout: 120000 });
  const expect = (await page.locator('#pivot-live-costs-expect').textContent()).trim();
  // the redraw has landed when the report shows R0's new value
  const landed = await page.waitForFunction((v) => document.querySelector('#pivot-live-costs .ex-grid')?.textContent.includes(v),
    expect, { timeout: 120000, polling: 50 }).then(() => true, () => false).catch(() => false);
  const g = landed ? await gc() : null;
  console.log(JSON.stringify({ step: i, s: (Date.now() - t) / 1000, landed, managedAfterGcMB: g, ...(await mem()), oom }));
}
console.log(JSON.stringify({ verdict: oom ? 'RED' : 'GREEN', oom, errors: errors.slice(0, 3).map((e) => e.slice(0, 200)) }));
await browser.close();
