// Runs one traced reveal and prints the trace panel: Ctrl+Down in the given mode, optionally with
// the CPU throttled so the key's task outlasts a frame, as it does under Citrix.
// Usage: node trace-run.mjs <site base url> <mode> [cpu rate]
import { createRequire } from 'node:module';
const require = createRequire('/home/akira/src/hobby/ex-grid/tests/ExGrid.Browser/');
const { chromium } = require('playwright-core');
const [base, mode, cpu] = process.argv.slice(2);
const browser = await chromium.launch({ headless: false, channel: 'chrome' });
const context = await browser.newContext({ viewport: { width: 1280, height: 800 } });
const page = await context.newPage();
await page.goto(`${base}showcase/blotter?reveal=${mode}&trace=1`, { waitUntil: 'load' });
await page.waitForSelector('.ex-grid .ex-row', { timeout: 60000 });
await page.waitForTimeout(800);
// Paused, so the price ticks do not crowd the trace.
await page.getByRole('button', { name: /Pause/ }).first().click().catch(() => {});
if (Number(cpu) > 1) {
    await (await context.newCDPSession(page)).send('Emulation.setCPUThrottlingRate', { rate: Number(cpu) });
}
const box = await page.locator('.ex-grid .ex-scroller').boundingBox();
await page.mouse.click(box.x + box.width * 0.4, box.y + 90);
await page.waitForTimeout(1500);
await page.evaluate(() => [...document.querySelectorAll('button')].find(b => b.textContent === 'Clear')?.click());
await page.keyboard.press('Control+ArrowDown');
await page.waitForTimeout(4000);
console.log(`== mode ${mode}, cpu x${cpu ?? 1}`);
console.log((await page.locator('pre').last().textContent()).split('\n').filter(l => !/getScrollOffset|\+2000ms/.test(l)).join('\n'));
await browser.close();
