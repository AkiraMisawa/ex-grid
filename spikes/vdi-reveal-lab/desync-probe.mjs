// Detects the Ctrl+Down blank seen under Citrix: the DOM's scrollTop is at the target, but the
// offset the browser paints and scrolls from is still the old one. A wheel turn after the reveal
// scrolls from the painted offset, so landing near the top instead of near the bottom is the fault.
// Usage: node desync-probe.mjs <label> <blotter url> [--channel=chrome|msedge] [--exe=PATH]
//        [--cpu=N] [--arg=--flag ...] [--require=<dir with node_modules/playwright-core>]
import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';

const argv = process.argv.slice(2);
const [label, url] = argv;
const opt = (name) => argv.filter(a => a.startsWith(`--${name}=`)).map(a => a.slice(name.length + 3));
const require = createRequire(opt('require')[0] ?? '/home/akira/src/hobby/ex-grid/tests/ExGrid.Browser/');
const { chromium } = require('playwright-core');
const exe = opt('exe')[0];
const cpu = Number(opt('cpu')[0] ?? 1);
const outDir = path.join(process.cwd(), 'out', label);
fs.mkdirSync(outDir, { recursive: true });

const browser = await chromium.launch({
    headless: false,
    executablePath: exe,
    channel: exe ? undefined : (opt('channel')[0] ?? 'chrome'),
    args: opt('arg'),
});
// --noemulate: the window's own size and the display's own scale, not Playwright's emulated ones.
const context = await browser.newContext(argv.includes('--noemulate') ? { viewport: null } : { viewport: { width: 1280, height: 800 } });
const page = await context.newPage();
await page.goto(url, { waitUntil: 'load', timeout: 180_000 });
await page.waitForSelector('.ex-grid .ex-viewport .ex-row', { timeout: 180_000 });
await page.waitForTimeout(1500);
if (cpu > 1) {
    const cdp = await context.newCDPSession(page);
    await cdp.send('Emulation.setCPUThrottlingRate', { rate: cpu });
}
const read = () => page.evaluate(() => {
    const s = document.querySelector('.ex-grid > .ex-scroller');
    return { top: Math.round(s.scrollTop), firstRow: s.querySelector('.ex-viewport').getAttribute('data-ex-first-row') };
});
const box = await page.locator('.ex-grid .ex-scroller').boundingBox();
const results = [];
for (const [key, expectBottom] of [['Control+ArrowDown', true], ['Control+ArrowUp', false]]) {
    // Each round starts from rest.
    await page.mouse.click(box.x + box.width * 0.6, box.y + box.height * 0.5);
    await page.waitForTimeout(1000);
    await page.keyboard.press(key);
    await page.waitForTimeout(2500);
    const dom = await read();
    await page.screenshot({ path: path.join(outDir, `${key.replace('Control+', 'ctrl-')}.png`) });
    // A wheel turn scrolls from the offset the browser paints.
    await page.mouse.move(box.x + box.width * 0.6, box.y + box.height * 0.5);
    await page.mouse.wheel(0, expectBottom ? -60 : 60);
    await page.waitForTimeout(2500);
    const after = await read();
    const desync = expectBottom ? after.top < dom.top - 5000 : after.top > 5000;
    results.push(`${key}: dom top=${dom.top} first=${dom.firstRow} -> after wheel top=${after.top} first=${after.firstRow} ${desync ? 'DESYNC' : 'ok'}`);
}
const dpr = await page.evaluate(() => devicePixelRatio);
console.log(`== ${label} (cpu x${cpu}, dpr ${dpr}, args ${opt('arg').join(' ') || '-'})`);
for (const line of results) console.log('   ' + line);
await browser.close();
