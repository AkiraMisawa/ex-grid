// The /paint-cost harness page on a published WebAssembly host: loads it, presses Run, waits for
// "Done.", and appends the page's JSON to <out.json>.
//
//   node browser-paintcost.mjs <baseUrl> <out.json> <label> [runs] [warmup]
import { createRequire } from 'node:module';
import fs from 'node:fs';
const require = createRequire('/Users/akira338/github/ex-grid/.claude/worktrees/live-data-next-cc/tests/ExGrid.Browser/package.json');
const { chromium } = require('playwright');

const [base, out, label, runs = '15', warmup = '3'] = process.argv.slice(2);
const browser = await chromium.launch({ channel: 'chrome', headless: true });
const page = await browser.newPage({ viewport: { width: 1800, height: 1400 } });
const consoleErrors = [];
page.on('console', (m) => { if (m.type() === 'error' || m.type() === 'warning') consoleErrors.push(`${m.type()}: ${m.text().slice(0, 300)}`); });
page.on('pageerror', (e) => consoleErrors.push(`pageerror: ${String(e).slice(0, 300)}`));
await page.goto(`${base}/paint-cost?runs=${runs}&warmup=${warmup}`, { timeout: 600_000 });
await page.locator('.ex-grid .ex-viewport .ex-row').first().waitFor({ timeout: 300_000 });
await page.click('#paint-cost-run');
await page.waitForFunction(() => /^(Done\.|Failed)/.test(document.querySelector('#paint-cost-status')?.textContent ?? ''), null, { timeout: 1_800_000, polling: 200 });
const status = await page.locator('#paint-cost-status').textContent();
const text = await page.locator('#paint-cost-out').textContent();
const measured = text ? JSON.parse(text) : null;
const record = { label, base, at: new Date().toISOString(), status, consoleErrors, measured };
for (const r of measured?.results ?? [])
    console.log(`${r.scenario.padEnd(42)} step ${r.step.min.toFixed(3)} / ${r.step.median.toFixed(3)} / ${r.step.max.toFixed(3)}   NotePaint ${r.notePaintAlone.min.toFixed(4)} / ${r.notePaintAlone.median.toFixed(4)} / ${r.notePaintAlone.max.toFixed(4)}   bytes ${r.notePaintBytes.median}   new paints ${r.newPaints.median}`);
console.log(JSON.stringify({ label, status: status.slice(0, 300), consoleErrors }));
const all = fs.existsSync(out) ? JSON.parse(fs.readFileSync(out, 'utf8')) : [];
all.push(record);
fs.writeFileSync(out, `${JSON.stringify(all, null, 2)}\n`);
await browser.close();
